using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
namespace FpsGame.Mission
{
    /// <summary>
    /// 搜索并摧毁
    /// </summary>
    [AddComponentMenu("任务/主要/搜索并摧毁", 30)]
    public class MissionDestroyNest : MissionBase
    {

        protected override void CreatMission()
        {

            GlobalEventManager.OnEnemyDead += OnActorDeath;
            MaxProgress = data.targetCount;
            MaxProgress /= 10;
        }

        protected override void Uninit()
        {
            base.Uninit();
            GlobalEventManager.OnEnemyDead -= OnActorDeath;
        }

        void OnActorDeath(Actor actor)
        {
            if (!actor.HasFlag(Core.ActorFlag.Nest)) return;
            if (++NowProgress < MaxProgress)
            {
                UpdateMission();
            }
            else
            {
                //UpdateMission();
                CompleteMission();
            }
        }
    }
}