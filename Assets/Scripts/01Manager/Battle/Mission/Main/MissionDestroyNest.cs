using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;

/// <summary>
/// 搜索并摧毁
/// </summary>
public class MissionDestroyNest : MissionBase
{


    protected override void CreatMission()
    {
        base.CreatMission();
        GlobalEventManager.OnEnemyDead += OnActorDeath;
        MaxProgress = data.targetCount;
    }
    protected override void EndMission()
    {
        base.EndMission();
        GlobalEventManager.OnEnemyDead -= OnActorDeath;
    }

    void OnActorDeath(Actor actor)
    {
        if (!actor.HasFlag(Core.ActorFlag.Nest)) return;
        if (++NowProgress < MaxProgress)
        {
            UpdateMission(false);
        }
        else
        {
            UpdateMission(false);
            CompleteMission();
        }
    }
}
