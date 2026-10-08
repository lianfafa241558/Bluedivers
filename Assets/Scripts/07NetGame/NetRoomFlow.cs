using System;
using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{
    /// <summary>
    /// 【房间流程编排】成员端"搜到房间 → 回连房主 → 申请入房"这一串动作的**唯一入口**。
    ///
    /// <para>▍它解决什么问题：</para>
    /// <list type="bullet">
    ///   <item>UI 不必自己「先 ConnectToRoom 再在回调里 JoinRoom」——两步的顺序和失败分支容易写错；</item>
    ///   <item>UI 不必自己 <c>MessageCenter.Register</c>（那是**按命令号单处理器**的静态字典，
    ///         谁注册谁生效、容易和别的模块互相顶掉）；</item>
    ///   <item>连接/入房**必须有超时**：房主关机、防火墙丢包时 KCP 的回调可能永远不回来。</item>
    /// </list>
    ///
    /// <para>▍挂在 <c>GameRoot/NetRoot</c> 下（与 <see cref="NetSvc"/> / <see cref="NetHostSvc"/> 同机常驻），
    /// 由 <see cref="Instance"/> 取用；事件在 <c>Awake</c> 注册、<c>OnDestroy</c> 反注册（成对）。</para>
    ///
    /// <para>▍⚠ 不要和 <c>LanRoomDemo</c> / <c>NetDemo</c> 同时挂在场景里：那两个 Demo 也注册了
    /// 同名命令号，后注册的会把本类的处理器顶掉（Demo 只是学习用，不该进正式场景）。</para>
    /// </summary>
    public class NetRoomFlow : MonoBehaviour
    {
        /// <summary>单例（由 <c>GameRoot</c> 下的常驻节点提供）。</summary>
        public static NetRoomFlow Instance;

        /// <summary>入房结果：ok / 失败原因（成功时为空串）。</summary>
        public static event Action<bool, string> OnJoinResult;

        /// <summary>房主广播的玩家列表 + 详细资料（入房成功、有人进出、准备/资料变化时都会来）。
        /// ⚠ <c>profiles</c> 可能为 null（旧版本房主不带资料）⇒ 消费方要判空。</summary>
        public static event Action<PlayerInfo[], PlayerProfile[]> OnPlayerList;

        /// <summary>战备选择同步（房主权威转发后触发，**房主自己也会收到**）。
        /// 参数：sid（权威标识）/ 房主视角下标 / 战备 id / 槽位。⚠ 本地下标要用 sid 去映射。</summary>
        public static event Action<uint, int, int, int> OnArmamentSync;

        /// <summary>全队强化同步（同上，房主自己也收）。参数：sid / 房主视角下标 / 强化 id。</summary>
        public static event Action<uint, int, int> OnBoosterSync;

        /// <summary>局内切枪同步（房主权威转发后触发，**房主自己也会收到**）。参数：sid / 武器槽位。</summary>
        public static event Action<uint, int> OnWeaponSwitch;

        /// <summary>开火表现同步（同上，房主自己也收）。参数：sid / 武器槽位 / **射击方向**（世界空间，零向量 = 未知）
        /// / **目标点**（开枪者准心实指那一点；零向量 = 未知 ⇒ 接收端只用方向）。</summary>
        public static event Action<uint, int, UnityEngine.Vector3, UnityEngine.Vector3> OnShoot;

        /// <summary>【房主侧】收到成员请求"场景 actor 快照"。参数：请求方 sid。
        /// <para>▍为什么要绕一手：快照要从 <c>ActorsManager</c> 里取（05/09 才看得见），而本程序集 <b>不引用</b>玩法层
        /// ⇒ 这里只做"路由 + 编解码"，取数由 09 的桥完成。</para></summary>
        public static event Action<uint> OnSceneActorReq;

        /// <summary>【成员侧】收到场景 actor 快照（房主只发给请求者）。</summary>
        public static event Action<SceneActorEntry[]> OnSceneActors;

        /// <summary>玩家生命状态同步（血/盾/倒地；房主自己也收）。参数：整条 DTO（含权威 Sid）。</summary>
        public static event Action<PlayerVital> OnVital;

        /// <summary>玩家弹药系数同步（房主自己也收）。参数：sid / 剩余弹药比例 0~1。</summary>
        public static event Action<uint, float> OnAmmo;

        /// <summary>【成员侧】房主开播了（时机权威 + 参数）。参数：整条 DTO。</summary>
        public static event Action<WaveStartMsg> OnWaveStart;

        /// <summary>【成员侧】房主下发了某只怪的移动目标（NetId / 目标点）。</summary>
        public static event Action<int, Vector3> OnEnemyMove;

        /// <summary>【房主侧】成员上报打中了某只怪。参数：开枪者 sid / NetId / 伤害。
        /// <para>⚠ 带上 sid 才能把"这一枪"归到那个成员的盟友实例上（受击表现/仇恨要落在他身上）。</para></summary>
        public static event Action<uint, int, int> OnEnemyHitUp;

        /// <summary>【成员侧】房主打中了某只怪。参数：开枪者 sid（房主 = 0）/ NetId / 伤害。
        /// <para>▍为什么要往下播：以前只有"成员 → 房主"，房主打出的伤害**从不下发** ⇒ 两侧副本血量各算各的。</para></summary>
        public static event Action<uint, int, int> OnEnemyDamaged;

        /// <summary>【成员侧】房主说某只怪死了（NetId）。</summary>
        public static event Action<int> OnEnemyDied;

        /// <summary>【成员侧】房主下发场景单位（NPC 等氛围单位）的移动目标 / 停下。
        /// <para>参数：匹配键（<c>Actor.Id</c>）/ 目标点 / 是否就地停下。键用 Id 而非 NetId 的原因见 <c>SceneUnitMoveMsg</c>。</para></summary>
        public static event Action<string, Vector3, bool> OnSceneUnitMove;

        /// <summary>战备呼叫同步（房主权威转发后触发，**房主自己不发**）。
        /// 参数：发起者 sid / 战备 id / 落点 / 信标朝向（Y 轴角度）。⚠ 发起方要靠 sid 丢掉自己那条（本地已经放过一次）。</summary>
        public static event Action<uint, int, Vector3, float> OnAirdropCall;

        /// <summary>某成员离开（房主下发，**战斗期也会来**）。参数：离开者 sid。
        /// <para>各端据此销毁对应盟友实例 / 收起 HUD 那行 —— 名单本身**不重排**（战斗期冻结，见 <see cref="PlayerLeftMsg"/>）。</para></summary>
        public static event Action<uint> OnPlayerLeft;

        /// <summary>喊话同步（房主权威转发后触发）。参数：喊话者 sid / <c>SpeechTypeEnum</c> 的 int。
        /// ⚠ 发起方要靠 sid 丢掉自己那条（本地已经喊过）。</summary>
        public static event Action<uint, int> OnSpeech;

        /// <summary>本机在房主那边的会话 id（入房成功后才有；0 = 未知/没入房）。房主 = 0。</summary>
        public uint SelfSid { get; private set; }

        /// <summary>本局局号（房主在 <see cref="TaskConfirmNtf.MatchId"/> 里下发；0 = 大厅 / 未开局）。
        /// <para>用途：位姿批次带上它即可丢弃上一局的残留包；也用于开局幂等。</para></summary>
        public static int CurrentMatchId { get; private set; }

        /// <summary>房主广播"**本局配置已确认**"（Bridge 选完任务 ⇒ 双方进 Ready，**不加载场景**）。
        /// 成员据此 <c>SetSeed</c> + <c>SetTask</c>；"加载战斗场景"由 <see cref="OnTransition"/> 驱动。</summary>
        public static event Action<TaskConfirmNtf> OnTaskConfirm;

        /// <summary>房主广播"进入 Transition"（Armament 结束，**同时开始加载战斗场景**）。参数 = 本局局号。
        /// <para>⚠ 成员收到后**只需**把 <c>GameState</c> 推到 <c>Transition</c>：加载动作在各自大厅既有的
        /// <c>GameStateController(state:8) → TransSceneController.StartLoad()</c> 链上，自己再调一次会双加载。</para></summary>
        public static event Action<int> OnTransition;

        /// <summary>【成员侧】房主宣告本局结束（结果 / 延迟）⇒ 本地走 BattleManager.EndGame。
        /// <para>⚠ 接收端必须"本局只结束一次"（BattleManager 有门），否则会排两个定时器。</para></summary>
        public static event Action<GameOverMsg> OnGameOver;

        /// <summary>【成员侧】房主宣告开始撤离（撤离点）⇒ 本地触发撤离。</summary>
        public static event Action<EvacuateMsg> OnEvacuate;

        /// <summary>【成员侧】房主下发任务状态 / 进度（Key = MissionBase.netOrder）⇒ 本地直接置位，不自行判定。</summary>
        public static event Action<MissionUpdateMsg> OnMissionUpdate;

        /// <summary>【双向】家具交互（<c>SyncId</c> = 家具跨端稳定键）⇒ 远端重放同一交互。</summary>
        public static event Action<FurnitureOperateMsg> OnFurnitureOperate;

        /// <summary>【双向】标记点位（表现类；目标实体是引用不能过网 ⇒ 只传点）⇒ 远端在自己的字幕/光环上复现。</summary>
        public static event Action<MarkMsg> OnMark;

        /// <summary>【双向】呼叫凯伊（点）⇒ 远端让本机凯伊走到同一点（放信标 / 就近救人）。</summary>
        public static event Action<CallKaiMsg> OnCallKai;

        /// <summary>【成员侧】房主下发的追击波次中心（~2Hz）⇒ 本端只跟随，不自己算"最近玩家"。</summary>
        public static event Action<WaveCenterMsg> OnWaveCenter;

        /// <summary>连接 + 入房的总超时（秒）。超时按失败处理，避免 UI 一直转圈。</summary>
        [InspectorName("入房总超时(秒)")]
        [SerializeField] private float joinTimeout = 10f;

        /// <summary>当前是否在"入房进行中"（防重复点）。</summary>
        public bool IsJoining { get; private set; }

        private float _timeoutLeft;
        private Action<bool, string> _joinCallback;

        private void Awake()
        {
            Instance = this;
            MessageCenter.Register<JoinRoomRsp>(CmdId.JoinRoomRsp, HandleJoinRoomRsp);
            MessageCenter.Register<PlayerListSync>(CmdId.PlayerListSync, HandlePlayerListSync);
            MessageCenter.Register<TaskConfirmNtf>(CmdId.TaskConfirm, HandleTaskConfirmNtf);
            MessageCenter.Register<TransitionNtf>(CmdId.Transition, HandleTransitionNtf);
            // 舰桥准备：房主转发出来的两条（成员收广播、房主收本地自派发，走同一条路）
            MessageCenter.Register<PlayerArmamentSync>(CmdId.PlayerArmamentSync, HandleArmamentSync);
            MessageCenter.Register<PlayerBoosterSync>(CmdId.PlayerBoosterSync, HandleBoosterSync);
            // 局内表现同步（切枪 / 开火 / 生命状态）
            MessageCenter.Register<PlayerWeaponSwitch>(CmdId.PlayerWeaponSwitchSync, HandleWeaponSwitchSync);
            MessageCenter.Register<PlayerShoot>(CmdId.PlayerShootSync, HandleShootSync);
            MessageCenter.Register<PlayerVital>(CmdId.PlayerVitalSync, HandleVitalSync);
            MessageCenter.Register<PlayerAmmo>(CmdId.PlayerAmmoSync, HandleAmmoSync);
            MessageCenter.Register<WaveStartMsg>(CmdId.WaveStartSync, HandleWaveStartSync);
            MessageCenter.Register<EnemyMoveMsg>(CmdId.EnemyMoveSync, HandleEnemyMoveSync);
            MessageCenter.Register<EnemyHitMsg>(CmdId.EnemyHitNtf, HandleEnemyHitNtf);
            MessageCenter.Register<EnemyDiedMsg>(CmdId.EnemyDiedSync, HandleEnemyDiedSync);
            MessageCenter.Register<EnemyDamagedMsg>(CmdId.EnemyDamagedSync, HandleEnemyDamagedSync);
            MessageCenter.Register<SceneUnitMoveMsg>(CmdId.SceneUnitMoveSync, HandleSceneUnitMoveSync);
            MessageCenter.Register<AirdropCallMsg>(CmdId.AirdropCallSync, HandleAirdropCallSync);
            MessageCenter.Register<PlayerLeftMsg>(CmdId.PlayerLeftNtf, HandlePlayerLeftNtf);
            MessageCenter.Register<PlayerSpeechMsg>(CmdId.SpeechSync, HandleSpeechSync);
            // 两条都注册：房主只会收到 Req、成员只会收到 Ntf（另一边永远收不到）⇒ 不必按角色分支，
            // 也避免"注册时角色还没定"（本组件在 Awake 注册，那时可能还没开房/还没连上）
            MessageCenter.Register<SceneActorSync>(CmdId.SceneActorReq, HandleSceneActorReq);
            MessageCenter.Register<SceneActorSync>(CmdId.SceneActorNtf, HandleSceneActorNtf);
            // 局内世界状态（本局结束 / 撤离）：房主广播，成员应用
            MessageCenter.Register<GameOverMsg>(CmdId.GameOverNtf, HandleGameOverNtf);
            MessageCenter.Register<EvacuateMsg>(CmdId.EvacuateNtf, HandleEvacuateNtf);
            MessageCenter.Register<MissionUpdateMsg>(CmdId.MissionUpdateNtf, HandleMissionUpdateNtf);
            MessageCenter.Register<FurnitureOperateMsg>(CmdId.FurnitureOperateNtf, HandleFurnitureOperateNtf);
            MessageCenter.Register<MarkMsg>(CmdId.MarkNtf, HandleMarkNtf);
            MessageCenter.Register<CallKaiMsg>(CmdId.CallKaiNtf, HandleCallKaiNtf);
            MessageCenter.Register<WaveCenterMsg>(CmdId.WaveCenterNtf, HandleWaveCenterNtf);
        }

        private void OnDestroy()
        {
            MessageCenter.Unregister(CmdId.JoinRoomRsp);
            MessageCenter.Unregister(CmdId.PlayerListSync);
            MessageCenter.Unregister(CmdId.TaskConfirm);
            MessageCenter.Unregister(CmdId.Transition);
            MessageCenter.Unregister(CmdId.PlayerArmamentSync);
            MessageCenter.Unregister(CmdId.PlayerBoosterSync);
            MessageCenter.Unregister(CmdId.PlayerWeaponSwitchSync);
            MessageCenter.Unregister(CmdId.PlayerShootSync);
            MessageCenter.Unregister(CmdId.PlayerVitalSync);
            MessageCenter.Unregister(CmdId.PlayerAmmoSync);
            MessageCenter.Unregister(CmdId.WaveStartSync);
            MessageCenter.Unregister(CmdId.EnemyMoveSync);
            MessageCenter.Unregister(CmdId.EnemyHitNtf);
            MessageCenter.Unregister(CmdId.EnemyDiedSync);
            MessageCenter.Unregister(CmdId.EnemyDamagedSync);
            MessageCenter.Unregister(CmdId.SceneUnitMoveSync);
            MessageCenter.Unregister(CmdId.AirdropCallSync);
            MessageCenter.Unregister(CmdId.PlayerLeftNtf);
            MessageCenter.Unregister(CmdId.SpeechSync);
            MessageCenter.Unregister(CmdId.SceneActorReq);
            MessageCenter.Unregister(CmdId.SceneActorNtf);
            MessageCenter.Unregister(CmdId.GameOverNtf);
            MessageCenter.Unregister(CmdId.EvacuateNtf);
            MessageCenter.Unregister(CmdId.MissionUpdateNtf);
            MessageCenter.Unregister(CmdId.FurnitureOperateNtf);
            MessageCenter.Unregister(CmdId.MarkNtf);
            MessageCenter.Unregister(CmdId.CallKaiNtf);
            MessageCenter.Unregister(CmdId.WaveCenterNtf);
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void Update()
        {
            if (!IsJoining) return;

            _timeoutLeft -= Time.unscaledDeltaTime;
            if (_timeoutLeft > 0f) return;

            FinishJoin(false, "入房超时（对端无响应）");
        }

        // ==================== 对外动词 ====================

        /// <summary>
        /// 【回连并申请入房】两段式：先 <see cref="NetSvc.ConnectToRoom"/> 回连房主，
        /// 连上后再发 <see cref="NetSvc.JoinRoom"/>；房主回 <c>JoinRoomRsp</c> 才算成功。
        /// </summary>
        /// <param name="room">搜到的房间（只用到 HostIp / HostPort）</param>
        /// <param name="playerName">玩家名</param>
        /// <param name="password">房间密码（无密码传空串）</param>
        /// <param name="cb">结果回调（可空；同时会广播 <see cref="OnJoinResult"/>）</param>
        public void Join(LanRoomInfo room, string playerName, string password, Action<bool, string> cb = null)
        {
            if (room == null)
            {
                FinishJoin(false, "房间数据为空");
                return;
            }
            if (IsJoining)
            {
                cb?.Invoke(false, "正在连接中，请稍候");
                return;
            }
            if (NetSvc.Instance == null)
            {
                FinishJoin(false, "场景缺少 NetSvc（网络未初始化）");
                return;
            }

            IsJoining = true;
            _timeoutLeft = Mathf.Max(1f, joinTimeout);
            _joinCallback = cb;

            Debug.Log($"[NetRoomFlow] 回连房主 {room.HostIp}:{room.HostPort} …");

            NetSvc.Instance.ConnectToRoom(room, ok =>
            {
                if (!IsJoining) return;                 // 已超时/已取消
                if (!ok)
                {
                    FinishJoin(false, "无法连接到房主（可能已关房 / 防火墙拦截）");
                    return;
                }

                // 连上之后再申请入房，剩下等 JoinRoomRsp（或超时）
                NetSvc.Instance.JoinRoom(playerName, password ?? string.Empty);
            });
        }

        /// <summary>【退出房间】通知房主并断开连接。</summary>
        public void Leave()
        {
            if (NetSvc.Instance != null && NetSvc.Instance.IsConnected) NetSvc.Instance.LeaveRoom();
            NetSvc.Instance?.Disconnect();
            IsJoining = false;
            _joinCallback = null;
            SelfSid = 0;
        }

        // ==================== 上行（房主/成员分流） ====================

        /// <summary>本机是不是房主（开了房）。房主的"上行"= 本地权威处理 + 广播；成员的"上行"= 发给房主。</summary>
        public bool IsHost => NetHostSvc.Instance != null && NetHostSvc.Instance.RoomInfo != null;

        private static bool CanSendToHost => NetSvc.Instance != null && NetSvc.Instance.IsConnected;

        /// <summary>
        /// 【上行】上报本机资料（角色切换 / 战备 / 强化变化后都要重发一次）。
        /// <para>房主 = 写进 <see cref="NetHostSvc.HostProfile"/> 并广播；成员 = 发 <c>PlayerProfileNtf</c> 给房主。</para>
        /// </summary>
        public void SendProfile(PlayerProfile profile)
        {
            if (profile == null) return;

            if (IsHost)
            {
                NetHostSvc.Instance.SetLocalProfile(profile);
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerProfileNtf, new PlayerProfileNtf { Profile = profile }));
        }

        /// <summary>
        /// 【上行】选择战备：房主自己立即本地处理 + 广播，成员发给房主（房主校验索引后再转发）。
        /// </summary>
        public void SendArmament(int playerIndex, int airdropId, int slotIndex)
        {
            if (IsHost)
            {
                // 房主自己：sid = 0（房主的权威标识），本地先应用再广播
                OnArmamentSync?.Invoke(0, playerIndex, airdropId, slotIndex);
                NetHostSvc.Instance.SendToAll(MessageCenter.Pack(CmdId.PlayerArmamentSync, new PlayerArmamentSync
                {
                    PlayerIndex = playerIndex,
                    AirdropId = airdropId,
                    SlotIndex = slotIndex,
                    Sid = 0,
                }));
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerArmamentNtf, new PlayerArmamentNtf
            {
                PlayerIndex = playerIndex,
                AirdropId = airdropId,
                SlotIndex = slotIndex,
                Sid = SelfSid,
            }));
        }

        /// <summary>【上行】选择全队强化（分流规则同 <see cref="SendArmament"/>）。</summary>
        public void SendBooster(int playerIndex, int boosterId)
        {
            if (IsHost)
            {
                OnBoosterSync?.Invoke(0, playerIndex, boosterId);
                NetHostSvc.Instance.SendToAll(MessageCenter.Pack(CmdId.PlayerBoosterSync, new PlayerBoosterSync
                {
                    PlayerIndex = playerIndex,
                    BoosterId = boosterId,
                    Sid = 0,
                }));
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerBoosterNtf, new PlayerBoosterNtf
            {
                PlayerIndex = playerIndex,
                BoosterId = boosterId,
                Sid = SelfSid,
            }));
        }

        /// <summary>【上行】准备状态：房主天然算准备（<c>NetHostSvc</c> 里 IsReady 恒 true）⇒ 不上报。</summary>
        public void SendReady(bool ready)
        {
            if (IsHost) return;
            if (CanSendToHost) NetSvc.Instance.SetReady(ready);
        }

        /// <summary>
        /// 【开房并广播】包一层 <see cref="NetHostSvc.StartHost(HostRoomOptions)"/>，让 UI 只认识本类。
        /// </summary>
        /// <returns>是否开房成功（端口未释放等失败返回 false，并已给出 reason）</returns>
        public bool Host(HostRoomOptions options, out string reason)
        {
            reason = string.Empty;
            if (NetHostSvc.Instance == null)
            {
                reason = "场景缺少 NetHostSvc（网络未初始化）";
                return false;
            }

            try
            {
                NetHostSvc.Instance.StartHost(options);
                return true;
            }
            catch (Exception e)
            {
                // ⚠ 刚关房立刻重开同一个 UDP 端口会抛 SocketException（端口 TIME_WAIT），
                //   NetHostSvc 已把它转成带中文说明的异常，这里只负责"别让它炸到 UI 之外"。
                reason = e.Message;
                return false;
            }
        }

        // ==================== 消息处理 ====================

        private void HandleJoinRoomRsp(JoinRoomRsp rsp)
        {
            if (!IsJoining)
            {
                Debug.LogWarning("[NetRoomFlow] 收到入房响应，但当前没有进行中的入房请求（已忽略）");
                return;
            }

            if (rsp == null)
            {
                FinishJoin(false, "入房响应为空");
                return;
            }

            if (rsp.ErrorCode != 0)
            {
                FinishJoin(false, string.IsNullOrEmpty(rsp.Reason) ? "入房被拒绝" : rsp.Reason);
                return;
            }

            // 记住"我在房主那边的 sid"：后续战备/强化同步要靠它映射本地下标
            SelfSid = rsp.Self != null ? rsp.Self.Sid : 0;
            FinishJoin(true, string.Empty, rsp.Players);
        }

        private void HandlePlayerListSync(PlayerListSync sync)
        {
            OnPlayerList?.Invoke(sync != null ? sync.Players : null,
                                sync != null ? sync.Profiles : null);
        }

        private void HandleArmamentSync(PlayerArmamentSync s)
        {
            if (s != null) OnArmamentSync?.Invoke(s.Sid, s.PlayerIndex, s.AirdropId, s.SlotIndex);
        }

        /// <summary>【上行】局内切枪（分流规则同 <see cref="SendArmament"/>）。</summary>
        public void SendWeaponSwitch(int slotIndex)
        {
            if (IsHost)
            {
                OnWeaponSwitch?.Invoke(0, slotIndex);   // 房主自己：sid = 0
                NetHostSvc.Instance.SendToAll(MessageCenter.Pack(CmdId.PlayerWeaponSwitchSync,
                    new PlayerWeaponSwitch { Sid = 0, SlotIndex = slotIndex }));
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerWeaponSwitchNtf,
                new PlayerWeaponSwitch { Sid = SelfSid, SlotIndex = slotIndex }));
        }

        /// <summary>【上行】开火表现（分流规则同 <see cref="SendArmament"/>）。</summary>
        /// <param name="dir">开火瞬间的射击方向（世界空间）；传零向量 = 接收端用枪口朝向</param>
        /// <param name="hitPoint">**目标点**（开枪者准心实指那一点，世界空间）；零向量 = 未知。
        /// <para>▍为什么要有它：接收端的"表现弹"是本地模拟的，只给方向时落点由本端地形与枪口偏移决定
        /// ⇒ 与开枪者看到的落点不一致（2026-10-07 实测）。带上它，接收端改成"枪口 → 目标点"的方向，
        /// 弹道就穿过同一个点（旧版发送端没有这个字段 ⇒ 全 0 ⇒ 自动退回老行为）。</para></param>
        public void SendShoot(int slotIndex, UnityEngine.Vector3 dir, UnityEngine.Vector3 hitPoint = default)
        {
            if (IsHost)
            {
                OnShoot?.Invoke(0, slotIndex, dir, hitPoint);
                NetHostSvc.Instance.SendToAll(MessageCenter.Pack(CmdId.PlayerShootSync,
                    new PlayerShoot
                    {
                        Sid = 0, SlotIndex = slotIndex,
                        DirX = dir.x, DirY = dir.y, DirZ = dir.z,
                        HitX = hitPoint.x, HitY = hitPoint.y, HitZ = hitPoint.z,
                    }));
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerShootNtf,
                new PlayerShoot
                {
                    Sid = SelfSid, SlotIndex = slotIndex,
                    DirX = dir.x, DirY = dir.y, DirZ = dir.z,
                    HitX = hitPoint.x, HitY = hitPoint.y, HitZ = hitPoint.z,
                }));
        }

        /// <summary>
        /// 【上行】呼叫了一次战备（战备 id + 落点）。分流规则与 <see cref="SendArmament"/> 略有不同：
        /// **本端不派发自己那条**（房主/成员都已经在本地真放过一次了，这只是"告诉别人"的消息）。
        /// </summary>
        /// <param name="airdropId">战备 id（<c>AirdropData_SO.ID</c>）</param>
        /// <param name="point">落点（世界空间，地面点）</param>
        /// <param name="yaw">信标朝向（Y 轴角度）—— 轰炸类战备的炮位/弹道按它摆，必须一起同步（见 <see cref="AirdropCallMsg.Yaw"/>）</param>
        public void SendAirdropCall(int airdropId, UnityEngine.Vector3 point, float yaw = 0f)
        {
            if (airdropId <= 0) return;

            if (IsHost)
            {
                NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.AirdropCallSync,
                    new AirdropCallMsg { Sid = 0, AirdropId = airdropId, X = point.x, Y = point.y, Z = point.z, Yaw = yaw }));
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.AirdropCallNtf,
                new AirdropCallMsg { Sid = SelfSid, AirdropId = airdropId, X = point.x, Y = point.y, Z = point.z, Yaw = yaw }));
        }

        /// <summary>
        /// 【上行】角色喊话（术语：<c>SpeechTypeEnum</c> 的 int）。分流规则同 <see cref="SendAirdropCall"/>：
        /// **本端不派发自己那条**（本地已经喊过了，这只是"告诉别人"）。
        /// </summary>
        public void SendSpeech(int speech)
        {
            if (IsHost)
            {
                NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.SpeechSync,
                    new PlayerSpeechMsg { Sid = 0, Speech = speech }));
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.SpeechNtf,
                new PlayerSpeechMsg { Sid = SelfSid, Speech = speech }));
        }

        // ==================== 场景 actor（NPC/特殊单位）快照 ====================

        /// <summary>【上行】请求一份场景 actor 快照（后加入的一方在**场景就绪**后调用）。</summary>
        public void SendSceneActorReq()
        {
            if (IsHost) return;                  // 房主自己就是数据源
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.SceneActorReq, new SceneActorSync()));
        }

        /// <summary>【房主】把快照回给请求者（只发给他一个人）。</summary>
        public void SendSceneActors(uint sid, SceneActorEntry[] entries)
        {
            if (!IsHost) return;
            NetHostSvc.Instance.SendToSession(sid, MessageCenter.Pack(CmdId.SceneActorNtf,
                new SceneActorSync { Actors = entries ?? new SceneActorEntry[0] }));
        }

        private void HandleSceneActorReq(SceneActorSync _)
        {
            OnSceneActorReq?.Invoke(CurrentSidOfRequest());   // 见下面说明：由 NetHostSvc 在派发前写入当前来源
        }

        /// <summary>请求来源 sid：房主派发时由 <see cref="NetHostSvc"/> 填 <c>CurrentSid</c>（同其它 Ntf 的口径）。</summary>
        private uint CurrentSidOfRequest() => NetHostSvc.Instance != null ? NetHostSvc.Instance.CurrentSid : 0u;

        private void HandleSceneActorNtf(SceneActorSync sync)
        {
            OnSceneActors?.Invoke(sync != null ? sync.Actors : null);
        }

        private void HandleWeaponSwitchSync(PlayerWeaponSwitch s)
        {
            if (s != null) OnWeaponSwitch?.Invoke(s.Sid, s.SlotIndex);
        }

        private void HandleShootSync(PlayerShoot s)
        {
            if (s != null)
                OnShoot?.Invoke(s.Sid, s.SlotIndex,
                    new UnityEngine.Vector3(s.DirX, s.DirY, s.DirZ),
                    new UnityEngine.Vector3(s.HitX, s.HitY, s.HitZ));
        }

        private void HandleAirdropCallSync(AirdropCallMsg m)
        {
            if (m != null) OnAirdropCall?.Invoke(m.Sid, m.AirdropId, new Vector3(m.X, m.Y, m.Z), m.Yaw);
        }

        private void HandlePlayerLeftNtf(PlayerLeftMsg m)
        {
            if (m != null && m.Sid != 0u) OnPlayerLeft?.Invoke(m.Sid);
        }

        private void HandleSpeechSync(PlayerSpeechMsg m)
        {
            if (m != null) OnSpeech?.Invoke(m.Sid, m.Speech);
        }

        /// <summary>【上行】生命状态（分流规则同 <see cref="SendArmament"/>）。</summary>
        public void SendVital(float hp, float hpMax, float shield, float shieldMax, bool down)
        {
            var v = new PlayerVital
            {
                Hp = hp,
                HpMax = hpMax,
                Shield = shield,
                ShieldMax = shieldMax,
                Down = down,
            };

            if (IsHost)
            {
                v.Sid = 0;                       // 房主自己
                OnVital?.Invoke(v);
                NetHostSvc.Instance.SendToAll(MessageCenter.Pack(CmdId.PlayerVitalSync, v));
                return;
            }
            if (!CanSendToHost) return;
            v.Sid = SelfSid;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerVitalNtf, v));
        }

        private void HandleVitalSync(PlayerVital v)
        {
            if (v != null) OnVital?.Invoke(v);
        }

        /// <summary>【上行】弹药系数（分流规则同 <see cref="SendArmament"/>）。</summary>
        public void SendAmmo(float ratio)
        {
            var a = new PlayerAmmo { Ratio = ratio };

            if (IsHost)
            {
                a.Sid = 0;                       // 房主自己
                OnAmmo?.Invoke(0u, ratio);
                NetHostSvc.Instance.SendToAll(MessageCenter.Pack(CmdId.PlayerAmmoSync, a));
                return;
            }
            if (!CanSendToHost) return;
            a.Sid = SelfSid;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerAmmoNtf, a));
        }

        private void HandleAmmoSync(PlayerAmmo a)
        {
            if (a != null) OnAmmo?.Invoke(a.Sid, a.Ratio);
        }

        /// <summary>【房主下行】开波广播（**不做本地自派发**：房主自己那份已经建过了，回环只会重复建）。</summary>
        public void SendWaveStart(WaveStartMsg msg)
        {
            if (!IsHost || msg == null) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.WaveStartSync, msg));
        }

        private void HandleWaveStartSync(WaveStartMsg msg)
        {
            if (msg != null) OnWaveStart?.Invoke(msg);
        }

        /// <summary>【房主】广播某只怪的移动目标（成员各自本地算路径）。</summary>
        public void SendEnemyMove(int netId, Vector3 dest)
        {
            if (!IsHost || netId == 0) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.EnemyMoveSync,
                new EnemyMoveMsg { NetId = netId, X = dest.x, Y = dest.y, Z = dest.z }));
        }

        /// <summary>【成员】上报一次命中，由房主结算。</summary>
        public void SendEnemyHit(int netId, int damage)
        {
            if (IsHost || netId == 0 || damage <= 0) return;
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.EnemyHitNtf,
                new EnemyHitMsg { Sid = SelfSid, NetId = netId, Damage = damage }));
        }

        /// <summary>【房主】广播某只怪死亡。</summary>
        public void SendEnemyDied(int netId)
        {
            if (!IsHost || netId == 0) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.EnemyDiedSync, new EnemyDiedMsg { NetId = netId }));
        }

        /// <summary>【房主】把自己打出的伤害下发给成员（成员扣自己那份副本 ⇒ 血量口径统一在房主）。</summary>
        public void SendEnemyDamaged(int netId, int damage)
        {
            if (!IsHost || netId == 0 || damage <= 0) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.EnemyDamagedSync,
                new EnemyDamagedMsg { Sid = 0, NetId = netId, Damage = damage }));
        }

        private void HandleEnemyMoveSync(EnemyMoveMsg m)
        {
            if (m != null) OnEnemyMove?.Invoke(m.NetId, new Vector3(m.X, m.Y, m.Z));
        }

        private void HandleEnemyHitNtf(EnemyHitMsg m)
        {
            if (m != null) OnEnemyHitUp?.Invoke(m.Sid, m.NetId, m.Damage);
        }

        private void HandleEnemyDamagedSync(EnemyDamagedMsg m)
        {
            if (m != null) OnEnemyDamaged?.Invoke(m.Sid, m.NetId, m.Damage);
        }

        private void HandleEnemyDiedSync(EnemyDiedMsg m)
        {
            if (m != null) OnEnemyDied?.Invoke(m.NetId);
        }

        /// <summary>【房主下行】场景单位（NPC）走向某点 / 就地停下。
        /// <para>⚠ 不做本地自派发：房主自己那份 NPC 本来就在执行了（与开波广播同款口径）。</para></summary>
        public void SendSceneUnitMove(string id, Vector3 destination, bool stop = false)
        {
            if (!IsHost || string.IsNullOrEmpty(id)) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.SceneUnitMoveSync,
                new SceneUnitMoveMsg { Id = id, X = destination.x, Y = destination.y, Z = destination.z, Stop = stop }));
        }

        private void HandleSceneUnitMoveSync(SceneUnitMoveMsg m)
        {
            if (m != null && !string.IsNullOrEmpty(m.Id))
                OnSceneUnitMove?.Invoke(m.Id, new Vector3(m.X, m.Y, m.Z), m.Stop);
        }

        // ==================== 局内世界状态（本局结束 / 撤离） ====================

        /// <summary>【房主】广播"本局结束"（⚠ 一局只广播一次，由 09 侧的桥保证）。</summary>
        public void SendGameOver(int matchId, int delay, int result)
        {
            if (!IsHost) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.GameOverNtf,
                new GameOverMsg { MatchId = matchId, Delay = delay, Result = result }));
        }

        /// <summary>【房主】广播"开始撤离"（⚠ 一局只广播一次，由 09 侧的桥保证）。</summary>
        public void SendEvacuate(float x, float y, float z)
        {
            if (!IsHost) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.EvacuateNtf,
                new EvacuateMsg { X = x, Y = y, Z = z }));
        }

        private void HandleGameOverNtf(GameOverMsg m)
        {
            if (m != null) OnGameOver?.Invoke(m);
        }

        private void HandleEvacuateNtf(EvacuateMsg m)
        {
            if (m != null) OnEvacuate?.Invoke(m);
        }

        /// <summary>【房主】广播某任务的状态 / 进度（去重由 09 侧的桥负责，见 NetMissionBridge.Broadcast）。</summary>
        public void SendMissionUpdate(int key, int state, int progress, int maxProgress, float percentage)
        {
            if (!IsHost) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.MissionUpdateNtf,
                new MissionUpdateMsg
                {
                    Key = key,
                    State = state,
                    Progress = progress,
                    MaxProgress = maxProgress,
                    Percentage = percentage,
                }));
        }

        private void HandleMissionUpdateNtf(MissionUpdateMsg m)
        {
            if (m != null) OnMissionUpdate?.Invoke(m);
        }

        /// <summary>家具交互：成员上报房主 / 房主转发全体（由 09 侧的 NetFurnitureBridge 决定走向）。</summary>
        public void SendFurnitureOperate(int syncId, uint sid)
        {
            var msg = MessageCenter.Pack(CmdId.FurnitureOperateNtf,
                new FurnitureOperateMsg { SyncId = syncId, Sid = sid });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }

        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)
        {
            if (m != null) OnFurnitureOperate?.Invoke(m);
        }

        /// <summary>标记点位：成员上报房主 / 房主转发全体（走向由 09 侧的 NetActionBridge 决定）。</summary>
        public void SendMark(uint sid, float x, float y, float z)
        {
            var msg = MessageCenter.Pack(CmdId.MarkNtf,
                new MarkMsg { Sid = sid, Kind = 0, X = x, Y = y, Z = z });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }

        /// <summary>呼叫凯伊：成员上报房主 / 房主转发全体。</summary>
        public void SendCallKai(uint sid, float x, float y, float z)
        {
            var msg = MessageCenter.Pack(CmdId.CallKaiNtf,
                new CallKaiMsg { Sid = sid, X = x, Y = y, Z = z });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else NetSvc.Instance?.SendMsg(msg);
        }

        private void HandleMarkNtf(MarkMsg m)
        {
            if (m != null) OnMark?.Invoke(m);
        }

        private void HandleCallKaiNtf(CallKaiMsg m)
        {
            if (m != null) OnCallKai?.Invoke(m);
        }

        /// <summary>【房主】下发追击波次的中心点（~2Hz 节流在 09 侧的 WaveManager 里）。</summary>
        public void SendWaveCenter(int waveIndex, Vector3 center)
        {
            if (!IsHost) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.WaveCenterNtf,
                new WaveCenterMsg { WaveIndex = waveIndex, X = center.x, Y = center.y, Z = center.z }));
        }

        private void HandleWaveCenterNtf(WaveCenterMsg m)
        {
            if (m != null) OnWaveCenter?.Invoke(m);
        }

        private void HandleBoosterSync(PlayerBoosterSync s)
        {
            if (s != null) OnBoosterSync?.Invoke(s.Sid, s.PlayerIndex, s.BoosterId);
        }

        private void HandleTaskConfirmNtf(TaskConfirmNtf ntf)
        {
            IsJoining = false;
            CurrentMatchId = ntf != null ? ntf.MatchId : 0;

            // 成员侧「确认本局配置」由 09_Managers 的 TeamNetBridge.HandleTaskConfirm 承接：
            //   幂等（MatchId）→ 校验（地图/任务下标 + 任务指纹）→ SetSeed + SetTask(…, ntf.Seed)
            //   ⇒ 落配置并进 Ready。⚠ **不加载场景**（那由随后的 TransitionNtf / 本机 ArmamentWnd 那条链驱动）。
            // 本类只负责"把事件抛出去"：02_Net 看不见 TaskManager/TeamManager（references 为空）。
            OnTaskConfirm?.Invoke(ntf);
        }

        /// <summary>房主通知"进入 Transition"（Armament 结束，**同时开始加载战斗场景**）。</summary>
        private void HandleTransitionNtf(TransitionNtf ntf)
        {
            OnTransition?.Invoke(ntf != null ? ntf.MatchId : 0);
        }

        private void FinishJoin(bool ok, string reason)
        {
            FinishJoin(ok, reason, null);
        }

        private void FinishJoin(bool ok, string reason, PlayerInfo[] players)
        {
            IsJoining = false;
            _timeoutLeft = 0f;

            var cb = _joinCallback;
            _joinCallback = null;

            if (!ok)
            {
                Debug.LogWarning($"[NetRoomFlow] 入房失败：{reason}");
                cb?.Invoke(false, reason);
                OnJoinResult?.Invoke(false, reason);
                return;
            }

            Debug.Log("[NetRoomFlow] 入房成功");
            cb?.Invoke(true, string.Empty);
            OnPlayerList?.Invoke(players, null);   // 入房响应里还没带资料 ⇒ profiles 传 null
            OnJoinResult?.Invoke(true, string.Empty);
        }
    }
}
