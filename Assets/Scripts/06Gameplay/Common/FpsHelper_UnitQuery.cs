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
    public static bool IsTarget(I_Actor actor, TargetCfg targetCfg)
    {
        if (actor.IsValidMono()
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
        if (!target.IsValidMono() || !target.gameObject) return 0;
        return PEVector3.Distance(pos, (PEVector3)target.CenterPos) * (PEInt)target.Threat;
    }
    public static PEInt ThreatValue(Vector3 pos, I_Actor target)
    {
        if (!target.IsValidMono()|| !target.gameObject) return 0;
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
}
}
