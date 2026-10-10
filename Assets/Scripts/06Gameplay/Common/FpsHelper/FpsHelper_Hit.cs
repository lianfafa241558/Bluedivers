using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.MapUtils;
using FPSGame.GameContract;
using PEMaths;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Rendering;
using FPSGame.Audio;
using FPSGame.Weapon;
using FPSGame.Core.Interface;

namespace FPSGame.Gameplay
{
    public static partial class FpsHelper
    {
        /// <summary>取离中心最近的肢体(单肢体单位即其主干)，返回是否找到</summary>
        static bool PickNearestPart(IActor unit, Vector3 center, out Damageable part, out Collider collider)
        {
            part = null;
            collider = null;
            var damageables = unit.Damageables;
            if (damageables == null) return false;

            float nearestSqr = float.MaxValue;
            for (int i = 0, l = damageables.Length; i < l; ++i)
            {
                Damageable dam = damageables[i] as Damageable;
                if (!dam) continue;

                Collider col = dam.ClosestCollider(center);
                if (!col) continue;

                float sqr = (col.ClosestPointOnBounds(center) - center).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    part = dam;
                    collider = col;
                }
            }
            return part != null;
        }

        /// <summary>按单位施加一次击退：方向背离中心，力度随距离衰减</summary>
        static void ApplyKnockback(List<IActor> unitList, KineticAreaHitData hitData)
        {
            PEInt radius = hitData.knockbackRadius;
            if (radius <= 0) return;

            for (int i = 0, l = unitList.Count; i < l; ++i)
            {
                IActor unit = unitList[i];
                if (!unit.transform.TryGetComponent(out IPhysical physical)) continue;

                PEVector3 offset = unit.Logic3Pos - hitData.pos;
                PEInt falloff = 1 - PEMath.Clamp(offset.Magnitude / radius,0,1);
                PEVector3 direction = offset.Magnitude > new PEInt(0.0001f) ? offset.Normalized : new(unit.transform.forward);
                PEVector3 vector = direction * (falloff * hitData.knockbackForce);
                vector.y *= hitData.knockbackUpScale;
                //Debug.LogError("施加力"+ vector);
                physical.ApplyImpulse(vector);
            }
        }


        /// <summary>荆棘护甲反伤伤害组（真实伤害，无视抗性稳定为24点）</summary>
        private static readonly List<SKVP<DamageTypeEnum, float>> ThornArmorDamageGroups = new() { new(DamageTypeEnum.Gun, 1) };

        /// <summary>范围动能伤害(HitAreaKinetic)未配置伤害成分时的兜底成分(动能)</summary>
        private static readonly List<SKVP<DamageTypeEnum, float>> DefaultKineticGroups = new() { new(DamageTypeEnum.Gun, 1) };

        /// <summary>
        /// 全队强化"荆棘护甲"：近战攻击玩家阵营单位的攻击者会受到 24 点反伤
        /// </summary>
        private static void TryThornArmorReflect(IDamageable target, GameObject attacker, Vector3 point, ProjectileHitData hitData)
        {
            // 仅近战攻击（子弹为 ProjectileMelee）触发反伤
            if (!hitData.weapon.IsValid() || !(hitData.self.GetComponent<ProjectileMelee>())) return;
            // 需要全队强化，且被攻击方为玩家阵营单位
            // 走数据自持：BattleManager 在 09_Managers，而本文件在玩法层 ⇒ 不能直连（见 BattleState.cs）
            if (!FPSGame.Data.BattleState.HaveBooster(BoosterType.ThornArmor)) return;
            if (!target.gameObject) return;
            Actor targetActor = target.gameObject.GetComponent<Actor>();
            if (!targetActor || (targetActor.Type != UnitTypeEnum.Player && targetActor.Type != UnitTypeEnum.Friend)) return;
            if (!attacker || !attacker.TryGetComponent(out Actor attackerActor)) return;

            // 对攻击者造成 24 点反伤
            IDamageable damageable = attackerActor.MainDamageable;

            if (damageable.Source.IsValid())
            {
                var thornPacket = new DamagePacket {
                    Damage = 24,
                    DamageGroups = ThornArmorDamageGroups,
                    WeaknessBonus = 0,
                    AP = 10,
                    NoSource = false,
                    DamageSource = target.gameObject,
                    Pos = point,
                    DemolishValue = 0,
                    isDirect = true,
                    damageAffected = null,
                };
                //反甲
                damageable.InflictDamage(thornPacket);
            }
        }

