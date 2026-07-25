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

        protected override void StartMission()
        {

            BattleEventSub.OnEnemyDead += OnActorDeath;
            MaxProgress = (int)(data.targetCount* root.campData.enemyVarietyType.ToEnemyType() switch {
                Core.EnemyType.Kaiser => 0.625f,
                Core.EnemyType.Decagrammaton => 1,
                Core.EnemyType.Colour => 0.75f,
                _ => 1,
            });

            //MaxProgress /= 10;
        }

        protected override void Uninit()
        {
            base.Uninit();
            BattleEventSub.OnEnemyDead -= OnActorDeath;
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