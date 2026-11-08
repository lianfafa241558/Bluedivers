using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.MapUtils;
using FPSGame.GameContract;
using PEMaths;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using FPSGame.Utils;

namespace FPSGame.Gameplay
{
public static partial class FpsHelper
{
    public static bool IsTarget(IActor actor, TargetCfg targetCfg)
    {
        // 位运算代替 Enum.HasFlag：等价但不装箱（这个方法在索敌/范围查询里被逐单位调用）
        if (actor.IsValidMono()
            && (actor.ActorState & targetCfg.actorState) == targetCfg.actorState
            && (actor.Type & targetCfg.targetType) == targetCfg.targetType
        ){
            return true;
        }
        return false;
    }
    public static bool VaildTarget(IActor target)
    {
        return target != null && !Object.ReferenceEquals(target, null) && target.ActorState != ActorState.Dead && !target.HasFlag(ActorFlag.Invincible);
    }

    
    /// <summary>仇恨值：**越大越该优先打** = 仇恨系数 ÷ 距离（就近优先）。
    /// <para>⚠ 原来是"距离 × 仇恨"= 越远越优先，与两处调用方的 `&gt;` 比较正好读反
    /// （2026-11-08 用户确认填反；表现为放着眼前的不打、锁住远处/空中的目标傻站）。</para>
    /// <para>返回 0 = 不值得打（仇恨系数 0，或取不到位置）。</para>
    /// </summary>
    public static PEInt ThreatValue(PEVector3 pos,IActor target)
    {
        if (!target.IsValidMono() || !target.gameObject) return 0;
        return ToThreat(target.Threat, PEVector3.Distance(pos, (PEVector3)target.CenterPos).RawFloat);
    }
    public static PEInt ThreatValue(Vector3 pos, IActor target)
    {
        if (!target.IsValidMono()|| !target.gameObject) return 0;
        return ToThreat(target.Threat, PEVector3.Distance((PEVector3)pos, (PEVector3)target.CenterPos).RawFloat);
    }

    /// <summary>仇恨系数 ÷ 距离（距离下限 1m，免得贴脸时爆成大数）；×100 留点精度。</summary>
    private static PEInt ToThreat(float threat, float distance)
    {
        if (threat <= 0f) return 0;
        return (PEInt)(threat * 100f / Mathf.Max(distance, 1f));
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
}
}
