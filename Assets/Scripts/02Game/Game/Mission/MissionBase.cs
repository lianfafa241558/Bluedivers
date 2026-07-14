using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using FPSGame.Attribute;
using GameContract;

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
    /// 任务目标(逻辑)
    /// </summary>
    public abstract class MissionBase : TickBehaviour  //虽然他自己不用，但是他的子类用
    {

        public event Action<MissionBase> OnMissionCompleted;
        public event Action<MissionBase> OnMissionEnd;
        protected BattleManager manager;
        protected System.Random random;


        [Foldout("标旗",true)]

        [InspectorName("标旗")]
        [UnityEngine.Serialization.FormerlySerializedAs("tag")]
        public MissionTag missionTag;


        [Foldout("信息",true)]
        public MissionType missionType;
        [InspectorName("优先级")]
        public int priority;
        //这玩意应该放着这里吗？我得想想
        [SerializeField]
        private List<GameObject> prefabs;

        [DisplayField(true, false)]
        [InspectorName("标题")]
        public string title;
        [DisplayField(true, false)]
        [InspectorName("当前目标提示")]
        public string tip;
        [DisplayField(true, false)]
        [InspectorName("最大任务进度")]
        public int MaxProgress;
        [DisplayField(true, false)]
        [InspectorName("当前任务进度")]
        public int NowProgress;

        [InspectorName("允许部署战备的范围")]
        public int AirdropRange = 10;

        [InspectorName("占地面积的取值范围（半径）")]
        public Vector2Int mapEntitySize = Vector2Int.one * 20;


        [DisplayField(true, false)]
        public MissionBase parent;//主任务

        
        [DisplayField(true, false)]
        public TaskManager.TaskItem data;
        [SerializeField]
        [DisplayField(true, false)]
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
        public float percentage;//完成百分比，用来显示

        [Foldout("显示",true)]
        public MissionBase[] subTask;

        [DisplayField(false)]
        [SerializeField]
        /// <summary>在部署战备范围内</summary>
        private bool InAirdropRange;
        /// <summary>已使用战备</summary>
        private bool allowUseAirdrop;

        //[HideInInspector]
        public MissionView entity;
        public bool end;
        public bool completed;

        private Transform entityParent;

        public bool IsInitialized { get; set;}

        public void Init(TaskManager.SelectTaskData root, TaskManager.TaskItem data, Sprite icon, Vector3 pos, int entitySize,Transform entityParent)
        {
            this.data = data;
            this.root = root;
            this.icon = icon;
            this.pos = pos;
            this.entitySize = entitySize;
            this.entityParent = entityParent;
            manager = BattleManager.Instance;
            random = manager.BattleRandom;
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


            
            /*
            GameRoot.CreateTimer(() => {
                if (parent) GameRoot.CreateTimer(() => { EventInit(); }, 0.1f);
                else EventInit();

            },0.8f);*/

            if (data.cfg.RequiredAD.Count > 0) BattleEventSub.OnAirdrop += OnAirdrop;
            InitMission();
        }

        public void EventStart()
        {
            StartMission();
            BattleEventSub.MissionStart(this);
            if (missionTag.HasFlag(MissionTag.StratDiscovered)&&entity.IsValid()) entity.TryDiscovered();
            
        }


        protected sealed override void Start()
        {
            base.Start();
            //CreatMission();
        }

        private void OnDestroy()
        {
            if (!end) Uninit();
        }
        /// <summary>
        /// 所有任务初始化完成后执行
        /// </summary>
        protected virtual void StartMission()
        {
            

        }
        /// <summary>
        /// 创建后就执行
        /// </summary>
        protected virtual void InitMission()
        {

            if (prefabs.Count > 0)
            {
                entity = Instantiate(prefabs.RandomTake(), pos, Quaternion.Euler(0, RandomUtils.Range(0, 360), 0), entityParent).GetComponent<MissionView>();
                entity.Init(this, this.data.cfg.RequiredAD.Select(item => item.ID).ToArray());
            }
            else
            {
                IsInitialized = true;
            }
            if (data.cfg.RequiredAD.Count == 0) AirdropRange = 0;

        }
        public virtual void UpdateMission()
        {
            BattleEventSub.MissionUpdate(this);
        }

        public virtual void CompleteMission()
        {
            data.complete = true;
            completed = true;
            if (missionType == MissionType.Main&&!parent) root.result = GameResult.Victory;
            BattleEventSub.MissionCompleted(this);
            OnMissionCompleted?.Invoke(this);
            EndMission();
        }

        protected virtual void FailMission()
        {
            if (missionType == MissionType.Main) root.result = GameResult.Failure;
            BattleEventSub.MissionFail(this);
            EndMission();
        }
        protected virtual void EndMission()
        {
            end = true;
            if (InAirdropRange)
            {
                foreach (var ad in data.cfg.RequiredAD)
                {
                    BattleManager.Instance.Authorize(ad.ID, false);
                }
            }
            BattleEventSub.MissionEnd(this);
            OnMissionEnd?.Invoke(this);
            Uninit();
        }

        protected virtual void Uninit()
        {
            if (entity.IsValid()) entity.Uninit();
            if (data.cfg.RequiredAD.Count > 0) BattleEventSub.OnAirdrop -= OnAirdrop;
        }

        protected void UpdateTip(string tip)
        {
            if (this.tip == tip) return;
            this.tip = tip;
            UpdateMission();
        }


        protected void UpdateText(string title, string tip)
        {
            this.title = title;
            this.tip = tip;
            UpdateMission();
        }


        protected void UpdateHide(bool hide)
        {
            if (hide) AddTag(MissionTag.hideAll);
            else RemoveTag(MissionTag.hideAll);
            UpdateMission();
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
            if (allowUseAirdrop) return true;
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

        private void OnAirdrop(GameObject source, GameObject _, Vector3 point, AirdropController.AirdropData data)
        {
            if (this.data.cfg.RequiredAD.Contains(data.cfg))
            {
                allowUseAirdrop = true;
                BattleManager.Instance.Authorize(data.cfg.ID, false);
                BattleEventSub.OnAirdrop -= OnAirdrop;
            }
        }

        protected void CreatNotice(string role, string type, Func<bool> func = default,float vaildTime = -1)
        {
            WndManager.Instance.CreatNotice(role, type, func,vaildTime);
        }


        public bool HasTag(MissionTag tagToCheck)
        {
            return missionTag.HasFlag(tagToCheck);
        }

        public void AddTag(MissionTag tagToAdd)
        {
            missionTag |= tagToAdd;
        }

        public void RemoveTag(MissionTag tagToRemove)
        {
            missionTag &= ~tagToRemove;
        }

    }
}