using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;
public class SelectMapWnd : Window
{
    private const float _SwitchTime=0.5f;
    private MapWndState mapState;
    private float magnifc;
    private Vector2 primeRootSize, primeRootPos;
    private float nowtime;

    [Foldout("基础",true)]
    [SerializeField]
    private Transform mapRoot,cancel,random,server;

    [Foldout("左侧边栏", true)]
    [SerializeField]
    private Animator infoRoot;
    [SerializeField]
    private Transform descBg,descName, descProducts, descDesc,descOccupierRoot;

    [Foldout("地图界面", true)]
    [SerializeField]
    private RectTransform areaRoot,areaSelectLayout;
    [SerializeField]
    private Transform mapInfoLayout,taskJoin,taskPublic,taskSolo;

    [Foldout("右侧边栏", true)]
    [SerializeField]
    private RectTransform areaInfoRoot,areaInfoFactorRoot, areaInfoExtraDiffRoot;
    [SerializeField]
    private Transform areaInfoName, areaInfoType, areaInfoIcon, areaInfoEnemy,
        areaInfoMainTarget, areaInfoExtraTarget, areaInfoMainReward, areaInfoExtraReward,
        areaInfoDiffLeft,areaInfoDiffRight, areaInfoDiff, areaInfoDiffReward,
        areaInfoReady;


    private int SelectMapIndex = 0, SelectMapTaskCount;
    private int SelectTaskDiff, SelectTaskIndex=-1;
    private int[] SelectTaskExtraDiff=new int[4];
    private OOPartEnum[] product;
    private int SelectPlayMode = 2;
    private ArchivesData_SO arch => ArchiveSvc.Archive;
    public void Init()
    {
        WndManager.Instance.selectMapWnd = this;
    }
    public void Uninit()
    {
        WndManager.Instance.selectMapWnd = null;
    }
    protected override void FirstShowWnd()
    {

        SelectTaskDiff = 3;

        for (int i = 0; i < mapRoot.childCount; ++i)
        {
            int a = i;

            var info = taskManager.MapData[mapRoot.GetChild(a).name].mapItemInfos;
           
            if (info.Length>0) {
                SetActive(mapRoot.GetChild(a, 1, 3), true);
                SetActive(mapRoot.GetChild(a, 1, 4), false);
                for (int u = 0; u < taskManager.TaskCount; ++u) taskManager.TaskCfgs[a,u].enable &= u<info.Length;
            }
            else
            {
                SetActive(mapRoot.GetChild(a,1,3),false);
                SetActive(mapRoot.GetChild(a, 1, 4), true);
                SetButtonInteractable(mapRoot.GetChild(a, 1),false);
            }

            SetCilck(mapRoot.GetChild(a, 1), () => {
                wndManager.PlaySound(new("UI/UI_Bubble"));
                SelectMap(mapRoot.GetChild(a).RectTransform(), a);
            });
            SetButtonEnter(mapRoot.GetChild(a, 1), (data) => {
                ShowAreaWnd(mapRoot.GetChild(a));
            });

            SetButtonExit(mapRoot.GetChild(a, 1), (data) => {
                HideAreaWnd();
            });
        }
        
        for (int i = 0; i < mapInfoLayout.childCount; ++i)
        {
            int a = i;
           
            SetCilck(mapInfoLayout.GetChild(a), () => {
                wndManager.PlaySound(new("UI/UI_Bubble"));
                SelectTask(a);
            });
            
            SetButtonEnter(mapInfoLayout.GetChild(a), (data) => {
                if(taskManager.TaskCfgs[SelectMapIndex,a].enable && a != SelectTaskIndex) ShowAreaInfoWnd(a, SelectTaskIndex > -1);
            });

            SetButtonExit(mapInfoLayout.GetChild(a), (data) => {
                if (SelectTaskIndex > -1)
                {
                    if (a != SelectTaskIndex)ShowAreaInfoWnd(SelectTaskIndex,true);
                }
                else if (taskManager.TaskCfgs[SelectMapIndex, a].enable && a != SelectTaskIndex) HideAreaInfoWnd();
            });
        }

        SetCilck(cancel, () =>
        {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            SetWndState(false);
        });


        SetCilck(areaInfoDiffLeft, () =>
        {
            SetDiff(false);
        });

        SetCilck(areaInfoDiffRight, () =>
        {
            SetDiff(true);
        });

        SetCilck(taskJoin, () =>
        {
            wndManager.PlaySound(new("UI/UI_Bubble"));
            wndManager.CreatTip(new() { 
                title = "未完成的功能",
                desc = "该功能尚未完成，请等待后续更新。",
            });
            //SelectPlayMode = 0;
        });
        SetCilck(taskPublic, () =>
        {
            wndManager.PlaySound(new("UI/UI_Bubble"));
            wndManager.CreatTip(new()
            {
                title = "未完成的功能",
                desc = "该功能尚未完成，请等待后续更新。",
            });
            //SelectPlayMode = 1;
        });
        SetCilck(taskSolo, () =>
        {
            SelectPlayMode = 2;
            wndManager.PlaySound(new("UI/UI_Bubble"));
            ExpandCfg();
        });

        for(int i = 0; i < 4; ++i)
        {
            int a = i;
            SetCilck(areaInfoExtraDiffRoot.GetChild(a, 1), () =>
            {
                SetExtraDiff(a,false);
            });

            SetCilck(areaInfoExtraDiffRoot.GetChild(a, 2), () =>
            {
                SetExtraDiff(a, true);
            });

        }



        SetCilck(areaInfoReady, () =>
        {
            StartTask();
        });
        
    }

