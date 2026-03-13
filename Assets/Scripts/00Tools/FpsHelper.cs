using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Game;
using PEMaths;
using System.Linq;
using BaseLibrary;
using Unity.BaseTool;
using Core;
using Utils;
using GameContract;
using UnityEngine.AI;

public static class FpsHelper
{
    static FpsHelper()
    {
        bulletHoles = ResManager.Instance.LoadObjects<GameObject>("VFX/Weapon/BulletHole/Base");    
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
        Collider collider = hitData.collider;//要考虑碰撞体为空(爆炸)
        float charg = hitData.chargeScale;
        GameObject owner = hitData.owner;
        
        var damageData = hitData.data;
        PEInt damageRange = damageData.GetExplosionRange(charg); 
        PEInt damageScale= (hitData.useDiffScale ? DiffDamageScale() : 1);
        //Debug.LogWarning(collider + "蓄力" + charg + "最终范围" + damageRange+"伤害组成数量"+ damageData.DamageGroup.Count, collider);

        //直击
        if (damageData.DamageDirect>0&&collider.IsValid()&& collider.TryGetComponent(out I_Damagable comp)&& comp.Source.IsValid())
        {
            //Debug.LogWarning("对" + collider.gameObject.name, collider.gameObject);
            //Debug.LogWarning("造成直击伤害" + damageData.GetDirectDamage(charg), collider.gameObject);
            //Debug.LogWarning(" 基础伤害" + damageData.DamageDirect, collider.gameObject);

            comp.InflictDamage(comp, damageData.GetDirectDamage(charg)* damageScale, damageData.DamageGroupDirect, damageData.NoSource || !owner, owner, point);
            
        }
        //爆炸
        if (damageRange > 0&&GameRoot.GameState == GameStateEnum.Game)
        {
            var unitList = BattleManager.Instance.FindUnits(new PECircle((PEVector2)point, damageRange), new());
            if (hitData.IgnoreSelf) unitList.Remove(owner.GetComponent<I_Actor>());
            var list= unitList.Select(item => item.Damageables)
                .SelectMany(item => item)
                .ToList();

            //Debug.LogWarning("范围"+ damageRange + "内的目标数量"+ list.Count);
            foreach (Damageable item in list)
            {
                if(item.ExplosionBlocking(point,out var hitCollider))
                {
                    //float distance = Vector3.Distance(hitCollider.bounds.center, point);
                    float distance = Vector3.Distance(hitCollider.ClosestPointOnBounds(point), point);
                    PEInt value = damageData.GetExplosionDamage(charg, (PEInt)distance) * damageScale;
                    if (value > 0)
                    {
                        //Debug.LogWarning("对" + item.gameObject.name + "造成爆炸伤害" + value + " 基础伤害" + damageData.DamageExplosion + "距离" + distance + "直击:" + (collider.gameObject == item.gameObject));
                        item.InflictDamage(item, value, damageData.DamageGroupExplosion, damageData.NoSource || !owner, owner, point);
                    }
                }
            }
            
        }

        //特效
        if (damageData.ImpactVfx)
        {
            var ps = VFXManager.Creat(damageData.ImpactVfx, point + (normal * damageData.ImpactVfxSpawnOffset), damageData.UseCollisionDirection ? Quaternion.LookRotation(normal) : default, collider.IsValid()? collider.transform:null);
            ps.GetComponentInChildren<VfxEffect>()?.SetOwner(owner,hitData.weapon.gameObject, collider, point);
            ps.GetComponentInChildren<ProjectileBase>()?.Shoot(hitData.weapon);
        }
        //音效
        if (damageData.ImpactSfx)
        {
            AudioManager.PlaySound(new(damageData.ImpactSfx, point, hitData.sfxRange,AudioGroups.Impact));
        }
        //弹痕
        if (damageData.UseHole)
        {
            VFXManager.Creat(damageData.Hole.IsValid() ? damageData.Hole : bulletHoles.RandomTake(), point, Quaternion.LookRotation(normal), collider.IsValid() ? collider.transform : null);
        }

        //地形破坏
        var terrainItem = hitData.data.DamageGroupDirect.Find(item => item.Key == DamageTypeEnum.Terrain);
        if (terrainItem.IsValid())
        {
            var range = (PEInt)terrainItem.Value * PEMath.Max(damageRange, 1);
            TerrainUtils.ModifyHeightMap(point, range.RawInt/2, range.RawInt, PEMath.Sqrt(range).RawFloat, ShapeType.Circle,false);
        }

        if (!hitData.data.NoSource&&(!collider.IsValid() || collider.GetComponent<I_Damagable>()==null))
        {
            //if (collider.transform.TryGetComponentInParent(out Actor actor) && actor != ActorsManager.Player) GlobalEventManager.BulletHit(owner, point);
            GlobalEventManager.BulletHit(owner, point);
        }
    }




    public static bool IsTarget(Actor actor, TargetCfg targetCfg)
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
        return PEVector3.Distance(pos, (PEVector3)target.AimPoint.position) * (PEInt)target.Threat;
    }
    public static PEInt ThreatValue(Vector3 pos, I_Actor target)
    {
        return PEVector3.Distance((PEVector3)pos, (PEVector3)target.AimPoint.position) * (PEInt)target.Threat;
    }

    public static bool HaveNavMeshAgent(NavMeshAgent navMeshAgent) => navMeshAgent && navMeshAgent.isActiveAndEnabled;




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
        PEInt scale = 1 + (TaskManager.Instance.nowTaskCfg.ExtraDifficulty[0] * (PEInt)0.334f)+(ActorsManager.Players.Count-1)*(PEInt)0.05f;
        switch (TaskManager.Instance.nowTaskCfg.difficulty)
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
                scale *= (PEInt)3;
                break;
            case DifficultyEnum.Lunatic:
                scale *= (PEInt)4;
                break;
        }
        return scale;
    }
}


