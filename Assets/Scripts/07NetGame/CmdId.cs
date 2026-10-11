
namespace FPSGame.Net
{

    /// <summary>
    /// 命令号常量表。
    /// 作用：为每条网络消息分配一个唯一的整数编号，用于在信封中区分消息类型。
    /// 约定：客户端与服务器必须使用同一份 CmdId 表，编号不能重复、不能随意修改。
    /// 分段规则：按业务模块分段，方便扩展和排查。
    /// 提示：本表为示例，实际业务可在此基础上扩展。
    /// </summary>
    public static class CmdId
    {
        // ===== 房间模块（房主权威 / 局域网联机）=====
        /// <summary>成员 -> 房主：请求加入房间</summary>
        public const int JoinRoomReq = 4001;
        /// <summary>房主 -> 成员：加入结果（成功/失败+原因）</summary>
        public const int JoinRoomRsp = 4002;
        /// <summary>成员 -> 房主：离开房间</summary>
        public const int LeaveRoomNtf = 4003;
        /// <summary>房主 -> 全体：成员列表同步（加入/离开/变更时）</summary>
        public const int PlayerListSync = 4004;
        /// <summary>成员 -> 房主：准备状态切换</summary>
        public const int ReadyState = 4005;
        /// <summary>房主 -> 全体：本局配置确认（Bridge 选完任务 ⇒ 双方进 Ready；仍在舰桥）</summary>
        public const int TaskConfirm = 4006;
        /// <summary>房主 -> 全体：进入 Transition（Armament 结束，同时开始加载战斗场景）</summary>
        public const int Transition = 4012;

        // ===== 局内表现同步（切枪 / 开火；只做表现，不结算伤害）=====
        /// <summary>成员 -> 房主：切枪（武器槽位）</summary>
        public const int PlayerWeaponSwitchNtf = 4013;
        /// <summary>房主 -> 全体：切枪同步（含房主自己）</summary>
        public const int PlayerWeaponSwitchSync = 4014;
        /// <summary>成员 -> 房主：开火</summary>
        public const int PlayerShootNtf = 4015;
        /// <summary>房主 -> 全体：开火同步（含房主自己）</summary>
        public const int PlayerShootSync = 4016;
        /// <summary>成员 -> 房主：生命状态（血/盾/倒地）</summary>
        public const int PlayerVitalNtf = 4017;
        /// <summary>房主 -> 全体：生命状态同步（含房主自己）</summary>
        public const int PlayerVitalSync = 4018;
        /// <summary>成员 -> 房主：弹药系数</summary>
        public const int PlayerAmmoNtf = 4019;
        /// <summary>房主 -> 全体：弹药系数同步（含房主自己）</summary>
        public const int PlayerAmmoSync = 4020;
        /// <summary>房主 -> 全体：开波（时机权威 + 参数；成员按同一 waveIndex 复刻 ⇒ 同构成/同落点）</summary>
        public const int WaveStartSync = 4023;
        /// <summary>房主 -> 全体：某只怪的移动意图（NetId / 目标点）⇒ 成员本地算路径</summary>
        public const int EnemyMoveSync = 4024;
        /// <summary>成员 -> 房主：打中了某只怪 ⇒ 房主结算（血量/死亡权威在房主）</summary>
        public const int EnemyHitNtf = 4025;
        /// <summary>房主 -> 全体：某只怪死亡 ⇒ 成员干掉自己的副本。
        /// <para>⚠ 2026-10-12 已并入 <c>EnemyDamagedSync</c>(4040) 的 <c>DamageMsg.IsDeath</c>（死亡就是"被打到 0 血"的特例）
        /// ⇒ 不再注册、不再外发。常量保留只为不与历史日志/抓包对不上号。</para></summary>
        public const int EnemyDiedSync = 4026;
        /// <summary>成员 -> 房主：呼叫了一次战备（战备 id + 落点）</summary>
        public const int AirdropCallNtf = 4027;
        /// <summary>房主 -> 全体：战备呼叫同步（含房主自己；发起方按 Sid 丢掉自己那条）</summary>
        public const int AirdropCallSync = 4028;
        /// <summary>房主 -> 全体：某成员离开。⚠ 与 <c>PlayerListSync</c> 分开：战斗期名单是**冻结**的
        /// （重排会让 players 下标漂移），但"有人走了"这件事必须传出去 ⇒ 用这条**只带 sid、不重排**的消息。</summary>
        public const int PlayerLeftNtf = 4029;
        /// <summary>成员 -> 房主：角色喊话（SpeechTypeEnum 的 int）</summary>
        public const int SpeechNtf = 4030;
        /// <summary>房主 -> 全体：喊话同步（含房主自己；发起方按 Sid 丢掉自己那条）</summary>
        public const int SpeechSync = 4031;
        /// <summary>房主 -> 全体：房主打中了某只怪（伤害 / NetId / 开枪者）⇒ 成员扣自己那份副本的血。
        /// <para>⚠ 与 4025（成员上报命中）成对：那条只往上、这条只往下 ⇒ 血量口径统一在"房主权威"。</para></summary>
        public const int EnemyDamagedSync = 4040;
        /// <summary>房主 -> 全体：场景单位（NPC 这类由场景摆好、会自己走动的氛围单位）的移动目标 / 停下。
        /// ⚠ 键是 <c>Actor.Id</c>（不是 NetId）：大厅会反复重建，而每局自增的号段依赖"每局归零 + 两端创建顺序一致"。</summary>
        public const int SceneUnitMoveSync = 4032;
        /// <summary>成员 -> 房主：请求场景 actor（NPC/特殊单位）快照</summary>
        public const int SceneActorReq = 4021;
        /// <summary>房主 -> 该成员：场景 actor 快照</summary>
        public const int SceneActorNtf = 4022;

