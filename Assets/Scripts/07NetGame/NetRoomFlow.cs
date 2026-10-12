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

        /// <summary>【成员侧】与房主断开（房主退出 / 崩溃 / 网络断）—— 本端已自动清掉房间标记，**这一局退回单机**。
        /// <para>▍消费方**必须**把"依赖房主权威"的开关全部复位，否则会永远卡在"等房主下发"
        /// （2026-10-10 实测：敌人不动 + 每帧刷"本端作废"）。目前三处，**新增同类开关时请一并补进这张清单**：</para>
        /// <list type="bullet">
        ///   <item><c>EnemyController.RemoteDrivenMovement</c>（<c>EnemyNetBridge</c>）—— 敌人恢复本端自主 AI；</item>
        ///   <item><c>MissionBase.RemoteDriven</c>（<c>NetMissionBridge</c>）—— 任务恢复本端判定，否则**这一局打不完**；</item>
        ///   <item><c>NPCWalk.RemoteDriven</c>（<c>SceneUnitMoveSink</c>）—— NPC 恢复各端本地游荡，否则**永远站着不动**。</item>
        /// </list>
        /// <para>另：<c>NetFriendBridge</c> 会销毁**全部**盟友实例（房主没了 ⇒ 别人的位姿/血量再也不会更新，
        /// 留着只会骗人）⇒ 模型与 <c>PlayerWnd</c> 那几行一起收掉，观感就是回到单机。</para></summary>
        public static event Action OnHostLost;

        /// <summary>房主广播的玩家列表 + 详细资料（入房成功、有人进出、准备/资料变化时都会来）。
        /// ⚠ <c>profiles</c> 可能为 null（旧版本房主不带资料）⇒ 消费方要判空。</summary>
        public static event Action<PlayerInfo[], PlayerProfile[]> OnPlayerList;

        /// <summary>战备选择同步（房主权威转发后触发，**房主自己也会收到**）。
        /// 参数：sid（权威标识）/ 房主视角下标 / 战备 id / 槽位。⚠ 本地下标要用 sid 去映射。</summary>
        public static event Action<uint, int, int, int> OnArmamentSync;

        /// <summary>全队强化同步（同上，房主自己也收）。参数：sid / 房主视角下标 / 强化 id。</summary>
        public static event Action<uint, int, int> OnBoosterSync;

        /// <summary>玩家配置（改装）同步：武器改装档位/模组 + 载具改装。
        /// <para>▍入房时每个玩家各来一次，之后**改配置**（SelectRoleWnd / VehicleWnd 关窗）再来一次；
        /// 新人入房时房主会**补发全量**（含已在房间里的其他玩家），所以订阅方按 <c>Sid</c> 覆盖即可。</para></summary>
        public static event Action<PlayerLoadoutMsg> OnLoadoutSync;

        /// <summary>局内切枪同步（房主权威转发后触发，**房主自己也会收到**）。参数：sid / 武器槽位。</summary>
        public static event Action<uint, int> OnWeaponSwitch;

        /// <summary>开火表现同步（同上，房主自己也收）。参数：sid / 武器槽位 / **伤害档位**
        /// （<c>WeaponBaseController.UseDamageIndex</c>：接收端必须用**开枪者**的档位，否则命中特效/表现弹取错配置）
        /// / **射击方向**（世界空间，零向量 = 未知）/ **目标点**（开枪者准心实指那一点；零向量 = 未知 ⇒ 只用方向）
        /// / **发射点下标**（<c>-1</c> = 齐射；多管枪轮流开火，接收端没有那份计数 ⇒ 必须传）。</summary>
        public static event Action<uint, int, int, UnityEngine.Vector3, UnityEngine.Vector3, int> OnShoot;

        /// <summary>【房主侧】收到成员请求"场景 actor 快照"。参数：请求方 sid。
        /// <para>▍为什么要绕一手：快照要从 <c>ActorsManager</c> 里取（05/09 才看得见），而本程序集 <b>不引用</b>玩法层
        /// ⇒ 这里只做"路由 + 编解码"，取数由 09 的桥完成。</para></summary>
        public static event Action<uint> OnSceneActorReq;

        /// <summary>【成员侧】收到场景 actor 快照（房主只发给请求者）。</summary>
        public static event Action<SceneActorEntry[]> OnSceneActors;

        /// <summary>玩家生命状态同步（血/盾/倒地；房主自己也收）。参数：整条 DTO（含权威 Sid）。</summary>
        public static event Action<PlayerVital> OnVital;

        /// <summary>有人把某个玩家扶起来了（<c>PlayerReviveNtf</c>(4050)）。参数：被救者的 sid（房主 = 0）。
        /// <para>▍各端都收：房主收到成员的请求要**转发全体**，成员只应用（sid 必然是自己）。</para></summary>
        public static event Action<uint> OnPlayerRevive;

        /// <summary>玩家弹药系数同步（房主自己也收）。参数：sid / 剩余弹药比例 0~1。</summary>
        public static event Action<uint, float> OnAmmo;

        /// <summary>【成员侧】房主开播了（时机权威 + 参数）。参数：整条 DTO。</summary>
        public static event Action<WaveStartMsg> OnWaveStart;

        /// <summary>【成员侧】房主下发了某只怪的移动目标（NetId / 目标点）。</summary>
        public static event Action<int, Vector3> OnEnemyMove;

        /// <summary>【房主侧】成员上报"打中了 / 打死了"（统一的 <c>DamageMsg</c>；方向由到达的 cmdId 决定）。
        /// <para>⚠ 带上 <c>Sid</c> 才能把"这一枪"归到那个成员的盟友实例上（受击表现/仇恨要落在他身上）。</para></summary>
        public static event Action<DamageMsg> OnEnemyHitUp;

        /// <summary>【成员侧】房主下发"打中了 / 打死了"（统一的 <c>DamageMsg</c>；<c>IsDeath</c> = 这一发打死了）。
        /// <para>▍为什么要往下播：以前只有"成员 → 房主"，房主打出的伤害**从不下发** ⇒ 两侧副本血量各算各的。
        /// ▍死亡也走这条（原来单开 4026）：死亡就是"被打到 0 血"的特例。</para></summary>
        public static event Action<DamageMsg> OnEnemyDamaged;

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

        /// <summary>【全体加载完成 ⇒ 一起开打】房主广播后触发（房主自己也收）。参数 = 本局局号。
        /// <para>▍与 <see cref="OnTransition"/> 的区别：那条是"开始加载"（各端加载快慢差很多），
        /// 这条才是"都加载完了"，也就是各端真正开打的时刻（见 <see cref="BattleStarted"/>）。</para></summary>
        public static event Action<int> OnBattleStart;

        /// <summary>【成员侧】房主宣告本局结束（结果 / 延迟）⇒ 本地走 BattleManager.EndGame。
        /// <para>⚠ 接收端必须"本局只结束一次"（BattleManager 有门），否则会排两个定时器。</para></summary>
        public static event Action<GameOverMsg> OnGameOver;

        /// <summary>【成员侧】房主宣告开始撤离（撤离点）⇒ 本地触发撤离。</summary>
        public static event Action<EvacuateMsg> OnEvacuate;

        /// <summary>【成员侧】房主下发任务状态 / 进度（Key = MissionBase.netOrder）⇒ 本地直接置位，不自行判定。</summary>
        public static event Action<MissionUpdateMsg> OnMissionUpdate;

        /// <summary>【双向】家具交互（<c>SyncId</c> = 家具跨端稳定键）⇒ 远端重放同一交互。</summary>
        public static event Action<FurnitureOperateMsg> OnFurnitureOperate;

        /// <summary>【双向】场景可破坏物（油桶这类）被打掉 ⇒ 远端把同一件也打掉（键 = 跨端稳定 SyncId）。
        /// <para>▍只传"死了"、不传伤害：这类物件没有血量口径要一致（打爆才是唯一有意义的状态），
        /// 少一条伤害通道就少一处两端不一致。</para></summary>
        public static event Action<DamageMsg> OnSceneDestructible;

        /// <summary>【双向】标记点位（表现类；目标实体是引用不能过网 ⇒ 只传点）⇒ 远端在自己的字幕/光环上复现。</summary>
        public static event Action<MarkMsg> OnMark;

        /// <summary>【双向】呼叫凯伊（点）⇒ 远端让本机凯伊走到同一点（放信标 / 就近救人）。</summary>
        public static event Action<CallKaiMsg> OnCallKai;

        /// <summary>【成员侧】房主下发的追击波次中心（~2Hz）⇒ 本端只跟随，不自己算"最近玩家"。</summary>
        public static event Action<WaveCenterMsg> OnWaveCenter;

        /// <summary>【双向】按键终端(KeyScreen)阶段推进（键 = 终端家具 SyncId）⇒ 其余端只应用、不自行推进。</summary>
        public static event Action<KeyScreenStageMsg> OnKeyScreenStage;

        /// <summary>【成员侧】房主广播的"实体生成"（原巡逻队 4047 + 运行时生成 4051 合并，见 <c>HostSpawnMsg</c>）⇒
        /// 本端按 <c>Kind</c> 复刻：巡逻队（生成点 + 逐单位目标点）/ 单只单位（来源键 + 槽位 ⇒ 生成器自己复刻）。</summary>
        public static event Action<HostSpawnMsg> OnHostSpawn;

        /// <summary>【双向】容器类家具的一次提交（个数 / 类型）⇒ 远端在盟友幽灵背包上重放。</summary>
        public static event Action<OOPartSubmitMsg> OnOOPartSubmit;

        /// <summary>【双向】玩家对终端(KeyScreen)的一次输入 ⇒ 非驱动端复现。</summary>
        public static event Action<KeyScreenInputMsg> OnKeyScreenInput;

        /// <summary>连接 + 入房的总超时（秒）。超时按失败处理，避免 UI 一直转圈。</summary>
        [InspectorName("入房总超时(秒)")]
        [SerializeField] private float joinTimeout = 10f;

        /// <summary>当前是否在"入房进行中"（防重复点）。</summary>
        public bool IsJoining { get; private set; }

        /// <summary>本机当前所在的那个房间（我作为**成员**回连成功的那一间）；不在别人的房里 = null。
        /// <para>▍用途：房间列表里判"我要加入的这间我已经在里面了"（见 <see cref="IsInRoom"/>）——
        /// 不然会对着自己所在的房再回连一次。</para></summary>
        public LanRoomInfo CurrentRoom { get; private set; }

        /// <summary>
        /// 本机玩家在**房主视角名单**里的序号（房主 = 0；成员 = 自己在房主名单里的下标；名单还没到位 = -1）。
        ///
        /// <para>▍为什么不能用 <c>TeamManager.SelfIndex</c>：本地名单把"自己"固定放在 <c>players[0]</c>
        /// （自己视角，见 <c>TeamNetBridge.HandlePlayerList</c> 的 ①）⇒ 它**恒为 0**，
        /// 拿它做"按序号错开"等于不错开（大厅出生点/准备点都要靠这个序号摊开，2026-10-10 实测）。</para>
        /// </summary>
        public int SelfHostIndex { get; private set; } = -1;

        /// <summary>本机是不是在一次**联机房间会话**里（房主，或已入房的成员）。单机 = false。
        /// <para>▍用途：开局加载闸门（<c>BattleManager.WaitAllPlayersLoaded</c>）—— 单机没人可等，直接开打。</para></summary>
        public bool InRoom
        {
            get
            {
                if (IsHost) return true;
                return NetSvc.Instance != null && NetSvc.Instance.IsConnected;
            }
        }

        /// <summary>本局是否已经"全体加载完成 ⇒ 一起开打"。
        /// <para>▍房主看权威位（<c>NetHostSvc.IsBattleStarted</c>，他收齐票才置位）；成员看广播位
        /// （收到 <c>BattleStartSync</c> 才翻）。两者由同一条广播驱动，判据只有一处。</para></summary>
        public bool BattleStarted => IsHost
            ? NetHostSvc.Instance != null && NetHostSvc.Instance.IsBattleStarted
            : _battleStarted;

        private float _timeoutLeft;
        private Action<bool, string> _joinCallback;
        /// <summary>正在回连的那间房（连上并成功入房后转正到 <see cref="CurrentRoom"/>）。</summary>
        private LanRoomInfo _pendingRoom;
        /// <summary>【成员位】收到房主"一起开打"广播即置位（新一局由 <see cref="HandleTaskConfirmNtf"/> 复位）。</summary>
        private bool _battleStarted;

        /// <summary>【成员位】上一帧"我在房间里"（见 <see cref="DetectHostLost"/>）：断线时靠它区分
        /// "房主掉了" 与 "本来就没入房 / 已主动退房"。</summary>
        private bool _wasInRoom;

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
            // 玩家配置（改装）同步：房主 -> 全体（房主自己那条也是本地自派发）
            MessageCenter.Register<PlayerLoadoutMsg>(CmdId.PlayerLoadoutSync, HandleLoadoutSync);
            // 局内表现同步（切枪 / 开火 / 生命状态）
            MessageCenter.Register<PlayerWeaponSwitch>(CmdId.PlayerWeaponSwitchSync, HandleWeaponSwitchSync);
            MessageCenter.Register<PlayerShoot>(CmdId.PlayerShootSync, HandleShootSync);
            MessageCenter.Register<PlayerVital>(CmdId.PlayerVitalSync, HandleVitalSync);
            MessageCenter.Register<PlayerReviveMsg>(CmdId.PlayerReviveNtf, HandlePlayerReviveNtf);
            MessageCenter.Register<PlayerAmmo>(CmdId.PlayerAmmoSync, HandleAmmoSync);
            MessageCenter.Register<WaveStartMsg>(CmdId.WaveStartSync, HandleWaveStartSync);
            MessageCenter.Register<EnemyMoveMsg>(CmdId.EnemyMoveSync, HandleEnemyMoveSync);
            // 伤害 / 生死族合并成一条 DTO（2026-10-12）：同一类挂两个号，**方向由到达的号决定**
            MessageCenter.Register<DamageMsg>(CmdId.EnemyHitNtf, HandleDamageUp);          // 成员 → 房主
            MessageCenter.Register<DamageMsg>(CmdId.EnemyDamagedSync, HandleDamageDown);   // 房主 → 全体（含 IsDeath）
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
            // 开局加载闸门：房主"全体加载完成 ⇒ 一起开打"（成员收广播、房主收本地自派发，走同一条路）
            MessageCenter.Register<BattleStartMsg>(CmdId.BattleStartSync, HandleBattleStartSync);
            // 场景可破坏物（油桶这类）被打掉 ⇒ 远端把同一件也打掉（双向一条；DTO 与伤害族共用，键走 DamageMsg.Key）
            MessageCenter.Register<DamageMsg>(CmdId.SceneDestructibleNtf, HandleSceneDestructibleNtf);
            // 按键终端阶段推进（驱动端上报 / 房主转发全体）
            MessageCenter.Register<KeyScreenStageMsg>(CmdId.KeyScreenStageNtf, HandleKeyScreenStageNtf);
            // 房主生成族（原 4047 巡逻队 + 4051 运行时生成合并）：房主唯一生成 + 成员按 Kind 复刻
            MessageCenter.Register<HostSpawnMsg>(CmdId.HostSpawnNtf, HandleHostSpawnNtf);
            MessageCenter.Register<OOPartSubmitMsg>(CmdId.OOPartSubmitNtf, HandleOOPartSubmitNtf);
            MessageCenter.Register<KeyScreenInputMsg>(CmdId.KeyScreenInputNtf, HandleKeyScreenInputNtf);
        }

        private void OnDestroy()
        {
            MessageCenter.Unregister(CmdId.JoinRoomRsp);
            MessageCenter.Unregister(CmdId.PlayerListSync);
            MessageCenter.Unregister(CmdId.TaskConfirm);
            MessageCenter.Unregister(CmdId.Transition);
            MessageCenter.Unregister(CmdId.PlayerArmamentSync);
            MessageCenter.Unregister(CmdId.PlayerBoosterSync);
            MessageCenter.Unregister(CmdId.PlayerLoadoutSync);
            MessageCenter.Unregister(CmdId.PlayerWeaponSwitchSync);
            MessageCenter.Unregister(CmdId.PlayerShootSync);
            MessageCenter.Unregister(CmdId.PlayerVitalSync);
            MessageCenter.Unregister(CmdId.PlayerReviveNtf);
            MessageCenter.Unregister(CmdId.PlayerAmmoSync);
            MessageCenter.Unregister(CmdId.WaveStartSync);
            MessageCenter.Unregister(CmdId.EnemyMoveSync);
            MessageCenter.Unregister(CmdId.EnemyHitNtf);
            MessageCenter.Unregister(CmdId.EnemyDamagedSync);
            // ⚠ CmdId.EnemyDiedSync(4026) 已并入 4040（DamageMsg.IsDeath）⇒ 不再注册（常量保留，不外发）
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
            MessageCenter.Unregister(CmdId.BattleStartSync);
            MessageCenter.Unregister(CmdId.SceneDestructibleNtf);
            MessageCenter.Unregister(CmdId.KeyScreenStageNtf);
            MessageCenter.Unregister(CmdId.HostSpawnNtf);
            // ⚠ CmdId.PatrolSpawnNtf(4047) / EnemySpawnNtf(4051) 已并入 HostSpawnNtf(4052) ⇒ 不再注册/外发
            MessageCenter.Unregister(CmdId.OOPartSubmitNtf);
            MessageCenter.Unregister(CmdId.KeyScreenInputNtf);
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        private void Update()
        {
            DetectHostLost();

            if (!IsJoining) return;

            _timeoutLeft -= Time.unscaledDeltaTime;
            if (_timeoutLeft > 0f) return;

            FinishJoin(false, "入房超时（对端无响应）");
        }

        /// <summary>
        /// 【成员侧】"房主掉了"要能**自己发现**。
        ///
        /// <para>▍背景（2026-10-10 用户实测）：房主因为报错退出后，客机这边**没有任何人告诉它** —— 房间标记还在
        /// （<c>CurrentRoom</c> 非空 ⇒ 房间列表还显示"已在房间"）、<c>EnemyController.RemoteDrivenMovement</c>
        /// 还是 true ⇒ 敌人永远等房主下发，而本端 AI 每次决策都被作废并刷日志。</para>
        ///
        /// <para>▍判据：传输层连接已断，且本端**本来是在房间里**（上一帧 <c>CurrentRoom != null</c>）。
        /// 只判连接会误伤"还没入房"与"主动退房"两种正常状态（那时连接本来就是断的）。</para>
        ///
        /// <para>▍动作：清房间标记 + 广播 <see cref="OnHostLost"/>（由 09 的桥恢复本端自主）。</para>
        /// </summary>
        private void DetectHostLost()
        {
            var svc = NetSvc.Instance;
            if (svc == null) return;

            if (IsHost) { _wasInRoom = true; return; }      // 房主自己没有"房主掉了"这回事

            if (svc.IsConnected) { _wasInRoom = CurrentRoom != null; return; }
            if (!_wasInRoom) return;                        // 本来就没连着（没入过房 / 已主动退房）

            _wasInRoom = false;
            Debug.LogWarning("[NetRoomFlow] ⚠ 与房主断开（房主退出/崩溃/断网）⇒ 本端退出房间，改为本端自主");
            CurrentRoom = null;
            SelfSid = 0u;
            SelfHostIndex = -1;
            _battleStarted = false;
            OnHostLost?.Invoke();
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
            _pendingRoom = room;

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
            _pendingRoom = null;
            CurrentRoom = null;      // 已经不在任何房间里了（房间列表据此判"我是不是已经在里面"）
            _battleStarted = false;
            SelfHostIndex = -1;      // 退房：序号作废（下次入房由名单重算）
            SelfSid = 0;
            // ★ 必须清：DetectHostLost 靠它区分"房主掉了"与"我主动退房"，而 Disconnect 是**同步**把
            //   IsConnected 置 false 的 ⇒ 不清的话下一帧就按"房主掉了"走一遍（误报 + 白跑一次 OnHostLost 的收尾）。
            _wasInRoom = false;
        }

        // ==================== 开局加载闸门（等所有人都加载完再一起开打，2026-10-10） ====================

        /// <summary>我是不是这个房间的**房主**（= 广播里带着本机的合成房主名）。
        /// <para>▍判据取 <c>NetHostSvc.SelfHostName</c>（<c>PlayerNames[0]</c> 那个运行时合成名），
        /// 那是"排除自己开的房"的既有约定 —— 比 IP/端口稳（本机房间的 HostIp 可能是 127.0.0.1 / 网卡地址之一）。</para></summary>
        public bool IsHostOfRoom(LanRoomInfo room)
        {
            var host = NetHostSvc.Instance;
            if (host == null || host.RoomInfo == null || room == null) return false;
            if (room.HostPort != host.RoomInfo.HostPort) return false;

            string self = host.SelfHostName;
            var names = room.PlayerNames;
            if (string.IsNullOrEmpty(self) || names == null) return false;
            for (int i = 0; i < names.Length; ++i)
            {
                if (names[i] == self) return true;
            }
            return false;
        }

        /// <summary>
        /// 我是不是**已经在这个房间里了**（自己开的房，或我已经连着的就是这间）。
        /// <para>▍用途：房间列表里点"加入"时先判它 ⇒ 只提示"已经在里面了"，不再回连一次
        /// （对已连着的房主再 <c>ConnectToRoom</c> 会把现有会话顶掉）。</para>
        /// </summary>
        public bool IsInRoom(LanRoomInfo room)
        {
            if (room == null) return false;
            if (IsHostOfRoom(room)) return true;

            var cur = CurrentRoom;
            return cur != null && cur.HostPort == room.HostPort && cur.HostIp == room.HostIp;
        }

        /// <summary>
        /// 【成员】上报"本机战斗场景加载完成"。房主收齐所有人（或超时）后广播"一起开打"。
        /// <para>调用点：<c>BattleManager.WaitAllPlayersLoaded</c>（开局闸门）。</para>
        /// </summary>
        public void SendLoaded()
        {
            if (IsHost) return;
            if (NetSvc.Instance == null || !NetSvc.Instance.IsConnected) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.LoadCompleteNtf, new LoadCompleteMsg { MatchId = CurrentMatchId }));
        }

        /// <summary>【房主】本机战斗场景加载完成 ⇒ 记一票（人到齐就广播，见 <c>NetHostSvc.MarkLocalLoaded</c>）。</summary>
        public void HostLoaded()
        {
            NetHostSvc.Instance?.MarkLocalLoaded();
        }

        // ==================== 上行（房主/成员分流） ====================

        /// <summary>本机是不是房主（开了房）。房主的"上行"= 本地权威处理 + 广播；成员的"上行"= 发给房主。</summary>
        public bool IsHost => NetHostSvc.Instance != null && NetHostSvc.Instance.RoomInfo != null;

        /// <summary>成员侧"能不能发给房主"：单机 / 未连接 ⇒ false。
        /// <para>▍⚠ "上行"的 send 一律用它收口，**别写 <c>NetSvc.Instance?.SendMsg(msg)</c> 直发**：
        /// <c>SendMsg</c> 在没连接时会 <c>Debug.LogError("服务器未连接")</c>，而单机下这些动作（标记 / 终端 /
        /// 家具 / 呼叫凯伊…）本来就该留在本地 ⇒ 直发会让单机玩家每次点击都刷一条像 Bug 的报错
        /// （2026-10-12 用户实测：单机单击标记点即刷）。真正的"与房主断开"由 <c>DetectHostLost</c> 统一告警，不靠这条。</para></summary>
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

        /// <summary>
        /// 【上行】上报本机**配置**（武器改装档位/模组 + 载具改装）。
        /// <para>▍何时调：① 入房成功各报一次（见 <c>TeamNetBridge.HandleJoinResult</c>）；
        /// ② <c>SelectRoleWnd</c> / <c>VehicleWnd</c> 关窗、且确实改过时再报一次。</para>
        /// <para>▍房主分流与其它 Ntf 一致：**本地权威 + 广播给全体**（不单发自己，避免双份）；
        /// 成员则发给房主，由房主校验 sid 后统一广播（含补发给新人）。</para>
        /// </summary>
        public void SendLoadout(PlayerLoadoutMsg msg)
        {
            if (msg == null) return;

            if (IsHost)
            {
                NetHostSvc.Instance?.SetLocalLoadout(msg);   // SetLocalLoadout 内部：落表 + 广播 + 本地自派发
                return;
            }
            if (!CanSendToHost) return;

            msg.Sid = SelfSid;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerLoadoutNtf, msg));
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
            // ★ 先算"我在房主名单里的序号"，再抛事件：订阅方（大厅角色管理）要靠它决定"我站哪一位"
            UpdateSelfHostIndex(sync != null ? sync.Players : null);
            OnPlayerList?.Invoke(sync != null ? sync.Players : null,
                                sync != null ? sync.Profiles : null);
        }

        /// <summary>从房主下发的名单里算出"我在房主视角的序号"（房主自己恒 0；找不到 = -1）。</summary>
        private void UpdateSelfHostIndex(PlayerInfo[] players)
        {
            if (IsHost) { SelfHostIndex = 0; return; }   // 房主视角里自己必然是 0

            uint self = SelfSid;
            if (players == null || players.Length == 0 || self == 0u) { SelfHostIndex = -1; return; }

            for (int i = 0; i < players.Length; ++i)
            {
                if (players[i] != null && players[i].Sid == self) { SelfHostIndex = i; return; }
            }
            SelfHostIndex = -1;
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
        /// <param name="damageIndex">开枪那把枪的**伤害档位**（<c>WeaponBaseController.UseDamageIndex</c>）：
        /// 接收端要按它取"命中特效/表现弹"（信号枪 0=标记 / 1=呼叫战备，不同步会放错特效，见 <see cref="PlayerShoot.DamageIndex"/>）。</param>
        /// <param name="muzzleIndex">开枪用的是**第几个发射点**（<c>-1</c> = 齐射；见 <see cref="PlayerShoot.MuzzleIndex"/>）。
        /// <para>▍为什么必须有：多管枪是轮流开火（<c>Muzzles[shootCount++ % N]</c>），而接收端那把武器从不本机开火
        /// ⇒ 它的计数永远是 0 ⇒ 弹道全从 0 号管出（2026-10-11 修）。</para></param>
        public void SendShoot(int slotIndex, UnityEngine.Vector3 dir, UnityEngine.Vector3 hitPoint = default, int damageIndex = 0,
            int muzzleIndex = 0)
        {
            if (IsHost)
            {
                OnShoot?.Invoke(0, slotIndex, damageIndex, dir, hitPoint, muzzleIndex);
                NetHostSvc.Instance.SendToAll(MessageCenter.Pack(CmdId.PlayerShootSync,
                    new PlayerShoot
                    {
                        Sid = 0, SlotIndex = slotIndex, DamageIndex = damageIndex, MuzzleIndex = muzzleIndex,
                        DirX = dir.x, DirY = dir.y, DirZ = dir.z,
                        HitX = hitPoint.x, HitY = hitPoint.y, HitZ = hitPoint.z,
                    }));
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerShootNtf,
                new PlayerShoot
                {
                    Sid = SelfSid, SlotIndex = slotIndex, DamageIndex = damageIndex, MuzzleIndex = muzzleIndex,
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
                OnShoot?.Invoke(s.Sid, s.SlotIndex, s.DamageIndex,
                    new UnityEngine.Vector3(s.DirX, s.DirY, s.DirZ),
                    new UnityEngine.Vector3(s.HitX, s.HitY, s.HitZ),
                    s.MuzzleIndex);
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

        /// <summary>【双向】把"某人被扶起来了"送出去（房主 = 广播给成员；成员 = 发给房主）。
        /// <para>⚠ 房主分支**不做本地自派发**（与 <see cref="SendWaveStart"/> 同款）：本地应用由桥在转发之后自己做。</para>
        /// <para>⚠ <paramref name="sid"/> 允许为 0（房主自己也会被救）⇒ 不能像别处那样当"无效值"挡掉。</para></summary>
        public void SendPlayerRevive(uint sid)
        {
            var msg = MessageCenter.Pack(CmdId.PlayerReviveNtf, new PlayerReviveMsg { Sid = sid });

            if (IsHost)
            {
                NetHostSvc.Instance?.SendToAll(msg);
                return;
            }
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(msg);
        }

        private void HandlePlayerReviveNtf(PlayerReviveMsg m)
        {
            if (m != null) OnPlayerRevive?.Invoke(m.Sid);
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
        /// <param name="attackerNetId">攻击者单位的 NetId（0 = 未知/玩家 ⇒ 房主退回按 sid 找盟友实例）</param>
        /// <param name="groups">原始伤害成分（摊平 `[count,type,coeff,...]`；null = 中性）⇒ 对端补护甲破坏/异常状态积蓄</param>
        public void SendEnemyHit(int netId, int damage, int attackerNetId = 0, float[] groups = null, int part = 0)
        {
            if (IsHost || netId == 0 || damage <= 0) return;
            if (!CanSendToHost) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.EnemyHitNtf,
                new DamageMsg { Sid = SelfSid, NetId = netId, Damage = damage, AttackerNetId = attackerNetId, Groups = groups, Part = part }));
        }

        /// <summary>【房主】广播某只怪死亡 —— 死亡已并入伤害族（<c>DamageMsg.IsDeath</c>，走 4040），不再单开 4026。
        /// <para>⚠ 与"这一发把它打到 0"的区别：这里是**只报死亡**（<c>Damage = 0</c>），用于"本地判定死亡但没走伤害事件"的场合
        /// （如 <c>EnemyNetBridge.OnLocalDeath</c> 收到 <c>UnitEventBus</c> 的死亡事件）。</para></summary>
        public void SendEnemyDied(int netId)
        {
            if (!IsHost || netId == 0) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.EnemyDamagedSync, new DamageMsg { NetId = netId, IsDeath = true }));
        }

        /// <summary>【房主】把伤害下发给成员（成员扣自己那份副本 ⇒ 血量口径统一在房主）。
        /// <para>▍<paramref name="exceptSid"/> 非 0 = 这是**转发某个成员上报的伤害**，要排除来源端本身
        /// （它本地已经扣过一次，发回去就是双扣）；同时把权威 `<c>Sid</c>` 填成**开枪者**（原来是恒 0 = 房主），
        /// 这样接收端能把"这一发"归到开枪者那个单位上（受击表现/仇恨）。</para></summary>
        public void SendEnemyDamaged(int netId, int damage, uint exceptSid = 0u, int attackerNetId = 0, float[] groups = null, int part = 0)
        {
            if (!IsHost || netId == 0 || damage <= 0) return;
            var msg = MessageCenter.Pack(CmdId.EnemyDamagedSync,
                new DamageMsg { Sid = exceptSid, NetId = netId, Damage = damage, AttackerNetId = attackerNetId, Groups = groups, Part = part });

            if (exceptSid != 0u) NetHostSvc.Instance?.SendToAllExcept(exceptSid, msg);
            else NetHostSvc.Instance?.SendToAll(msg);
        }

        private void HandleEnemyMoveSync(EnemyMoveMsg m)
        {
            if (m != null) OnEnemyMove?.Invoke(m.NetId, new Vector3(m.X, m.Y, m.Z));
        }

        /// <summary>成员上报（4025）⇒ 交给房主侧的桥结算 / 转发。⚠ 死亡也走这条（<c>IsDeath</c>）。</summary>
        private void HandleDamageUp(DamageMsg m)
        {
            if (m != null) OnEnemyHitUp?.Invoke(m);
        }

        /// <summary>房主下发（4040）⇒ 成员按数扣自己那份副本（<c>IsDeath</c> 的话直接干掉）。</summary>
        private void HandleDamageDown(DamageMsg m)
        {
            if (m != null) OnEnemyDamaged?.Invoke(m);
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

        /// <summary>家具交互：成员上报房主 / 房主转发全体（由 09 侧的 NetFurnitureBridge 决定走向）。
        /// <para>▍<paramref name="stateKey"/> 非空 = 这是一条**状态**同步（不是交互重放）：接收端走
        /// <c>Furniture_Attached.ApplyRemoteState</c> 而不是 <c>Operate()</c> —— 用于"改了状态但没人交互"
        /// （如任务把管道打成 <c>PipeError</c>）。<paramref name="state"/> 只是编码，接收端可用 Key 自行解析。</para></summary>
        public void SendFurnitureOperate(int syncId, uint sid, int state = 0, string stateKey = null)
        {
            var msg = MessageCenter.Pack(CmdId.FurnitureOperateNtf,
                new FurnitureOperateMsg { SyncId = syncId, Sid = sid, State = state, StateKey = stateKey });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else if (CanSendToHost) NetSvc.Instance.SendMsg(msg);
        }

        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)
        {
            if (m != null) OnFurnitureOperate?.Invoke(m);
        }

        /// <summary>终端阶段推进：驱动端上报 / 房主转发全体（走向由 09 侧的 NetKeyScreenBridge 决定）。</summary>
        public void SendKeyScreenStage(int syncId, uint sid, int stage)
        {
            var msg = MessageCenter.Pack(CmdId.KeyScreenStageNtf,
                new KeyScreenStageMsg { SyncId = syncId, Sid = sid, Stage = stage });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else if (CanSendToHost) NetSvc.Instance.SendMsg(msg);
        }

        private void HandleKeyScreenStageNtf(KeyScreenStageMsg m)
        {
            if (m != null) OnKeyScreenStage?.Invoke(m);
        }

        /// <summary>房主生成族（巡逻队 / 运行时生成）：**只有房主发**（生成时机与号都由他定）。</summary>
        public void SendHostSpawn(HostSpawnMsg msg)
        {
            if (!IsHost || msg == null) return;
            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.HostSpawnNtf, msg));
        }

        private void HandleHostSpawnNtf(HostSpawnMsg m)
        {
            if (m != null) OnHostSpawn?.Invoke(m);
        }

        /// <summary>容器类家具提交：成员上报房主 / 房主转发全体（走向由 09 侧的 NetFurnitureBridge 决定）。</summary>
        public void SendOOPartSubmit(int syncId, uint sid, int count, int type)
        {
            var msg = MessageCenter.Pack(CmdId.OOPartSubmitNtf,
                new OOPartSubmitMsg { SyncId = syncId, Sid = sid, Count = count, Type = type });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else if (CanSendToHost) NetSvc.Instance.SendMsg(msg);
        }

        private void HandleOOPartSubmitNtf(OOPartSubmitMsg m)
        {
            if (m != null) OnOOPartSubmit?.Invoke(m);
        }

        /// <summary>终端输入：驱动端上报 / 房主转发全体（走向由 09 侧的 NetKeyScreenBridge 决定）。</summary>
        public void SendKeyScreenInput(int syncId, uint sid, int key, int kind)
        {
            var msg = MessageCenter.Pack(CmdId.KeyScreenInputNtf,
                new KeyScreenInputMsg { SyncId = syncId, Sid = sid, Key = key, Kind = kind });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else if (CanSendToHost) NetSvc.Instance.SendMsg(msg);
        }

        private void HandleKeyScreenInputNtf(KeyScreenInputMsg m)
        {
            if (m != null) OnKeyScreenInput?.Invoke(m);
        }

        /// <summary>场景可破坏物被打掉：成员上报房主 / 房主转发全体（走向由 09 侧的 NetDestructibleBridge 决定）。</summary>
        public void SendSceneDestructible(int syncId, uint sid)
        {
            var msg = MessageCenter.Pack(CmdId.SceneDestructibleNtf,
                new DamageMsg { Sid = sid, Key = syncId, IsDeath = true });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else if (CanSendToHost) NetSvc.Instance.SendMsg(msg);
        }

        private void HandleSceneDestructibleNtf(DamageMsg m)
        {
            if (m != null) OnSceneDestructible?.Invoke(m);
        }

        /// <summary>标记点位：成员上报房主 / 房主转发全体（走向由 09 侧的 NetActionBridge 决定）。</summary>
        public void SendMark(uint sid, float x, float y, float z)
        {
            var msg = MessageCenter.Pack(CmdId.MarkNtf,
                new MarkMsg { Sid = sid, Kind = 0, X = x, Y = y, Z = z });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else if (CanSendToHost) NetSvc.Instance.SendMsg(msg);
        }

        /// <summary>呼叫凯伊：成员上报房主 / 房主转发全体。</summary>
        public void SendCallKai(uint sid, float x, float y, float z)
        {
            var msg = MessageCenter.Pack(CmdId.CallKaiNtf,
                new CallKaiMsg { Sid = sid, X = x, Y = y, Z = z });

            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);
            else if (CanSendToHost) NetSvc.Instance.SendMsg(msg);
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

        private void HandleLoadoutSync(PlayerLoadoutMsg m)
        {
            if (m != null) OnLoadoutSync?.Invoke(m);
        }

        private void HandleTaskConfirmNtf(TaskConfirmNtf ntf)
        {
            IsJoining = false;
            CurrentMatchId = ntf != null ? ntf.MatchId : 0;
            _battleStarted = false;   // ★ 新一局：加载闸门复位 —— 要重新等"全体加载完成"，不能用上一局的位

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

        /// <summary>房主广播"全体加载完成 ⇒ 一起开打"（房主自己那条由 <c>NetHostSvc</c> 本地自派发）。</summary>
        private void HandleBattleStartSync(BattleStartMsg m)
        {
            _battleStarted = true;
            if (m != null && m.MatchId != 0) CurrentMatchId = m.MatchId;
            OnBattleStart?.Invoke(m != null ? m.MatchId : 0);
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
                _pendingRoom = null;
                Debug.LogWarning($"[NetRoomFlow] 入房失败：{reason}");
                cb?.Invoke(false, reason);
                OnJoinResult?.Invoke(false, reason);
                return;
            }

            CurrentRoom = _pendingRoom;   // 现在"我在这个房间里"（房间列表靠它判"已经在里面了"）
            _pendingRoom = null;
            _battleStarted = false;       // 新入房 = 新一局：加载闸门复位

            Debug.Log("[NetRoomFlow] 入房成功");
            cb?.Invoke(true, string.Empty);
            OnPlayerList?.Invoke(players, null);   // 入房响应里还没带资料 ⇒ profiles 传 null
            OnJoinResult?.Invoke(true, string.Empty);
        }
    }
}
