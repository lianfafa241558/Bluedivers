namespace FPSGame.Net
{
    /// <summary>
    /// 命令号常量表。
    /// 作用：为每条网络消息分配一个唯一的整数编号，用于在信封中区分消息类型。
    /// 约定：客户端与服务器必须使用同一份 CmdId 表，编号不能重复、不能随意修改。
    /// </summary>
    /// <remarks>
    /// ▍号段（2026-11-04 重排；段间留空是**刻意的**，新增项只往本段末尾追加）：
    /// <br/>1001/1002 传输控制（<c>NetCmdId</c>，不在本表）
    /// <br/>4001-4019 会话 / 开局闸门 · 4020-4049 玩家表现 · 4050-4089 局内世界状态
    /// <br/>4090-4099 **已停用**（号只占位、永不复用）
    /// <br/>5001-5099 位姿 · 6001-6099 玩家配置
    /// <para>⚠ 别为了"紧凑"把别的段挪进空档：段=功能分区，挪一次所有历史日志/抓包就全对不上号了。</para>
    /// </remarks>
    public static class CmdId
    {
        #region 会话 / 开局闸门（4001-4019）

        /// <summary>请求加入房间</summary>
        public const int JoinRoomReq = 4001;
        /// <summary>响应加入结果（成功/失败+原因）</summary>
        public const int JoinRoomRsp = 4002;
        /// <summary>通知离开房间</summary>
        public const int LeaveRoomNtf = 4003;

        /// <summary>广播成员列表同步（加入/离开/变更时）</summary>
        public const int PlayerListSync = 4004;

        /// <summary>成员 -> 房主：准备状态切换</summary>
        public const int ReadyState = 4005;
        /// <summary>广播本局任务配置（Bridge 选完任务 ⇒ 双方进 Ready）</summary>
        public const int TaskConfirm = 4006;

        /// <summary>广播 进入 Transition（Armament 结束，同时开始加载战斗场景）</summary>
        public const int Transition = 4007;

        /// <summary>房主 -> 全体：某成员离开。⚠ 与 <c>PlayerListSync</c> 分开：战斗期名单是**冻结**的
        /// （重排会让 players 下标漂移），但"有人走了"这件事必须传出去 ⇒ 用这条**只带 sid、不重排**的消息。</summary>
        public const int PlayerLeftNtf = 4008;

        /// <summary>成员 -> 房主：本机**战斗场景加载完成**（本地初始化跑到 <c>BattleManager</c> 的开局闸门）。</summary>
        public const int LoadCompleteNtf = 4009;
        /// <summary>房主 -> 全体：**全体加载完成 ⇒ 一起开打**（各端凭它把 <c>IsStartBattle</c> 打开）。
        /// <para>⚠ 单独一条而不是复用 <c>Transition</c>：Transition 是"开始加载"（各端加载快慢差很多），
        /// 这条才是"都加载完了"，差的就是各端开打的时机。</para></summary>
        public const int BattleStartSync = 4010;

        #endregion

        #region 玩家表现（4020-4049）

        // ===== 切枪 / 开火 / 生命 / 弹药 / 复活（只做表现，不结算伤害）=====
        /// <summary>成员 -> 房主：切枪（武器槽位）</summary>
        public const int PlayerWeaponSwitchNtf = 4020;
        /// <summary>房主 -> 全体：切枪同步（含房主自己）</summary>
        public const int PlayerWeaponSwitchSync = 4021;
        /// <summary>成员 -> 房主：开火</summary>
        public const int PlayerShootNtf = 4022;
        /// <summary>房主 -> 全体：开火同步（含房主自己）</summary>
        public const int PlayerShootSync = 4023;
        /// <summary>成员 -> 房主：生命状态（血/盾/倒地）</summary>
        public const int PlayerVitalNtf = 4024;
        /// <summary>房主 -> 全体：生命状态同步（含房主自己）</summary>
        public const int PlayerVitalSync = 4025;
        /// <summary>成员 -> 房主：弹药系数</summary>
        public const int PlayerAmmoNtf = 4026;
        /// <summary>房主 -> 全体：弹药系数同步（含房主自己）</summary>
        public const int PlayerAmmoSync = 4027;

        /// <summary>双向：**把某个倒地的玩家扶起来**（值 = 被救者 sid，房主 = 0）。
        /// <para>▍为什么不能蹭家具交互那条（<see cref="FurnitureOperateNtf"/>）：<c>Furniture_PlayerDown</c> 的键 = <c>FNV1a(Id + 位置量化)</c>，
        /// 而盟友实例的出生点与本人出生点**差 0.2m（Y）**（<c>NetFriendBridge.NextSpawnPos</c> 没带
        /// <c>BridgeRoleManager.RawStartPoint</c> 的 +0.2 抬升）⇒ 两端键必然对不上、救人消息必被丢弃；
        /// 且同机多个盟友实例（Id 都空）位置键还会互相覆盖。⇒ 按 **sid** 直指被救者，由它本机执行复活。</para>
        /// <para>▍走向：救人者（操作盟友"倒地"实例）发 → 房主转发全体 → 被救者本机应用。</para></summary>
        public const int PlayerReviveNtf = 4028;

        // ===== 语音 / 标记 / 凯伊（表现类，丢了不影响判定）=====
        /// <summary>成员 -> 房主：角色喊话（SpeechTypeEnum 的 int）</summary>
        public const int SpeechNtf = 4029;
        /// <summary>房主 -> 全体：喊话同步（含房主自己；发起方按 Sid 丢掉自己那条）</summary>
        public const int SpeechSync = 4030;
        /// <summary>双向：标记点位（表现类，可不可靠通道，丢了无所谓）。</summary>
        public const int MarkNtf = 4031;
        /// <summary>双向：呼叫凯伊（带表现 + 凯伊移动）。</summary>
        public const int CallKaiNtf = 4032;

        // ===== 战备 / 载具 =====
        /// <summary>成员 -> 房主：呼叫了一次战备（战备 id + 落点）。⚠ 2026-10-14 起**多带一个"载具号"字段**
        /// （<c>AirdropCallMsg.VehicleNetId</c>，追加在 <c>[Key(6)]</c>）：成员这条先发 0，由房主转发时补号。</summary>
        public const int AirdropCallNtf = 4033;
        /// <summary>房主 -> 全体：战备呼叫同步（含房主自己；发起方按 Sid 丢掉自己那条）。
        /// ⚠ 2026-10-14 起 <c>VehicleNetId</c> 由房主填：载具两端各生成一份、时机不同 ⇒ 号只能随本条分配。</summary>
        public const int AirdropCallSync = 4034;

        /// <summary>双向：**载具武器开火表现**（键 = 载具 <c>Actor.NetId</c>；**只做表现**）。
        /// <para>▍只同步"看得见的东西"（枪口闪 + 弹道 + 命中特效）：伤害由**驾驶者那台**本机结算
        /// （与玩家开火同口径，见 <see cref="PlayerShootNtf"/> / <see cref="PlayerShootSync"/>），本条不重放伤害 —— 收端走 <c>SpawnVisualSalvo</c> 的"只表现"路
        /// （它会把 <c>FpsHelper.Hit</c> 摘掉，见 <c>WeaponBaseController</c>）。</para>
        /// <para>▍为什么不复用玩家开火两条：那套的"槽位"是 <c>PlayerWeaponsManager</c> 的语义
        /// （背包已经借了虚拟槽位 7）⇒ 载具武器（<c>VehicleWeaponsManager.m_WeaponSlots</c>）再塞一个虚拟值会把语义搅浑；
        /// 且键也不同（那条键 = sid，这条键 = 载具号）。</para>
        /// <para>▍**一个号两个方向**（同 <see cref="FurnitureOperateNtf"/> / 已停用的载具上下机走法）：成员开火 → 上报房主；
        /// 房主转发全体 + 自己本地应用；房主自己开火直接广播。⚠ 发起方按"**这辆车是不是我开的**"丢掉自己那条
        /// （本机已经真放过一发）；同一个号只能有一个注册者 ⇒ 注册在 <c>NetRoomFlow</c>，桥只订阅事件。</para>
        /// <para>▍【弹量随发 · 2026-10-15】载荷末尾还带"这一发打完之后的弹匣/后备弹"（<c>VehicleShootMsg.Magazine/Ammo</c>）：
        /// 载具武器两端各一份实例、弹量本来会分叉（实测 a 打完下车、b 上车看到满弹），口径改为"**驾驶者那台是权威**"，每次开火顺手推给其余端。</para></summary>
        public const int VehicleShootNtf = 4035;

        #endregion

        #region 局内世界状态（4050-4089）

        // ===== 波次 / 敌人（房主权威）=====
        /// <summary>房主 -> 全体：开波（时机权威 + 参数；成员按同一 waveIndex 复刻 ⇒ 同构成/同落点）</summary>
        public const int WaveStartSync = 4050;
        /// <summary>房主 -> 全体：波次中心点（每 Tick 会移动的"追击"中心，低频补发）。
        /// <para>▍为什么单独一条：开波时的 center 由 <see cref="WaveStartSync"/> 权威下发；但有些波的 center
        /// 会跟随玩家移动 ⇒ 各端若自己算"最近玩家"会因位姿延迟翻边（离散判定）。改由房主 ~2Hz 下发，
        /// 客户端只做插值跟随 ⇒ 判定只存在一处。</para></summary>
        public const int WaveCenterNtf = 4051;
        /// <summary>房主 -> 全体：某只怪的移动意图（NetId / 目标点）⇒ 成员本地算路径</summary>
        public const int EnemyMoveSync = 4052;

        /// <summary>成员 -> 房主：打中了某只怪 ⇒ 房主结算（血量/死亡权威在房主）</summary>
        public const int EnemyHitNtf = 4053;
        /// <summary>房主 -> 全体：房主打中了某只怪（伤害 / NetId / 开枪者）⇒ 成员扣自己那份副本的血。
        /// <para>⚠ 与 <see cref="EnemyHitNtf"/> 成对：那条只往上、这条只往下 ⇒ 血量口径统一在"房主权威"。
        /// 敌人的死亡也在这条里（<c>DamageMsg.IsDeath</c>），不再单开死亡号。</para></summary>
        public const int EnemyDamagedSync = 4054;

        // ===== 场景单位 / 场景物件 =====
        /// <summary>房主 -> 全体：场景单位（NPC 这类由场景摆好、会自己走动的氛围单位）的移动目标 / 停下。
        /// ⚠ 键是 <c>Actor.Id</c>（不是 NetId）：大厅会反复重建，而每局自增的号段依赖"每局归零 + 两端创建顺序一致"。</summary>
        public const int SceneUnitMoveSync = 4055;
        /// <summary>成员 -> 房主：请求场景 actor（NPC/特殊单位）快照</summary>
        public const int SceneActorReq = 4056;
        /// <summary>房主 -> 该成员：场景 actor 快照</summary>
        public const int SceneActorNtf = 4057;

        /// <summary>双向：**场景可破坏物**（油桶这类摆在场上的物件）被打掉 ⇒ 远端把同一件也打掉。
        /// <para>▍为什么单独一条：这类物件**没有 NetId**（<c>Actor.NetId</c> 只发给敌人），
        /// 也不走家具那条交互通道（它不是家具、没有交互，是被打爆的）；不加这条就是
        /// "客机打爆了油桶、房主那边还是完好"（2026-10-10 用户实测）。</para>
        /// <para>▍键 = <c>FNV1a(Actor.Id + 位置 0.1m 量化)</c>（同 <c>Furniture_Attached.SyncId</c> 口径：
        /// 同类物件多实例，只用 Id 会撞车、用 IndexID 两端不一致）。</para></summary>
        public const int SceneDestructibleNtf = 4058;

        // ===== 家具交互 =====
        /// <summary>双向：家具交互（<c>SyncId</c> = 家具确定性键）⇒ 远端重放同一交互。
        /// <para>⚠ 载具上下机也走这条（<c>NetId != 0</c> 的 NetId 分支，原独立号已停用）：键是载具 <c>Actor.NetId</c>、
        /// <c>SyncId/State</c> 不参与；远端**不能**走 <c>ApplyRemoteOperate</c>（那是本机玩家专属），应用一律走
        /// <c>NetVehicleBridge</c> 的挂点收口。</para></summary>
        public const int FurnitureOperateNtf = 4059;

        /// <summary>双向：**容器类家具的一次提交已完成**（键 = 家具 <c>SyncId</c>；值 = 个数 + 主类型）。
        /// <para>▍为什么单独一条：这类家具的效果写在 <c>IStepPress.StepPress</c>（逐个扣背包 + <c>OnSubmit</c>），
        /// 而远端重放只走 <c>Operate()</c>（只收尾）⇒ 必须把"提交了几个"传过去，由远端在盟友的幽灵背包上重放。</para></summary>
        public const int OOPartSubmitNtf = 4060;

        /// <summary>双向：**按键终端(KeyScreen)的阶段推进**（键 = 终端家具的 <c>Furniture_Attached.SyncId</c>）。
        /// <para>▍为什么单独一条：阶段推进原本纯本地（<c>OnUpdateStage</c> 只在本地抛），而任务的**中间阶段标志**
        /// （黑盒子的 <c>startDefense</c>、炼油的 Start/Repair、撤离的 Wait、<c>KeyScreenContGo</c> 的显隐）
        /// 全靠它 ⇒ 两端阶段一分叉，后续任务行为就各走各的（2026-10-11 用户实测）。只传"推进到第几阶段"。</para>
        /// <para>▍驱动口径：只有**驱动端**（操作者所在那台 / 无人阶段时房主）上报，其余端只应用 ——
        /// 判定唯一收口在 <c>KeyScreen.IsStageDriver</c>，桥是 09 的 <c>NetKeyScreenBridge</c>。</para></summary>
        public const int KeyScreenStageNtf = 4061;

        /// <summary>双向：**玩家对按键终端(KeyScreen)的操作**（键 = 终端家具 <c>SyncId</c>；值 = 按键 + 类型）。
        /// <para>▍与 <see cref="KeyScreenStageNtf"/>（阶段推进）分工：那条保证"阶段权威、不分叉"；本条让"终端上正在发生什么"对所有端可见
        /// （非驱动端把本机键盘换成远端按键，逻辑照跑 ⇒ 显示自然一致）。</para></summary>
        public const int KeyScreenInputNtf = 4062;

        // ===== 生成族（房主唯一生成权威）=====
        /// <summary>房主 -> 全体：**房主生成族**（巡逻队 + 运行时生成的单只单位）。
        /// <para>▍为什么必须房主权威：<c>Actor.Awake</c> 会给新单位领 NetId（口径是"房主分配 + 两端创建顺序一致"），
        /// 成员端自造必然错位（其后所有单位错号：伤害打到别的怪身上、移动指令发错对象）。合并成一条消息、一处回环门、一处日志口径。</para>
        /// <para>▍形态（<c>HostSpawnMsg.Kind</c>）：<c>Patrol</c> = 巡逻队（生成点 + 逐单位目标点；构成由落点派生 ⇒ 不传）、
        /// <c>Unit</c> = 单只单位（来源键 + 槽位 ⇒ 成员让本端同一份生成器复刻并覆盖号，见 <c>INetSpawner</c>）。</para>
        /// <para>▍与 <see cref="WaveStartSync"/> / <see cref="SceneActorNtf"/> 的分工：波次是**逐 Tick 投放**、场景单位是**加载期摆好**，
        /// 频率档与本条不同 ⇒ 不合并（它们各自的路由已被验证，见 <c>WaveManager</c> 的整批复刻与 <c>SceneActorSync</c>）。</para></summary>
        public const int HostSpawnNtf = 4063;

        // ===== 终局 / 任务 =====
        /// <summary>房主 -> 全体：本局结束（结果/延迟）⇒ 各端本地走 <c>BattleManager.EndGame</c>。
        /// ⚠ 接收端必须"本局只结束一次"（否则会排两个定时器）。</summary>
        public const int GameOverNtf = 4064;
        /// <summary>房主 -> 全体：开始撤离（撤离点）⇒ 各端本地触发撤离。</summary>
        public const int EvacuateNtf = 4065;
        /// <summary>房主 -> 全体：任务状态/进度（<c>Key = MissionBase.netOrder</c>）。</summary>
        public const int MissionUpdateNtf = 4066;

        #endregion

        #region 已停用（4090-4099；号只占位、勿复用）

        // ⚠ 下面几条**不再注册、不再外发**，常量保留只为两件事：占住"不许再分配"的号 + 留住当初合并的理由。
        //    它们的旧值见 git 历史（4026 / 4047 / 4051 / 4053 / 4055）。

        /// <summary>【停用】房主 -> 全体：某只怪死亡。
        /// <para>2026-10-12 并入 <see cref="EnemyDamagedSync"/> 的 <c>DamageMsg.IsDeath</c>（死亡就是"被打到 0 血"的特例）。</para></summary>
        public const int EnemyDiedSync = 4090;

        /// <summary>【停用】房主 -> 全体：巡逻队生成（生成点 + 逐单位目标点）。
        /// <para>2026-10-12 并入 <see cref="HostSpawnNtf"/> 的 <c>Kind = Patrol</c>（与"运行时生成的单只单位"同族：
        /// 前提都是"房主是唯一生成权威 + 号由房主分配"）。</para></summary>
        public const int PatrolSpawnNtf = 4091;

        /// <summary>【停用】房主 -> 全体：运行时生成的单位（巢穴 / 召唤师 / 刷怪器）。
        /// <para>2026-10-12 并入 <see cref="HostSpawnNtf"/> 的 <c>Kind = Unit</c>；当初不能再各生各的：
        /// 这类生成的**时机由各自的 AI / 计时驱动**，两端必然不同 ⇒ 各自生成就会把 <c>Actor.Awake</c> 的号段整体推歪。</para></summary>
        public const int EnemySpawnNtf = 4092;

        /// <summary>【停用】双向：载具上下机（键 = 载具 <c>Actor.NetId</c>；值 = 驾驶者 sid + <c>Mounted</c>）。
        /// <para>2026-10-27 并入 <see cref="FurnitureOperateNtf"/> 的 <c>NetId</c> 分支（只当**传输**，应用走 <c>NetVehicleBridge</c>）。</para></summary>
        public const int VehicleMountNtf = 4093;

        /// <summary>【停用】背包护盾被打（护盾归属者本机处理）。
        /// <para>2026-11-04 并入伤害族（<see cref="EnemyHitNtf"/> / <see cref="EnemyDamagedSync"/> 的 <c>DamageMsg.OwnerSid</c>）。</para></summary>
        public const int ShieldDamageNtf = 4094;

        #endregion

        #region 位姿（5001-5099）

        /// <summary>本机向房主提交位姿</summary>
        public const int PlayerTransformUp = 5001;
        /// <summary>房主广播位姿批次</summary>
        public const int TransformBatchSync = 5002;

        #endregion

        #region 玩家配置（6001-6099）

        /// <summary>成员 -> 房主：本机玩家资料（角色/等级/武器/战备/强化）</summary>
        public const int PlayerProfileNtf = 6001;

        /// <summary>成员 -> 房主：舰桥准备项变更（战备槽 0-3 / 全队强化槽 4，见 <c>PlayerSetupMsg.BoosterSlot</c>）。
        /// <para>▍战备与强化共用一条：<c>ArmamentWnd</c> 里强化本来就是同一排的第 5 格
        /// （<c>GetChild(playerIndex, 5, 4)</c>，战备是 0-3）⇒ 协议按同一个槽位口径走，省一套 DTO / 命令号 / handler。</para></summary>
        public const int PlayerSetupNtf = 6002;
        /// <summary>房主 -> 全体：舰桥准备项（房主权威转发，含房主自己）</summary>
        public const int PlayerSetupSync = 6003;

        /// <summary>成员 -> 房主：本机**配置**（武器选择 + 改装档位/模组 + 载具改装）。
        /// <para>▍与 <see cref="PlayerProfileNtf"/> 分开：资料那条走"名单广播"（高频、跟着准备状态一起发），
        /// 而本配置**只在入房与真正改动时**各发一次（换武器/改档位/改模组/改载具才有意义）。</para>
        /// <para>▍⚠ **武器选择也在这里**：它和改装是同一个窗口、同一个触发点（SelectRoleWnd 关窗 ⇒ SendSelfLoadout），
        /// 放资料条会漏同步 —— 关窗时资料压根不重发。</para></summary>
        public const int PlayerLoadoutNtf = 6004;
        /// <summary>房主 -> 全体：玩家配置（含房主自己）。⚠ 新人入房时房主**补发全量**（见 NetHostSvc.BroadcastLoadouts）。</summary>
        public const int PlayerLoadoutSync = 6005;

        #endregion
    }
}
