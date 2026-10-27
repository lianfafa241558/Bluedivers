using MessagePack;

namespace FPSGame.Net
{
    // ============================================================
    // 局内世界状态同步（4033+，2026-10-08）
    // 设计见 .codebuddy/plans/联机_未同步清单与同步方案.md
    //
    // ⚠ 字段类型只能用 NetMsgCodec 支持的那些（int/uint/float/bool/string 等），
    //    不能用游戏枚举/游戏类型 —— 07_NetGame 看不见 05/06。
    // ============================================================

    #region 游戏阶段和任务

    /// <summary>本局结束（房主 -> 全体）。</summary>
    [MessagePackObject]
    public class GameOverMsg
    {
        /// <summary>本局局号（幂等键：同一局只结束一次）。</summary>
        [Key(0)] public int MatchId;
        /// <summary>延迟秒数（与房主一致，用于两端"同时"进结算）。</summary>
        [Key(1)] public int Delay;
        /// <summary><c>GameResult</c> 的 int（房主权威）。</summary>
        [Key(2)] public int Result;
    }

    /// <summary>开始撤离（房主 -> 全体）。</summary>
    [MessagePackObject]
    public class EvacuateMsg
    {
        [Key(0)] public float X;
        [Key(1)] public float Y;
        [Key(2)] public float Z;
    }

    /// <summary>任务状态/进度（房主 -> 全体）。<c>Key = MissionBase.netOrder</c>。
    /// <para>⚠ 必须带 <c>State</c>：任务的完成有"进度"与"事件触发"两个独立出口，只同步进度会漏掉触发型任务。</para></summary>
    [MessagePackObject]
    public class MissionUpdateMsg
    {
        /// <summary>跨端稳定键（房主在生成任务时按顺序赋值）。</summary>
        [Key(0)] public int Key;
        /// <summary>任务状态（与桥侧约定：1=进行中 2=完成 3=失败）。</summary>
        [Key(1)] public int State;
        [Key(2)] public int Progress;
        [Key(3)] public int MaxProgress;
        /// <summary>进度条用的 0~1 值（部分任务不是 NowProgress/MaxProgress 的简单比值）。</summary>
        [Key(4)] public float Percentage;
    }

    #endregion

    #region 必须同步的玩家操作
    /// <summary>标记点位</summary>
    [MessagePackObject]
    public class MarkMsg
    {
        [Key(0)] public uint Sid;
        /// <summary>标记种类（本地表现用；两端约定同一套）。</summary>
        [Key(1)] public int Kind;
        [Key(2)] public float X;
        [Key(3)] public float Y;
        [Key(4)] public float Z;
    }

    /// <summary>呼叫凯伊（双向；带表现 + 凯伊移动）。</summary>
    [MessagePackObject]
    public class CallKaiMsg
    {
        [Key(0)] public uint Sid;
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
    }

    /// <summary>有人把某个玩家扶起来了（同型双向：救人者 -> 房主 -> 全体）。
    /// <para>▍为什么按 <c>Sid</c> 而不是复用家具交互（4036）：见 <c>CmdId.PlayerReviveNtf</c> 的注释。</para></summary>
    [MessagePackObject]
    public class PlayerReviveMsg
    {
        [Key(0)] public uint Sid;         // 被救者（房主 = 0）
    }

    #endregion

    #region 物体交互
    /// <summary>家具交互（双向）。<c>SyncId</c> = FNV1a(Id + 位置量化)，创建时算一次并缓存。</summary>
    [MessagePackObject]
    public class FurnitureOperateMsg
    {
        /// <summary>权威标识：操作者会话号（房主 = 0）。远端据此解析"操作者"（Friend 实例）。</summary>
        [Key(0)] public uint Sid;
        [Key(1)] public int SyncId;

        /// <summary>状态的 int 编码（可选）。接收端**优先**用 <see cref="StateKey"/> 自己解析，
        /// 这个只在对方没带 Key 时兜底。默认 0 ⇒ 旧发送端 = "无状态负载"。</summary>
        [Key(2)] public int State;
        /// <summary>状态的原始键（可选，如管道的 <c>PipeWait</c>/<c>PipeError</c>）。
        /// <para>▍非空 = 这是一条**状态**同步（不是操作重放）：接收端走 <c>ApplyRemoteState</c>，**不调用</b> <c>Operate()</c>。
        /// 用于"改了状态但没有人交互"的场合（任务把管道打成 PipeError）。</para></summary>
        [Key(3)] public string StateKey;

