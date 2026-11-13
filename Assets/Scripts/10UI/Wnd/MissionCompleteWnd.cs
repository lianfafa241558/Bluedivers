using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FPSGame.Core;
using FPSGame.Mission;
using UnityEngine;
using FPSGame.Audio;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 任务完成提示窗口。
/// </summary>
[AddComponentMenu("UI/窗口/任务完成")]
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
        BattleEventBus.OnMissionCompleted += MissionCompleted;
        SetWndState(false);
    }

    protected override void FirstShowWnd()
    {
        

    }
    public override void OnDestroy()
    {
        // 先退订：base.OnDestroy 里 WndHub/HideWnd 在场景拆除时可能抛异常，一旦抛出后面的 -= 不会执行
        // ⇒ 静态事件会一直指着一个已销毁的窗口（同 MiniMapWnd 的坑，2026-11-13 实测）。
        BattleEventBus.OnMissionCompleted -= MissionCompleted;
        base.OnDestroy();

    }

    void MissionCompleted(MissionBase mission)
    {
        // 静态事件会把已拆掉的窗口叫起来，而 Destroy 是延迟的：同帧里 this 已是伪 null、OnDestroy 还没跑。
        // 必须自己判活 —— 下面 SetWndState 开头就 return、SetColor/SetText 内部也判空，只有 anim.Play 裸露
        // ⇒ 它成了唯一的 MissingReferenceException 报点（2026-11-13 实测）。
        if (!this) return;
        // 只在对局进行中弹：结算/加载/过场阶段 UI 根随时会被拆掉，这时再去 SetWndState
        // 就是在"正在被销毁的层级"上跑（打包版 NRE 的现场，栈里只剩 SetWndState 一个方法名）。
        if (mission == null) return;
        if (FPSGame.Data.FlowState.GameState != GameStateEnum.Game)
        {
            Debug.LogWarning($"[任务完成窗] 当前状态 {FPSGame.Data.FlowState.GameState}，不弹「{mission.title}」完成提示");
            return;
        }

        SetWndState(true);

        Color color = mission.missionType == MissionType.Main ? MainColor : ExtraColor;
        SetColor(left, color);
        SetColor(text, color);
        SetColor(right, color);
        SetText(desc, mission.title);
        // data 来自 TaskItem：场景摆放的任务若没配 _sceneMissionData 就是 null（策划可改的 SO 数据）
        int reward = mission.data != null ? mission.data.reward : 0;
        if (mission.data == null) Debug.LogError($"[任务完成窗] 任务「{mission.name}」没有任务数据(data)，奖励显示按 0 处理", mission);
        SetText(item1, reward);
        SetText(item2, reward / 5);
        anim.Play("Entry", 0, 0);
        if (!mission.HasTag(FPSGame.GameContract.MissionTag.NoAudio)) AudioSvc.PlaySound(new(mission.missionType == MissionType.Main || mission.missionType == MissionType.Sub ? m_MainCompletedSound : m_ExtraCompletedSound, AudioGroups.UI,1,1));
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
}
