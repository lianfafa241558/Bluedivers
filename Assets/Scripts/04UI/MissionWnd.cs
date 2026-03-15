using System.Collections.Generic;
using Core;
using FpsGame.Mission;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class MissionWnd : WindowRoot
{

    public MissionHUDItem MissionMainPrefab;
    public MissionHUDItem MissionSubPrefab;
    public MissionHUDItem MissionExtraPrefab;

    public RectTransform MainLayout, ExtraLayout;
    //public AudioClip m_InitSound;//任务初始化时播放的声音
    public AudioClip m_MainCompletedSound;//主要任务完成时播放的声音
    public AudioClip m_ExtraCompletedSound;//额外任务完成时播放的声音


    //任务和对应ui的字典
    Dictionary<MissionBase, MissionHUDItem> m_ObjectivesDictionnary=new();


    public override void Init()
    {
        
    }
    public override void UnInit()
    {

    }

    protected override void FirstShowWnd()
    {

    }

    protected override void ShowWnd()
    {

        GlobalEventManager.OnMissionCreated += OnObjectiveCreated;
        GlobalEventManager.OnMissionStateChange += OnMissionShowStateChange;
        GlobalEventManager.OnMissionUpdate += OnObjectiveUpdate;
        GlobalEventManager.OnMissionCompleted += OnObjectiveCompleted;
        GlobalEventManager.OnMissionEnd += OnObjectiveEnd;
    }

    protected override void HideWnd()
    {
        GlobalEventManager.OnMissionCreated -= OnObjectiveCreated;
        GlobalEventManager.OnMissionStateChange -= OnMissionShowStateChange;
        GlobalEventManager.OnMissionUpdate -= OnObjectiveUpdate;
        GlobalEventManager.OnMissionCompleted -= OnObjectiveCompleted;
        GlobalEventManager.OnMissionEnd -= OnObjectiveEnd;
    }

    /// <summary>
    /// 有任务更新时
    /// </summary>
    void OnObjectiveUpdate(MissionBase evt,bool refresh)
    {
        if (m_ObjectivesDictionnary.TryGetValue(evt, out MissionHUDItem toast))
        {

            SetActive(toast, !evt.hide);
            bool emptyTip = string.IsNullOrEmpty(evt.tip);
            if (GetActive(toast.tip) == emptyTip)
            {
                SetActive(toast.tip, !emptyTip);
            }
            if (!emptyTip) SetText(toast.tip, evt.tip);

            bool emptyCounter = evt.MaxProgress==0;
            if (!emptyCounter)
            {
                SetText(toast.counter, evt.NowProgress+"/"+evt.MaxProgress);
            }
            if (evt.percentage>0&& evt.percentage<1)
            {
                toast.bar.SetBar(evt.percentage);
                SetActive(toast.bar.transform.parent, true);
            }
            else
            {
                SetActive(toast.bar.transform.parent, false);
            }

            if (GetActive(toast.tip) != emptyTip || (refresh&&toast.GetComponent<RectTransform>()))
            {
                //为目标设置新的更新描述，并强制重新计算内容大小
                //Canvas.ForceUpdateCanvases();
                RefreshLayout(toast.transform);
             }
        }
    }

    /// <summary>
    /// 有任务创建时
    /// </summary>
    public void OnObjectiveCreated(MissionBase mission)
    {
        //Debug.LogError("创建任务"+ objective.title,objective);
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
                par = ExtraLayout;
                break;
        }
        
        MissionHUDItem toast = Instantiate(go, par);
        m_ObjectivesDictionnary.Add(mission, toast);
        // 初始化并提供描述
        toast.Initialize(mission);
        //全部隐藏，直到显示状态变化
        SetActive(toast, false);

    }

    public void OnMissionShowStateChange(MissionBase mission,bool state)
    {
        if (m_ObjectivesDictionnary.TryGetValue(mission, out MissionHUDItem toast))
        {
            toast.StateChange(state);
            GameRoot.CreateTimer(() => {
                //RefreshLayout(toast.transform.parent);
                RefreshLayout(transform);
            }, state ? 0.01f : 0.51f);

        }
    }

    /// <summary>
    /// 有任务完成时
    /// </summary>
    public void OnObjectiveCompleted(MissionBase mission)
    {
        wndManager.PlaySound(new(mission.missionType==MissionType.Main?m_MainCompletedSound:m_ExtraCompletedSound, AudioGroups.UI));
        AudioManager.Suppressed(3);
        if (m_ObjectivesDictionnary.TryGetValue(mission, out MissionHUDItem toast))
        {
            toast.Complete();
        }
        //Debug.LogError("任务"+objective.title+"完成");
    }

    /// <summary>
    /// 有任务移除时
    /// </summary>
    public void OnObjectiveEnd(MissionBase objective)
    {
        //Debug.LogError("任务" + objective.title + "结束");
        //支线任务才移除，主线是显示完成的
        if (objective.missionType != MissionType.Main && m_ObjectivesDictionnary.TryGetValue(objective, out MissionHUDItem toast))
        {
            //Debug.LogError("移除" + toast.gameObject);
            Tool.Destroy(toast.gameObject);
        }
        m_ObjectivesDictionnary.Remove(objective);
    }

}
