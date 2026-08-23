using System.Collections.Generic;
using Core;
using FPSGame.Attribute;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static ArchivesData_SO;
using static WndTools.WndRootTool;

public partial class SettingWnd : Window
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
    public Transform freeCamera, rebirth,returnShop, exitGame,teach;

    [Foldout("设置",true)]
    public GameObject tempTitle, tempDrop, tempToggle, tempSilder;
    public Transform settingRoot,updateTitle, updateTime, updateDesc, updateCount, updateLeft, updateRight;
    public Transform secondaryLayoutRoot;
    public RectTransform nowsecondarySelect;

    private List<UpdateData_SO> m_UpdateDataArr;
    private int nowUpdateIndex=0;
    private int nowExpandIndex=1;
    private int _secondaryIndex = 0;
    private List<string> _secondaryTitles = new();
    private List<List<KVP<string, ArchSettingData>>> _secondaryGroups = new();
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
        BuildSettingGroups();
        BuildKeyCodeDict();

        // 首次打开时隐藏次级菜单和选中高亮
        SetActive(secondaryLayoutRoot, false);
        if (nowsecondarySelect != null)
        {
            nowsecondarySelect.gameObject.SetActive(false);
        }

        // 首次默认显示第一个分组
        if (_secondaryGroups.Count > 0)
        {
            RefreshSettingContentByTitle(_secondaryTitles[0]);
        }


        SetCilck(layoutRoot.GetChild(0), () => SwitchNextExpand(false));
        SetCilck(layoutRoot.GetChild(layoutRoot.childCount-1), () => SwitchNextExpand(true));

        // 次级layout的Z/C按钮点击（使用 Find 获取引用，避免 siblingIndex 变化后引用错乱）
        if (secondaryLayoutRoot != null && secondaryLayoutRoot.childCount >= 3)
        {
            var zBtn = secondaryLayoutRoot.GetChild(0);
            var cBtn = secondaryLayoutRoot.GetChild(secondaryLayoutRoot.childCount - 1);
            // 先清除可能已有的 Inspector 绑定
            ClearButton(zBtn);
            ClearButton(cBtn);
            SetCilck(zBtn, () => SwitchSecondaryIndex(false));
            SetCilck(cBtn, () => SwitchSecondaryIndex(true));
        }

        SetCilck(freeCamera, EnterFreeCamera);
        SetCilck(returnShop, TryReturnShop);
        SetCilck(rebirth, TryRebirth);
        SetCilck(exitGame, TryExitGame);
        SetCilck(teach, TryTeach);

        


        WndManager.OnWindowStateSet += OnWindowStateChange;

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
        if (_haveInputChanged)
        {
            (InputManager.Instance as InputManager).Save();
        }
        Tool.Destroy(showModle);

    }

    void Update()
    {
        if (IsKeyCodeModuleActive)
        {
            HandleKeyCodeInput();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                SwitchNextExpand(false);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                SwitchNextExpand(true);
            }
            if (Input.GetKeyDown(KeyCode.Z))
            {
                SwitchSecondaryIndex(false);
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                SwitchSecondaryIndex(true);
            }
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

        // 切换到非设置子界面时隐藏次级layout
        RefreshSecondaryLayoutVisibility();
    }


    public void SetUpdate(bool add)
    {
        nowUpdateIndex = Tool.PositiveRemainder(nowUpdateIndex + (add ? 1 : -1),m_UpdateDataArr.Count);
        SetText(updateTitle, m_UpdateDataArr[nowUpdateIndex].title);
        SetText(updateDesc, m_UpdateDataArr[nowUpdateIndex].desc);
        SetText(updateTime, m_UpdateDataArr[nowUpdateIndex].time);
        SetText(updateCount, "" + (nowUpdateIndex+1) + "/" + m_UpdateDataArr.Count + "");
        
    }

    /// <summary>
    /// 根据settingDic的titile分组。出现某个title后，后续项都属于该title，直到下一个title出现
    /// </summary>
    private void BuildSettingGroups()
    {
        _secondaryTitles.Clear();
        _secondaryGroups.Clear();

        string currentTitle = "其他";
        List<KVP<string, ArchSettingData>> currentGroup = null;

        ArchiveSvc.Archive.settingDic.ForEach((key, data) =>
        {
            // 有非空title → 开启新分组
            if (!string.IsNullOrEmpty(data.titile))
            {
                currentTitle = data.titile;
                currentGroup = new List<KVP<string, ArchSettingData>>();
                _secondaryTitles.Add(currentTitle);
                _secondaryGroups.Add(currentGroup);
            }

            // 第一个分组还没建立时（第一条数据没有title），创建默认分组
            if (currentGroup == null)
            {
                currentGroup = new List<KVP<string, ArchSettingData>>();
                _secondaryTitles.Add(currentTitle);
                _secondaryGroups.Add(currentGroup);
            }

            currentGroup.Add(new KVP<string, ArchSettingData>(key, data));
        });
    }

    /// <summary>
    /// 刷新次级layout的显示状态
    /// </summary>
    private void RefreshSecondaryLayoutVisibility()
    {
        if (secondaryLayoutRoot == null) return;

        // 判断当前选中的是否是设置子界面（settingRoot 所在的 expandRoot 子项）
        bool isSettingTab = false;
        if (expandRoot != null && expandRoot.childCount > nowExpandIndex)
        {
            var currentExpand = expandRoot.GetChild(nowExpandIndex);
            isSettingTab = settingRoot != null && settingRoot.IsChildOf(currentExpand);
        }

        SetActive(secondaryLayoutRoot, isSettingTab);
        if (nowsecondarySelect != null)
        {
            nowsecondarySelect.gameObject.SetActive(isSettingTab);
        }

        if (isSettingTab)
        {
            InitSecondaryLayoutItems();
            RefreshSecondaryContent();
            // 显示时重置到第一项，会同步高亮位置
            RefreshSettingContentByTitle(_secondaryTitles[0]);
        }
    }

    /// <summary>
    /// 初始化次级layout的子项（index 0=Z按钮, index 1=模板, 最后一项=C按钮）
    /// </summary>
    private void InitSecondaryLayoutItems()
    {
        if (secondaryLayoutRoot == null || secondaryLayoutRoot.childCount < 3) return;

        var template = secondaryLayoutRoot.GetChild(1);
        var neededCount = _secondaryTitles.Count;
        // 除去首尾按钮，当前内容项数量
        var currentContentCount = secondaryLayoutRoot.childCount - 2;

        // 补足缺少的项（插入到C按钮之前）
        while (currentContentCount < neededCount)
        {
            var newItem = Instantiate(template.gameObject, secondaryLayoutRoot);
            newItem.transform.SetSiblingIndex(secondaryLayoutRoot.childCount - 2);
            currentContentCount++;
        }

        // 隐藏多余的项，首尾按钮始终显示
        for (int i = 0; i < secondaryLayoutRoot.childCount; ++i)
        {
            bool isButton = i == 0 || i == secondaryLayoutRoot.childCount - 1;
            bool isContent = i > 0 && i < secondaryLayoutRoot.childCount - 1;
            if (isButton)
            {
                SetActive(secondaryLayoutRoot.GetChild(i), true);
            }
            else if (isContent)
            {
                var contentIndex = i - 1;
                SetActive(secondaryLayoutRoot.GetChild(i), contentIndex < neededCount);
            }
        }
    }

    /// <summary>
    /// 刷新次级layout每个内容项的名称和点击事件（第0个子物体是text），跳过首尾按钮，并同步选中高亮到当前项
    /// </summary>
    private void RefreshSecondaryContent()
    {
        if (secondaryLayoutRoot == null || secondaryLayoutRoot.childCount < 3) return;

        var lastIndex = secondaryLayoutRoot.childCount - 1;
        for (int i = 0; i < _secondaryTitles.Count; ++i)
        {
            var contentIndex = i + 1; // 跳过 index 0 的 Z 按钮
            if (contentIndex >= lastIndex) break;
            var item = secondaryLayoutRoot.GetChild(contentIndex);
            SetText(item.GetChild(0), _secondaryTitles[i]);

            // 绑定内容项按钮点击（按钮在 item 本体上）
            ClearButton(item);
            var titleIndex = i; // 闭包捕获
            SetCilck(item, () => RefreshSettingContentByTitle(_secondaryTitles[titleIndex]));
        }

        // 同步 nowsecondarySelect 位置到当前选中项
        if (nowsecondarySelect != null && _secondaryIndex >= 0 && _secondaryIndex < _secondaryTitles.Count)
        {
            var selectTarget = (RectTransform)secondaryLayoutRoot.GetChild(_secondaryIndex + 1);
            nowsecondarySelect.position = selectTarget.position;
            nowsecondarySelect.sizeDelta = selectTarget.sizeDelta;
        }
    }

    /// <summary>
    /// 切换次级tab
    /// </summary>
    private void SwitchSecondaryIndex(bool isAdd)
    {
        if (_secondaryTitles.Count == 0) return;
        _secondaryIndex = Tool.PositiveRemainder(_secondaryIndex + (isAdd ? 1 : -1), _secondaryTitles.Count);
        RefreshSettingContentByTitle(_secondaryTitles[_secondaryIndex]);
        wndManager.PlaySound(new("UI/UI_Notice"));
    }

    /// <summary>
    /// 根据次级tab的title显示对应的设置项内容
    /// </summary>
    public void RefreshSettingContentByTitle(string title)
    {
        if (_secondaryGroups.Count == 0 || secondaryLayoutRoot == null) return;

        var index = _secondaryTitles.IndexOf(title);
        if (index < 0) return;

        _secondaryIndex = index;

        // 移动次级选中高亮到对应内容项（跳过 index 0 的 Z 按钮）
        if (nowsecondarySelect != null && secondaryLayoutRoot.childCount > index + 1)
        {
            var selectTarget = (RectTransform)secondaryLayoutRoot.GetChild(index + 1);
            nowsecondarySelect.position = selectTarget.position;
            nowsecondarySelect.sizeDelta = selectTarget.sizeDelta;
        }

        // 清除settingRoot旧内容
        settingRoot.ForEach(child => Destroy(child.gameObject));

        // 重新创建对应分组的设置项
        var group = _secondaryGroups[index];
        foreach (var kvp in group)
        {
            CreatItem(kvp.Key, kvp.Value);
        }
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
        showModle.transform.position = transform.TransformPoint(new(600, -650, 600));
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

    /// <summary>(在舰船上)新手教程</summary>
    void TryTeach()
    {
        SetWndState(false);
        ResSvc.Instance.AsyncLoadScene("Teach", () => {
            BattleManager.Creat(false);
        });
    }

    /// <summary>(在舰船上)重置</summary>
    void TryRebirth()
    {
        //不需要判定状态，已经隐藏??
        //其实按理说应该是传送回房间的，但是现在没做
        ActorsManager.Player.transform.GetComponent<CharacterController>().enabled = false;
        ActorsManager.Player.Pos = TransformUtils.SceenFind("ShowStudentPoint").transform.position;
        ActorsManager.Player.transform.GetComponent<CharacterController>().enabled = true;
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

        // HandleKeyCodeInput 已在本帧处理了选择框/改键的取消，不再重复关闭窗口
        if (_escHandledThisFrame)
        {
            return true;
        }

        // 如果选择框或改键模式激活，先取消它们（兜底）
        if (_rebindSelectionRoot != null && _rebindSelectionRoot.activeSelf)
        {
            HideSelection();
            return true;
        }
        if (_rebindState == RebindState.WaitingForKey)
        {
            ResetRebindState();
            return true;
        }

        wndManager.PlaySound(new("UI/UI_Button_Back"));
        SetWndState(false);
        justClosed = Time.time;
        //鼠标按esc不锁是unity编辑器的特殊情况，无视就行（编译没这个问题）
        return true;
    }


    // 这个方法会被按钮的OnClick事件调用
    public void _OpenURL(string url)
    {
        Application.OpenURL(url);
    }


}

