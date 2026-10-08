using System;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Data;
using FPSGame.Gameplay;
using FPSGame.GameData;

namespace FPSGame.Mission
{
    public enum MissionType
    {
        Main,Extra,Nest,Sub,Evacuate,
    }


    /// <summary>
    /// 任务目标(逻辑)
    /// </summary>
    public abstract class MissionBase : TickBehaviour, IResConsumer  //虽然他自己不用，但是他的子类用
    {

        /// <summary>
        /// 资源加载服务（**注入**，非定位器）：由装配根 <c>MissionController</c> 在 <c>Instantiate</c> 后、
        /// <c>Init</c> 之前注入（见 <see cref="IResConsumer"/>）。
        /// <para>⚠ 2026-10-01 取代 <c>ServiceLocator.Res</c> 槽。注入时机安全：本类与子类**不在 <c>Awake</c> 里**
        /// 用资源，只有运行时的 <c>CreatKei</c>/<c>CreatMedivac</c>/<c>Landing</c> 会用到。</para>
        /// </summary>
        protected IResService Res { get; private set; }

        /// <summary><see cref="IResConsumer"/> 实现：装配根调用（本类实例的创建点见 <c>MissionController</c>）。</summary>
        public void Inject(IResService res) => Res = res;

        public event Action<MissionBase> OnMissionCompleted;
        public event Action<MissionBase> OnMissionEnd;
        protected IBattleService manager;// 走契约（原 BattleManager），玩法层不再点名上层具体类型
        protected System.Random random;


        [Foldout("标旗",true)]

        [InspectorName("标旗")]
        [UnityEngine.Serialization.FormerlySerializedAs("tag")]
        public MissionTag missionTag;


        [Foldout("信息",true)]
        public MissionType missionType;
        [InspectorName("优先级")]
        public int priority;

        [SerializeField]
        private List<GameObject> prefabs;
        [SerializeField]
        private EnemyActorVariant_SO prefabVarients;


        [DisplayField(DisplayFieldEnum.RunRead)]
        [InspectorName("标题")]
        public string title;
        [DisplayField(DisplayFieldEnum.RunRead)]
        [InspectorName("当前目标提示")]
        public string tip;
        [DisplayField(DisplayFieldEnum.RunRead)]
        [InspectorName("最大任务进度")]
        public int MaxProgress;
        [DisplayField(DisplayFieldEnum.RunRead)]
        [InspectorName("当前任务进度")]
        public int NowProgress;

        [InspectorName("允许部署战备的范围")]
        public int AirdropRange = 10;

        [InspectorName("占地面积的取值范围（半径）")]
        public Vector2Int mapEntitySize = Vector2Int.one * 20;


        [DisplayField(DisplayFieldEnum.RunRead)]
        public MissionBase parent;//主任务

        
        [DisplayField(DisplayFieldEnum.RunRead)]
        public TaskItem data;
        [SerializeField]
        [DisplayField(DisplayFieldEnum.RunRead)]
        protected SelectTaskData root;

        [HideInInspector]
        public Color color;//暂时只有巢穴用
        [HideInInspector]
        public Sprite icon;
        [HideInInspector]
        public Vector3 pos;
        [HideInInspector]
        public int entitySize;//半径




        [Foldout("场景专用", true)]
        [InspectorName("场景任务数据(场景模式用)")]
        [SerializeField]
        protected MissionData_SO _sceneMissionData;

        [Foldout("显示",true)]
        public MissionBase[] subTask;
        [DisplayField(DisplayFieldEnum.RunRead)]
        public float percentage;//完成百分比，用来显示
        [DisplayField(DisplayFieldEnum.RunRead)]
        [SerializeField]
        /// <summary>在部署战备范围内</summary>
        private bool InAirdropRange;
        /// <summary>已使用战备</summary>
        private bool allowUseAirdrop;

        //[HideInInspector]
        public MissionView entity;
        public bool end;
        public bool completed;

        /// <summary>跨端稳定键：由 <c>MissionController</c> 按创建顺序赋值（每局归零），联机同步用。
        /// <para>⚠ 用属性而非字段 ⇒ 不参与序列化（不产生 prefab / .meta 变动）。</para></summary>
        public int netOrder { get; set; }

        /// <summary>成员侧 = true：任务**不自行推进 / 完成**，状态与进度一律以房主下发为准。
        /// <para>由 09 侧的 <c>NetMissionBridge</c> 在 Install 时按角色置位（同 <c>EnemyController.RemoteDrivenMovement</c>）。</para></summary>
        public static bool RemoteDriven;

        /// <summary>远端应用中的放行标记（见 <see cref="ApplyRemoteState"/>）：只影响"是否被 <see cref="RemoteDriven"/> 门住"。</summary>
        static bool _applyingRemote;

        private Transform entityParent;

        public bool IsInitialized { get; set;}

