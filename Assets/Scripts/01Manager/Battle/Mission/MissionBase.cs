using System;
using System.Collections.Generic;
using Core;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

/// <summary>
/// 任务目标(逻辑层)
/// </summary>
public abstract class MissionBase : TickBehaviour  //虽然他自己不用，但是他的子类用
{
    
    public event Action<MissionBase> OnMissionCompleted;


    public bool isMain=>data.cfg.type<MissionEnum.BlackBox;//小于第一个副任务

    [CustomLabel("是否隐藏")]
    public bool hide;

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
    [DisplayField(true,false,false)]
    [CustomLabel("当前任务进度")]
    public int NowProgress;

    [CustomLabel("允许部署战备的范围")]
    public int AirdropRange=10;

    [CustomLabel("占地面积(半径)")]
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
    /// <summary>在任务范围内</summary>
    private bool InEntityRange;
    /// <summary>在部署战备范围内</summary>
    private bool InAirdropRange;
    /// <summary>已使用战备</summary>
    private bool allowUseAirdrop;
    /// <summary>已被发现</summary>
    private bool discovered;

    public I_MissionPoint entity;

    

    public void Init(TaskManager.SelectTaskData root,TaskManager.TaskItem data,Sprite icon,Vector3 pos,int entitySize)
    {
        this.data = data;
        this.root = root;
        this.icon = icon;
        this.pos = pos;
        this.entitySize = entitySize;
        title = data.cfg.desc;

        if (prefabs.Count > 0)
        {
            entity = Instantiate(prefabs.RandomTake(), pos, Quaternion.Euler(0, RandomUtils.Range(0, 360), 0)).GetComponent<I_MissionPoint>();
            //Debug.LogError("初始化实体"+ entity);
        }
        CreatMission();

        GameRoot.CreateTimer(() => {
            GlobalEventManager.MissionCreated(this);
            //主任务直接显示
            if (isMain && !hide)
            {
                if(entity.IsValid()) GlobalEventManager.MissionShow(this);
                GlobalEventManager.MissionStateChange(this, true);
            }
        }, 0.2f);
    }



    protected virtual void CreatMission()
    {
        if (data.cfg.RequiredAD.Count == 0) AirdropRange = 0;
        else GlobalEventManager.OnAirdrop += OnAirdrop;

        if (entity.IsValid()) GlobalEventManager.OnMark += Mark;
    }

    protected virtual void UpdateMission(bool refresh=true)
    {
        GlobalEventManager.MissionUpdate(this, refresh);
    }

    protected virtual void CompleteMission()
    {
        data.complete = true;
        if (isMain) root.result = GameResult.Victory;
        GlobalEventManager.MissionCompleted(this);
        OnMissionCompleted?.Invoke(this);
        EndMission();
    }

    protected virtual void FailMission()
    {
        if (isMain) root.result = GameResult.Failure;
        GlobalEventManager.MissionFail(this);
        EndMission();
    }
    protected virtual void EndMission()
    {
        GlobalEventManager.MissionEnd(this);
        if (!allowUseAirdrop) GlobalEventManager.OnAirdrop -= OnAirdrop;
        if (entity.IsValid() && !discovered) GlobalEventManager.OnMark -= Mark;
    }

    private void Mark(GameObject owner, GameObject target, Vector3 point)
    {
        if (!target) return;
        
        if (!discovered && target && target.transform.IsChildOf(entity.transform))
        {
            TryDiscovered();
        }
    }

    private void TryDiscovered()
    {
        discovered = true;
        GlobalEventManager.MissionShow(this);
        GlobalEventManager.OnMark -= Mark;
    }

    private void OnAirdrop(GameObject source, GameObject beacon, Vector3 point, AirdropController.AirdropData data)
    {
        if (this.data.cfg.RequiredAD.FindIndex(item => item.ID == data.cfg.ID) > -1)
        {
            allowUseAirdrop = true;
            GlobalEventManager.OnAirdrop -= OnAirdrop;
        }

    }

    protected void UpdateTip(string tip)
    {
        this.tip = tip;
        UpdateMission(false);
    }


    protected void UpdateText(string title,string tip)
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
    public virtual void Link(MissionBase mission) {


    }

    public override bool Tick()
    {
        if (entitySize <= 0) return true;

        float dis = Vector2.Distance(ActorsManager.Player.Pos.ToVector2(), pos.ToVector2());
        bool entityRange = dis < entitySize+10;

        if (entityRange!= InEntityRange)
        {
            InEntityRange = entityRange;
            if (entityRange&&!discovered) 
            {
                TryDiscovered();
                CreatNotice("Kotama", "ApproachingTarget", () => InEntityRange && !InAirdropRange);
            }
            GlobalEventManager.MissionStateChange(this, entityRange);
        }

        bool airdropRange = dis < AirdropRange;
        if (airdropRange != InAirdropRange)
        {
            InAirdropRange = airdropRange;
            if (airdropRange)//进去又出来就不说了
            {
                if (!allowUseAirdrop) CreatNotice("Kotama", "TaskPodVaildAble", () => InAirdropRange);

                foreach (var ad in data.cfg.RequiredAD)
                {
                    BattleManager.Instance.Authorize(ad.ID, true);
                }

            }
            else
            {
                if (!allowUseAirdrop) CreatNotice("Kotama", "TaskPodUnvaildAble", () => InAirdropRange);
                foreach (var ad in data.cfg.RequiredAD)
                {
                    BattleManager.Instance.Authorize(ad.ID, false);
                }
            }
        }
        

        return true;
    }

    protected void CreatNotice(string role, string type,Func<bool> func = default, float delay = 0, float vaildTime = -1)
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
