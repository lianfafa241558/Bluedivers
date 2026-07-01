using System.Collections.Generic;
using Core;
using Core.Interface;
using FpsGame.Mission;
using UnityEngine;

public class MissionWnd : Window
{

    public MissionHUDItem MissionMainPrefab;
    public MissionHUDItem MissionSubPrefab;
    public MissionHUDItem MissionExtraPrefab;

    public RectTransform MainLayout, ExtraLayout, NestLayout;
    //public AudioClip m_InitSound;//任务初始化时播放的声音
    public AudioClip m_MainCompletedSound;//主要任务完成时播放的声音
    public AudioClip m_ExtraCompletedSound;//额外任务完成时播放的声音


    //任务和对应ui的字
    Dictionary<MissionBase, MissionHUDItem> m_ObjectivesDictionnary=new();


 

    protected override void FirstShowWnd()
    {

    }

    protected override void ShowWnd()
    {
        BattleEventSub.OnMissionStart += OnObjectiveCreated;
        BattleEventSub.OnMissionStateChange += OnMissionShowStateChange;
        BattleEventSub.OnMissionUpdate += OnObjectiveUpdate;
        BattleEventSub.OnMissionCompleted += OnObjectiveCompleted;
        BattleEventSub.OnMissionFail += OnObjectiveFail;
        BattleEventSub.OnMissionEnd += OnObjectiveEnd;
        BattleEventSub.OnMissionEntityShow += OnMissionShow;
    }

    protected override void HideWnd()
    {
        BattleEventSub.OnMissionStart -= OnObjectiveCreated;
        BattleEventSub.OnMissionStateChange -= OnMissionShowStateChange;
        BattleEventSub.OnMissionUpdate -= OnObjectiveUpdate;
        BattleEventSub.OnMissionCompleted -= OnObjectiveCompleted;
        BattleEventSub.OnMissionFail -= OnObjectiveFail;
        BattleEventSub.OnMissionEnd -= OnObjectiveEnd;
        BattleEventSub.OnMissionEntityShow -= OnMissionShow;
    }

    /// <summary>
    /// 有任务更新时
    /// </summary>
    void OnObjectiveUpdate(MissionBase evt)
    {
        if (m_ObjectivesDictionnary.TryGetValue(evt, out MissionHUDItem toast))
        {
            toast.UpdateStage();
        }
    }

    /// <summary>
    /// 有任务创建时
    /// </summary>
    public void OnObjectiveCreated(MissionBase mission)
    {
        //Debug.LogError("创建任务"+ mission.title, mission);
        MissionHUDItem go = null;
        RectTransform par = null;
        switch (mission.missionType)
        {
            case MissionType.Main:
                if (mission.parent)
                {
                    go = MissionSubPrefab;
                    if (m_ObjectivesDictionnary.TryGetValue(mission.parent, out MissionHUDItem toast2))
                    {
                        par = (RectTransform)toast2.subGroup.transform;
                    }
                    else
                    {
                        Debug.LogError(mission.title + "没有找到父级任务");
                    }
                }
                else
                {
                    go = MissionMainPrefab;
                    par = MainLayout;
                }
                break;
            case MissionType.Extra:
                go = MissionExtraPrefab;
                par = ExtraLayout;
                break;
            case MissionType.Nest:
                go = MissionExtraPrefab;
                par = NestLayout;
                break;
        }
        
        MissionHUDItem toast = Instantiate(go, par);
        toast.Initialize(mission);

        int targetIndex = 0;
        // 遍历父物体下所有子物体，找到比当前 priority 小的第一个位置
        for (int i = 0; i < par.childCount; i++)
        {
            MissionHUDItem item = par.GetChild(i).GetComponent<MissionHUDItem>();
            if (mission.priority > item.mission.priority)
            {
                targetIndex = i;
                break;
            }
            else
            {
                targetIndex = par.childCount;
            }
            
        }
        // 设置排序位置
        toast.transform.SetSiblingIndex(targetIndex);
        m_ObjectivesDictionnary.Add(mission, toast);
        //RefreshContentSizeFitter(toast.transform);


    }

    public void OnMissionShowStateChange(MissionBase mission,bool state)
    {
        if (m_ObjectivesDictionnary.TryGetValue(mission, out MissionHUDItem toast))
        {
            //隐藏的优先级最高
            state |= (mission.HasTag(GameContract.MissionTag.StratDiscovered)&& mission.missionType== MissionType.Main);
            state &= !mission.HasTag(GameContract.MissionTag.hideSelf);
            state &= !mission.HasTag(GameContract.MissionTag.hideAll);
            toast.StateChange(state);
        }
    }

    /// <summary>
    /// 有任务完成时
    /// </summary>
    public void OnObjectiveCompleted(MissionBase mission)
    {
        AudioSvc.PlaySound(new(mission.missionType==MissionType.Main&& mission .parent==null ? m_MainCompletedSound:m_ExtraCompletedSound, AudioGroups.UI));
        AudioSvc.Suppressed(3);
        if (m_ObjectivesDictionnary.TryGetValue(mission, out MissionHUDItem toast))
        {
            toast.Completed();
        }
        //Debug.LogError("任务"+objective.title+"完成");
    }

    /// <summary>
    /// 有任务失败时
    /// </summary>
    public void OnObjectiveFail(MissionBase mission)
    {
        if (m_ObjectivesDictionnary.TryGetValue(mission, out MissionHUDItem toast))
        {
            toast.Fail();
        }
    }

    /// <summary>
    /// 有任务移除时
    /// </summary>
    public void OnObjectiveEnd(MissionBase objective)
    {
        //Debug.LogError("任务" + objective.title + "结束");
        //支线任务才移除，主线是显示完成的
        //if (objective.missionType != MissionType.Main && m_ObjectivesDictionnary.TryGetValue(objective, out MissionHUDItem toast))
        //{
        //    //Debug.LogError("移除" + toast.gameObject);
        //    //Tool.Destroy(toast.gameObject);
        //}
        m_ObjectivesDictionnary.Remove(objective);
    }


    /// <summary>
    /// 有任务暴露时
    /// </summary>
    /// <param name="mission"></param>
    public void OnMissionShow(I_Entity point)
    {
        if (point is MissionView view)
        {
            MissionBase mission = view.mission;
            if (mission.missionType == MissionType.Main && mission.parent != null && m_ObjectivesDictionnary.TryGetValue(mission, out MissionHUDItem toast))
            {
                toast.StateChange(true);
            }
        }
        
    }
}
