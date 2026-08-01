using UnityEngine;
using System.Linq;
using Core;
using GameContract;
using Utils;
using Unity.FPS.Game;

using System.Collections;
using UnityEngine.Events;

namespace FpsGame.Mission
{

    public class MissionView : BaseObject, I_MissionPoint
    {
        #region 接口

        public override float HalfRange => mission.entitySize;

        float I_MissionPoint.IconSizeScale => 1;

        float I_MissionPoint.AreaRange { get => areaRange; set => areaRange = value; }

        public bool HaveTag(MissionTag tag) => mission.missionTag.HasFlag(tag);


        #endregion
        #region

        #endregion
        /// <summary>在范围内</summary>
        private bool InHalfRange;
        /// <summary>在部署战备范围内</summary>
        private bool InAirdropRange;
        /// <summary>已使用战备</summary>
        private bool allowUseAirdrop;
        /// <summary>已被发现</summary>
        private bool discovered { get; set; }


        private float areaRange;//会变的
        //[HideInInspector]
        public MissionBase mission;//仅用来触发事件
        private int[] requiredAD;
        [SerializeField]
        private UnityEvent actions;


        public void Start()
        {
            StartCoroutine(nameof(Wait));
        }

        public void Init(MissionBase mission, int[] requiredAD)
        {
            this.mission = mission;
            this.requiredAD = requiredAD;
            this.discovered = HaveTag(MissionTag.StratDiscovered);
            areaRange = HaveTag(MissionTag.IsArea) ? mission.entitySize : 0;
            this.mission.OnMissionCompleted += OnMissionComplete;
            GlobalEventSub.OnMark += Mark;
            if (requiredAD.Length > 0) BattleEventSub.OnAirdrop += OnAirdrop;
        }

        public void Uninit()
        {
            this.mission.OnMissionCompleted -= OnMissionComplete;
            if (!discovered) GlobalEventSub.OnMark -= Mark;
            if (requiredAD!=null&&requiredAD.Length > 0) BattleEventSub.OnAirdrop -= OnAirdrop;
            enabled = false;
        }

        private IEnumerator Wait()
        {
            while (GetComponent<ModifyTerrain>())
            {
                yield return null;
            }
            while (!mission)
            {
                yield return null;
            }
            mission.IsInitialized = true;
        }

        private void Update()
        {
            

            if (!mission||!mission.IsInitialized) return;
            if (!BattleManager.Instance || !BattleManager.Instance.IsStartBattle) return;

            var dis = ActorsManager.Players.Min(item => Vector2.Distance(item.Pos.ToVector2(), Pos.ToVector2()));

            bool entityRange = dis < HalfRange + 10;
            if (HaveTag(MissionTag.OneDiscovered))
            {
                if (entityRange && !discovered)
                {
                    TryDiscovered();
                }
            }
            else//超出距离自动消失的任务
            {
                if (entityRange != InHalfRange)
                {
                    InHalfRange = entityRange;
                    BattleEventSub.MissionStateChange(mission, entityRange);
                }
            }


            if (entityRange && !discovered)
            {
                TryDiscovered();
                CreatNotice("Kotama", "ApproachingTarget", () => !InAirdropRange);
            }

            bool inAirdropRange = dis < mission.AirdropRange&& mission.data.cfg.RequiredAD.Count>0;
            if (inAirdropRange != InAirdropRange)
            {
                InAirdropRange = inAirdropRange;
                if (inAirdropRange)//进去又出来就不说了
                {
                    if (!allowUseAirdrop) CreatNotice("Kotama", "TaskPodVaildAble", () => InAirdropRange);
                }
                else
                {
                    if (!allowUseAirdrop) CreatNotice("Kotama", "TaskPodUnvaildAble", () => !InAirdropRange);
                }
            }

        }

        protected void CreatNotice(string role, string type, System.Func<bool> func = default, float delay = 0, float vaildTime = -1)
        {
            WndManager.Instance.CreatNotice(role, type, func,vaildTime);
        }

        public void TryDiscovered()
        {
            discovered = true;
            BattleEventSub.MissionEnityShow(this);
            GlobalEventSub.OnMark -= Mark;
            if (HaveTag(MissionTag.OneDiscovered))
            {
                BattleEventSub.MissionStateChange(mission, true);
            }
        }
        private void Mark(GameObject owner, GameObject target, Vector3 point)
        {
            if (!target) return;

            if (!discovered && target && target.transform.IsChildOf(transform))
            {
                TryDiscovered();
            }
        }

        private void OnAirdrop(GameObject source, GameObject _, Vector3 point, AirdropController.AirdropData data)
        {
            if (requiredAD.Contains(data.cfg.ID))
            {
                allowUseAirdrop = true;
                BattleEventSub.OnAirdrop -= OnAirdrop;
            }
        }

        private void OnMissionComplete(MissionBase _)
        {
            actions?.Invoke();
        }
    }
}