using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 玩家倒地组件
    /// </summary>
    [AddComponentMenu("交互/倒地救援")]
    public class Furniture_PlayerDown : Furniture_Attached
    {

        HealthPlayer Health { get; set; }

        protected override void Awake()
        {
            base.Awake();
            Health = GetComponent<HealthPlayer>();
            canOperate = false;
            Health.OnDie += OnDown;
        }

        private void OnDestroy()
        {
            Health.OnDie -= OnDown;
        }

        public override void Operate()
        {
            base.Operate();
            //复活
            Health.Revive();
            canOperate = false;
            //PlaySound(audioOper);
            FPSGame.Gameplay.BattleEventBus.RequestAuthorize(Constants.HealBag, false);
        }


        void OnDown(GameObject _)
        {
            canOperate = true;
            PlaySound(audioClose);
            FPSGame.Gameplay.BattleEventBus.RequestAuthorize(Constants.HealBag, true);
        }
    }
}