    protected override void ShowWnd()
    {
        WindowState = WindowStateEnum.UI;
        SetActive(mapRoot, true);
        SetActive(areaRoot, false);
        SetActive(areaSelectLayout, false);
        mapState = MapWndState.Map;
        infoRoot.Play("Exit", 0, 1);
        areaInfoRoot.GetComponent<Animator>().Play("Exit", 0, 1);
        InputManager.AddListenerCancel(Cancel);
        SetText(areaInfoDiff, ((DifficultyEnum)SelectTaskDiff).ToString());
        RefreshDisplay();

        //GlobalEventManager.OnFakeBg(mapRoot.parent);
    }

    protected override void HideWnd()
    {
        WindowState = WindowStateEnum.Game;
        InputManager.RemoveListenerCancel(Cancel);
        //GlobalEventManager.OnFakeBg(null);
    }

    private void Update()
    {
        if (mapState== MapWndState.Switch && nowtime>0)
        {
            //不用lerp是因为效果不好
            areaRoot.anchoredPosition = (nowtime/ _SwitchTime) * primeRootPos;
            areaRoot.sizeDelta = primeRootSize+primeRootSize * (magnifc-1)*(_SwitchTime - nowtime)/ _SwitchTime;
            //mapInfoRoot.GetChild(0).RectTransform().sizeDelta = primeIconSize + primeIconSize * (magnifc - 1) * (_SwitchTime - nowtime) / _SwitchTime;

            if ((nowtime-=Time.deltaTime) <= 0) FinalSwitch();
        }
    }

    private bool Cancel()
    {
        if (this==null||!State) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        switch (mapState)
        {
            case MapWndState.Map:
                SetWndState(false);
                break;
            case MapWndState.Switch:
                InputManager.AddListenerCancel(Cancel);
                break;
            case MapWndState.Info:
                SetActive(mapRoot, true);
                SetActive(areaRoot, false);
                mapState = MapWndState.Map;
                HideAreaInfoWnd(true);
                InputManager.AddListenerCancel(Cancel);
                break;
            case MapWndState.SelectTask:
                CancelTask();
                InputManager.AddListenerCancel(Cancel);
                break;
        }

        return true;
    }

