using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Core;
using FpsGame.Mission;
using UnityEngine;
using static WndTools.WndRootTool;

public class MissionCompleteWnd : Window
{
    [SerializeField]
    private AudioClip m_MainCompletedSound;//主要任务完成时播放的声音
    [SerializeField]
    private AudioClip m_ExtraCompletedSound;//额外任务完成时播放的声音

    [SerializeField]
    Animator anim;

    [SerializeField]
    Color MainColor, ExtraColor;

    [SerializeField]
    Transform left, text, right;

    [SerializeField]
    Transform desc, item1, item2;

    public void Init()
    {
        BattleEventSub.OnMissionCompleted += MissionCompleted;
        SetWndState(false);
    }

    protected override void FirstShowWnd()
    {
        

    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        BattleEventSub.OnMissionCompleted -= MissionCompleted;

    }

    void MissionCompleted(MissionBase mission)
    {
        SetWndState(true);

        Color color = mission.missionType == MissionType.Main ? MainColor : ExtraColor;
        SetColor(left, color);
        SetColor(text, color);
        SetColor(right, color);
        SetText(desc, mission.title);
        SetText(item1, mission.data.reward);
        SetText(item2, mission.data.reward / 5);
        anim.Play("Entry", 0, 0);
        if (!mission.HasTag(GameContract.MissionTag.NoAudio)) AudioSvc.PlaySound(new(mission.missionType == MissionType.Main || mission.missionType == MissionType.Sub ? m_MainCompletedSound : m_ExtraCompletedSound, AudioGroups.UI,1,1));
        AudioSvc.Suppressed(3);
    }


    IEnumerator CloseWndAfterDelay()
    {
        yield return new WaitForSeconds(4f);
        CloseWnd();
    }

    protected override void ShowWnd()
    {
        StartCoroutine(CloseWndAfterDelay());
    }

    protected override void HideWnd()
    {

    }


}
