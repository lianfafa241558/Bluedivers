using UnityEngine;
using System.Linq;
using Core;
using GameContract;
using Utils;
using Unity.FPS.Game;



namespace FpsGame.Mission
{


    public class MissionView : BaseObject, I_MissionPoint
    {
        #region 接口
        public override float HalfRange => mission.entitySize;
        public bool IsArea => mission.isArea;
        #endregion


        /// <summary>在部署战备范围内</summary>
        private bool InAirdropRange;
        /// <summary>已使用战备</summary>
        private bool allowUseAirdrop;
        /// <summary>已被发现</summary>
        private bool discovered { get; set; }

        private MissionBase mission;
        private int[] requiredAD;

        public void Init(MissionBase mission, int[] requiredAD)
        {
            this.mission = mission;
            this.requiredAD = requiredAD;
            GlobalEventManager.OnMark += Mark;
            if (requiredAD.Length > 0) GlobalEventManager.OnAirdrop += OnAirdrop;
        }

        public void Uninit()
        {
            if (!discovered) GlobalEventManager.OnMark -= Mark;
            if (requiredAD.Length > 0) GlobalEventManager.OnAirdrop -= OnAirdrop;
        }

        private void Update()
        {
            if (!BattleManager.Instance.IsStartBattle) return;

            var dis = Vector2.Distance(ActorsManager.Player.Pos.ToVector2(), Pos.ToVector2());

            bool entityRange = dis < HalfRange + 10;

            if (entityRange && !discovered)
            {
                TryDiscovered();
                CreatNotice("Kotama", "ApproachingTarget", () => !InAirdropRange);
                GlobalEventManager.MissionStateChange(mission, entityRange);
            }

            bool airdropRange = dis < mission.AirdropRange;
            if (airdropRange != InAirdropRange)
            {
                InAirdropRange = airdropRange;
                if (airdropRange)//进去又出来就不说了
                {
                    if (!allowUseAirdrop) CreatNotice("Kotama", "TaskPodVaildAble", () => InAirdropRange);
                }
                else
                {
                    if (!allowUseAirdrop) CreatNotice("Kotama", "TaskPodUnvaildAble", () => InAirdropRange);
                }
            }

        }

        protected void CreatNotice(string role, string type, System.Func<bool> func = default, float delay = 0, float vaildTime = -1)
        {
            WndManager.Instance.CreatNotice(role, type, func, delay, vaildTime);
        }

        public void TryDiscovered()
        {
            discovered = true;
            GlobalEventManager.MissionShow(mission);
            GlobalEventManager.OnMark -= Mark;
        }

        private void Mark(GameObject owner, GameObject target, Vector3 point)
        {
            if (!target) return;

            if (!discovered && target && target.transform.IsChildOf(transform))
            {
                TryDiscovered();
            }
        }

        private void OnAirdrop(GameObject source, GameObject beacon, Vector3 point, AirdropController.AirdropData data)
        {
            if (requiredAD.Contains(data.cfg.ID))
            {
                allowUseAirdrop = true;
                GlobalEventManager.OnAirdrop -= OnAirdrop;
            }
        }
    }
}