        /// <summary>【载具上下机】载具的 <c>Actor.NetId</c>（= 原 4053 的键；0 = 普通家具消息，本字段不参与）。
        /// <para>▍为什么载具不用自己的 <c>SyncId</c>：它两端各生成一份、生成时机与落点都差几厘米
        /// ⇒ <c>FNV1a(Id+位置量化)</c> 必然对不上；号由房主随 4028 分配、两端一致，只能用它。</para>
        /// <para>⚠ 必须**追加在末尾**：旧端跳过它拿默认 0 ⇒ 退化成"按家具处理并丢弃"，不会串字段。</para></summary>
        [Key(4)] public int NetId;
        /// <summary>【载具上下机】驾驶者会话号（房主 = 0）⇒ 接收端据此解析"本端那个实体"（本机玩家 / 盟友实例）。</summary>
        [Key(5)] public uint VehicleSid;
        /// <summary>【载具上下机】true = 上机；false = 下机 / 被打爆 / 驾驶者离场 / 驾驶者死亡。</summary>
        [Key(6)] public bool Mounted;
    }

  /// <summary>按键终端(KeyScreen)阶段推进（双向）。<c>SyncId</c> = 终端家具的 <c>Furniture_Attached.SyncId</c>
    /// （与 4036 同口径 ⇒ 两端日志可直接对照）。
    /// <para>只传"推进到第几阶段"：阶段本身的语义（UI 显隐 / 谜题初始化 / 任务标志）由各端跑同一条本地
    /// <c>SetStage</c> 逻辑，不需要过网。</para></summary>
    [MessagePackObject]
    public class KeyScreenStageMsg
    {
        /// <summary>权威标识：推进者的会话号（房主 = 0）⇒ 接收端据此丢掉"自己发出去的那条回环"。</summary>
        [Key(0)] public uint Sid;
        /// <summary>终端家具的跨端稳定键。</summary>
        [Key(1)] public int SyncId;
        /// <summary>目标阶段号。</summary>
        [Key(2)] public int Stage;
    }


    /// <summary>容器类家具的一次提交（双向）。</summary>
    [MessagePackObject]
    public class OOPartSubmitMsg
    {
        /// <summary>提交者会话号（房主 = 0）⇒ 接收端据此解析"操作者"（盟友实例）并丢弃自己的回环。</summary>
        [Key(0)] public uint Sid;
        /// <summary>家具跨端稳定键（<c>Furniture_Attached.SyncId</c>）。</summary>
        [Key(1)] public int SyncId;
        /// <summary>本次提交个数。</summary>
        [Key(2)] public int Count;
        /// <summary>主类型（<c>OOPartEnum</c> 的 int；-1 = 未知）。</summary>
        [Key(3)] public int Type;
    }

    /// <summary>玩家对终端(KeyScreen)的一次输入（双向）。
    /// <para>▍Key = <c>InputState</c> 的 int；Kind：0 = 本帧按下、1 = 按住开始、2 = 松开。</para>
    /// <para>▍只传"按键"而不是传终端状态：非驱动端把本机键盘换成远端按键后，**跑的是同一套阶段逻辑**
    /// ⇒ UI/判定/光标位置全部自然一致，不用再抽一堆"刷新 UI"的接口。</para></summary>
    [MessagePackObject]
    public class KeyScreenInputMsg
    {
        [Key(0)] public uint Sid;
        [Key(1)] public int SyncId;
        [Key(2)] public int Key;
        [Key(3)] public int Kind;
    }


    #endregion

    #region 实体和波次（房主权威）