        public void Init(SelectTaskData root, TaskItem data, Sprite icon, Vector3 pos, int entitySize,Transform entityParent)
        {
            this.data = data;
            this.root = root;
            this.icon = icon;
            this.pos = pos;
            this.entitySize = entitySize;
            this.entityParent = entityParent;
            manager = BattleHub.Current;
            random = BattleState.BattleRandom;
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
            FPSGame.Core.TimerHost.CreateTimer(() => {
                if (parent) FPSGame.Core.TimerHost.CreateTimer(() => { EventInit(); }, 0.1f);
                else EventInit();

            },0.8f);*/

            if (data.cfg.RequiredAD.Count > 0) BattleEventBus.OnAirdrop += OnAirdrop;
            InitMission();
        }

        /// <summary>
        /// 场景模式轻量初始化：仅注入数据引用，不实例化 MissionView（场景已有）
        /// </summary>
        public void InitFromSceneData(SelectTaskData root)
        {
            this.root = root;

            manager = BattleHub.Current;
            random = BattleState.BattleRandom;

            if (_sceneMissionData != null)
            {
                if (_sceneMissionData is MissionMainData_SO maincfg)
                    data = new TaskItem(maincfg, 1f);
                else
                    data = new TaskItem(_sceneMissionData);
            }

            switch (missionType)
            {
                case MissionType.Evacuate:
                case MissionType.Sub:
                case MissionType.Main:
                    if (_sceneMissionData is MissionMainData_SO maincfg)
                        color = maincfg.color;
                    else
                        color = Color.white;
                    break;
                case MissionType.Extra:
                    color = Color.white;
                    break;
                case MissionType.Nest:
                    color = root.campData.Color;
                    break;
            }

            // 场景中 MissionView 已作为子对象存在，直接获取引用
            if (entity == null)
            {
                Debug.LogWarning("没有为其设置实体",this);
            }


            if (entity != null && data?.cfg?.RequiredAD != null)
            {
                this.icon = entity.Portrait;
                this.pos = entity.Pos;
                this.title = entity.Title;
                entity.Init(this, data.cfg.RequiredAD.Select(item => item.ID).ToArray());
            }

            if (data?.cfg?.RequiredAD?.Count > 0)
                BattleEventBus.OnAirdrop += OnAirdrop;

            if (data == null || data.cfg.RequiredAD.Count == 0)
                AirdropRange = 0;

            InitMission();
        }