        public static LayerMask GetHittableLayers(float speed){
            //高速武器不能穿盾
            if (speed>50){
                return LayerDefinition.HittableHighSpeedLayers;
            }
            else
            {
                return LayerDefinition.HittableLayers;
            }
        
        }

        /// <summary>
        /// 求爆炸伤害的落点（供伤害数字与受击反馈使用）：被命中碰撞体表面、朝向爆心的最近点。
        /// 直接用爆心会让大范围爆炸的所有伤害都堆在同一位置；用碰撞体 bounds.center 则巨型单位
        /// （例如一整片护盾）会把伤害点显示在球心，看不出真正命中在哪里。
        /// </summary>
        /// <param name="collider">被命中的碰撞体</param>
        /// <param name="explosionCenter">爆心（爆炸判定起点）</param>
        /// <returns>伤害落点；碰撞体无效或爆心在其内部时返回爆心本身</returns>
        private static Vector3 GetExplosionHitPoint(Collider collider, Vector3 explosionCenter)
        {
            if (!collider) return explosionCenter;
            // 非凸 MeshCollider 无法求表面最近点（会原样返回入参），退化为包围盒最近点
            Vector3 hitPoint = collider is MeshCollider mesh && !mesh.convex
                ? collider.ClosestPointOnBounds(explosionCenter)
                : collider.ClosestPoint(explosionCenter);
            // 爆心落在碰撞体内部时最近点就是爆心本身，此时直接沿用爆心
            if ((hitPoint - explosionCenter).sqrMagnitude <= 0.0001f) return explosionCenter;
            return hitPoint;
        }

