using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Attributes;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.UI;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using FPSGame.GameContract;
using FPSGame.Net;
using static GameData.ArchivesData_SO;
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Data;
using FPSGame.Managers;
using FPSGame.GameData;

    /// <summary>
    /// 设置窗口（含快捷键分部）。
    /// </summary>
    [AddComponentMenu("UI/窗口/设置")]
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

    [Foldout("状态", true)]
    [InspectorName("空玩家位根（stateWnd/PlayerStateSelf/FriendRoot）")]
    [SerializeField]
    private Transform emptySlotRoot;

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
        BindEmptyRoomSlots();

        


        WndManager.OnWindowStateSet += OnWindowStateChange;

        lookPoint = new GameObject("LookPoint");
        lookPoint.transform.parent = transform;
        lookPoint.transform.position = uiCamera.ScreenToWorldPoint(Input.mousePosition);
    }

    /// <summary>本次打开是否动过时间流速。**必须记着**：恢复时不能重算条件 ——
    /// "打开时房里只有我、关窗时房里变多人"会让重算判成"不用恢复" ⇒ 时间永远停在 0.01（2026-10-07 用户实测）。</summary>
    private bool _slowedTime;

    /// <summary>本机是不是"单机战斗"（只有我一个真人、且不在联机房间里）。
    /// 联机时**绝不动**时间流速：两端时间基准一旦不同步，表现就是"对面像快进/慢放"。
    /// ⚠ <c>BridgeSys</c> 只在舰桥场景 ⇒ 战斗中它回答不了，靠"真人玩家数"兜底。</summary>
    private bool IsStandaloneBattle =>
        GameState == GameStateEnum.Game
        && teamManager != null && teamManager.IsSingle
        && (BridgeSys.Instance == null || !BridgeSys.InOnlineRoom);

    protected override void ShowWnd()
    {
        selfChangeState = true;
        oldStste = WindowState!= WindowStateEnum.FreeCamera? WindowState : WindowStateEnum.Game;
        WindowState = WindowStateEnum.UI;
        selfChangeState = false;

        //wndManager.WndUI.gameObject.SetActive(false);
        //feature.SetActive(true);
        // ⚠ 只有单机战斗才降速（联机时降速会与别人时间基准打架，而且退出后必须还回来）
        if (IsStandaloneBattle)
        {
            TimeScale = 0.01f;
            _slowedTime = true;
        }
        // 创建临时Texture2D（⚠ 抓不到就保留上一张背景：Camera.main 可能为 null，见 CameraCaptureToSprite）
        var bgSprite = FpsHelper.CameraCaptureToSprite(Camera.main);
        if (bgSprite != null) BG.sprite = bgSprite;
        BG.material.SetFloat("_TimeScale", TimeScale);
        //GlobalEventManager.OnFakeBg(BG.transform);
        haveSettingChagne = false;
        SetActive(returnShop,GameState == GameStateEnum.Game);
        SetActive(rebirth, GameState == GameStateEnum.Bridge);
        SetActive(freeCamera, teamManager.IsSingle);

        SetStateRoot();
        InputManager.AddListenerCancel(Cancel);
    }
    protected override void HideWnd()
    {
        WindowState = oldStste;
        //wndManager.WndUI.gameObject.SetActive(true);
        //feature.SetActive(false);
        if (_slowedTime)
        {
            TimeScale = 1;      // 动了就一定要还原（别重算条件，见 _slowedTime 的说明）
            _slowedTime = false;
        }
        BG.material.SetFloat("_TimeScale", TimeScale);
        //GlobalEventManager.OnFakeBg(null);
        if (haveSettingChagne)
        {
            ArchivesData_SO.Current.Save();
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

        ArchivesData_SO.Current.settingDic.ForEach((key, data) =>
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
        SetText(mapName, taskManager.MapId);
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
    /// <summary>
    /// 把 <c>stateWnd/PlayerStateSelf/FriendRoot</c> 下每个玩家位的**空位**节点绑成"创建房间"入口。
    ///
    /// <para>▍节点约定（预制体实测）：每个 <c>PlayerState</c> 有 2 个子节点 ——
    /// 子0 = 有玩家（头像 + 角色名称，默认 inactive）、子1 = 空位（显示"空位"）。
    /// 空位底图是 <c>PlayerState(n)/GameObject (1)/Image</c>，它原本没有 Button ⇒ 这里就地补一个
    /// （同 <c>ServerListPanel.SetItemClick</c> 的做法），并把 raycast 目标指到那张底图上。</para>
    /// </summary>
    private void BindEmptyRoomSlots()
    {
        if (emptySlotRoot == null) return;

        for (int i = 0; i < emptySlotRoot.childCount; ++i)
        {
            var slot = emptySlotRoot.GetChild(i);
            if (slot.childCount < 2) continue;                 // 子0 = 有人、子1 = 空位

            var empty = slot.GetChild(1);
            var click = empty.childCount > 0 ? empty.GetChild(0) : empty;
            var graphic = click.GetComponent<Image>();
            var btn = click.TryGetOrAddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            if (graphic != null)
            {
                graphic.raycastTarget = true;
                btn.targetGraphic = graphic;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                wndManager.PlaySound(new("UI/UI_Bubble"));
                CreateRoomFromEmptySlot();
            });
        }
    }

    /// <summary>
    /// 空玩家位 → 以**当前这局的地图与难度**开房并局域网广播（等于地图界面 <c>pubilc</c> 那个入口）。
    /// 这里不走 <c>SelectMapWnd</c>：设置窗口是常驻窗口，战场里也要能用，而选图窗口不一定在场景里。
    /// </summary>
    private void CreateRoomFromEmptySlot()
    {
        var flow = NetRoomFlow.Instance;
        if (flow == null)
        {
            ShowTip("联机不可用", "网络服务未初始化：GameRoot 下缺少 NetSvc / NetHostSvc / NetRoomFlow。");
            return;
        }

        var archive = ArchivesData_SO.Current;
        string hostName = archive == null || string.IsNullOrEmpty(archive.playerName) ? "玩家" : archive.playerName;

        var options = new HostRoomOptions
        {
            RoomName = hostName + "的房间",
            MapName = taskManager.MapId,
            MaxPlayers = 4,
            Password = string.Empty,
            Difficulty = taskManager.nowTask.activeTask ? (int)taskManager.nowTask.difficulty : -1,
            HostName = hostName,
            TaskMain = taskManager.NowTaskMain,
        };

        if (!flow.Host(options, out string reason))
        {
            ShowTip("开房失败", reason);
            return;
        }

        ShowTip("房间已创建", "已在局域网广播：" + (string.IsNullOrEmpty(options.MapName) ? "未指定地图" : options.MapName));
    }

    private void ShowTip(string title, string desc)
    {
        var tip = WndHub.Tip;
        if (tip != null) tip.Creat(new() { title = title, desc = desc });
        else Debug.LogWarning("[SettingWnd] " + title + "：" + desc);
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
        // ⚠ 两个都可能为 null：没有玩家实体的场合（大厅 / 实体已销毁），或模型路径取不到。
        //   漏判的表现就是 ESC 打开设置窗直接抛 NullReferenceException（2026-10-07 实测）。
        showModle = player != null ? resManager.CreatPrefab("Prefabs/StudentModle/" + player.Id, false) : null;
        if (showModle != null)
        {
            showModle.transform.position = transform.TransformPoint(new(600, -650, 600));
            showModle.transform.eulerAngles = new(0, -170, 0);
            showModle.transform.localScale = new(550, 550, 550);
            showModle.SetChildLayer(gameObject.layer, 3);
            var comp = showModle.GetComponent<RootMotion.FinalIK.LookAtController>();
            if (comp != null && comp.ik != null && comp.ik.solver != null)
            {
                comp.ik.solver.bodyWeight = 0;
                comp.target = lookPoint.transform;
            }
        }

        if (player == null) return;   // 下面全是玩家档案数据（名称/头像/等级/经验）
        SetText(selfName, player.ShowName);
        SetSprite(selfIcon, player.Portrait);
        ArchivesData_SO.Current.GetRoleLevel(player.Id, out int level, out float expScale);
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
        
        WndHub.Tip.Creat(new() {
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
        WndHub.Tip.Creat(new() {
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

}
