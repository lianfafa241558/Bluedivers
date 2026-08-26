using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Interface;
using GameContract;
using PEMaths;

using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using Utils;
using static UnityEditor.Progress;
using static UnityEngine.Rendering.DebugUI;

public static class FpsHelper
{
    static FpsHelper()
    {
        //bulletHoles = ResSvc.Instance.LoadObjects<GameObject>("VFX/Weapon/BulletHole/Base");
        bulletHoles = Resources.LoadAll<GameObject>("VFX/BulletHole/Base").ToList();
    }


    public static Vector3 PlayerCameraLookPoint { get;private set; }

    public static PEVector3 PlayerCameraLookLogicPoint { get; private set; }

    public static void SetPlayerCameraLookPoint(Vector3 pos)
    {
        PlayerCameraLookPoint = pos;
        PlayerCameraLookLogicPoint = new(pos);
    }



    private static List<GameObject> bulletHoles;

    /// <summary>荆棘护甲反伤伤害组（真实伤害，无视抗性稳定为24点）</summary>
    private static readonly List<SKVP<DamageTypeEnum, float>> ThornArmorDamageGroups = new() { new(DamageTypeEnum.Real, 1) };

    /// <summary>
    /// 全队强化"荆棘护甲"：近战攻击玩家阵营单位的攻击者会受到 24 点反伤
    /// </summary>
    private static void TryThornArmorReflect(I_Damagable target, GameObject attacker, Vector3 point, ProjectileHitData hitData)
    {
        // 仅近战攻击（子弹为 ProjectileMelee）触发反伤
        if (!hitData.weapon.IsValid() || !(hitData.weapon.ProjectilePrefab is ProjectileMelee)) return;
        // 需要全队强化，且被攻击方为玩家阵营单位
        if (!BattleManager.Instance.IsValid() || !BattleManager.Instance.HaveBooster(BoosterType.ThornArmor)) return;
        if (!target.gameObject) return;
        Actor targetActor = target.gameObject.GetComponent<Actor>();
        if (!targetActor || (targetActor.Type != UnitTypeEnum.Player && targetActor.Type != UnitTypeEnum.Friend)) return;
        if (!attacker || !attacker.TryGetComponent(out Actor attackerActor)) return;

        // 对攻击者造成 24 点反伤（取第一个有效肢体）
        foreach (I_Damagable damageable in attackerActor.Damageables)
        {
            if (damageable.Source.IsValid())
            {
                var thornPacket = new DamagePacket
                {
                    Source = damageable,
                    Damage = (PEInt)24,
                    DamageGroups = ThornArmorDamageGroups,
                    WeaknessBonus = 0,
                    AP = 0,
                    NoSource = false,
                    DamageSource = target.gameObject,
                    Pos = point,
                    DemolishValue = 0,
                };
                damageable.InflictDamage(thornPacket);
                break;
            }
        }
    }

