using Core;
using FPSGame.Attribute;
using GameContract;
using Photon.Realtime;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static WndTools.WndRootTool;


public class BridgeWnd : Window
{
    [Foldout("玩家", true)]
    [SerializeField]
    private Transform selfRoot, friendRoot,
        selfLevel,selfName,selfIcon,selfExp, selfFrame;


    [Foldout("任务",true)]
    [SerializeField]
    private Transform taskRoot,taskName, taskType, taskIcon,
        taskMainTarget, taskExtraTarget, taskMainReward, taskExtraReward,
        taskMap,taskDiff, taskDiffReward,tastExtraDiffRoot, tastPropuctRoot;


    protected override void FirstShowWnd()
    {
        SetActive(taskRoot, false);
    }


    protected override void ShowWnd()
    {
        GlobalEventSub.OnGainExp += OnGainExp;
        GlobalEventSub.OnSwitchRole += OnSwitchRole;
        GlobalEventSub.OnSelectRolePreview += OnSelectRolePreview;
        GlobalEventSub.OnPlayerCreate += SwitchRolePreview;
    }

    protected override void HideWnd()
    {
        GlobalEventSub.OnGainExp -= OnGainExp;
        GlobalEventSub.OnSwitchRole -= OnSwitchRole;
        GlobalEventSub.OnSelectRolePreview -= OnSelectRolePreview;
        GlobalEventSub.OnPlayerCreate -= SwitchRolePreview;
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

        ArchiveSvc.Archive.GetRoleLevel(player.Id,out int level,out float expScale);
        SetText(selfLevel, level);
        SetFill(selfExp, expScale);
    }

    private void OnSelectRolePreview(RoleData_SO data)
    {
        var go = resManager.LoadPrefab("StudentModle/" + data.ID);
        SwitchRolePreview(go.GetComponent<I_Actor>());
    }

    private void SwitchRolePreview(I_Actor player)
    {
        SetText(selfName, player.ShowName);
        SetSprite(selfIcon, player.Portrait);
        SetColor(selfFrame, player.Color);
        ArchiveSvc.Archive.GetRoleLevel(player.Id, out int level, out float expScale);
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
        WndManager.Instance.CreatNotice("Yuuka", "Ready");
        var task = taskManager.nowTask;
        var info = task.taskCfg;
        var cfg = task.MainCfg;
        float diffScale = taskManager.FinalDiffScale();
        
        SetActive(friendRoot, task.PlayMode !=2);

        SetActive(taskRoot, true);
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

        
        SetText(taskMap, task.mapName);
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