        // ===== 局内世界状态同步（4033+；设计见 .codebuddy/plans/联机_未同步清单与同步方案.md）=====
        /// <summary>房主 -> 全体：本局结束（结果/延迟）⇒ 各端本地走 <c>BattleManager.EndGame</c>。
        /// ⚠ 接收端必须"本局只结束一次"（否则会排两个定时器）。</summary>
        public const int GameOverNtf = 4033;
        /// <summary>房主 -> 全体：开始撤离（撤离点）⇒ 各端本地触发撤离。</summary>
        public const int EvacuateNtf = 4034;
        /// <summary>房主 -> 全体：任务状态/进度（<c>Key = MissionBase.netOrder</c>）。</summary>
        public const int MissionUpdateNtf = 4035;
        /// <summary>双向：家具交互（<c>SyncId</c> = 家具确定性键）⇒ 远端重放同一交互。</summary>
        public const int FurnitureOperateNtf = 4036;
        /// <summary>双向：标记点位（表现类，可不可靠通道，丢了无所谓）。</summary>
        public const int MarkNtf = 4037;
        /// <summary>双向：呼叫凯伊（带表现 + 凯伊移动）。</summary>
        public const int CallKaiNtf = 4038;
        /// <summary>房主 -> 全体：波次中心点（每 Tick 会移动的"追击"中心，低频补发）。
        /// <para>▍为什么单独一条：开波时的 center 由 <see cref="WaveStartSync"/> 权威下发；但有些波的 center
        /// 会跟随玩家移动 ⇒ 各端若自己算"最近玩家"会因位姿延迟翻边（离散判定）。改由房主 ~2Hz 下发，
        /// 客户端只做插值跟随 ⇒ 判定只存在一处。</para></summary>
        public const int WaveCenterNtf = 4039;

        // ===== 开局加载闸门（4041+；2026-10-10）=====
        /// <summary>成员 -> 房主：本机**战斗场景加载完成**（本地初始化跑到 <c>BattleManager</c> 的开局闸门）。</summary>
        public const int LoadCompleteNtf = 4041;
        /// <summary>房主 -> 全体：**全体加载完成 ⇒ 一起开打**（各端凭它把 <c>IsStartBattle</c> 打开）。
        /// <para>⚠ 单独一条而不是复用 <c>Transition</c>：Transition 是"开始加载"（各端加载快慢差很多），
        /// 这条才是"都加载完了"，差的就是各端开打的时机。</para></summary>
        public const int BattleStartSync = 4042;