    /// <summary>
    /// 是否是主要阶段（战斗中、准备、过渡）
    /// </summary>
    public static bool IsMainStage()
    {
        return GameRoot.GameState == GameStateEnum.Game
            || GameRoot.GameState == GameStateEnum.Ready
            || GameRoot.GameState == GameStateEnum.Bridge;
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
    /// 击中
    /// </summary>
    public static void Hit(ProjectileHitData hitData)
    {
 

        //真的会有这种情况吗？？？
        if (!hitData.data.IsValid()) {
            Debug.LogError("没有伤害组件"+ hitData.owner + hitData.pos);
            return;
        }
        Vector3 point = hitData.pos;
        Vector3 normal = hitData.normal;
        Collider collider = hitData.collider;//要考虑碰撞体为空（爆炸）
        PEInt charg = hitData.chargeScale;
        GameObject owner = hitData.owner;
        
        var damageData = hitData.data;
        PEInt damageOuterRadius = damageData.GetDamageOuterRadius(charg);
        PEInt damageInnerRadius = damageData.GetDamageInnerRadius(charg);
        PEInt shockwave = damageData.GetShockwaveRadius(charg);
        PEInt destructe = damageData.GetDestructeRadius(charg);
        PEInt soundRadius = damageData.GetSoundRadius(charg);

        PEInt damageScale= (hitData.useDiffScale ? DiffDamageScale() : 1);
        //Debug.LogWarning(collider + "蓄力" + charg + "最终范范围 + damageRange+"伤害组成数量"+ damageData.DamageGroup.Count, collider);

        //直击
        if (damageData.GetDirectDamage(1)>0&&collider.IsValid()&& collider.TryGetComponent(out I_Damagable comp)&& comp.Source.IsValid())
        {
            //Debug.LogWarning("对" + collider.gameObject.name, collider.gameObject);
            //Debug.LogWarning("造成直击伤害" + damageData.GetDirectDamage(charg), collider.gameObject);
            //Debug.LogWarning(" 基础伤害" + damageData.DamageDirect, collider.gameObject);

            // 全队强化"荆棘护甲"：近战攻击玩家阵营单位时，攻击者受到 24 点反伤
            TryThornArmorReflect(comp, owner, point, hitData);
            Debug.LogWarning("对" + comp.gameObject.name + "造成直击伤害" + damageData.GetDirectDamage(charg) * damageScale);
            var directPacket = new DamagePacket
            {
                Source = comp,
                Damage = damageData.GetDirectDamage(charg) * damageScale,
                DamageGroups = damageData.DamageGroupDirect,
                WeaknessBonus = damageData.GetWeaknessBonus(),
                AP = damageData.GetDirectAP(charg),
                NoSource = damageData.NoSource || !owner,
                DamageSource = owner,
                Pos = point,
                DemolishValue = damageData.GetDemolishValue(),
            };
            comp.InflictDamage(directPacket);
        }
         
        //爆炸
        if (BattleManager.Instance.IsValid() && damageData.UseExplode)
        {
            var unitList = BattleManager.Instance.FindUnits(new PECircle((PEVector2)point, damageOuterRadius), new());
            if (hitData.IgnoreSelf) unitList.Remove(owner.GetComponent<I_Actor>());
            //Debug.LogError("目标数量"+unitList.Count);
            for (int i = 0; i < unitList.Count; ++i)
            {
                Debug.LogWarning("目标"+ unitList[i].gameObject);
            }
            var list= unitList.Select(item => item.Damageables)
                .SelectMany(item => item)
                .ToList();

            //Debug.LogWarning("范围" + "内的目标数量"+ list.Count);
            foreach (Damageable item in list)//每一个伤害组件
            {
                PEInt value = 0; 
                if (PEVector3.Distance((PEVector3)item.transform.position, (PEVector3)point)<= damageInnerRadius)
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
                    Debug.LogWarning("对" + item.gameObject.name + "造成爆炸伤害" + value , item.gameObject);
                    var explosionPacket = new DamagePacket
                    {
                        Source = item,
                        Damage = value,
                        DamageGroups = damageData.DamageGroupExplosion,
                        WeaknessBonus = damageData.GetWeaknessBonus(),
                        AP = damageData.GetExplosionAP(charg),
                        NoSource = damageData.NoSource || !owner,
                        DamageSource = owner,
                        Pos = point,
                        DemolishValue = damageData.GetDemolishValue(),
                    };
                    item.InflictDamage(explosionPacket);
                }
            }

            //冲击波
            if (shockwave>0) {
                foreach (var item in unitList)
                {
                    if (item.transform.TryGetComponent(out IPhysical physical))
                    {
                        var distance = PEVector3.Distance((PEVector3)item.transform.position, (PEVector3)point);
                        PEVector3 vector = (PEVector3)(item.CenterPos - point).normalized * (1 - (distance / shockwave)) * 100;
                        vector.y *= 4;
                        physical.ApplyForce(vector);
                        //Debug.LogError("对物体" + item.gameObject + "施加力" + vector);
                    }
                }
            }


            //地形破坏
            if (destructe > 0)
            {
                TerrainUtils.ModifyHeightMap(point, (destructe / new PEInt(1.5f)).RawFloat, destructe.RawFloat, (destructe / 5).RawFloat, ShapeType.Circle, false);
            }

           

        }

        //警告
        if (BattleManager.Instance.IsValid()&&!hitData.data.NoSource && (!collider.IsValid() || collider.GetComponent<I_Damagable>() == null) && soundRadius > 0)
        {
            var unitList = BattleManager.Instance.FindUnits(new PECircle((PEVector2)point, soundRadius), TargetCfg.Enemy);
            foreach (var item in unitList)
            {
                if (item.transform.TryGetComponent(out I_AIController physical))
                {

                }
            }

            //if (collider.transform.TryGetComponentInParent(out Actor actor) && actor != ActorsManager.Player) GlobalEventManager.BulletHit(owner, point);
            BattleEventSub.BulletHit(owner, point);

        }

        //特效
        if (damageData.ImpactVfx)
        {
            if (damageData.ImpactVfx.TryGetComponent(out ProjectileBase projectile))
            {
                var ps = VFXManager.Creat(projectile, point + (normal * damageData.ImpactVfxSpawnOffset), damageData.UseCollisionDirection ? Quaternion.LookRotation(normal) : default);
                ps.GetComponentInChildren<IVfxEffect>()?.SetOwner(owner, hitData.weapon.IsValid() ? hitData.weapon.gameObject : null, collider, point);
                ps.GetComponentInChildren<ProjectileBase>()?.Shoot(hitData.weapon);
            }
            else
            {
                var ps = VFXManager.Creat(damageData.ImpactVfx, point + (normal * damageData.ImpactVfxSpawnOffset), damageData.UseCollisionDirection ? Quaternion.LookRotation(normal) : default, (collider.IsValid() && (!damageData.OnlyTerrain || collider is TerrainCollider)) ? collider.transform : null);
                ps.GetComponentInChildren<IVfxEffect>()?.SetOwner(owner, hitData.weapon.IsValid() ? hitData.weapon.gameObject : null, collider, point);
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
            VFXManager.Creat(damageData.Hole.IsValid() ? damageData.Hole : bulletHoles.RandomTake(), point, Quaternion.LookRotation(normal), (collider.IsValid() && (!damageData.OnlyTerrain || collider is TerrainCollider)) ? collider.transform : null);
        }
    }




    public static bool IsTarget(I_Actor actor, TargetCfg targetCfg)
    {
        if (actor.IsValid()
            && actor.ActorState.HasFlag(targetCfg.actorState)
            && actor.Type.HasFlag(targetCfg.targetType)
        ){
            return true;
        }
        return false;
    }
    public static bool VaildTarget(I_Actor target)
    {
        return target != null && !Object.ReferenceEquals(target, null) && target.ActorState != ActorState.Dead && !target.HasFlag(ActorFlag.Invincible);
    }

    
    public static PEInt ThreatValue(PEVector3 pos,I_Actor target)
    {
        if (!target.IsValid() || !target.gameObject) return 0;
        return PEVector3.Distance(pos, (PEVector3)target.CenterPos) * (PEInt)target.Threat;
    }
    public static PEInt ThreatValue(Vector3 pos, I_Actor target)
    {
        if (!target.IsValid()|| !target.gameObject) return 0;
        return PEVector3.Distance((PEVector3)pos, (PEVector3)target.CenterPos) * (PEInt)target.Threat;
    }

    public static bool HaveNavMeshAgent(NavMeshAgent navMeshAgent) => navMeshAgent && navMeshAgent.isActiveAndEnabled&& navMeshAgent.isOnNavMesh;


    public static Vector3 GetNavMeshPoint(Vector3 pos)
    {
        // 用 TerrainUtils 获取地面高度（Main 不存在时回退到原始 Y）
        float groundHeight = TerrainUtils.Main != null ? TerrainUtils.Main.WSToHeight(pos) : pos.y;
        Vector3 dropPos = new Vector3(pos.x, groundHeight, pos.z);

        // 用 NavMesh.SamplePosition 找最近的可用点
        if (NavMesh.SamplePosition(dropPos, out var hit, 2f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        else
        {
            return dropPos;
        }
    }




    private static readonly string[] opterTmp = new string[] { "<sprite=2", "<sprite=0", "<sprite=1", "<sprite=3" };
    public static string OpterTMPString(this IEnumerable<DirectionEnum> opter)
    {
        return string.Join(">", opter.Select(item => opterTmp[(int)item])) + ">";
    }

    public static string OpterColorString(this IEnumerable<DirectionEnum> opter, int index, Color less, Color equal, Color greater)
    {
        return opter.Select((item, i) => {
            var color = i < index ? less : (i == index ? equal : greater);
            return opterTmp[(int)item] + " color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">";
        }).Aggregate("", (current, next) => current + next);
        //Aggregate是累积计算
    }
    public static bool Compare(this IEnumerable<DirectionEnum> target, IEnumerable<DirectionEnum> input)
    {
        if (!input.Any()) return false;//判断非空
        return input.Zip(target, (i, t) => i == t).All(b => b);
        //zip生成一个list<Bool>，all检查出现false就返回false
    }


    public static EnemyType ToEnemyType(this EnemyVarietyType variety)
    {
        if (Tool.In(variety, EnemyVarietyType.KaiserBase - 1, EnemyVarietyType.Placeholder1 + 1)) return EnemyType.Kaiser;
        if (Tool.In(variety, EnemyVarietyType.Decagrammaton - 1, EnemyVarietyType.Placeholder3 + 1)) return EnemyType.Decagrammaton;
        if (Tool.In(variety, EnemyVarietyType.Colour - 1, EnemyVarietyType.Placeholder6 + 1)) return EnemyType.Colour;
        return EnemyType.Kaiser;
    }


    public static PEInt DiffDamageScale()
    {
        PEInt scale = 1 + (TaskManager.Instance.nowTask.ExtraDifficulty[0] * (PEInt)0.15f)+(ActorsManager.Players.Count-1)*(PEInt)0.05f;
        switch (TaskManager.Instance.nowTask.difficulty)
        {
            case DifficultyEnum.Normal:
                scale *= (PEInt)0.6f;
                break;
            case DifficultyEnum.Hard:
                scale *= (PEInt)0.75f;
                break;
            case DifficultyEnum.VeryHard:
                scale *= (PEInt)0.9f;
                break;
            case DifficultyEnum.HardCode:
                break;
            case DifficultyEnum.Extreme:
                scale *= (PEInt)1.5f;
                break;
            case DifficultyEnum.Insane:
                scale *= (PEInt)2;
                break;
            case DifficultyEnum.Torment:
                scale *= (PEInt)2.5f;
                break;
            case DifficultyEnum.Lunatic:
                scale *= (PEInt)3;
                break;
        }
        return scale;
    }

    /// <summary>
    /// 根据根骨骼和末端骨骼，手动更新蒙皮网格的包围盒
    /// </summary>
    /// <param name="smr">目标蒙皮网格渲染</param>
    /// <param name="endBone">管道末端骨骼</param>
    /// <param name="boundsExpand">包围盒向外扩大值（适配管道粗细</param>
    public static void UpdatePipeBounds(SkinnedMeshRenderer smr, Transform endBone, float expand = 1f)
    {
        // 参数校验
        if (smr == null || endBone == null)
        {
            Debug.LogError("参数错误", smr);
            return;
        }

        Transform bone = endBone;

        // 初始化包围盒（以第一个骨骼为起点）
        Bounds bounds = new Bounds(smr.transform.InverseTransformPoint(bone.position), Vector3.zero);
        bone = bone.parent;
        while (bone != null && bone != smr.rootBone)
        {
            bounds.Encapsulate(smr.transform.InverseTransformPoint(bone.position));
            bone = bone.parent;
        }
        bounds.center = new(-bounds.center.z, bounds.center.y, bounds.center.x);
        bounds.size = new(bounds.size.z, bounds.size.y, bounds.size.x);
        // 扩展并赋值
        bounds.Expand(expand);
        smr.localBounds = bounds;
    }

    public static void TryMove(this CharacterController Controller,Vector3 value,bool isTeleport=false)
    {
        if (Controller.enabled)
        {
            if (isTeleport) Controller.Teleport(value);
            else Controller.Move(value);
        }
    }


   
    /// <summary>
    /// 向指定方向传送，忽略路径障碍，允许空中，但避免卡进地下
    /// </summary>
    public static void Teleport(this CharacterController controller, Vector3 direction)
    {
        Vector3 targetPosition = controller.transform.position + direction;

        // 先检测地下再移动，避免 CharacterController 自身的碰撞体干扰 SphereCast
        Vector3 finalPosition = PreventUnderground(controller, targetPosition);
        controller.transform.position = finalPosition + 0.2f * Vector3.up;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// 防止角色卡进地下（修正Y轴位置）
    /// 先用地形高度保底，再用 SphereCast 检测建筑物等人工结构
    /// </summary>
    private static Vector3 PreventUnderground(CharacterController controller, Vector3 position)
    {
        float halfHeight = controller.height * 0.5f;
        float radius = controller.radius * 0.9f;
        float skinWidth = 0.01f;
        float maxCheckDistance = 100f;
        LayerMask groundMask = LayerDefinition.GroundLayers;
        RaycastHit hit;

        // === 第一步：地形优先 ===
        float terrainSurfaceY = TerrainUtils.Main != null ? TerrainUtils.Main.WSToHeight(position) : float.MinValue;
        float terrainMinY = terrainSurfaceY - skinWidth;

        if (TerrainUtils.Main != null)
        {
            // 目标在地形以下 → 直接修正到地形表面
            if (position.y < terrainMinY)
            {
                float correctedY = terrainSurfaceY + halfHeight + skinWidth;
                return new Vector3(position.x, correctedY, position.z);
            }
        }

        // === 第二步：SphereCast 向下检测（建筑物等人工结构）===
        // 注：能走到这里的 position.y 一定在地形以上（Step 1 已处理地下），
        // 所以 origin = position + (halfHeight + radius) 不会从地形内部出发
        Vector3 origin = position + Vector3.up * (halfHeight + radius);

        if (Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out hit,
            maxCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            float groundDistance = hit.distance - halfHeight - radius;

            // 仅在紧贴/低于地面时修正（正常空中保持原位）
            if (groundDistance < skinWidth)
            {
                float sphereCastY = hit.point.y + halfHeight + skinWidth;
                float bestY = TerrainUtils.Main != null
                    ? Mathf.Max(sphereCastY, terrainSurfaceY + halfHeight + skinWidth)
                    : sphereCastY;
                return new Vector3(position.x, bestY, position.z);
            }
            // 空中正常 → 不修正
            return position;
        }

        // === 第三步：SphereCast 向上检测（完全在地下时的兜底）===
        origin = position + Vector3.up * (radius + 0.01f);

        if (Physics.SphereCast(
            origin,
            radius,
            Vector3.up,
            out hit,
            maxCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            float correctedY = hit.point.y + halfHeight + skinWidth;
            return new Vector3(position.x, correctedY, position.z);
        }

        // === 第四步：最终兜底 ===
        if (TerrainUtils.Main != null && position.y < terrainMinY)
        {
            float correctedY = terrainSurfaceY + halfHeight + skinWidth;
            return new Vector3(position.x, correctedY, position.z);
        }

        return position;
    }




}


