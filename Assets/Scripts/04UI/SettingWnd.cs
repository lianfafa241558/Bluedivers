using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static ArchivesData_SO;
using static WndTools.WndRootTool;

public class SettingWnd : Window
{
    public Image BG;
    public Transform expandRoot, layoutRoot;
    public RectTransform nowSelect;

    [Foldout("状态", true)]
    public Transform stateActiveRoot,stateHideRoot,mapName,mapImage,mapIcon,
        taskName,taskDiff,taskType,taskTypeIcon,
        taskMainDesc, taskExtraDesc, taskMainReward, taskExtraReward, tastExtraDiffRoot,
        selfIcon,selfName,selfLevel,selfExp, selfFrame;

    [Foldout("右上按钮", true)]
    public Transform freeCamera, rebirth,returnShop, exitGame;

    [Foldout("设置",true)]
    public GameObject tempTitle, tempDrop, tempToggle, tempSilder;
    public Transform settingRoot,updateTitle, updateTime, updateDesc, updateCount, updateLeft, updateRight;
    
    private List<UpdateData_SO> m_UpdateDataArr;
    private int nowUpdateIndex=1;
    private int nowExpandIndex=1;
    //[SerializeField]
    //private MyVolumeFeature feature;
    WindowStateEnum oldStste;
    bool haveSettingChagne;

    [SerializeField]
    private Camera uiCamera;

    [SerializeField]
    private GameObject showModle;

    private GameObject lookPoint;

    private float justClosed;
    private bool selfChangeState;

    public void Init()
    {
        //先尝试移除绑定,如果没有那就什么都不发生
        UnInit();
        //这玩意一般是那种需要关了也能触发的需求才用的 update改用就行
        InputManager.BindDown(WindowStateEnum.All, InputState.Esc, OnEsc);
        //Debug.LogError("绑定");
        //此时还没初始化
        WndManager.Instance.settingWnd = this;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnInit();
    }
    public void UnInit()
    {
        InputManager.UnBindDown(WindowStateEnum.All, InputState.Esc, OnEsc);
    }


    protected override void FirstShowWnd()
    {
        m_UpdateDataArr = resManager.LoadObjects<UpdateData_SO>("GameData/Update");
        SetUpdate(false);
        ArchiveSvc.Archive.settingDic.ForEach(CreatItem);


        SetCilck(layoutRoot.GetChild(0), () => SwitchNextExpand(false));
        SetCilck(layoutRoot.GetChild(layoutRoot.childCount-1), () => SwitchNextExpand(true));

        SetCilck(freeCamera, EnterFreeCamera);
        SetCilck(returnShop, TryReturnShop);
        SetCilck(rebirth, TryRebirth);
        SetCilck(exitGame, TryExitGame);
        
        WndManager.OnWindowStateChange += OnWindowStateChange;

        lookPoint = new GameObject("LookPoint");
        lookPoint.transform.parent = transform;
        lookPoint.transform.position = uiCamera.ScreenToWorldPoint(Input.mousePosition);
    }

    protected override void ShowWnd()
    {
        selfChangeState = true;
        oldStste = WindowState!= WindowStateEnum.FreeCamera? WindowState : WindowStateEnum.Game;
        WindowState = WindowStateEnum.UI;
        selfChangeState = false;

        //wndManager.WndUI.gameObject.SetActive(false);
        //feature.SetActive(true);
        if(roomManager.IsSingle&&GameState == GameStateEnum.Game)TimeScale = 0.01f;//TODO:如果是单机的话
                                                           // 创建临时Texture2D
        BG.sprite = CameraCaptureToSprite(Camera.main);
        BG.material.SetFloat("_TimeScale", TimeScale);
        //GlobalEventManager.OnFakeBg(BG.transform);
        haveSettingChagne = false;
        SetActive(returnShop,GameState == GameStateEnum.Game);
        SetActive(rebirth, GameState == GameStateEnum.Bridge);
        SetActive(freeCamera, roomManager.IsSingle);

        SetStateRoot();
        InputManager.AddListenerCancel(Cancel);
    }
    protected override void HideWnd()
    {
        WindowState = oldStste;
        //wndManager.WndUI.gameObject.SetActive(true);
        //feature.SetActive(false);
        if (roomManager.IsSingle && GameState == GameStateEnum.Game) TimeScale = 1;//TODO:如果是单机的话
        BG.material.SetFloat("_TimeScale", TimeScale);
        //GlobalEventManager.OnFakeBg(null);
        if (haveSettingChagne)
        {
            ArchiveSvc.Archive.Save();
        }
        Tool.Destroy(showModle);

    }

    void Update()
    {
        if (InputManager.GetDown(InputState.Left))
        {
            SwitchNextExpand(false);
        }
        else if (InputManager.GetDown(InputState.Right))
        {
            SwitchNextExpand(true);
        }
        if (lookPoint)
        {
            lookPoint.transform.position = uiCamera.ScreenToWorldPoint(Input.mousePosition);
        }
    }