    /// <summary>
    /// 显示每个地图有什么任务
    /// </summary>
    private void RefreshDisplay()
    {
        for (int i = 0; i < mapRoot.childCount; ++i)
        {
            if (GetActive(mapRoot.GetChild(i, 1, 3)))
            {
                int nowIndex = 0;
                for (int u = 0; u < taskManager.TaskCount; ++u)
                {
                    var info = taskManager.TaskCfgs[i, u];
                    if (info.enable)
                    {
                        var item = mapRoot.GetChild(i, 1, 3, nowIndex);
                        SetActive(item, true);
                        SetColor(item, info.Color);
                        SetColor(item.GetChild(0), info.Color);
                        SetSprite(item.GetChild(0), info.Sprite);
                        ++nowIndex;
                        if (nowIndex == 5) break;
                    }
                }
                if (nowIndex == 0)
                {
                    SetActive(mapRoot.GetChild(i, 1, 3), false);
                    SetActive(mapRoot.GetChild(i, 1, 4), true);
                }
            }
        }
    }

    /// <summary>
    /// 点击地图
    /// </summary>
    /// <param name="trans"></param>
    /// <param name="index"></param>
    private void SelectMap(RectTransform trans,int index)
    {
        var infoIcon = areaRoot.GetChild(0).RectTransform();
        var tansIcon = trans.GetChild(0).RectTransform();
        magnifc = Mathf.Floor(Constants.CanvasHeight * 0.8f / trans.sizeDelta.y / trans.lossyScale.y*4)/4f;
        //Debug.LogWarning("放大倍率"+ magnifc+"屏幕高度"+(Screen.height * 0.8f)+"地图高度"+ trans.sizeDelta.y * trans.lossyScale.y);
        SetActive(mapRoot,false);

        SetActive(areaRoot,true);
        SetActive(areaRoot.GetChild(0), false);
        SetActive(areaSelectLayout, false);

        areaRoot.anchoredPosition = trans.anchoredPosition;
        areaRoot.sizeDelta = trans.sizeDelta* trans.lossyScale;
        CopySprite(trans,areaRoot);
        CopySprite(trans.GetChild(0), infoIcon);
        infoIcon.sizeDelta = (tansIcon.sizeDelta * trans.lossyScale * magnifc).ToInt();
        infoIcon.anchoredPosition = (tansIcon.anchoredPosition * trans.lossyScale.y * magnifc).ToInt();


        mapState = MapWndState.Switch;
        nowtime = _SwitchTime;
        primeRootSize = trans.sizeDelta * trans.lossyScale;
        primeRootPos = trans.anchoredPosition;
        var data= taskManager.MapData[trans.name];
        var info = data.mapItemInfos;
        SelectMapIndex = index;
        SelectMapTaskCount = info.Length;
        product = data.product;

        for (int i = 0; i < areaRoot.GetChild(1).childCount; ++i)
        {
            SetActive(areaRoot.GetChild(1, i), false);
            if (i < info.Length)
            {
                var item = taskManager.TaskCfgs[SelectMapIndex, i];

                areaRoot.GetChild(1, i).RectTransform().anchoredPosition = info[i].pos;
                SetText(areaRoot.GetChild(1, i, 2),info[i].name);
                SetButtonInteractable(areaRoot.GetChild(1, i), item.enable);
                SetActive(areaRoot.GetChild(1, i, 3), item.enable);
                SetSprite(areaRoot.GetChild(1, i, 3,0), item.Sprite);
                SetColor(areaRoot.GetChild(1, i, 3), item.Color);
                SetColor(areaRoot.GetChild(1, i, 3, 0), item.Color);

                SetActive(areaRoot.GetChild(1, i, 4), !item.enable);
            }

        }
    }