    /// <summary>波次中心点（房主 -> 全体，~2Hz 低频补发）。
    /// <para>用于"center 跟随玩家移动"的波：各端不自己算"最近玩家"（会因位姿延迟翻边），
    /// 只跟随房主下发的中心（客户端做插值平滑）。</para></summary>
    [MessagePackObject]
    public class WaveCenterMsg
    {
        /// <summary>波序（丢过期包 / 确认这一波还在）。</summary>
        [Key(0)] public int WaveIndex;
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
    }

  
    /// <summary>房主生成消息的两种形态（＝ <see cref="HostSpawnMsg.Kind"/>）。</summary>
    public enum HostSpawnKind
    {
        /// <summary>巡逻队（用 X/Y/Z 生成点 + <c>Targets[]</c> 逐单位目标点；构成由落点派生 ⇒ 不传）。</summary>
        Patrol = 0,
        /// <summary>运行时生成的**单只**单位（用 SourceKey/Slot ⇒ 让本端同一份生成器复刻）。</summary>
        Unit = 1,
    }

    /// <summary>【房主 → 全体】**实体生成**的统一消息（原 4047 巡逻队 + 4051 运行时生成合并，2026-10-12）。
    ///
    /// <para>▍为什么合成一条：两者的前提完全同型 —— 都是"房主是唯一的生成权威"，都是因为
    /// <c>Actor.Awake</c> 会领 NetId（号的口径是"房主分配 + 两端创建顺序一致"，成员端自造必然错位）。
    /// 合并后一条消息、一处回环门、一处日志口径，差异用 <see cref="Kind"/> + 可选字段表达。</para>
    ///
    /// <para>▍Kind = Patrol（0）：构成不传 —— <c>WaveManager.CreatPatrol</c> 按**落点位置**派生随机流 ⇒
    /// 同落点 ⇒ 同构成同顺序；但**逐单位目标点必须传**（它由房主侧的几何 / 玩家位置算出，成员算不出同一个值）。</para>
    /// <para>▍Kind = Unit（1）：不传 prefab 引用（过不了网）—— 成员的生成器拿 <see cref="Slot"/> 回自己的表取同一把 prefab，
    /// <see cref="PrefabId"/> 只是给"按类型抽表"的刷怪器做二次确认。</para></summary>
    [MessagePackObject]
    public class HostSpawnMsg
    {
        /// <summary>形态：见 <see cref="HostSpawnKind"/>。</summary>
        [Key(0)] public int Kind;

        /// <summary>生成点（Patrol）/ 单位位置（Unit）—— 两类共用。</summary>
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
        /// <summary>水平朝向（Unit 用；单位只用 yaw，俯仰由 AI/瞄准点决定）。</summary>
        [Key(4)] public float Yaw;

        /// <summary>【Patrol】目标点类型：0 = <c>PatrolPos</c>（据点巡逻，逐单位不同），1 = <c>HomePoint</c>（任务维修巡逻，全体同一处）。</summary>
        [Key(5)] public int Sub;
        /// <summary>【Patrol】逐单位目标点（x,y,z 三元组，长度 = 单位数 × 3；Sub=1 时取第 0 组给所有单位）。</summary>
        [Key(6)] public float[] Targets;

        /// <summary>【Unit】生成者身份键（<c>INetSpawner.SpawnSourceKey</c>）—— 成员用它在本端找到"同一份生成器"。</summary>
        [Key(7)] public int SourceKey;
        /// <summary>【Unit】生成者自己表里的下标（巢穴 = creatData 序 / 召唤师 = summoned 序 / 刷怪器 = (int)tier / 环绕装甲 = 装甲表序）。</summary>
        [Key(8)] public int Slot;
        /// <summary>【Unit】房主分配的号（<c>ActorsManager.HostSpawnNetIdBase</c> 段）⇒ 成员复刻后**覆盖**自己的 NetId。</summary>
        [Key(9)] public int NetId;
        /// <summary>【Unit】首帧目标点（全 0 = 无；接收端复刻后按它设目标，避免"复刻出来原地罚站"）。</summary>
        [Key(10)] public float Tx;
        [Key(11)] public float Ty;
        [Key(12)] public float Tz;
        /// <summary>【Unit】单位的 <c>Actor.Id</c>（可为空）：刷怪器按 tier 抽表时用它精确取回同一把 prefab。</summary>
        [Key(13)] public string PrefabId;
    }