        /// <summary>
        /// 只播"命中表现"：命中特效 / 命中音效 / 弹痕（**不结算任何伤害、不发噪声、不推命中事件**）。
        ///
        /// <para>▍为什么要单独抽出来（2026-10-09）：联机里"别人开枪"的表现弹
        /// （<c>WeaponBaseController.SpawnVisualBullet</c>）必须摘掉 <see cref="Hit"/>（伤害由开枪者本机结算），
        /// 但特效/音效/弹痕也都住在 <see cref="Hit"/> 里 ⇒ 一起被摘掉后，**对方屏幕上那颗子弹命中时没有任何反馈**，
        /// 看起来就是"穿过去了、没命中"（用户实测）。现在拆开：表现弹只挂本方法 ⇒ 伤害为零、表现照旧。</para>
        ///
        /// <para>⚠ 调用方应传**已经算好的命中点**（表现弹用同步过来的终点）。</para>
        /// </summary>
        public static void PlayImpactFx(ProjectileHitData hitData)
        {
            var damageData = hitData.data;
            if (damageData == null)
            {
                // 数据说话：表现弹的伤害配置为空 ⇒ 命中时**一点反馈都没有**（本方法直接返回）
                FPSGame.Utils.NetSyncLog.Warn("命中表现", $"伤害配置为空 ⇒ 跳过全部命中表现 点={hitData.pos:F2} 武器={(hitData.weapon.IsValid() ? hitData.weapon.name : "<无>")}");
                return;
            }

            FPSGame.Utils.NetSyncLog.BulletLog("命中表现", $"点={hitData.pos:F2} 命中物={(hitData.collider != null ? hitData.collider.name : "<无>")}" +
                $" 特效={damageData.ImpactVfx} 音效={damageData.ImpactSfx} 弹痕={damageData.UseHole}" +
                // 三项都没有 ⇒ "打中了但什么也看不见"，这条日志就是那件事的直接证据
                ((!damageData.ImpactVfx && !damageData.ImpactSfx && !damageData.UseHole) ? " ←三项全空：本发命中注定没有任何视觉反馈" : ""));

            Vector3 point = hitData.pos;
            Vector3 normal = hitData.normal;
            Collider collider = hitData.collider;   // 允许为空（表现弹只有终点、没有碰撞体）
            GameObject soure = hitData.soure;

            //特效
            if (damageData.ImpactVfx)
            {
                if (damageData.ImpactVfx.TryGetComponent(out ProjectileBase projectile))
                {
                    // 走特效服务契约（VFXManager 在 01Manager，本文件未来要随玩法层进 asmdef）
                    Component ps = VfxPool.Creat(projectile, point + (normal * damageData.ImpactVfxSpawnOffset), damageData.UseCollisionDirection ? Quaternion.LookRotation(normal) : default);
                    ps?.GetComponentInChildren<IVfxEffect>()?.SetOwner(soure, hitData.weapon.IsValid() ? hitData.weapon.gameObject : null, collider, point);
                    ps?.GetComponentInChildren<ProjectileBase>()?.Shoot(hitData.weapon);
                }
                else
                {
                    GameObject ps = VfxPool.Creat(damageData.ImpactVfx, point + (normal * damageData.ImpactVfxSpawnOffset), damageData.UseCollisionDirection ? Quaternion.LookRotation(normal) : default, (collider.IsValid() && (!damageData.OnlyTerrain || collider is TerrainCollider)) ? collider.transform : null);
                    ps?.GetComponentInChildren<IVfxEffect>()?.SetOwner(soure, hitData.weapon.IsValid() ? hitData.weapon.gameObject : null, collider, point);
                }

            }
            //音效
            if (damageData.ImpactSfx)
            {
                AudioSvc.PlaySound(new(damageData.ImpactSfx, point, hitData.sfxRange, AudioGroups.Impact));
            }
            //弹痕
            if (damageData.UseHole)
            {
                var parent = (collider.IsValid() && (!damageData.OnlyTerrain || collider is TerrainCollider)) ? collider.transform : null;
                var go=VfxPool.Creat(damageData.Hole.IsValid() ? damageData.Hole : bulletHoles.RandomTake(), point, Quaternion.LookRotation(normal), parent);
                //Debug.LogError(parent != null?( 1 << parent.gameObject.layer)+"/"+ LayerDefinition.UnitLayers.value: "无父级");
                if (parent!=null&&LayerDefinition.UnitLayers.Contains(1 << parent.gameObject.layer))
                {
                    go.transform.localScale *= 0.5f;
                }
            }
        }

