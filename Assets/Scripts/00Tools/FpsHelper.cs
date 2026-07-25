using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Interface;
using GameContract;
using PEMaths;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using Utils;

public static class FpsHelper
{
    static FpsHelper()
    {
        //bulletHoles = ResSvc.Instance.LoadObjects<GameObject>("VFX/Weapon/BulletHole/Base");
        bulletHoles = Resources.LoadAll<GameObject>("VFX/Weapon/BulletHole/Base").ToList();
    }


    private static List<GameObject> bulletHoles;



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

            comp.InflictDamage(comp, damageData.GetDirectDamage(charg)* damageScale, damageData.DamageGroupDirect, damageData.GetWeaknessBonus(), damageData.NoSource || !owner, owner, point);
            
        }
         
        //爆炸
        if (BattleManager.Instance.IsValid() && damageData.UseExplode)
        {
            var unitList = BattleManager.Instance.FindUnits(new PECircle((PEVector2)point, damageOuterRadius), new());
            if (hitData.IgnoreSelf) unitList.Remove(owner.GetComponent<I_Actor>());
            var list= unitList.Select(item => item.Damageables)
                .SelectMany(item => item)
                .Where(item=>!item.IsExplosionImmunity())
                .ToList();

            //Debug.LogWarning("范围"+ damageRange + "内的目标数量"+ list.Count);
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
                    //Debug.LogWarning("对" + item.gameObject.name + "造成爆炸伤害" + value + " 基础伤害" + damageData.DamageExplosion + "距离" + distance + "直击:" + (collider.gameObject == item.gameObject));
                    item.InflictDamage(item, value, damageData.DamageGroupExplosion, damageData.GetWeaknessBonus(), damageData.NoSource || !owner, owner, point);
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
        // 用 TerrainUtils 获取地面高度
        float groundHeight = TerrainUtils.WSToHeight(pos);
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
        // 直接移动到目标点
        controller.transform.position = targetPosition;

        // 检测是否卡进地下，如果是则向上修正
        Vector3 finalPosition = PreventUnderground(controller, targetPosition);
        controller.transform.position = finalPosition;

    }

    /// <summary>
    /// 防止角色卡进地下（修正Y 轴位置）
    /// </summary>
    private static Vector3 PreventUnderground(CharacterController controller, Vector3 position)
    {
        float halfHeight = controller.height * 0.5f;
        float radius = controller.radius * 0.9f; // 用角色半径的90%，避免边缘碰撞
        float skinWidth = 0.01f;
        float maxCheckDistance = 100f;
        LayerMask groundMask = LayerDefinition.GroundLayers;

        RaycastHit hit;

        // ========== 第一步：向下检测（处理正常情况）==========
        // 从角色中心稍上位置发射 SphereCast
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
            // 计算地面距离
            float groundDistance = hit.distance - halfHeight - radius;

            // 如果卡进地下了（地面距离为负）
            if (groundDistance < 0)
            {
                // 修正到地面之上
                float correctedY = hit.point.y + halfHeight + skinWidth;
                return new Vector3(position.x, correctedY, position.z);
            }

            // 如果太靠近地面，也稍微修正一下防止抖动
            if (groundDistance < skinWidth)
            {
                float correctedY = hit.point.y + halfHeight + skinWidth;
                return new Vector3(position.x, correctedY, position.z);
            }

            //return position;
        }


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
            // 说明在地下，修正到地面之上
            float correctedY = hit.point.y + halfHeight + skinWidth;
            Debug.Log($"从地下 {position.y:F2} 修正到地面 {correctedY:F2}");
            return new Vector3(position.x, correctedY, position.z);
        }
        /*
        origin = position + Vector3.up * 100f;

        if (Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out hit,
            200f,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            float correctedY = hit.point.y + halfHeight + skinWidth;
            Debug.Log($"从高空找到地面，修正到 {correctedY:F2}");
            return new Vector3(position.x, correctedY, position.z);
        }*/

        // 真的找不到地面，返回原位置
        Debug.LogWarning("PreventUnderground: 无法检测到地面，位置保持不变");
        return position;
    }




}


