using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Attributes;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Net;
using KCPNet;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
    using FPSGame.GameContract;
using FPSGame.GameData;

    /// <summary>
    /// 地图与任务选择窗口。
    /// </summary>
    [AddComponentMenu("UI/窗口/选图")]
public class SelectMapWnd : Window
{
    private const float _SwitchTime=0.5f;
    /// <summary>「加入」快速匹配的搜房等待上限（秒）。库的 <c>LanDiscoverer.Scan()</c> 是异步的
    /// （等 <c>DiscoveryTimeoutMs</c> ≈1.5s 才有结果）⇒ 必须给它一轮时间，否则永远搜不到房间。
    /// 期间一旦搜到就提前结束（见 <see cref="MatchRoutine"/>）。</summary>
    private const float MatchSearchSeconds=2f;

    /// <summary>快速匹配期间**重发 DISCOVER 的间隔**（秒）。库的 <c>Scan()</c> 是裸 UDP 一次性发出、不重传，
    /// 只扫一次的话那一个包丢了就整轮空 ⇒ 搜不到同伴、两人各自开房、互相看不见（展会现场最易翻车的点）。</summary>
    private const float MatchRescanSeconds=1f;

    private MapWndState mapState;
    private float magnifc;
    private Vector2 primeRootSize, primeRootPos;
    private float nowtime;
    /// <summary>进行中的快速匹配协程（重复点「加入」时先停掉上一条）。</summary>
    private Coroutine _matchRoutine;
#if UNITY_EDITOR
    /// <summary>【仅编辑器】「可选任务」图标行，首次显示任务详情时生成（见 <see cref="RefreshExtraTaskIcons"/>）。</summary>
    private RectTransform _extraIconRow;
#endif

    [Foldout("基础",true)]
    [SerializeField]
    private Transform mapRoot,cancel,random,server;

    [Foldout("服务器列表", true)]
    [SerializeField]
    private ServerListPanel serverPanel;
    [InspectorName("展开面板时隐藏的按钮栏")]
    [SerializeField]
    private Transform buttonsRoot;

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
    private ArchivesData_SO arch => ArchivesData_SO.Current;
    public void Init()
    {
    }
    public void Uninit()
    {
    }
    protected override void FirstShowWnd()
    {

        SelectTaskDiff = 3;

        // 任务表跨时间窗口就整张重建（TaskManager.Update → CreatAllTask）⇒ 窗口开着时就地重画内外两层；
        // 否则内层点地图才刷、外层停在开窗那一刻，一层新一层旧（见 RefreshTaskBoard）。
        taskManager.OnTaskTableRefreshed -= OnTaskTableRefreshed;   // 窗口会被反复唤醒，先减后加防重复订阅
        taskManager.OnTaskTableRefreshed += OnTaskTableRefreshed;

        for (int i = 0; i < mapRoot.childCount; ++i)
        {
            int a = i;

            // 图标行 / 空标记 / 按钮可交互性统一交给 RefreshMapRoot 按当前任务表算
            //（原来只在首开时算一次，还顺手改写了 TaskCfgs 的 enable ⇒ 换表后内外对不上）
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
                if(taskManager.TaskCfgs[SelectTaskRow,a].enable && a != SelectTaskIndex) ShowAreaInfoWnd(a, SelectTaskIndex > -1);
            });

