using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;


public class BridgeWnd : WindowRoot
{
    [Foldout("玩家", true)]
    [SerializeField]
    private Transform selfRoot, friendRoot,
        selfLevel,selfName,selfIcon,selfExp;


    [Foldout("任务",true)]
    [SerializeField]
    private Transform taskRoot,taskName, taskType, taskIcon,
        taskMainTarget, taskExtraTarget, taskMainReward, taskExtraReward,
        taskMap,taskDiff, taskDiffReward,tastExtraDiffRoot, tastPropuctRoot;



    private void Awake()
    {

    }
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
        GameRoot.OnGameStateChange += OnGameStateChange;
        GlobalEventManager.OnGainExp += OnGainExp;
        GlobalEventManager.OnSwitchRole += OnSwitchRole;

        SetActive(taskRoot, false);
    }

    protected override void HideWnd()
    {
        GameRoot.OnGameStateChange -= OnGameStateChange;
        GlobalEventManager.OnGainExp -= OnGainExp;
        GlobalEventManager.OnSwitchRole -= OnSwitchRole;
    }

    void Update()
    {
        
    }

    private void OnGameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        switch (entry)
        {
            case GameStateEnum.Bridge:

                break;
            case GameStateEnum.Ready:
                DisplayTask();
                break;
            case GameStateEnum.Transition:

                break;
        }
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

        GameRoot.Archive.GetRoleLevel(player.Id,out int level,out float expScale);
        SetText(selfLevel, level);
        SetFill(selfExp, expScale);
    }

    

    private void DisplayTask()
    {
        var task = taskManager.nowTask;
        var info = task.taskCfg;
        var cfg = task.MainCfg;
        float diffScale = taskManager.FinalDiffScale();
        
        SetActive(friendRoot, task.PlayMode !=2);

        taskRoot.GetComponent<Animator>().Play("Entry",0,0);
        SetActive(taskRoot,true);
        SetText(taskType, cfg.name);
        SetText(taskName, info.name);

        SetText(taskMainTarget, cfg.desc);
        SetText(taskExtraTarget,"可选目标 * " +info.extra.Length);
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