        /// <summary>双向：**场景可破坏物**（油桶这类摆在场上的物件）被打掉 ⇒ 远端把同一件也打掉。
        /// <para>▍为什么单独一条：这类物件**没有 NetId**（<c>Actor.NetId</c> 只发给敌人），
        /// 也不走家具那条交互通道（它不是家具、没有交互，是被打爆的）；不加这条就是
        /// "客机打爆了油桶、房主那边还是完好"（2026-10-10 用户实测）。</para>
        /// <para>▍键 = <c>FNV1a(Actor.Id + 位置 0.1m 量化)</c>（同 <c>Furniture_Attached.SyncId</c> 口径：
        /// 同类物件多实例，只用 Id 会撞车、用 IndexID 两端不一致）。</para></summary>
        public const int SceneDestructibleNtf = 4043;

        /// <summary>双向：**按键终端(KeyScreen)的阶段推进**（键 = 终端家具的 <c>Furniture_Attached.SyncId</c>）。
        /// <para>▍为什么单独一条：阶段推进原本纯本地（<c>OnUpdateStage</c> 只在本地抛），而任务的**中间阶段标志**
        /// （黑盒子的 <c>startDefense</c>、炼油的 Start/Repair、撤离的 Wait、<c>KeyScreenContGo</c> 的显隐）
        /// 全靠它 ⇒ 两端阶段一分叉，后续任务行为就各走各的（2026-10-11 用户实测）。只传"推进到第几阶段"。</para>
        /// <para>▍驱动口径：只有**驱动端**（操作者所在那台 / 无人阶段时房主）上报，其余端只应用 ——
        /// 判定唯一收口在 <c>KeyScreen.IsStageDriver</c>，桥是 09 的 <c>NetKeyScreenBridge</c>。</para></summary>
        public const int KeyScreenStageNtf = 4046;

        /// <summary>房主 -> 全体：**巡逻队生成**（生成点 + 逐单位目标点）。
        /// <para>⚠ 2026-10-12 已并入 <c>HostSpawnNtf</c>(4052) 的 <c>Kind = Patrol</c>（与"运行时生成的单只单位"同族：
        /// 前提都是"房主是唯一生成权威 + 号由房主分配"）⇒ 不再注册、不再外发。</para>
        /// <para>▍为什么当初必须房主权威：巡逻队（任务的维修巡逻 / 据点的压力巡逻）原本是**本地生成**的 ⇒ 成员端自己也会造一批，
        /// 而 <c>Actor.Awake</c> 会给新单位分配 <c>NetId</c>（口径是"房主分配 + 两端创建顺序一致"）
        /// ⇒ 成员端自造的单位与房主下发的号段**撞号**（移动指令打到别的怪身上、死亡错杀）。</para></summary>
        public const int PatrolSpawnNtf = 4047;

        /// <summary>双向：**容器类家具的一次提交已完成**（键 = 家具 <c>SyncId</c>；值 = 个数 + 主类型）。
        /// <para>▍为什么单独一条：这类家具的效果写在 <c>IStepPress.StepPress</c>（逐个扣背包 + <c>OnSubmit</c>），
        /// 而远端重放只走 <c>Operate()</c>（只收尾）⇒ 必须把"提交了几个"传过去，由远端在盟友的幽灵背包上重放。</para></summary>
        public const int OOPartSubmitNtf = 4048;

        /// <summary>双向：**玩家对按键终端(KeyScreen)的操作**（键 = 终端家具 <c>SyncId</c>；值 = 按键 + 类型）。
        /// <para>▍与 4046（阶段推进）分工：4046 保证"阶段权威、不分叉"；本条让"终端上正在发生什么"对所有端可见
        /// （非驱动端把本机键盘换成远端按键，逻辑照跑 ⇒ 显示自然一致）。</para></summary>
        public const int KeyScreenInputNtf = 4049;

        /// <summary>双向：**把某个倒地的玩家扶起来**（值 = 被救者 sid，房主 = 0）。
        /// <para>▍为什么不能蹭家具交互那条（4036）：<c>Furniture_PlayerDown</c> 的键 = <c>FNV1a(Id + 位置量化)</c>，
        /// 而盟友实例的出生点与本人出生点**差 0.2m（Y）**（<c>NetFriendBridge.NextSpawnPos</c> 没带
        /// <c>BridgeRoleManager.RawStartPoint</c> 的 +0.2 抬升）⇒ 两端键必然对不上、救人消息必被丢弃；
        /// 且同机多个盟友实例（Id 都空）位置键还会互相覆盖。⇒ 按 **sid** 直指被救者，由它本机执行复活。</para>
        /// <para>▍走向：救人者（操作盟友"倒地"实例）发 → 房主转发全体 → 被救者本机应用。</para></summary>
        public const int PlayerReviveNtf = 4050;