            SetButtonExit(mapInfoLayout.GetChild(a), (data) => {
                if (SelectTaskIndex > -1)
                {
                    if (a != SelectTaskIndex)ShowAreaInfoWnd(SelectTaskIndex,true);
                }
                else if (taskManager.TaskCfgs[SelectTaskRow, a].enable && a != SelectTaskIndex) HideAreaInfoWnd();
            });
        }

        SetCilck(cancel, () =>
        {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            SetWndState(false);
        });

        //点击 Server 展开「服务器列表」子界面（面板自身负责收起/筛选/行填充；
        //展开期间由 SetServerPanelVisible 隐藏「地图 + 按钮栏」，收起时按当前状态复位）
        if (serverPanel) serverPanel.OnVisibleChanged = SetServerPanelVisible;
        // 面板只抛出"选中的房间 + 密码"，回连/入房/超时统一走 NetRoomFlow（见 OnRoomActivated）
        if (serverPanel) serverPanel.OnRoomActivated = OnRoomActivated;
        SetCilck(server, () =>
        {
            wndManager.PlaySound(new("UI/UI_Bubble"));
            if (serverPanel) serverPanel.Open();
        });


        SetCilck(areaInfoDiffLeft, () =>
        {
            SetDiff(false);
        });

        SetCilck(areaInfoDiffRight, () =>
        {
            SetDiff(true);
        });

        // 「加入」= 快速匹配（2026-10-10 口径）：先按"当前地图 + 当前任务"搜局域网里的房，
        // 搜到就直接回连入房（<see cref="QuickMatch"/>）；一个都没有才自己开一个 —— 走「公开房」那条链，
        // 由玩家按「准备」时才静默建服（<see cref="ConfirmTask"/>）⇒ 房间广播里才带得上本局正确的任务枚举。
        // （想看/挑房仍然走 <see cref="server"/> 那个「服务器列表」入口）
        SetCilck(taskJoin, () =>
        {
            SelectPlayMode = 0;
            wndManager.PlaySound(new("UI/UI_Bubble"));
            QuickMatch();
        });
        // 「公开/开房」= 只记住"这一局是公开房"，展开的界面与「单人」完全一致（调难度 / 选任务 / 选加成）。
        // 服务器推迟到按下「准备」时才**静默**创建（见 ConfirmTask）——不再一点按钮就开房、弹提示、
        // 还把「服务器列表」顶上来（2026-10-06 口径）。
        SetCilck(taskPublic, () =>
        {
            SelectPlayMode = 1;
            wndManager.PlaySound(new("UI/UI_Bubble"));
            ExpandCfg();
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
            ConfirmTask();
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
        if (serverPanel) serverPanel.Close();
        SetText(areaInfoDiff, ((DifficultyEnum)SelectTaskDiff).ToString());
        RefreshTaskBoard();

        //GlobalEventManager.OnFakeBg(mapRoot.parent);
    }

    protected override void HideWnd()
    {
        WindowState = WindowStateEnum.Game;
        InputManager.RemoveListenerCancel(Cancel);
        // ★ 窗口收起时把「房间列表」一并收掉：它的发现器占着局域网 UDP 广播端口，而本机随后要开房时
        //   <c>LanBroadcaster</c> 也要同一个端口 ⇒ 监听不能留到窗口之外（否则开房直接 SocketException）。
        if (serverPanel) serverPanel.Close();
        //GlobalEventManager.OnFakeBg(null);
    }

    /// <summary>任务表重建（30 分钟一个窗口）：窗口开着就当场重画内外两层；收着就等下次 <see cref="ShowWnd"/>。</summary>
    private void OnTaskTableRefreshed()
    {
        if (!State) return;
        RefreshTaskBoard();
    }

    public override void OnDestroy()
    {
        // 与 FirstShowWnd 里的订阅成对（窗口没首开过 ⇒ taskManager 还是 null）
        if (taskManager) taskManager.OnTaskTableRefreshed -= OnTaskTableRefreshed;
        base.OnDestroy();
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

    /// <summary>
    /// 「房间列表」子界面展开 / 收起时切换其它 UI：
    /// 展开 ⇒ 隐藏「地图(Bg/Map) + 地图放大视图(AreaRoot) + 按钮栏(Buttons)」；
    /// 收起 ⇒ 按钮栏恢复，地图与放大视图按当前状态复位
    /// （<see cref="MapWndState.Map"/> 显示地图、其余状态显示 AreaRoot，与 <see cref="Cancel"/> 的状态机口径一致）。
    /// <para>▍AreaRoot 也要收：它只由「点地图」那条链点亮（那时 mapRoot 已经藏了），
    /// 不收就会连"任务图标 + 配置面板"一起压在地图上面，房间列表看着像半透明叠图。</para>
    /// </summary>
    private void SetServerPanelVisible(bool visible)
    {
        SetActive(buttonsRoot, !visible);
        SetActive(mapRoot, !visible && mapState == MapWndState.Map);
        SetActive(areaRoot, !visible && mapState != MapWndState.Map);
    }

    /// <summary>
    /// 「房间列表」里点了加入（<see cref="ServerListPanel.OnRoomActivated"/>）：
    /// 回连房主 → 申请入房 → 等房主回执，全过程与超时都在 <see cref="NetRoomFlow"/> 里。
    /// </summary>
    private void OnRoomActivated(LanRoomInfo room, string password)
    {
        // ★ 要去别人的房间了 ⇒ 收起本地的任务配置面板：进了房以后本局配置由房主说了算，
        //   面板留着还能点「单人 / 公开」把本机切成单机（与所在房间冲突）。收起来之后
        //   也点不开（见 SelectTask 的 IsRoomMember 门）。
        if (mapState == MapWndState.SelectTask) CancelTask();

        var flow = NetRoomFlow.Instance;
        if (flow == null)
        {
            WndHub.Tip.Creat(new()
            {
                title = "联机不可用",
                desc = "网络服务未初始化：GameRoot 下缺少 NetSvc / NetHostSvc / NetRoomFlow。",
            });
            return;
        }

        flow.Join(room, string.IsNullOrEmpty(arch.playerName) ? "玩家" : arch.playerName, password);
    }

    /// <summary>
    /// 【快速匹配】点「加入」：先搜有没有**同地图 + 同任务**的房，有就直接进；没有就自己开一个。
    /// <para>▍搜房走 <see cref="ServerListPanel"/>（发现器在那边），但**不展开面板**；
    /// 面板从没打开过时发现器还没在听广播 ⇒ 先把它拉起来再扫（见 <see cref="MatchRoutine"/>）。</para>
    /// </summary>
    private void QuickMatch()
    {
        if (SelectTaskIndex < 0)
        {
            WndHub.Tip.Creat(new() { title = "还没选任务", desc = "先点地图上的任务图标，再点「加入」。" });
            return;
        }

        if (_matchRoutine != null) StopCoroutine(_matchRoutine);
        _matchRoutine = StartCoroutine(MatchRoutine());
    }

    /// <summary>
    /// 快速匹配主体：拉起发现器 → 扫描 → 轮询等结果（最多 <see cref="MatchSearchSeconds"/>）→
    /// 搜到就 <see cref="OnRoomActivated"/> 入房；搜不到就**直接自己公开一个**（静默建服 + 展开配置，
    /// 见方法末尾），玩家按「准备」时 <see cref="ConfirmTask"/> 再把本局任务补进广播。
    /// <para>▍为什么必须等：库的 <c>LanDiscoverer.Scan()</c> 是异步的（<c>DiscoveryTimeoutMs</c> 量级），
    /// 扫完下一行就 <c>GetRooms()</c> 必然拿到空表 ⇒ 每次都会误判成"没人开房"。</para>
    /// <para>▍窗口内**反复重扫**（<see cref="MatchRescanSeconds"/>）：DISCOVER 是裸 UDP、库不重传，
    /// 只扫一次的话那一个包丢了就整轮空 ⇒ 两人都点「加入」却各自开房、互相看不见。</para>
    /// </summary>
    private IEnumerator MatchRoutine()
    {
        string mapName = mapRoot.childCount > SelectMapIndex ? mapRoot.GetChild(SelectMapIndex).name : string.Empty;
        int taskMain = (int)taskManager.TaskCfgs[SelectTaskRow, SelectTaskIndex].main;

        var panel = serverPanel;
        if (panel) panel.EnsureDiscovery();   // 面板没打开过 ⇒ 先让发现器在听广播

        var tip = WndHub.Tip;
        if (tip) tip.Creat(new() { title = "正在搜索房间", desc = "正在搜索相同任务的房间…" });

        LanRoomInfo room = null;
        float left = MatchSearchSeconds;
        float rescan = 0f;                    // ≤0 ⇒ 进循环立刻发第一枪（等价于原来"进循环前 Scan 一次"）
        while (left > 0f)
        {
            // 每 MatchRescanSeconds 补一枪（库的 Scan 要 ~DiscoveryTimeoutMs 才出结果，别每帧扫）
            if (panel && rescan <= 0f)
            {
                panel.Scan();
                rescan = MatchRescanSeconds;
            }
            rescan -= Time.unscaledDeltaTime;

            if (panel) room = panel.FindJoinableRoom(mapName, taskMain);
            if (room != null) break;
            left -= Time.unscaledDeltaTime;
            yield return null;
        }
        _matchRoutine = null;

        // 收掉"搜索中"那条；队列里若还排着别的提示，TipWnd.Close 会自动接着显示下一条
        if (tip) tip.Close();

        if (room != null)
        {
            OnRoomActivated(room, string.Empty);
            yield break;   // 迭代器里收尾用 yield break（普通 return 会被判 CS1622）
        }

        // 没有同任务的房 ⇒ **直接自己公开一个**（2026-10-10 用户口径：不再弹"没找到房间"的提示）。
        // 静默建服（不弹"房间已创建"、不掀服务器列表），配置面板照常展开。
        // ⚠ 建服时就把**当前选中的任务内容**写进广播（CreateRoom 内部取 SelectedTaskCfg）：不写的话房间广播里
        //   还是上一局的任务，别人按同任务搜房就搜不到这一间（要等按「准备」时 ConfirmTask 才刷新）。
        SelectPlayMode = 1;
        if (!IsHosting()) CreateRoom(true);   // ⚠ 已经在开房就别重开：同端口会 SocketException
        ExpandCfg();
    }

    /// <summary>当前**选中**的任务内容（未选 / 下标非法 ⇒ null）。
    /// <para>▍开房时给房间广播一份，列表立刻能显示任务名；**不读** <c>TaskManager.NowTaskMain</c>
    /// —— 那是上一局的值（建服发生在选完任务之前）。</para></summary>
    private TaskCfgDto SelectedTaskCfg()
    {
        var cfgs = taskManager != null ? taskManager.TaskCfgs : null;
        if (cfgs == null || SelectTaskRow < 0 || SelectTaskIndex < 0) return null;
        if (SelectTaskRow >= cfgs.GetLength(0) || SelectTaskIndex >= cfgs.GetLength(1)) return null;
        return TaskManager.ToDto(cfgs[SelectTaskRow, SelectTaskIndex]);
    }

    /// <summary>
    /// 【开房】用当前选中的地图 + 任务难度开房并局域网广播。
    /// <para>▍当前唯一调用者是「公开房」流程的 <see cref="ConfirmTask"/>（选完任务才建服）。
    /// 设置界面的空玩家位走的是自己的 <c>SettingWnd.CreateRoomFromEmptySlot</c>（它是常驻窗口，选图窗口不一定在场景里）。</para>
    /// </summary>
    /// <param name="silent">
    /// true = 静默建服：成功不弹提示、不打开「服务器列表」。失败**仍然**弹提示（失败不该静默）。
    /// </param>
    /// <param name="cfg">本房任务内容（null = 用当前选中的那一份 <see cref="SelectedTaskCfg"/>）。
    /// <para>▍为什么直接给**内容**而不是"主任务枚举 + 下标"：列表要的图标/颜色/标题都能从内容里取，
    /// 而下标在"任务表各端本地按时间窗口生成"的前提下不可靠。⚠ 此刻本局可能还没 `SetTask`
    /// （公开房是先建服）⇒ 真正的权威时刻仍是 <see cref="ConfirmTask"/> 里的 <c>host.ConfirmTask</c>。</para></param>
    public void CreateRoom(bool silent = false, TaskCfgDto cfg = null)
    {
        var flow = NetRoomFlow.Instance;
        if (flow == null)
        {
            WndHub.Tip.Creat(new()
            {
                title = "联机不可用",
                desc = "网络服务未初始化：GameRoot 下缺少 NetSvc / NetHostSvc / NetRoomFlow。",
            });
            return;
        }

        string mapName = mapRoot.childCount > SelectMapIndex ? mapRoot.GetChild(SelectMapIndex).name : string.Empty;
        string hostName = string.IsNullOrEmpty(arch.playerName) ? "玩家" : arch.playerName;

        var options = new HostRoomOptions
        {
            RoomName = hostName + "的房间",
            MapName = mapName,
            MaxPlayers = 4,
            Password = string.Empty,
            Difficulty = SelectTaskDiff,
            HostName = hostName,
            // ⚠ 此刻本局任务**还没 SetTask**（「公开房」是先建服再确认任务）⇒ 内容取自"当前选中的那一项"，
            //   不必读 NowTaskMain（那是**上一局**的值）。真正的权威时刻仍是 ConfirmTask。
            TaskCfg = cfg ?? SelectedTaskCfg(),
        };

        if (!flow.Host(options, out string reason))
        {
            WndHub.Tip.Creat(new() { title = "开房失败", desc = reason });
            return;
        }

        if (silent) return;   // 静默建服：连"房间已创建"提示与服务器列表都不掀（那是显式开房入口的待遇）

        WndHub.Tip.Creat(new()
        {
            title = "房间已创建",
            desc = "已在局域网广播：" + (string.IsNullOrEmpty(mapName) ? "未指定地图" : mapName) + "\n其他玩家可在「加入」里搜到。",
        });

        // 开完房把房间列表打开：能立刻看到自己的房间（也是"房间大厅"的临时落脚点）
        if (serverPanel) serverPanel.Open();
    }

    /// <summary>
    /// 本机当前是否已经开好房 —— <c>NetHostSvc.RoomInfo</c> 是"我是不是房主"的唯一判据
    /// （<c>StartHost</c> 里设、<c>StopHost</c> 里清；<c>ConfirmTask</c> 也按它决定要不要广播本局配置）。
    /// </summary>
    private static bool IsHosting()
    {
        var host = NetHostSvc.Instance;
        return host != null && host.RoomInfo != null;
    }

    /// <summary>
    /// 本机是不是"已经进了别人的房间"（= 正在回连/入房中，或已连上房主且自己不是房主）。
    /// <para>▍用途：进了别人的房以后，本局的地图 / 任务 / 难度 / 模式都由房主下发
    /// ⇒ 再展开任务配置面板就会把本机切成「单人 / 公开房」，与所在房间冲突。
    /// 所以 <see cref="SelectTask"/>（展开面板的唯一入口）在这里设门。房主自己、单机都不受影响。</para>
    /// </summary>
    private static bool IsRoomMember()
    {
        var flow = NetRoomFlow.Instance;
        if (flow == null) return false;
        if (flow.IsJoining) return true;      // 正在回连/入房：这一局已经交给别人了
        if (flow.IsHost) return false;

        var svc = NetSvc.Instance;
        return svc != null && svc.IsConnected;   // 连上了但自己不是房主 ⇒ 我是成员
    }

    private bool Cancel()
    {
        if (this==null||!State) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));

        //「服务器列表」子界面优先消费 Cancel：先收面板，再退窗口层级
        if (serverPanel && serverPanel.IsOpen)
        {
            serverPanel.Close();
            InputManager.AddListenerCancel(Cancel);
            return true;
        }

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

    /// <summary>mapRoot 子物体下标 → <c>TaskCfgs</c> 行下标。
    /// <para>▍两套下标不是一回事：行 = <c>OrderedMaps()</c>（按 mapId 排序）。预制体目前恰好同序，
    /// 但地图增删就会错位（静默）⇒ 一律走 <see cref="TaskManager.MapIndex"/>，别再拿子物体下标硬算。</para>
    /// </summary>
    private int TaskRow(int mapChildIndex) => taskManager.MapIndex(mapRoot.GetChild(mapChildIndex).name);

    /// <summary>当前选中地图对应的任务表行（-1 = 取不到）。</summary>
    private int SelectTaskRow => TaskRow(SelectMapIndex);

    /// <summary>
    /// 一次画全「外层地图列表的小任务图标」+「内层地图上的任务标记」。
    /// <para>▍为什么必须一起画：两层原来各有各的触发点（外层只在 <see cref="ShowWnd"/>、内层只在
    /// <see cref="SelectMap"/>）⇒ 任务表重建后一层新一层旧，看起来就是"刷新时内外任务对不上"。</para>
    /// </summary>
    private void RefreshTaskBoard()
    {
        RefreshMapRoot();
        RefreshAreaRoot();
    }

    /// <summary>重画外层：每张地图卡片上的任务图标行；一个可接任务都没有时换成"空"标记。</summary>
    private void RefreshMapRoot()
    {
        for (int i = 0; i < mapRoot.childCount; ++i)
        {
            var icons = mapRoot.GetChild(i, 1, 3);
            int row = TaskRow(i);
            // ⚠ 上限取 min(TaskCount, mapItemInfos.Length)：CreatAllTask 只生成这一段，
            //   越界的格子留着上一张表的旧值（以前靠"只算一次"的 enable 掩码兜，换表就失效）
            int capacity = row < 0 ? 0
                : Mathf.Min(taskManager.TaskCount, taskManager.MapData[mapRoot.GetChild(i).name].mapItemInfos.Length);

            // 先全关：这张表比上一张少几个图标时，不清掉就会残留成"旧任务"
            for (int u = 0; u < icons.childCount; ++u) SetActive(icons.GetChild(u), false);

            int shown = 0;
            for (int u = 0; u < capacity; ++u)
            {
                if (shown >= 5 || shown >= icons.childCount) break;
                var cfg = taskManager.TaskCfgs[row, u];
                if (!cfg.enable) continue;

                var item = icons.GetChild(shown++);
                SetActive(item, true);
                SetColor(item, cfg.Color);
                SetColor(item.GetChild(0), cfg.Color);
                SetSprite(item.GetChild(0), cfg.Sprite);
            }

            SetActive(icons, shown > 0);
            SetActive(mapRoot.GetChild(i, 1, 4), shown == 0);
            SetButtonInteractable(mapRoot.GetChild(i, 1), capacity > 0);
        }
    }

    /// <summary>重画内层：当前地图上的任务标记（位置 / 名字 / 任务图标 / 可用状态）。</summary>
    private void RefreshAreaRoot()
    {
        if (mapState == MapWndState.Map) return;   // 内层没开（外层地图列表在显示）

        var data = taskManager.MapData[mapRoot.GetChild(SelectMapIndex).name];
        var info = data.mapItemInfos;
        int row = SelectTaskRow;
        SelectMapTaskCount = info.Length;
        product = data.product;
        bool visible = mapState != MapWndState.Switch;   // 缩放过渡中先别点亮，等 FinalSwitch

        for (int i = 0; i < areaRoot.GetChild(1).childCount; ++i)
        {
            var marker = areaRoot.GetChild(1, i);
            // 先全关：上一张表的标记更多时，多出来的会残留成"旧任务"
            SetActive(marker, false);
            if (i >= info.Length || row < 0 || i >= taskManager.TaskCount) continue;

            var cfg = taskManager.TaskCfgs[row, i];
            marker.RectTransform().anchoredPosition = info[i].pos;
            SetText(areaRoot.GetChild(1, i, 2), info[i].name);
            SetButtonInteractable(marker, cfg.enable);
            SetActive(areaRoot.GetChild(1, i, 3), cfg.enable);
            SetSprite(areaRoot.GetChild(1, i, 3, 0), cfg.Sprite);
            SetColor(areaRoot.GetChild(1, i, 3), cfg.Color);
            SetColor(areaRoot.GetChild(1, i, 3, 0), cfg.Color);
            SetActive(areaRoot.GetChild(1, i, 4), !cfg.enable);
            SetActive(marker, visible);
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
        SelectMapIndex = index;
        // ⚠ 此刻 mapState 已是 Switch ⇒ 只摆位置、不点亮标记（FinalSwitch 才亮）
        RefreshAreaRoot();
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
        var info = taskManager.TaskCfgs[SelectTaskRow, index];
        SetText(areaInfoType, info.TaskType);
        SetText(areaInfoName, info.name);
        SetText(areaInfoMainTarget, info.TaskDesc);
        SetText(areaInfoExtraTarget,"可选任务* " + info.extra.Length);
#if UNITY_EDITOR
        RefreshExtraTaskIcons(info);
#endif

        AccountReward(index,false);

        //SetText(areaInfoName, GetText(trans.GetChild(2)));
        SetColor(areaInfoIcon, info.Color);
        SetColor(areaInfoIcon.parent, info.Color);
        SetSprite(areaInfoIcon, info.Sprite);
        var mapData = taskManager.MapData[mapRoot.GetChild(SelectMapIndex).name];
        var campData = taskManager.Camps[mapData.mapItemInfos[index].enemyVarietyType];
        SetSprite(areaInfoEnemy, campData.Sprite);
        SetColor(areaInfoEnemy, campData.Color);

        float normScale =Mathf.Clamp01(areaInfoRoot.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime);

        areaInfoRoot.GetComponent<Animator>().Play("Entry", 0, immediately ? 1 : 1- normScale);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 【仅编辑器】把本任务的「可选任务」种类用图标排在「可选任务* N」文字右侧。
    /// <para>▍为什么只在编辑器：#if UNITY_EDITOR ⇒ 打包后这段连同字段一起不存在，
    /// 不会因为预制体里没配引用而在真机刷空引用；它是给策划核对任务表（谁来当可选任务）用的。</para>
    /// <para>▍为什么运行时生成而不是改预制体：可选任务数量随地图大小在 2~5 浮动，
    /// 生成一份再复用比在预制体里预埋 5 个图标（还要各自配引用）省事，也不污染正式 UI。</para>
    /// </summary>
    private void RefreshExtraTaskIcons(in TaskCfg info)
    {
        if (info.extra == null) return;
        var target = areaInfoExtraTarget.RectTransform();

        if (_extraIconRow == null)
        {
            // 挂在文字同一个父物体下：共用「左下角原点」的锚，图标和文字才不会在 Expand 动画里错位
            var go = new GameObject("ExtraTaskIcons", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
            go.layer = target.gameObject.layer;
            _extraIconRow = (RectTransform)go.transform;
            _extraIconRow.SetParent(target.parent, false);
            _extraIconRow.anchorMin = _extraIconRow.anchorMax = target.anchorMin;
            _extraIconRow.pivot = new(0f, target.pivot.y);
            _extraIconRow.sizeDelta = new(0f, target.sizeDelta.y);

            var layout = go.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        int count = Mathf.Max(info.extra.Length, _extraIconRow.childCount);
        for (int i = 0; i < count; ++i)
        {
            if (i >= _extraIconRow.childCount)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                iconGo.layer = target.gameObject.layer;
                var iconRt = (RectTransform)iconGo.transform;
                iconRt.SetParent(_extraIconRow, false);
                iconRt.sizeDelta = new(24f, 24f);
                var img = iconGo.GetComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
            }

            var icon = _extraIconRow.GetChild(i);
            if (i >= info.extra.Length || MissionData_SO.Catalog == null
                || !MissionData_SO.Catalog.TryGetValue(info.extra[i], out var cfg))
            {
                SetActive(icon, false);
                continue;
            }

            SetActive(icon, true);
            SetSprite(icon, cfg.sprite);
            SetColor(icon, Color.white);   // 支线任务 SO 没有 color 字段（只有 MissionMainData_SO 才有）
        }

        // 贴在文字右侧：文字宽度按 TMP 排版结果取，写死偏移会在「可选任务* 2」和「* 5」之间忽宽忽窄
        float textWidth = 0f;
        if (target.TryGetComponent(out TMPro.TMP_Text tmp))
        {
            tmp.ForceMeshUpdate();
            textWidth = Mathf.Min(tmp.preferredWidth, 200f);
        }
        _extraIconRow.anchoredPosition = new(target.anchoredPosition.x + textWidth + 8f, target.anchoredPosition.y + 1f);
        _extraIconRow.sizeDelta = new(info.extra.Length * 30f, _extraIconRow.sizeDelta.y);
    }
#endif

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
        // ★ 已经在别人的房间里 ⇒ 禁止展开任务配置面板（任务/难度/模式都归房主，见 IsRoomMember）
        if (IsRoomMember()) return;
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
            var info = taskManager.TaskCfgs[SelectTaskRow, index];

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
            var info = taskManager.TaskCfgs[SelectTaskRow, index];
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
    /// **选完任务**：确认本局的配置并进入 Ready 阶段（⚠ 这时**还在舰桥**，不是"开局"）。
    /// <para>▍阶段链（用户口径）：Bridge 选任务 →（本方法）Ready → 所有人就位 → Armament（舰桥选战备）
    /// → 全员准备 → Transition（同时加载战斗场景，由大厅家具/动画那条链触发）→ Game。</para>
    /// </summary>
    private void ConfirmTask()
    {
        // ★ 公开房：**建服推迟到这一刻**（2026-10-06 口径）—— 地图界面的「公开」只负责记住模式，
        //   玩家像单人一样调难度 / 选任务 / 选加成，按下「准备」时服务器才**静默**创建。
        //   ⚠ 放在 SetWndState 之前：建服失败（端口刚被占用等）就停在选图界面重试，别先把界面关掉。
        //   ⚠ 也必须在下面那句 host.ConfirmTask(...) 之前：那段广播是靠 NetHostSvc.RoomInfo
        //     判断"我是不是房主"的，房间还不存在就轮不到广播本局配置（成员也就拿不到配置）。
        if (SelectPlayMode == 1 && !IsHosting())
        {
            CreateRoom(true);   // 任务内容随后由 host.ConfirmTask 就地刷新（那才是权威时刻）
            if (!IsHosting()) return;
        }

        SetWndState(false);
        string mapId = mapRoot.GetChild(SelectMapIndex).name;

        // 种子传 0 ⇒ SetTask 内部回落到该任务自己的 taskCfg.seed（可复现），并发布到 TaskState.Seed
        taskManager.SetTaskByIndex(mapId, SelectTaskIndex,(DifficultyEnum)SelectTaskDiff, SelectTaskExtraDiff,SelectPlayMode);

        // 房主：把这一局的配置广播给成员（地图 / 难度 / 额外难度 / 种子 / 模式 / **任务内容**）
        // ⚠ 种子必须与本次 SetTask 用的是同一个 ⇒ 从 TaskState.Seed 读回来（唯一来源，避免两处各算一次）
        // ⚠ 成员侧由 TeamNetBridge.HandleTaskConfirm 承接：**只落配置**（SetSeed + SetTask ⇒ 进 Ready，仍在舰桥）。
        //    真正的"加载战斗场景"由 Transition 那条链驱动（房主广播 TransitionNtf → 成员 GameState=Transition
        //    → 各自大厅的 GameStateController(state:8) → TransSceneController.StartLoad()）。
        var host = NetHostSvc.Instance;
        if (host != null && host.RoomInfo != null)
        {
            int seed = FPSGame.Data.TaskState.Seed;
            // ★ **只发配置内容**（TaskCfg 就 9 个字段，内容即全部）：任务表是各端本地按时间窗口生成的，
            //   跨窗口 / 刚刷过表时同一个下标会指向另一个任务 ⇒ 成员一律按内容复现、不查本地表。
            //   后进者（Ready/Armament 期间入房）由 _lastTaskConfirm 补发同一份内容。
            // ★ 房间广播里的 TaskMain / TaskName 也由它从内容里刷新（建服可能早于选任务）
            host.ConfirmTask(mapId, SelectTaskExtraDiff, seed, SelectPlayMode,
                TaskManager.ToDto(taskManager.nowTask.taskCfg));
        }
    }


    private enum MapWndState
    {
        Map,
        Switch,
        Info,
        SelectTask
    }

}
}