    private void FinalSwitch()
    {
        mapState = MapWndState.Info;
        areaRoot.anchoredPosition = Vector2.zero;
        areaRoot.sizeDelta = (primeRootSize*magnifc).ToInt();
        //mapInfoRoot.GetChild(0).RectTransform().sizeDelta = primeIconSize * magnifc;
        //mapInfoRoot.GetChild(0).RectTransform().anchoredPosition = primeIconPos * magnifc;
        SetActive(areaRoot.GetChild(0), true);
        SetActive(areaInfoExtraDiffRoot.parent, false);
        HideAreaWnd();
        for (int i = 0; i < areaRoot.GetChild(1).childCount; ++i)
        {
            SetActive(areaRoot.GetChild(1, i), i< SelectMapTaskCount);
        }
    }
    /// <summary>
    /// 移入地图
    /// </summary>
    private void ShowAreaWnd(Transform trans)
    {
        var data = taskManager.MapData[trans.name];
        SetText(descName, GetText(trans.GetChild(1, 2)));
        SetText(descDesc, data.AreaDesc);
        SetSprite(descBg, data.AreaBackground);

        var occ=arch.occupierDic[trans.name];
        for(int i = 0; i < 4; ++i)
        {
            bool show = i < data.product.Length;
            SetActive(descProducts.GetChild(i), show);
            if (show)
            {
                SetSprite(descProducts.GetChild(i), propertyManager.GetIcon(data.product[i]));
            }   
        }

        for (int i = 0; i < 4; ++i)
        {
            if (i < occ.Count)
            {
                SetActive(descOccupierRoot.GetChild(i), true);
                SetSprite(descOccupierRoot.GetChild(i, 0), taskManager.GetOccupierIcon(occ[i].name));
                SetText(descOccupierRoot.GetChild(i, 1), occ[i].name);
                SetFill(descOccupierRoot.GetChild(i, 2, 0), occ[i].value / 100f);
                SetText(descOccupierRoot.GetChild(i, 2, 1), occ[i].value.ToString("F2") + "%");
            }
            else
            {
                SetActive(descOccupierRoot.GetChild(i), false);
            }
        }
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(descName.parent.RectTransform());
        infoRoot.Play("Entry",0,0);
    }
    /// <summary>
    /// 移出地图
    /// </summary>
    private void HideAreaWnd()
    {
        infoRoot.Play("Exit", 0, 0);
    }

    /// <summary>
    /// 鼠标移入任务
    /// </summary>
    private void ShowAreaInfoWnd(int index, bool immediately = false)
    {
        //Debug.LogWarning("鼠标进入任务");
        var info = taskManager.TaskCfgs[SelectMapIndex, index];
        SetText(areaInfoType, info.TaskType);
        SetText(areaInfoName, info.name);
        SetText(areaInfoMainTarget, info.TaskDesc);
        SetText(areaInfoExtraTarget,"可选任务* " + info.extra.Length);

        AccountReward(index,false);

        //SetText(areaInfoName, GetText(trans.GetChild(2)));
        SetColor(areaInfoIcon, info.Color);
        SetColor(areaInfoIcon.parent, info.Color);
        SetSprite(areaInfoIcon, info.Sprite);
        var mapData = taskManager.MapData[mapRoot.GetChild(SelectMapIndex).name];
        var campData = taskManager.Camps[mapData.enemyVarietyType];
        SetSprite(areaInfoEnemy, campData.Sprite);
        SetColor(areaInfoEnemy, campData.Color);

        float normScale =Mathf.Clamp01(areaInfoRoot.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime);

        areaInfoRoot.GetComponent<Animator>().Play("Entry", 0, immediately ? 1 : 1- normScale);
    }
    /// <summary>
    /// 鼠标移出任务
    /// </summary>
    private void HideAreaInfoWnd(bool immediately=false)
    {
        //Debug.LogWarning("鼠标移除任务");
        float normScale = Mathf.Clamp01(areaInfoRoot.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime);

        areaInfoRoot.GetComponent<Animator>().Play("Exit", 0, immediately?1 : 1 - normScale);
    }

