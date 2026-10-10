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

    private MapWndState mapState;
    private float magnifc;
    private Vector2 primeRootSize, primeRootPos;
    private float nowtime;
    /// <summary>进行中的快速匹配协程（重复点「加入」时先停掉上一条）。</summary>
    private Coroutine _matchRoutine;

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
        RefreshDisplay();

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
    /// </summary>
    private IEnumerator MatchRoutine()
    {
        string mapName = mapRoot.childCount > SelectMapIndex ? mapRoot.GetChild(SelectMapIndex).name : string.Empty;
        int taskMain = (int)taskManager.TaskCfgs[SelectMapIndex, SelectTaskIndex].main;

        var panel = serverPanel;
        if (panel) panel.EnsureDiscovery();   // 面板没打开过 ⇒ 先让发现器在听广播
        if (panel) panel.Scan();

        var tip = WndHub.Tip;
        if (tip) tip.Creat(new() { title = "正在搜索房间", desc = "正在搜索相同任务的房间…" });

        LanRoomInfo room = null;
        float left = MatchSearchSeconds;
        while (left > 0f)
        {
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
        // ⚠ 建服时就把**当前选中的任务枚举**写进广播（CreateRoom 的 taskMain 形参）：不写的话房间广播里
        //   还是上一局的任务，别人按同任务搜房就搜不到这一间（要等按「准备」时 ConfirmTask 才刷新）。
        SelectPlayMode = 1;
        if (!IsHosting()) CreateRoom(true, taskMain);   // ⚠ 已经在开房就别重开：同端口会 SocketException
        ExpandCfg();
    }

    /// <summary>
    /// 【开房】用当前选中的地图 + 任务难度开房并局域网广播。
    /// <para>▍当前唯一调用者是「公开房」流程的 <see cref="ConfirmTask"/>（选完任务才建服）。
    /// 设置界面的空玩家位走的是自己的 <c>SettingWnd.CreateRoomFromEmptySlot</c>（它是常驻窗口，选图窗口不一定在场景里）。</para>
    /// </summary>
    /// <param name="silent">
    /// true = 静默建服：成功不弹提示、不打开「服务器列表」。失败**仍然**弹提示（失败不该静默）。
    /// </param>
    /// <param name="taskMain">本房**主任务类型枚举值**（<c>MissionEnum</c> 的 int；&lt;0 = 用 <c>TaskManager.NowTaskMain</c>）。
    /// <para>▍快速匹配要传它：建服发生在"选完任务"之前，<c>NowTaskMain</c> 还是**上一局**的值
    /// ⇒ 广播出去的任务对不上，别人按同任务搜房就搜不到这一间。</para></param>
    public void CreateRoom(bool silent = false, int taskMain = -1)
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
            // ⚠ 此刻本局任务**还没 SetTask**（「公开房」是先建服再确认任务）⇒ 这里拿到的可能是上一局的值；
            //   真正的权威时刻是下面的 ConfirmTask，它会就地刷新广播里的主任务枚举。
            //   （快速匹配那条路会显式传 taskMain，避开这个"上一局的值"的坑。）
            TaskMain = taskMain >= 0 ? taskMain : taskManager.NowTaskMain,
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
        var campData = taskManager.Camps[mapData.mapItemInfos[index].enemyVarietyType];
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
            CreateRoom(true);
            if (!IsHosting()) return;
        }

        SetWndState(false);
        string mapId = mapRoot.GetChild(SelectMapIndex).name;

        // 种子传 0 ⇒ SetTask 内部回落到该任务自己的 taskCfg.seed（可复现），并发布到 TaskState.Seed
        taskManager.SetTask(mapId, SelectTaskIndex,(DifficultyEnum)SelectTaskDiff, SelectTaskExtraDiff,SelectPlayMode);

        // 房主：把这一局的配置广播给成员（地图 / 难度 / 任务下标 / 额外难度 / 种子 / 模式 / 任务指纹）
        // ⚠ 种子必须与本次 SetTask 用的是同一个 ⇒ 从 TaskState.Seed 读回来（唯一来源，避免两处各算一次）
        // ⚠ 成员侧由 TeamNetBridge.HandleTaskConfirm 承接：**只落配置**（SetSeed + SetTask ⇒ 进 Ready，仍在舰桥）。
        //    真正的"加载战斗场景"由 Transition 那条链驱动（房主广播 TransitionNtf → 成员 GameState=Transition
        //    → 各自大厅的 GameStateController(state:8) → TransSceneController.StartLoad()）。
        var host = NetHostSvc.Instance;
        if (host != null && host.RoomInfo != null)
        {
            int seed = FPSGame.Data.TaskState.Seed;
            int fingerprint = taskManager.TaskFingerprint(mapId, SelectTaskIndex);
            // ★ 连**配置内容**一起下发（不能只发下标）：任务表是各端本地按时间窗口生成的，
            //   跨窗口 / 刚刷过表时同一个下标会指向另一个任务。带上内容后：
            //   ① 后进者（Ready/Armament 期间入房）即使本地表里已经没有这个任务，也能按内容复现；
            //   ② 两端表不同也不再是致命错误（指纹降级为诊断告警，见 TeamNetBridge.HandleTaskConfirm）。
            // ★ 末尾带上主任务枚举：房间可能建在选任务之前（公开房流程）⇒ 由这里刷新广播里的任务类型
            host.ConfirmTask(mapId, SelectTaskIndex, SelectTaskExtraDiff, seed, SelectPlayMode, fingerprint,
                TaskManager.ToDto(taskManager.nowTask.taskCfg), taskManager.NowTaskMain);
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