    /// <summary>
    /// 【房主 → 全体】**开波**：把"什么时候开、以什么参数开"交给房主定。
    ///
    /// <para>▍为什么要它：开波时机原本由各端本地进度驱动（本地清场 → 下一波）⇒ 两端从第一波之后就彻底错相位；
    /// 而 `WaveCreateParams.center` 常常是"玩家位置"（追击语义）⇒ 各端算出的中心天生不同。
    /// 现在：房主开波时广播这一条，成员**按同一个 <see cref="WaveIndex"/>** 在本端建同一波 ——
    /// 配合波次的种子派生流（`SeedStream.Wave` / `WaveUnits`）⇒ 两端同一时刻、同一构成、同一落点。</para>
    ///
    /// <para>▍⚠ 故意不带 <c>onEnd</c>/<c>centerGetter</c> 委托：续航由房主继续驱动（成员那波是复刻，不自己续刷）；
    /// 追击中心用 <see cref="ChaseCenter"/> 标记，成员端复现为"离中心**最近的玩家**"（两端用同一份同步位置算，答案一致）。</para>
    /// </summary>
    [MessagePackObject]
    public class WaveStartMsg
    {
        [Key(0)] public int MatchId;
        [Key(1)] public int WaveIndex;        // 本局第几波（随机细分键，必须两端一致）
        [Key(2)] public bool ExtraWave;
        [Key(3)] public bool Tip;
        [Key(4)] public float Scale;
        [Key(5)] public float Range;
        [Key(6)] public float CenterX;
        [Key(7)] public float CenterY;
        [Key(8)] public float CenterZ;
        [Key(9)] public float[] Points;       // 扁平 xyz…（null = 无预设生成点）
        [Key(10)] public bool ChaseCenter;    // 原来带 centerGetter ⇒ 成员端用"离中心最近的玩家"复现
    }


    /// <summary>【房主 → 全体】某只怪的移动意图：成员按同一个目标点各自本地算路径（NavMesh 一致 ⇒ 位置接近）。</summary>
    [MessagePackObject]
    public class EnemyMoveMsg
    {
        [Key(0)] public int NetId;
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
    }


    #endregion

    #region 其他同步


    // ============================================================
    // 局内**玩法状态**（战备呼叫 / 载具开火 / 波次 / 单位移动 / 伤害 / 护盾 / 场景 actor）
    // 房主权威 ⇒ 缺了它们两端会分叉。
    // ============================================================

    /// <summary>
    /// 呼叫战备（局内释放一次战备）：成员 -> 房主（<c>AirdropCallNtf</c>）/ 房主 -> 全体（<c>AirdropCallSync</c>）。
    /// 各端用 <c>BattleManager.ReleaseAirdrop(point, id)</c> 就地复现（那条路 <c>owner = null</c> ⇒ 不计呼叫次数）。</para>
    /// </summary>
    [MessagePackObject]
    public class AirdropCallMsg
    {
        /// <summary>权威标识：0 = 房主自己（与其它 Ntf 同口径）。接收端用它丢掉"自己发的那条"。</summary>
        [Key(0)] public uint Sid;
        /// <summary>战备 id（<c>AirdropData_SO.ID</c> / <c>ResSvc.airdropDic</c> 的键）。</summary>
        [Key(1)] public int AirdropId;
        /// <summary>落点（世界空间，各端一致的地面点）。</summary>
        [Key(2)] public float X;
        [Key(3)] public float Y;
        [Key(4)] public float Z;
        /// <summary>
        /// 信标**朝向**（Y 轴角度）。
        /// <para>▍为什么必须带：轰炸类战备（如 <c>Track_380MM</c>）的炮位/弹道是**按信标 rotation 摆的**
        /// （<c>VFXAirdropEffect.UpdateBomb</c> 用 <c>transform.rotation</c> 实例化），而 <c>TmpAirdrop</c> 只对齐位置、
        /// 不带 yaw ⇒ 远端那份炮击方向与发起方不同 ⇒ "时间同步了但位置不对"（2026-10-07 实测）。
        /// 远端复现时把它当作 <c>BattleManager.ReleaseAirdrop(point, angle, id)</c> 的 angle 用。</para>
        /// </summary>
        [Key(5)] public float Yaw;
        /// <summary>【联机 · 载具身份】本次呼叫生成物的 <c>Actor.NetId</c>（房主分配；0 = 未分配 ⇒ 该端不托管号）。
        /// <para>▍为什么要它：载具（机甲/炮台/运输机）两端**各生成一份、且生成时机不同**（呼叫方立刻生成、
        /// 其余端收到本条才生成），而落点又是各端各自打地面射线求的（会差几厘米）⇒ 号只能"房主随本条分配、
        /// 两端显式覆盖"（见 <c>ActorsManager.VehicleNetIdBase</c>）。</para>
        /// <para>⚠ **只许追加在末尾**：旧端会 <c>Skip</c> 掉它拿默认值 0 ⇒ 退化成"各端自增号"（= 改动前的行为），
        /// 于是新旧端可混联；若插到中间就会串字段。</para>
        /// </summary>
        [Key(6)] public int VehicleNetId;
    }