    /// <summary>
    /// 点击任务
    /// </summary>
    private void SelectTask(int index)
    {
        if (SelectTaskIndex == index) return;
        //Debug.LogWarning("鼠标点击任务");
        mapState = MapWndState.SelectTask;
        Transform trans = mapInfoLayout.GetChild(index);
        int oldselect = SelectTaskIndex;
        SelectTaskIndex = index;
        SetActive(areaSelectLayout, true);
        areaSelectLayout.position = trans.position + Vector3.down*60;
        areaSelectLayout.GetComponent<Animator>().Play("Entry", 0, 0);

        if (areaInfoRoot.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Exit"))
        {
            areaInfoRoot.GetComponent<Animator>().Play("Entry", 0,0);
        }

    }

    private void CancelTask()
    {
        mapState = MapWndState.Info;
        areaSelectLayout.GetComponent<Animator>().Play("Exit", 0, 0);
        SelectTaskIndex = -1;
        SetActive(areaInfoExtraDiffRoot.parent, false);
        HideAreaInfoWnd();
    }
    /// <summary>
    /// 修改难度
    /// </summary>
    /// <param name="add"></param>
    private void SetDiff(bool add)
    {
        int value = add ? 1 : -1;
        SelectTaskDiff = Mathf.Clamp(SelectTaskDiff + value, 0, Tool.EnumLenght<DifficultyEnum>() - 1);
        SetText(areaInfoDiff, ((DifficultyEnum)SelectTaskDiff).ToString());
        AccountReward(SelectTaskIndex);
    }

    /// <summary>
    /// 修改额外难度
    /// </summary>
    /// <param name="add"></param>
    private void SetExtraDiff(int index,bool add)
    {
        int value = add ? 1 : -1;
        SelectTaskExtraDiff[index] = Mathf.Clamp(SelectTaskExtraDiff[index] + value, 0, 3);
        SetFill(areaInfoExtraDiffRoot.GetChild(index, 0, 0), 0.33f * SelectTaskExtraDiff[index]);
        AccountReward(SelectTaskIndex);
    }

    private void AccountReward(int index,bool useDiff=true)
    {
        if (useDiff)
        {
            float diffScale = taskManager.DiffScale((DifficultyEnum)SelectTaskDiff);
            float extraScale = taskManager.ExtraDiffScale((DifficultyEnum)SelectTaskDiff);
            var info = taskManager.TaskCfgs[SelectMapIndex, index];

            for (int i = 0; i < 4; ++i)
            {
                diffScale += extraScale * SelectTaskExtraDiff[i];
            }
            SetText(areaInfoMainReward, (int)(info.MainReward * (1 + diffScale)));
            SetText(areaInfoExtraReward, (int)(info.ExtraReward * (1 + diffScale)));
            SetText(areaInfoDiffReward, "风险奖励:" + Mathf.RoundToInt(diffScale * 100) + "%");
        }
        else
        {
            var info = taskManager.TaskCfgs[SelectMapIndex, index];
            SetText(areaInfoMainReward, info.MainReward);
            SetText(areaInfoExtraReward,info.ExtraReward);
        }


    }


    private void ExpandCfg()
    {
        SetActive(areaInfoExtraDiffRoot.parent, true);
        AccountReward(SelectTaskIndex);
        areaInfoRoot.GetComponent<Animator>().Play("Expand", 1, 0);
    }


    /// <summary>
    /// 开始任务
    /// </summary>
    private void StartTask()
    {
        //现在暂时只能单人
        taskManager.SetTask(mapRoot.GetChild(SelectMapIndex).name, SelectTaskIndex,(DifficultyEnum)SelectTaskDiff, SelectTaskExtraDiff,SelectPlayMode);
        SetWndState(false);
    }


    private enum MapWndState
    {
        Map,
        Switch,
        Info,
        SelectTask
    }

}