        /// <summary>房主 -> 全体：**运行时生成的单位**（巢穴 / 召唤师 / 刷怪器）。
        /// <para>⚠ 2026-10-12 已并入 <c>HostSpawnNtf</c>(4052) 的 <c>Kind = Unit</c> ⇒ 不再注册、不再外发。</para>
        /// <para>▍为什么当时不能再各生各的：这类生成的**时机由各自的 AI / 计时驱动**，两端必然不同 ⇒ 各自生成就会把
        /// <c>Actor.Awake</c> 的号段整体推歪（其后所有单位错位：伤害打到别的怪身上、移动指令发错对象）。</para></summary>
        public const int EnemySpawnNtf = 4051;

        /// <summary>房主 -> 全体：**房主生成族**（原 4047 巡逻队 + 4051 运行时生成合并，2026-10-12）。
        /// <para>▍为什么合并：两者的前提完全同型 —— 都是"房主是唯一的生成权威"，都是因为 <c>Actor.Awake</c> 会领 NetId
        /// （号的口径是"房主分配 + 两端创建顺序一致"，成员端自造必然错位）。合并后一条消息、一处回环门、一处日志口径。</para>
        /// <para>▍形态（<c>HostSpawnMsg.Kind</c>）：<c>Patrol</c> = 巡逻队（生成点 + 逐单位目标点；构成由落点派生 ⇒ 不传）、
        /// <c>Unit</c> = 单只单位（来源键 + 槽位 ⇒ 成员让本端同一份生成器复刻并覆盖号，见 <c>INetSpawner</c>）。</para>
        /// <para>▍与 <c>WaveStartSync</c>(4023) / <c>SceneActorNtf</c>(4039) 的分工：波次是**逐 Tick 投放**、场景单位是**加载期摆好**，
        /// 频率档与本条不同 ⇒ 不合并（它们各自的路由已被验证，见 <c>WaveManager</c> 的整批复刻与 <c>SceneActorSync</c>）。</para></summary>
        public const int HostSpawnNtf = 4052;

        // ===== 舰桥准备（战备/强化/资料；对应原 BridgeSys 的三条本地回环）=====
        /// <summary>成员 -> 房主：本机玩家资料（角色/等级/武器/战备/强化）</summary>
        public const int PlayerProfileNtf = 4007;
        /// <summary>成员 -> 房主：选择战备</summary>
        public const int PlayerArmamentNtf = 4008;
        /// <summary>房主 -> 全体：战备选择（房主自己也走这条）</summary>
        public const int PlayerArmamentSync = 4009;
        /// <summary>成员 -> 房主：选择全队强化</summary>
        public const int PlayerBoosterNtf = 4010;
        /// <summary>房主 -> 全体：全队强化（房主自己也走这条）</summary>
        public const int PlayerBoosterSync = 4011;
        /// <summary>成员 -> 房主：本机**配置**（武器改装：档位 + 模组；载具改装）。
        /// <para>▍与 <c>PlayerProfileNtf</c> 分开：资料那条走"名单广播"（高频、跟着准备状态一起发），
        /// 而本配置**只在入房与真正改动时**各发一次（改档位/模组/载具才有意义）。</para></summary>
        public const int PlayerLoadoutNtf = 4044;
        /// <summary>房主 -> 全体：玩家配置（含房主自己）。⚠ 新人入房时房主**补发全量**（见 NetHostSvc.BroadcastLoadouts）。</summary>
        public const int PlayerLoadoutSync = 4045;

        // ===== 战斗·关键物体位姿同步（5000 段；房主权威"聚合 + 广播"）=====
        /// <summary>成员 -> 房主：本机权威实体的位姿（当前：本机玩家）</summary>
        public const int PlayerTransformUp = 5001;
        /// <summary>房主 -> 全体：聚合后的位姿批次（含房主自己；房主本地也自派发一次）</summary>
        public const int TransformBatchSync = 5002;
    }

}