    /// <summary>
    /// 载具武器开火（**只做表现**：枪口闪光 + 弹道 + 命中特效；伤害由驾驶者那台本机结算）。
    /// 一律由收端本地重算（<c>FpsHelper.TryGetAimPoint</c>），本条不负责每帧更新。</para>
    /// </summary>
    [MessagePackObject]
    public class VehicleShootMsg
    {
        /// <summary>载具 <c>Actor.NetId</c>（房主随 4028 分配 ⇒ 两端一致；0 = 无效，收端丢弃）。</summary>
        [Key(0)] public int NetId;

        /// <summary>载具武器下标（<c>VehicleWeaponsManager.GetWeaponAtSlotIndex</c> 的口径：两端按**同一个持有者配置**重建，顺序一致）。</summary>
        [Key(1)] public int WeaponIndex;

        // ⚠ 开火瞬间的射击方向（世界空间、已归一化）。零向量 = 取不到 ⇒ 收端退回"枪口朝向 + 散布"。
        [Key(2)] public float DirX;
        [Key(3)] public float DirY;
        [Key(4)] public float DirZ;

        // ⚠ 驾驶者准心**实指的那一点**（世界空间）。零向量 = 未知 ⇒ 收端只用方向（见类注释）。
        [Key(5)] public float HitX;
        [Key(6)] public float HitY;
        [Key(7)] public float HitZ;

        /// <summary>开火那把武器的**伤害档位**（<c>WeaponBaseController.UseDamageIndex</c>）。
        /// 收端必须按**开枪者**的档位摆命中特效/表现弹（同 <c>PlayerShoot.DamageIndex</c> 的理由）。</summary>
        [Key(8)] public int DamageIndex;

        /// <summary>第几个发射点（<c>LastMuzzleIndex</c>；<c>-1</c> = 齐射全部发射点）。
        /// 多管武器轮流开火，收端没有那份计数 ⇒ 不发就都从 0 号管出。</summary>
        [Key(9)] public int MuzzleIndex;

        /// <summary>开火**之后**该武器的弹匣剩余（驾驶者那台的权威值；<c>&lt;0</c> = 未带/无限弹匣 ⇒ 收端不动）。
        /// <para>▍为什么要它：载具武器是**每端各一份实例**（<c>BattleApplyVehicleData</c> 按持有者配置重建），
        /// 弹量天然分叉 —— 驾驶者那台打了 10 发，另一端的副本还是满的（实测：玩家 a 打完下车、玩家 b 上车看到满弹）。
        /// 现在的口径 = **驾驶者那台才是权威**，每次开火顺手把弹量推给其余端（见
        /// <c>VehicleWeaponsManager.TryGetAmmo / ApplyRemoteAmmo</c>）。</para></summary>
        [Key(10)] public int Magazine;

        /// <summary>开火之后该武器的**后备弹**剩余（同上；<c>&lt;0</c> = 未带/无限后备 ⇒ 收端不动）。</summary>
        [Key(11)] public int Ammo;
    }

    #endregion

    #region 伤害

