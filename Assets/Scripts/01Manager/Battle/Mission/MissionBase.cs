using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FpsGame.Mission
{
    public enum MissionType
    {
        Main,Extra,Nest
    }


    /// <summary>
    /// 任务目标(逻辑层)
    /// </summary>
    public abstract class MissionBase : TickBehaviour  //虽然他自己不用，但是他的子类用
    {

        public event Action<MissionBase> OnMissionCompleted;

        public MissionType missionType;


        [Foldout("标旗")]
        [CustomLabel("是否隐藏")]
        public bool hide;
        [Foldout("标旗")]
        [CustomLabel("是否暴露在小地图上")]
        public bool displayMiniMap;
        [Foldout("标旗")]
        [CustomLabel("显示一个区域")]
        public bool isArea;
        [Foldout("标旗")]
        [CustomLabel("跟随地图缩放")]
        public bool followMapScale;
        [Foldout("标旗")]
        [CustomLabel("完成时隐藏图标")]
        public bool compleHide;

        //这玩意应该放着这里吗？我得想想
        [SerializeField]
        private List<GameObject> prefabs;

        [Header("信息")]
        [DisplayField(true, false, false)]
        [CustomLabel("标题")]
        public string title;
        [DisplayField(true, false, false)]
        [CustomLabel("当前目标提示")]
        public string tip;
        [DisplayField(true, false, false)]
        [CustomLabel("最大任务进度")]
        public int MaxProgress;
        [DisplayField(true, false, false)]
        [CustomLabel("当前任务进度")]
        public int NowProgress;

        [CustomLabel("允许部署战备的范围")]
        public int AirdropRange = 10;

        [CustomLabel("占地面积的取值范围(半径)")]
        public Vector2Int mapEntitySize = Vector2Int.one * 20;

        [Header("子任务")]
        public MissionBase[] subTask;

        [DisplayField(true, false, false)]
        public MissionBase parent;//主任务

        [SerializeField]
        [DisplayField(true, false, false)]
        protected TaskManager.TaskItem data;
        [SerializeField]
        [DisplayField(true, false, false)]
        protected TaskManager.SelectTaskData root;

        [HideInInspector]
        public Color color;//暂时只有巢穴用
        [HideInInspector]
        public Sprite icon;
        [HideInInspector]
        public Vector3 pos;
        [HideInInspector]
        public int entitySize;//半径

        [DisplayField(false)]
        public float percentage;//完成百分比(用来显示条)

        [Header("显示")]
        [DisplayField(false)]
        [SerializeField]
        /// <summary>在部署战备范围内</summary>
        private bool InAirdropRange;


        public MissionView entity;



        public void Init(TaskManager.SelectTaskData root, TaskManager.TaskItem data, Sprite icon, Vector3 pos, int entitySize)
        {
            this.data = data;
            this.root = root;
            this.icon = icon;
            this.pos = pos;
            this.entitySize = entitySize;
            switch (missionType)
            {
                case MissionType.Main:
                    if (data.cfg is MissionMainData_SO maincfg)
                    {
                        color = maincfg.color;
                    }
                    else
                    {
                        //Debug.LogError("错误:mission"+name+"不是主要任务", gameObject);
                        color = Color.white;
                    }
                    break;
                case MissionType.Extra:
                    color = Color.white;
                    break;
                case MissionType.Nest:
                    color = root.campData.Color;
                    break;
            }
            
            title = data.cfg.desc;

            if (prefabs.Count > 0)
            {
                entity = Instantiate(prefabs.RandomTake(), pos, Quaternion.Euler(0, RandomUtils.Range(0, 360), 0)).GetComponent<MissionView>();
                //Debug.LogError("初始化实体"+ entity);
                entity.Init(this, this.data.cfg.RequiredAD.Select(item=>item.ID).ToArray());
            }
            if (data.cfg.RequiredAD.Count == 0) AirdropRange = 0;
            CreatMission();

            GameRoot.CreateTimer(() => {
                GlobalEventManager.MissionCreated(this);
                //主任务直接显示
                if (missionType==MissionType.Main && !hide)
                {
                    GlobalEventManager.MissionStateChange(this, true);
                }
                if (displayMiniMap)
                {
                    if(entity.IsValid()) entity.TryDiscovered();
                }

            }, 0.2f);
        }



        protected virtual void CreatMission()
        {
            

        }

        protected virtual void UpdateMission(bool refresh = true)
        {
            GlobalEventManager.MissionUpdate(this, refresh);
        }

        protected virtual void CompleteMission()
        {
            data.complete = true;
            if (missionType == MissionType.Main&&!parent) root.result = GameResult.Victory;
            GlobalEventManager.MissionCompleted(this);
            OnMissionCompleted?.Invoke(this);
            EndMission();
        }

        protected virtual void FailMission()
        {
            if (missionType == MissionType.Main) root.result = GameResult.Failure;
            GlobalEventManager.MissionFail(this);
            EndMission();
        }
        protected virtual void EndMission()
        {
            GlobalEventManager.MissionEnd(this);
            if(entity.IsValid()) entity.Uninit();
        }

        protected void UpdateTip(string tip)
        {
            this.tip = tip;
            UpdateMission(false);
        }


        protected void UpdateText(string title, string tip)
        {
            this.title = title;
            this.tip = tip;
            UpdateMission(true);
        }


        protected void UpdateHide(bool hide)
        {
            this.hide = hide;
            UpdateMission(true);
        }

        /// <summary>
        /// 用来暴露一个任务给另一个任务
        /// </summary>
        /// <param name="mission"></param>
        public virtual void Link(MissionBase mission)
        {


        }

        public override bool Tick()
        {
            if (entitySize <= 0) return true;

            float dis = Vector2.Distance(ActorsManager.Player.Pos.ToVector2(), pos.ToVector2());
           
            bool airdropRange = dis < AirdropRange;
            if (airdropRange != InAirdropRange)
            {
                InAirdropRange = airdropRange;
                foreach (var ad in data.cfg.RequiredAD)
                {
                    BattleManager.Instance.Authorize(ad.ID, airdropRange);
                }
            }

            return true;
        }

        protected void CreatNotice(string role, string type, Func<bool> func = default, float delay = 0, float vaildTime = -1)
        {
            WndManager.Instance.CreatNotice(role, type, func, delay, vaildTime);
        }


        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, entitySize + 10);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(pos, entitySize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pos, AirdropRange);
        }

    }
}