        public void EventStart()
        {

            StartMission();
            //Debug.LogError("触发事件"+this,this);
            BattleEventBus.MissionStart(this);
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

        private bool GetEntiryPrefab(out GameObject prefab)
        {
            if (prefabs.Count > 0)
            {
                prefab=prefabs.RandomTake(BattleState.BattleRandom);
                return true;
            }
            else if (prefabVarients != null)
            {
                prefab = prefabVarients.Get(TaskState.EnemyVarietyType);
                return true;
            }
            prefab = null;
            return false;
        }


        /// <summary>
        /// 任务实体的朝向角（0~359）。**必须两端一致** —— 它决定任务区域（`MissionView` 的矩形/Range）的朝向，
        /// 各端各摇一次会让同一个任务在两边朝向不同（2026-10-07 用户实测）。
        ///
        /// <para>▍口径：从本局权威种子派生（用已吸附的 <c>pos</c> 当细分键，两端位置也由同一种子生成）
        /// ⇒ 同 seed + 同位置 ⇒ 恒等。单机（<c>TaskState.Seed == 0</c>）保持原来的全局静态流行为。</para>
        /// </summary>
        protected int MissionAngle()
        {
            int seed = TaskState.Seed;
            if (seed == 0) return RandomUtils.Range(0, 360);

            int h = SeedUtil.Derive(seed, SeedStream.MissionEntity);
            h = SeedUtil.Derive(h, Mathf.RoundToInt(pos.x * 10f));
            h = SeedUtil.Derive(h, Mathf.RoundToInt(pos.z * 10f));
            return (int)(unchecked((uint)h) % 360u);
        }

        /// <summary>
        /// 创建后就执行
        /// </summary>
        protected virtual void InitMission()
        {
            if (!entity && GetEntiryPrefab(out GameObject prefab))
            {
                entity = Instantiate(prefab, pos, Quaternion.Euler(0, MissionAngle(), 0), entityParent).GetComponent<MissionView>();
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
            BattleEventBus.MissionUpdate(this);
        }

        public virtual void CompleteMission()
        {
            // 成员：完成与否由房主下发（ApplyRemoteState）⇒ 本端自判一律作废，避免双重触发
            if (RemoteDriven && !_applyingRemote) return;

            data.complete = true;
            completed = true;
            if (missionType == MissionType.Main&&!parent) root.result = GameResult.Victory;
            BattleEventBus.MissionCompleted(this);
            OnMissionCompleted?.Invoke(this);
            EndMission();
        }

        protected virtual void FailMission()
        {
            if (RemoteDriven && !_applyingRemote) return;   // 同 CompleteMission：成员不自判

            if (missionType == MissionType.Main) root.result = GameResult.Failure;
            BattleEventBus.MissionFail(this);
            EndMission();
        }
        protected virtual void EndMission()
        {
            end = true;
            if (InAirdropRange)
            {
                foreach (var ad in data.cfg.RequiredAD)
                {
                    FPSGame.Gameplay.BattleEventBus.RequestAuthorize(ad.ID, false);
                }
            }
            BattleEventBus.MissionEnd(this);
            OnMissionEnd?.Invoke(this);
            Uninit();
        }

        protected virtual void Uninit()
        {
            if (entity.IsValid()) entity.Uninit();
            if (data.cfg.RequiredAD.Count > 0) BattleEventBus.OnAirdrop -= OnAirdrop;
        }

        /// <summary>
        /// 任务进度自增（只计数，不做 UI 与结束处理）。
        /// 已结束的任务不再计数。
        /// </summary>
        /// <returns>true = 本次自增后进度已达到 <see cref="MaxProgress"/></returns>
        protected bool TryAddProgress()
        {
            // 成员：进度以房主为准（返回 false = "尚未达成"，安全，不会被上层当成"已完成"）
            if (RemoteDriven && !_applyingRemote) return false;

            if (completed) return true;
            if (NowProgress < MaxProgress) ++NowProgress;
            return NowProgress >= MaxProgress;
        }

        /// <summary>
        /// 【联机】成员侧：按房主权威的"状态 / 进度"**直接置位**（不自行判定）。
        /// <para>由 09 侧的 <c>NetMissionBridge</c> 收包后调用。内部短暂放行 <see cref="RemoteDriven"/> 门，
        /// 让 <c>CompleteMission/FailMission/UpdateMission</c> 走完同一条链（UI 刷新与收尾都复用）。</para>
        /// <para>状态约定（与 <c>NetMissionBridge</c> 一致）：1=进行中 2=完成 3=失败。</para>
        /// </summary>
        public void ApplyRemoteState(int state, int progress, int maxProgress, float percentage)
        {
            if (end) return;                    // 已结束的不再改

            MaxProgress = maxProgress;
            NowProgress = progress;
            this.percentage = percentage;

            _applyingRemote = true;
            try
            {
                if (state == 2) { if (!completed) CompleteMission(); return; }
                if (state == 3) { FailMission(); return; }
                UpdateMission();
            }
            finally
            {
                _applyingRemote = false;
            }
        }

        /// <summary>
        /// 任务进度自增 + 通用收尾：未达成时刷新 HUD，达成时结束任务。
        /// 适用于"计满即完成"的任务（摧毁单位、交互指定物体、等子任务等），
        /// 达成时还有额外业务动作的任务请改用 <see cref="TryAddProgress"/> 自行处理。
        /// </summary>
        /// <param name="syncPercentage">是否同步刷新 <see cref="percentage"/>（要显示进度条的任务传 true）</param>
        /// <param name="updateBeforeComplete">达成时是否先刷新一次进度再结束任务</param>
        /// <returns>true = 本次调用已达成并结束了任务（可用于顺带退订事件）</returns>
        protected bool AddProgress(bool syncPercentage = false, bool updateBeforeComplete = false)
        {
            if (completed) return true;

            if (!TryAddProgress())
            {
                if (syncPercentage) percentage = NowProgress / (MaxProgress + 0f);
                UpdateMission();
                return false;
            }

            if (updateBeforeComplete) UpdateMission();
            CompleteMission();
            return true;
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
            if (hide) AddTag(MissionTag.HideAll);
            else RemoveTag(MissionTag.HideAll);
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
            if (!ActorsManager.Player.IsValidMono()) return true;
            float dis = Vector2.Distance(ActorsManager.Player.Pos.ToVector2(), pos.ToVector2());
           
            bool airdropRange = dis < AirdropRange;
            if (airdropRange != InAirdropRange)
            {
                InAirdropRange = airdropRange;
                foreach (var ad in data.cfg.RequiredAD)
                {
                    FPSGame.Gameplay.BattleEventBus.RequestAuthorize(ad.ID, airdropRange);
                }
            }

            return true;
        }

        private void OnAirdrop(GameObject source, GameObject _, Vector3 point, AirdropData data)
        {
            if (this.data.cfg.RequiredAD.Contains(data.cfg))
            {
                if (!HasTag(MissionTag.RepeatCall))
                {
                    allowUseAirdrop = true;
                    FPSGame.Gameplay.BattleEventBus.RequestAuthorize(data.cfg.ID, false);
                    BattleEventBus.OnAirdrop -= OnAirdrop;

                }

            }
        }

        protected void CreatNotice(string role, string type, Func<bool> func = default,float vaildTime = -1)
        {
            FPSGame.Gameplay.GlobalEventBus.Notice(role, type, func, vaildTime);
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