    /// <summary>**统一的"伤害 / 生死"事件**（房主与成员共用；三条号共挂这一个 DTO）。
    /// <para>▍方向由**到达的 cmdId** 决定：<c>EnemyHitNtf</c>(4025) = 成员上报房主；
    /// <c>EnemyDamagedSync</c>(4040) = 房主下发全体；<c>SceneDestructibleNtf</c>(4043) = 无号可破坏物的双向兜底 + 房主"已打掉"账本补发。</para>
    /// <para>⚠ 只同步"数值结果"，不重放弹道：两边各自的表现弹照旧（那是 <c>PlayerShootSync</c> 的事）。</para></summary>
    [MessagePackObject]
    public class DamageMsg
    {
        /// <summary>开枪者（房主 = 0，成员 = 其会话 sid）—— 接收端据此把受击表现归给对应的盟友实例；
        [Key(0)] public uint Sid;

        /// <summary>受害者号（0 = 无号 ⇒ 用 <see cref="Key"/> 认）。</summary>
        [Key(1)] public int NetId;

        /// <summary>无号受害者的兜底键 = <c>FNV1a(Actor.Id + 位置0.1m)</c>（同 <c>NetDestructibleBridge.KeyOf</c>）；<see cref="NetId"/> != 0 时忽略。</summary>
        [Key(2)] public int Key;

        /// <summary>最终伤害值；0 + <see cref="IsDeath"/> = 只报"死了"。</summary>
        [Key(3)] public int Damage;

        /// <summary>**起源**单位的 NetId（谁该记这笔账；0 = 未知/玩家 ⇒ 退回按 <see cref="Sid"/> 找盟友实例）。
        [Key(4)] public int AttackerNetId;

        /// <summary>伤害成分，摊平编码 <c>[count, type0, coeff0, type1, coeff1, ...]</c>（null/空 = 按中性成分结算）。
        [Key(5)] public float[] Groups;

        /// <summary>命中部位：**下标 + 1**（<c>0</c> = 未指定 ⇒ 接收端按自己的受击体挑主躯干）。
        /// <para>▍为什么要 +1：下标 <c>0</c> 本身是合法部位（<c>IActor.Damageables[0]</c>），不能拿它当"没值"。</para>
        /// 打在**同一部位**上（发送端 = <c>Actor.IndexOfDamageable(collider)</c>）。</para></summary>
        [Key(6)] public int Part;

        /// <summary> 是否击杀 </summary>
        [Key(7)] public bool IsDeath;

        /// <summary>**直接来源**单位的 NetId（屏幕上该显示谁；0 = 拿不到 ⇒ 接收端退成
        /// <see cref="AttackerNetId"/> / 按 <see cref="Sid"/> 找盟友实例）。
        [Key(8)] public int CausedNetId;

    }

    /// <summary>
    /// **背包护盾被打**（<c>ShieldBag</c> 的罩子）：把"开枪那台已经算好的最终扣减量"送到**护盾归属者**那台落地。
    ///
    /// <para>▍为什么不能走 4025/4040：护盾罩子（<c>Sphere</c>）没有 <c>Actor</c>、没有 <c>NetId</c>，
    /// 统一伤害通道按号寻址，认不到它；而它又确实是独立可破坏单位（自带 <c>Damageable</c> + <c>HealthShield</c>）。</para>
    ///
    /// <para>▍只带三样：归属者 sid（谁认领）、最终扣减量（<c>UnitEventBus.OnUnitDamaged</c> 的那个值，接收端
    /// <c>Resolved = true</c> 直接用，别再算护甲）、落点（受击特效位置）。不带护盾键 —— 每个玩家同时只装一件背包
    /// （<c>BagBase.NeedUninstall</c> 对任何 <c>Id</c> 含 "Bag" 的都返回 true）⇒ 归属者的"本人那件"唯一。</para>
    /// </summary>
    [MessagePackObject]
    public class ShieldDamageMsg
    {
        /// <summary>护盾归属者 sid（房主 = 0）。收端**只在自己是它时**应用。</summary>
        [Key(0)] public uint Sid;

        /// <summary>最终扣减量（已乘完护甲/抗性，见 <c>UnitEventBus.OnUnitDamaged</c>）。</summary>
        [Key(1)] public float Damage;

        // 受击落点（世界空间）：归装者那台用它摆受击粒子/法线，零向量 = 未知 ⇒ 用护盾中心。
        [Key(2)] public float X;
        [Key(3)] public float Y;
        [Key(4)] public float Z;
    }

    #endregion

}