        /// <summary>
        /// 击中
        /// </summary>
        public static void Hit(ProjectileHitData hitData)
        {
 

            //真的会有这种情况吗？？？
            if (!hitData.data.IsValid()) {
                Debug.LogError("没有伤害组件"+ hitData.soure + hitData.pos);
                return;
            }
            Vector3 point = hitData.pos;
            Vector3 normal = hitData.normal;
            Collider collider = hitData.collider;//要考虑碰撞体为空（爆炸）
            PEInt charg = hitData.chargeScale;
            GameObject soure = hitData.soure;
        
            var damageData = hitData.data;
            PEInt damageOuterRadius = damageData.GetDamageOuterRadius(charg);
            PEInt damageInnerRadius = damageData.GetDamageInnerRadius(charg);
            PEInt shockwave = damageData.GetShockwaveRadius(charg);
            PEInt destructe = damageData.GetDestructeRadius(charg);
            PEInt soundRadius = damageData.GetSoundRadius(charg);

            PEInt damageScale= (hitData.useDiffScale ? DiffDamageScale() : 1);
            //Debug.LogWarning(collider + "蓄力" + charg + "最终范范围 + damageRange+"伤害组成数量"+ damageData.DamageGroup.Count, collider);
            //Debug.LogWarning(collider + "蓄力" + charg);
            //直击
            if (damageData.GetDirectDamage(1)>0&&collider.IsValid()&& collider.TryGetComponent(out IDamageable comp)&& comp.Source.IsValid())
            {
                //Debug.LogWarning("对" + collider.gameObject.name, collider.gameObject);
                //Debug.LogWarning("造成直击伤害" + damageData.GetDirectDamage(charg), collider.gameObject);
                //Debug.LogWarning(" 基础伤害" + damageData.DamageDirect, collider.gameObject);

                // 全队强化"荆棘护甲"：近战攻击玩家阵营单位时，攻击者受到 24 点反伤
                TryThornArmorReflect(comp, soure, point, hitData);
                //Debug.LogWarning("对" + comp.gameObject.name + "造成直击伤害" + damageData.GetDirectDamage(charg) * damageScale+"穿甲等级"+ damageData.GetDirectAP(charg)+"原始值");
                var directPacket = new DamagePacket
                {
                    Damage = damageData.GetDirectDamage(charg) * damageScale,
                    DamageGroups = damageData.DamageGroupDirect,
                    WeaknessBonus = damageData.GetWeaknessBonus(),
                    AP = damageData.GetDirectAP(charg),
                    NoSource = damageData.NoSource || !soure,
                    DamageSource = soure,
                    Pos = point,
                    DemolishValue = damageData.GetDemolishValue(0),
                    isDirect = true,
                    damageAffected = collider,
                };
                //直击
                comp.InflictDamage(directPacket);
            }
         
            //爆炸
            if (damageData.UseExplode)
            {
                var unitList = FPSGame.Game.UnitQuery.FindUnits(new PECircle((PEVector2)point, damageOuterRadius), new());
                if (hitData.IgnoreSelf&& hitData.self.TryGetComponent<IActor>(out var self)) unitList.Remove(self);
                //Debug.LogError("忽略自身?"+hitData.IgnoreSelf+" "+owner);
                //Debug.LogError("目标数量"+unitList.Count);
                //for (int i = 0; i < unitList.Count; ++i)
                //{
                    //Debug.LogWarning("目标"+ unitList[i].gameObject);
                //}
                var list= unitList.Select(item => item.Damageables)
                    .SelectMany(item => item)
                    .ToList();

                //Debug.LogWarning("范围" + "内的目标数量"+ list.Count);
                foreach (Damageable item in list)//每一个伤害组件
                {
                    Collider exolosionCollider = item.ClosestCollider(point);
                    if (!exolosionCollider)
                    {
                        Debug.LogError("目标"+item+"没有碰撞箱",item);
                        continue;
                    }
                    PEInt value = 0;
                    var centerDisance = PEVector3.Distance((PEVector3)exolosionCollider.bounds.center, (PEVector3)point);
                    //伤害数字/受击反馈的落点：碰撞体表面朝向爆心的最近点
                    //用爆心 point 会让大范围爆炸的所有伤害堆在同一处；用 bounds.center 则巨型单位（如整片护盾）会把数字显示在球心，看不出实际命中位置
                    Vector3 hitPoint = GetExplosionHitPoint(exolosionCollider, point);
                    if (centerDisance <= damageInnerRadius)
                    {
                        value = damageData.GetExplosionDamage(charg, 0) * damageScale;
                    }
                    else if(item.ExplosionBlocking(point, out var hitCollider))
                    {
                        float distance = Vector3.Distance(hitCollider.ClosestPointOnBounds(point), point);
                        value = damageData.GetExplosionDamage(charg, (PEInt)distance) * damageScale;
                    }
                    if (value > 0)
                    {
                        //Debug.LogWarning("对" + item.gameObject.name + "造成爆炸伤害" + value +"穿甲等级"+ damageData.GetExplosionAP(charg), item.gameObject);
                    
                        var explosionPacket = new DamagePacket
                        {
                            Damage = value,
                            DamageGroups = damageData.DamageGroupExplosion,
                            WeaknessBonus = damageData.GetWeaknessBonus(),
                            AP = damageData.GetExplosionAP(charg),
                            NoSource = damageData.NoSource || !soure,
                            DamageSource = soure,
                            //Pos = point,//大范围爆炸时所有目标都显示在同一处
                            //Pos = exolosionCollider.bounds.center,//巨型单位会把伤害点显示在包围盒中心，看不出命中位置
                            Pos = hitPoint,
                            DemolishValue = damageData.GetDemolishValue(centerDisance),
                            isDirect = false,
                            damageAffected = exolosionCollider,
                        };
                        //爆炸
                        item.InflictDamage(explosionPacket);
                    }
                }

                //冲击波
                if (shockwave>0) {
                    foreach (var item in unitList)
                    {
                        if (item.transform.TryGetComponent(out IPhysical physical))
                        {
                            var distance = PEVector3.Distance((PEVector3)item.CenterPos, (PEVector3)point);
                            PEVector3 vector = (PEVector3)(item.CenterPos - point).normalized * (1 - PEMath.Clamp(distance / shockwave,0,1)) * 10;
                            //vector.y *= 2;
                            physical.ApplyImpulse(vector);
                            Debug.LogWarning("对物体" + item.gameObject + "施加力" + vector);
                        }
                    }
                }


                //地形破坏
                if (destructe > 0)
                {
                    //Debug.LogError($"地形破坏{point} 内半径{(destructe / new PEInt(1.5f)).RawFloat} 外半径{destructe.RawFloat} 深度{(destructe / 5).RawFloat}");
                    //按破坏外半径擦除积雪遮罩
                    SnowController.RemoveSnow(point, destructe.RawFloat);
                    //ModifyHeightMap 是协程（迭代器），必须用 StartCoroutine 启动，直接调用不会执行；
                    //⚠ 最后一个 refresh 传 false：原来默认 true ⇒ 每一发爆炸弹坑都整张 NavMesh 重烘。
                    //现在只"标脏"，由 TerrainClearer 按 RefreshInterval 合并成一次重烘
                    TimerHost.RunCoroutine(TerrainUtils.ModifyHeightMap(point, (destructe / new PEInt(1.5f)).RawFloat, destructe.RawFloat, (destructe / 5).RawFloat, ShapeType.Circle, false, false));
                    TerrainClearer.MarkTerrainChanged();
                    //地表物：与弹坑同半径摧毁（树/石块走"可被摧毁"白名单，内部按帧合并提交，不会逐棵重建地形树数据），
                    TerrainClearer.DestroyInRadius(point, destructe.RawFloat);
                }

           

            }

            //警告
            if (BattleHub.Current.IsPresent)
            {
                //表现层：HUD 擦弹/受击提示(仍用旧的音效半径口径)
                if (soundRadius > 0)
                {
                    UnitEventBus.BulletHit(soure, (PEVector3)point, soundRadius);
                }

                //逻辑层噪声：命中点发出(开火处的枪声由 WeaponBaseController 在开火时另发)
                PEInt impactNoise = damageData.GetImpactSoundRadius(charg);
                //爆炸本来就该是大动静：至少按伤害外半径发声
                if (damageData.UseExplode && damageOuterRadius > impactNoise) impactNoise = damageOuterRadius;
                if (impactNoise > 0)
                {
                    UnitEventBus.Noise(new NoiseData { source = soure, pos = (PEVector3)point, radius = impactNoise });
                }
            }

            // 命中表现（特效 / 音效 / 弹痕）：抽进 PlayImpactFx —— 联机的"表现弹"要单独复用它
            // （只摘伤害、保留表现），见那个方法的说明。
            PlayImpactFx(hitData);
        }