    private void OnWindowStateChange(WindowStateEnum oldState, WindowStateEnum state)
    {
        if (selfChangeState) return;//是自己干的就不管
        //selfChangeState = true;//表面在打开设置界面期间有人改变了界面状??
        oldStste = state;
    }

    private void OnEsc()
    {
        //Debug.LogError("当前状态"+ GameRoot.GameState +"目标 "+ (GameStateEnum.Front | GameStateEnum.Transition | GameStateEnum.Load | GameStateEnum.GameEnd));
        if(justClosed<Time.time&&(GameState & (GameStateEnum.Front| GameStateEnum.Transition| GameStateEnum.Load| GameStateEnum.GameEnd)) == 0)
        {
            if (InputManager.CancelEmpty() && !State) SetWndState(true);
        }
    }

    public Sprite CameraCaptureToSprite(Camera targetCamera)
    {
        // 创建RenderTexture
        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        //int rewordMask = targetCamera.cullingMask;
        //targetCamera.cullingMask|= LayerDefinition.WeaponLayers;
        targetCamera.targetTexture = rt;
        targetCamera.Render();

        // 转换为Texture2D
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        // 生成Sprite
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        targetCamera.targetTexture = null;
        //targetCamera.cullingMask = rewordMask;
        sprite.name = "抓取";
        return sprite;
    }

    private void SwitchNextExpand(bool isAdd)
    {
        SwitchExpand(expandRoot.GetChild(Tool.PositiveRemainder(nowExpandIndex + (isAdd ? 1 : -1), expandRoot.childCount)).gameObject);
    }
    public void SwitchExpand(GameObject go)
    {
        if (go.activeSelf) return;
        nowExpandIndex = go.transform.GetSiblingIndex();
        var select = (RectTransform)layoutRoot.GetChild(nowExpandIndex + 1);
        nowSelect.position = select.position;
        nowSelect.sizeDelta = select.sizeDelta;
        expandRoot.ForEach(item =>SetActive(item,false));
        SetActive(go, true);
        wndManager.PlaySound(new("UI/UI_Notice"));
    }


    public void SetUpdate(bool add)
    {
        nowUpdateIndex = Tool.PositiveRemainder(nowUpdateIndex + (add ? 1 : -1),m_UpdateDataArr.Count);
        SetText(updateTitle, m_UpdateDataArr[nowUpdateIndex].title);
        SetText(updateDesc, m_UpdateDataArr[nowUpdateIndex].desc);
        SetText(updateTime, m_UpdateDataArr[nowUpdateIndex].time);
        SetText(updateCount, "" + (nowUpdateIndex+1) + "/" + m_UpdateDataArr.Count + "");
        
    }


    private void CreatItem(string name,ArchSettingData data)
    {
        if(!string.IsNullOrEmpty(data.titile))SetText(Instantiate(tempTitle, settingRoot).transform,data.titile);
        Transform tran;
        switch (data.type)
        {
            case SettingBtnType.Dropdown:
                tran = Instantiate(tempDrop, settingRoot).transform;
                var left = tran.GetChild(1,1).GetComponent<Button>();
                var right = tran.GetChild(1,0).GetComponent<Button>();
                var textD = tran.GetChild(1);
                SetText(textD, data.showTexts[data.value.RawInt]);
                left.onClick.AddListener(() => {
                    
                    Debug.Log("点击" + (data.value.RawInt - 1)+"  "+ data.showTexts.Length);
                    data.value = Tool.PositiveRemainder(data.value.RawInt - 1, data.showTexts.Length);
                    SetText(textD, data.showTexts[data.value.RawInt]);
                    haveSettingChagne = true;
                    GlobalEventSub.SettingCange(name, data.value.RawInt);
                    wndManager.PlaySound(new("UI/UI_Bubble"));
                });
                right.onClick.AddListener(() => {
                    
                    Debug.Log("点击 " + (data.value.RawInt + 1) + "  " + data.showTexts.Length);
                    data.value = Tool.PositiveRemainder(data.value.RawInt + 1, data.showTexts.Length);
                    SetText(textD, data.showTexts[data.value.RawInt]);
                    haveSettingChagne = true;
                    GlobalEventSub.SettingCange(name, data.value.RawInt);
                    wndManager.PlaySound(new("UI/UI_Bubble"));
                });
                break;
            case SettingBtnType.Toggle:
                tran = Instantiate(tempToggle, settingRoot).transform;
                var toggle = tran.GetChild(1).GetComponent<Toggle>();
                toggle.isOn = data.value.RawInt > 0;
                toggle.onValueChanged.AddListener((bool value) => {

                    data.value = value?1:0;
                    haveSettingChagne = true;
                    GlobalEventSub.SettingCange(name, data.value.RawInt);
                    wndManager.PlaySound(new("UI/UI_Bubble"));
                });
                break;
            case SettingBtnType.Slider:
                tran = Instantiate(tempSilder, settingRoot).transform;
                var slider = tran.GetChild(1,0).GetComponent<Slider>();
                slider.minValue = data.sliderRange.x;
                slider.maxValue  = data.sliderRange.y;
                slider.wholeNumbers = true;
                slider.value = data.value.RawInt;
                var textS = tran.GetChild(1,1);
                SetText(textS,data.value.RawInt + data.sliderSuffix) ;
                slider.onValueChanged.AddListener((float value) => {
                    data.value = value;
                    haveSettingChagne = true;
                    SetText(textS, (int)value + data.sliderSuffix);
                    GlobalEventSub.SettingCange(name, data.value.RawInt);
                    //AudioManager.PlaySound(new("UI/UI_Bubble"));
                });
                break;
            default:
                return;
        }
        SetText(tran.GetChild(0), name);
    }

