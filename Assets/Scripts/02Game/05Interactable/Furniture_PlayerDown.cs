using System.Collections;
using System.Collections.Generic;
using FPSGame.Furn;
using Unity.FPS.Game;
using UnityEngine;

namespace FPSGame.Furn
{
    /// <summary>
    /// 玩家倒地组件
    /// </summary>
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
            BattleManager.Instance.Authorize(Constants.HealBag, false);
        }


        void OnDown(GameObject _)
        {
            canOperate = true;
            PlaySound(audioClose);
            BattleManager.Instance.Authorize(Constants.HealBag, true);
        }
    }
}