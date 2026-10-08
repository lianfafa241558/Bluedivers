using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.Utils;
namespace FPSGame.Mission
{
    /// <summary>
    /// 搜索并摧毁
    /// </summary>
    [AddComponentMenu("任务/主要/搜索并摧毁", 30)]
    public class MissionDestroyNest : MissionBase
    {
        [SerializeField]
        private ActorFlag targetTag = ActorFlag.Nest;

        protected override void StartMission()
        {

            UnitEventBus.OnEnemyDead += OnActorDeath;
            MaxProgress = (int)(data.targetCount* root.campData.enemyVarietyType.ToEnemyType() switch {
                FPSGame.Core.EnemyType.Kaiser => 0.625f,
                FPSGame.Core.EnemyType.Decagrammaton => 1,
                FPSGame.Core.EnemyType.Colour => 0.75f,
                _ => 1,
            });

            //MaxProgress /= 10;
        }

        protected override void Uninit()
        {
            base.Uninit();
            UnitEventBus.OnEnemyDead -= OnActorDeath;
        }

        void OnActorDeath(Actor actor)
        {
            if (!actor.HasFlag(targetTag)) return;
            AddProgress();
        }
    }
}