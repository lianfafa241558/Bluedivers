using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
using FPSGame.GameData;


/// <summary>
/// 舰桥玩家与任务信息窗。
/// </summary>
[AddComponentMenu("UI/窗口/舰桥信息")]
public class BridgeWnd : Window
{
    [Foldout("玩家", true)]
    [SerializeField]
    // ⚠ friendRoot 的**填充与显隐**已抽成共用件 FriendSlotGroup（挂在该节点上自驱动，2026-10-09）⇒ 本窗口不再使用它。
    private Transform selfRoot, friendRoot,
        selfLevel,selfName,selfIcon,selfExp, selfFrame;


    [Foldout("任务",true)]
    [SerializeField]
    private Transform taskRoot,taskName, taskType, taskIcon,
        taskMainTarget, taskExtraTarget, taskMainReward, taskExtraReward,
        taskMap,taskDiff, taskDiffReward,tastExtraDiffRoot, tastPropuctRoot;


    /// <summary>已经展开过的任务标识（"地图|任务名|难度"）：同一局被多条路径调 <see cref="DisplayTask"/> 时只展开一次。</summary>
    private string _shownTaskKey;

    protected override void FirstShowWnd()
    {
        //SetActive(taskRoot, false);
    }


    protected override void ShowWnd()
    {
        GlobalEventBus.OnGainExp += OnGainExp;
        GlobalEventBus.OnSwitchRole += OnSwitchRole;
        GlobalEventBus.OnSelectRolePreview += OnSelectRolePreview;
        UnitEventBus.OnPlayerCreate += SwitchRolePreview;

            // 任务面板：Ready 那一刻本窗口可能还是隐藏的（还开着选图/房间列表 ⇒ WindowState = UI），
        // 这时大厅场景里那张 GameState 状态表调的 DisplayTask 会因"animator 所在物体未激活"**静默失败**
        //（Console 留一条 `Game object with animator is inactive`，面板停在 Idle）⇒ 窗口一显示就补一次。
        // 不在 Ready 则清掉"已展开"记录，下一局重新展开。
        if (GameState == FPSGame.Core.GameStateEnum.Ready)
        {
            DisplayTask();
        }
        else
        {
            _shownTaskKey = null;
        }

        // ⚠ 队友位（谁在房间里）**本窗口不再自己管**：已抽成共用件 <see cref="FriendSlotGroup"/>，
        //   挂在 `PlayerStateSelf/FriendRoot` 上**自驱动**（每帧按场上盟友实体对账；单机时整块隐藏）。
        //   ⇒ BridgeWnd / SettingWnd 共用同一份，别再往这里加槽位代码（细节见 FriendSlotGroup 的类注释）。
    }

    protected override void HideWnd()
    {
        GlobalEventBus.OnGainExp -= OnGainExp;
        GlobalEventBus.OnSwitchRole -= OnSwitchRole;
        GlobalEventBus.OnSelectRolePreview -= OnSelectRolePreview;
        UnitEventBus.OnPlayerCreate -= SwitchRolePreview;
    }




    private void OnGainExp(string ID,int level,float expScale)
    {
        SetText(selfLevel, level);
        SetFill(selfExp, expScale);
    }
    private void OnSwitchRole(PlayerController player)
    {
        SetText(selfName, player.PlayerName);
        SetSprite(selfIcon, player.Portrait);

        ArchivesData_SO.Current.GetRoleLevel(player.Id,out int level,out float expScale);
        SetText(selfLevel, level);
        SetFill(selfExp, expScale);
    }

    private void OnSelectRolePreview(RoleData_SO data)
    {
        //Debug.LogError("实体路径" + "StudentModle/" + data.ID);
        var go = resManager.LoadPrefab("StudentModle/" + data.ID);
        SwitchRolePreview(go.GetComponent<IEntity>());
    }

    private void SwitchRolePreview(IEntity player)
    {
        SetText(selfName, player.ShowName);
        SetSprite(selfIcon, player.Portrait);
        SetColor(selfFrame, player.Color);
        ArchivesData_SO.Current.GetRoleLevel(player.Id, out int level, out float expScale);
        SetText(selfLevel, level);
        SetFill(selfExp, expScale);
        Color.RGBToHSV(player.Color, out var h, out var s, out var v);
        SetColor(selfExp, Color.HSVToRGB(h, s * 0.5f, v));
    }

    
    /// <summary>
    /// 事件控制
    /// </summary>
    public void DisplayTask()
    {
        var task = taskManager.nowTask;

        // ⚠ 还没有本局任务（触发源早于 SetTask）⇒ 直接跳过，等真正的 Ready：
        //   否则 `task.taskCfg.name` 读到空配置，而且就算有值，`Animator.Play` 打在未激活物体上也会静默失败。
        if (task == null || !task.activeTask) return;

        // 同一局只展开一次（"场景 GameState 状态表"与"窗口重新显示"都会调这里）。
        string key = taskManager.MapId + "|" + task.taskCfg.name + "|" + task.difficulty;
        if (key == _shownTaskKey) return;
        _shownTaskKey = key;

        WndManager.Instance.CreatNotice("Yuuka", "Ready");
        var info = task.taskCfg;
        var cfg = task.MainCfg;
        float diffScale = taskManager.FinalDiffScale();
        
        // ⚠ 队友位的显隐**不在这里**：谁在房间里随时会变（且单机要整块隐藏）
        //   ⇒ 统一交给每帧的 RefreshFriendRoot()（判据 BridgeSys.InOnlineRoom），避免两处各写一份又会互相覆盖。

        //SetActive(taskRoot, true);
        taskRoot.GetComponent<Animator>().Play("Entry",0,0);
        SetText(taskType, cfg.name);
        SetText(taskName, info.name);

        SetText(taskMainTarget, cfg.desc);
        SetText(taskExtraTarget,"可选目标* " +info.extra.Length);
        SetText(taskMainReward, (int)(info.MainReward* diffScale));
        SetText(taskExtraReward, (int)(info.ExtraReward* diffScale));
        SetColor(taskIcon, info.Color);
        SetColor(taskIcon.parent, info.Color);
        SetSprite(taskIcon, info.Sprite);

        
        SetText(taskMap, taskManager.MapId);
        SetText(taskDiff, task.difficulty.ToString());
        SetText(taskDiffReward,"EXP: +"+(int)(diffScale * 100)+"%");

        for(int i=0;i< tastExtraDiffRoot.childCount; ++i)
        {
            SetActive(tastExtraDiffRoot.GetChild(i), task.ExtraDifficulty[i]>0);
            SetText(tastExtraDiffRoot.GetChild(i,0),Tool.IntToRoman(task.ExtraDifficulty[i]));
        }
        for (int i = 0; i < tastExtraDiffRoot.childCount; ++i)
        {
            bool show = i < task.SpecialtyPropertys.Length;
            if (show)
            {
                SetSprite(tastPropuctRoot.GetChild(i), propertyManager.GetIcon(task.SpecialtyPropertys[i]));
            }
            SetActive(tastPropuctRoot.GetChild(i), show);

        }


        
    }

}
}