        /// <summary>
        /// 范围动能(非爆炸)伤害：对范围内每个单位逐个结算一次"直击包"。
        /// 与 <see cref="Hit(ProjectileHitData)"/> 爆炸分支的区别：
        /// - isDirect=true：绕开爆炸抗性与爆炸遮挡判定(即"非爆炸伤害")
        /// - 每个单位只结算一次(取离中心最近的肢体)，而非每个肢体各算一次
        /// - 不派发命中特效/音效/弹痕/地形破坏等副作用
        /// - 可排除自身单位、可附加击退
        /// </summary>
        /// <param name="hitData">范围动能伤害参数</param>
        public static void HitAreaKinetic(KineticAreaHitData hitData)
        {
            if (!hitData.data.IsValid()) return;
            PEInt outer = hitData.data.GetDamageOuterRadius(1);
            float outerRadius = outer.RawFloat;
            if (outerRadius <= 0f) return;

            // 打击范围内任意单位(不做队伍过滤)，仅排除 hitData.exclude
            // 查询走 05_UnitCore 的入口（未接管时返回空列表 ⇒ 下面的 Count==0 提前 return，等价原 IsPresent 守卫）
            List<IActor> unitList = FPSGame.Game.UnitQuery.FindUnits(
                new PECircle((PEVector2)hitData.pos, outer),
                TargetCfg.EnemyAI,
                unit => VaildTarget(unit)
                     && unit != hitData.exclude
                     && (hitData.filter == null || hitData.filter(unit)));
            if (unitList == null || unitList.Count == 0) return;

            var groups = hitData.groups != null && hitData.groups.Count > 0 ? hitData.groups : DefaultKineticGroups;
            bool noSource = hitData.data.NoSource || !hitData.soure;

            // 依次逐个单位结算
            for (int i = 0, l = unitList.Count; i < l; ++i)
            {
                // 每个单位只结算一次：取离中心最近的肢体(单肢体单位即其主干)
                if (!PickNearestPart(unitList[i], hitData.pos.RawVector3, out Damageable part, out Collider collider)) continue;

                PEInt distance = PEVector3.Distance((PEVector3)collider.bounds.center, (PEVector3)hitData.pos);
                PEInt value = hitData.data.GetExplosionDamage(1, distance);
                if (value <= 0) continue;

                part.InflictDamage(new DamagePacket
                {
                    Damage = value,
                    DamageGroups = groups,
                    WeaknessBonus = hitData.data.GetWeaknessBonus(),
                    AP = hitData.data.GetExplosionAP(1),
                    NoSource = noSource,
                    DamageSource = hitData.soure,
                    Pos = collider.ClosestPointOnBounds(hitData.pos.RawVector3),
                    DemolishValue = hitData.data.GetDemolishValue(distance),
                    isDirect = true,//直击包：绕开爆炸抗性/爆炸遮挡，即"非爆炸伤害"
                    damageAffected = collider,
                });
            }

            if (hitData.knockbackForce > 0) ApplyKnockback(unitList, hitData);
        }

        /// <summary>范围动能伤害参数(仿 <see cref="ProjectileHitData"/> 的扁平结构)</summary>
        public struct KineticAreaHitData
        {
            /// <summary>伤害配置：取用其外半径、伤害值、穿甲、弱点火、拆毁值、无源</summary>
            public IDamageData data;
            /// <summary>伤害成分(动能填 {Gun,1})；null/空则退化为 {动能,1}</summary>
            public List<SKVP<DamageTypeEnum, float>> groups;
            /// <summary>范围中心</summary>
            public PEVector3 pos;
            /// <summary>伤害来源(可空)</summary>
            public GameObject soure;
            /// <summary>需要排除的单位(通常为自身，可空)</summary>
            public IActor exclude;
            /// <summary>额外过滤(如只打敌人)；null=打范围内所有单位</summary>
            public System.Func<IActor, bool> filter;
            /// <summary>击退力度(0=不击退)</summary>
            public PEInt knockbackForce;
            /// <summary>击退半径(0=沿用伤害外半径)</summary>
            public PEInt knockbackRadius;
            /// <summary>击退竖直倍率(1=保持原方向)</summary>
            public PEInt knockbackUpScale;
        }
    }
}