    private void HideTask()
    {
        SetActive(stateActiveRoot,false);
        SetActive(stateHideRoot, true);
    }

    private void DisplayTask()
    {
        var cfg = taskManager.nowTask;
        var info = cfg.taskCfg;
        float diffScale = taskManager.FinalDiffScale();

        SetActive(stateActiveRoot, true);
        SetActive(stateHideRoot, false);
        SetText(mapName, cfg.mapName);
        SetSprite(mapIcon, cfg.mapCfg.Icon);
        SetSprite(mapImage, cfg.mapCfg.Map);

        SetText(taskType, cfg.MainCfg.name);
        SetText(taskName, info.name);

        SetText(taskMainDesc, cfg.MainCfg.desc);
        SetText(taskExtraDesc, "额外目标");
        SetText(taskMainReward, (int)(info.MainReward * diffScale));
        SetText(taskExtraReward, (int)(info.ExtraReward * diffScale));
        SetColor(taskTypeIcon, info.Color);
        SetSprite(taskTypeIcon, info.Sprite);

        SetText(taskDiff, cfg.difficulty.ToString());

        for (int i = 0; i < tastExtraDiffRoot.childCount; ++i)
        {
            SetActive(tastExtraDiffRoot.GetChild(i), cfg.ExtraDifficulty[i] > 0);
            SetText(tastExtraDiffRoot.GetChild(i, 0), Tool.IntToRoman(cfg.ExtraDifficulty[i]));
        }

    }
    public void SetStateRoot()
    {
        var player = ActorsManager.Player;
        if (taskManager.nowTask.activeTask)
        {
            DisplayTask();
        }
        else
        {
            HideTask();
        }
        showModle = resManager.CreatPrefab("Prefabs/StudentModle/" + player.Id, false);
        //var lookAtController = showModle.GetComponentInChildren<LookAtIK>();
        //lookAtController.enabled = false;
        showModle.transform.position = transform.TransformPoint(new(600, -650, 700));
        showModle.transform.eulerAngles = new(0, -170, 0);
        //showModle.transform.GetChild(0).localScale = new(550, 550, 550);
        showModle.transform.localScale = new(550, 550, 550);
        showModle.SetChildLayer(gameObject.layer,3);
        var comp = showModle.GetComponent<RootMotion.FinalIK.LookAtController>();
        comp.ik.solver.bodyWeight = 0;
        comp.target = lookPoint.transform;


        SetText(selfName, player.ShowName);
        SetSprite(selfIcon, player.Portrait);
        ArchiveSvc.Archive.GetRoleLevel(player.Id, out int level, out float expScale);
        SetColor(selfFrame, player.Color);
        SetText(selfLevel, level);
        SetFill(selfExp, expScale);
        Color.RGBToHSV(player.Color, out var h, out var s, out var v);
        SetColor(selfExp, Color.HSVToRGB(h, s * 0.5f, v));
    }
    /// <summary>返回舰船</summary>
    void TryReturnShop()
    {
        //不需要判定状态，已经隐藏??
        
        wndManager.CreatTip(new() {
            title = "中止任务",
            desc = "\n确定要中止任务吗?",
            optA_Click = () =>
            {
                SetWndState(false);
                wndManager.ClearNotice();
                BattleManager.Instance.EndGame(1, GameResult.Interrupt);
            },
            optA_Text = "确认",
            optB_Text = "取消"
        });

    }
    /// <summary>(在舰船上)重置</summary>
    void TryRebirth()
    {
        //不需要判定状态，已经隐藏??
        //其实按理说应该是传送回房间的，但是现在没做
        ActorsManager.Player.Pos = Vector3.zero;
        SetWndState(false);
    }

    void EnterFreeCamera()
    {
        WindowState = WindowStateEnum.FreeCamera;
        SetWndState(false);
    }
    /// <summary>退出游戏</summary>
    void TryExitGame()
    {
        wndManager.CreatTip(new() {
            title = "退出游戏",
            desc = "\n确定要退出游戏吗?",
            optA_Click = () => {
                GameRoot.ExitGame();
            },
            optA_Text = "确认",
            optB_Text = "取消"
        });
    }

    private bool Cancel()
    {
        if (!State) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        SetWndState(false);
        justClosed = Time.time;
        //鼠标按esc不锁是unity编辑器的特殊情况，无视就行（编译没这个问题）
        return true;
    }

}

