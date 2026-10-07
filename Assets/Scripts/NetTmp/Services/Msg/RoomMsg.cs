using MessagePack;

namespace FPSGame.Net
{

// ============================================================
// 房间业务消息（房主权威 / 局域网联机）
// 通信模型：成员 --消息--> 房主，房主决定并转发/广播。
// 新增房间消息时在这里加 [MessagePackObject] 类，并在 CmdId 加常量。
// ============================================================

/// <summary>
/// 开房参数（<see cref="NetHostSvc.StartHost(HostRoomOptions)"/> 的入参）。
///
/// <para>▍为什么用结构体而不是继续加参数：房间名 / 地图 / 人数 / 密码 / 难度 / 房主名
/// 已经 6 项，继续展开会让方法签名超长且调用点极易传错位。这里一次性打包，
/// 顺便让"以后再加一项"不再改签名。</para>
/// </summary>
public struct HostRoomOptions
{
    /// <summary>房间名（列表里显示的名字；⚠ 库缺字段期间由 <see cref="RoomMeta.ComposeRoomName"/> 拼上 "#T=任务类型" 后缀）。</summary>
    public string RoomName;
    /// <summary>地图名（⚠ 库缺难度字段期间由 <see cref="RoomMeta.ComposeMapName"/> 拼上 "#难度" 后缀）。</summary>
    public string MapName;
    /// <summary>最大人数（含房主）。</summary>
    public int MaxPlayers;
    /// <summary>房间密码（空 = 无密码）。</summary>
    public string Password;
    /// <summary>难度（<see cref="DifficultyEnum"/> 的 int；-1 = 不指定）。</summary>
    public int Difficulty;
    /// <summary>房主玩家名（显示用；广播里的 <c>PlayerNames[0]</c> 仍是用于"排除自己开的房"的合成名）。</summary>
    public string HostName;
    /// <summary>本局**任务类型名**（如「歼灭」，取 <c>TaskCfg.TaskType</c>；空 = 还没选任务）。
    /// ⚠ 库缺字段期间由 <see cref="RoomMeta.ComposeRoomName"/> 拼进 <see cref="RoomName"/> ⇒ 房间列表能显示出来。</summary>
    public string TaskType;
    /// <summary>本局**主任务类型枚举值**（<c>MissionEnum</c> 的 int；-1 = 未知/还没选任务）。
    /// <para>▍为什么要它：任务类型**名字不唯一**（`GameData/Mission/Main` 里 3 份「进攻任务」颜色各不相同）
    /// ⇒ 房间列表要**精确**取图标/颜色只能靠枚举；与 <see cref="TaskType"/> 一起拼进房间名
    /// （<c>"#T=枚举|名字"</c>，见 <see cref="RoomMeta.ComposeRoomName"/>）。</para>
    /// ⚠ 这里用 <c>int</c> 而非 <c>MissionEnum</c>：本结构体在 <c>02_Net</c>，看不见 <c>01_GameContract</c> 的枚举。</summary>
    public int TaskMain;

    /// <summary>常用默认值：4 人、无密码、难度与任务类型未知。</summary>
    public static HostRoomOptions Default(string roomName, string mapName)
    {
        return new HostRoomOptions
        {
            RoomName = roomName,
            MapName = mapName,
            MaxPlayers = 4,
            Password = "",
            Difficulty = -1,
            HostName = "",
            TaskType = "",
            TaskMain = -1,
        };
    }
}

/// <summary>
/// 单个玩家信息（用于玩家列表同步）。
/// </summary>
[MessagePackObject]
public class PlayerInfo
{
    [Key(0)]
    public uint Sid;          // 房主分配/持有的会话ID
    [Key(1)]
    public string PlayerName; // 玩家名
    [Key(2)]
    public bool IsReady;      // 是否已准备
    [Key(3)]
    public bool IsHost;       // 是否是房主
}

/// <summary>
/// 加入房间请求：成员 -> 房主。
/// 成员在局域网发现房间并回连成功后，用它向房主申请进房。
/// </summary>
[MessagePackObject]
public class JoinRoomReq
{
    [Key(0)]
    public string PlayerName; // 成员想用的名字
    [Key(1)]
    public string Password;   // 房间密码（没有则为空）
}

/// <summary>
/// 加入房间响应：房主 -> 成员。
/// ErrorCode==0 表示成功，否则带失败原因。
/// </summary>
[MessagePackObject]
public class JoinRoomRsp
{
    [Key(0)]
    public int ErrorCode;     // 0=成功，其他见错误码
    [Key(1)]
    public string Reason;     // 失败原因（如"房间已满""密码错误"）
    [Key(2)]
    public PlayerInfo Self;   // 加入成功后，自己在这个房间里的信息
    [Key(3)]
    public PlayerInfo[] Players; // 当前房间里所有玩家（含自己）
}

/// <summary>
/// 离开房间通知：成员 -> 房主。
/// 成员退出时告知房主，房主再广播 PlayerListSync 给其余成员。
/// </summary>
[MessagePackObject]
public class LeaveRoomNtf
{
    [Key(0)]
    public uint Sid;          // 离开者的会话ID（房主用它识别是谁）
}

/// <summary>
/// 玩家资料（**网络镜像**，与 <c>FPSGame.Managers.PlayerData</c> 一一对应）。
///
/// <para>▍为什么要平行一份：<c>PlayerData</c> 声明在 <c>09Manager/Global/TeamManager.cs</c>
/// （namespace <c>FPSGame.Managers</c>），而本程序集 <c>02_Net</c> **看不见 09_Managers**
/// （编号小的不能引用编号大的）⇒ 网络层只能用自己的 DTO 收发，
/// 由 09 侧的桥（<c>TeamNetBridge</c>）在 `PlayerProfile ↔ PlayerData` 之间做映射。</para>
/// </summary>
[MessagePackObject]
public class PlayerProfile
{
    [Key(0)]
    public uint Sid;            // 房主分配的会话 id（房主自己 = 0）
    [Key(1)]
    public string Name;         // 玩家名
    [Key(2)]
    public string RoleName;     // 角色 id（"Prefabs/StudentModle/" + RoleName）
    [Key(3)]
    public int RoleLevel;
    [Key(4)]
    public float RoleExp;
    [Key(5)]
    public int[] Weapons;       // 角色携带武器
    [Key(6)]
    public int[][] Upgrades;    // 每把武器的改装
    [Key(7)]
    public int[] Airdrop;       // 战备（4 槽）
    [Key(8)]
    public int BoosterId;       // 全队强化 id（0 = 未选）
    [Key(9)]
    public bool IsReady;
    [Key(10)]
    public bool IsHost;
}

/// <summary>玩家资料上报：成员 -> 房主（角色切换 / 战备 / 强化变更后都重发自己那份）。</summary>
[MessagePackObject]
public class PlayerProfileNtf
{
    [Key(0)]
    public PlayerProfile Profile;
}

/// <summary>玩家列表同步：房主 -> 全体。
/// 每当有人加入/离开/改变准备状态时，房主把最新玩家列表广播给所有成员。
/// <para>▍<c>Profiles</c> 是后加的（<c>[Key(1)]</c>）：旧版本端缺 key 拿到 null ⇒ 消费方要判空。</para>
/// </summary>
[MessagePackObject]
public class PlayerListSync
{
    [Key(0)]
    public PlayerInfo[] Players;
    /// <summary>与 <see cref="Players"/> **同序**的详细资料（含角色/武器/战备/强化）。</summary>
    [Key(1)]
    public PlayerProfile[] Profiles;
}

/// <summary>选择战备：成员 -> 房主（房主自己不走这条，见 <see cref="PlayerArmamentSync"/>）。
/// <para>⚠ <c>Sid</c> 是**权威标识**（房主填），因为 <c>PlayerIndex</c> 是"房主视角的下标"，
/// 而各端本地 <c>TeamManager.players</c> 的下标是自己视角（自己永远在 0）⇒ 接收端要按 Sid 映射本地下标。</para></summary>
[MessagePackObject]
public class PlayerArmamentNtf
{
    [Key(0)]
    public int PlayerIndex;
    [Key(1)]
    public int AirdropId;
    [Key(2)]
    public int SlotIndex;
    [Key(3)]
    public uint Sid;
}

/// <summary>战备同步：房主 -> 全体（含房主自己，房主发送时本地也会走一遍处理）。</summary>
[MessagePackObject]
public class PlayerArmamentSync
{
    [Key(0)]
    public int PlayerIndex;
    [Key(1)]
    public int AirdropId;
    [Key(2)]
    public int SlotIndex;
    [Key(3)]
    public uint Sid;
}

/// <summary>
/// 局内切枪：成员 -> 房主 / 房主 -> 全体（**同一个 DTO 两个方向**都用，靠命令号区分）。
/// <para>▍<c>SlotIndex</c> 用 <c>PlayerWeaponsManager</c> 的槽位口径（0-8，类型→槽位映射见其 <c>SlotOf</c>）。</para>
/// </summary>
[MessagePackObject]
public class PlayerWeaponSwitch
{
    [Key(0)]
    public uint Sid;          // 权威标识（房主填：0 = 房主自己）
    [Key(1)]
    public int SlotIndex;     // 当前武器槽位
}

/// <summary>
/// 开火（**只做表现同步**：枪口闪光/音效/枪械动画；伤害由开枪者本机结算，这里不重放）。
/// <para>▍为什么不带方向/命中：那是弹道与判定的同步，属于战斗同步的另一条线（见关键物体同步计划）；
/// 表现层只需要"哪把枪在什么时候响了"。</para>
/// </summary>
[MessagePackObject]
public class PlayerShoot
{
    [Key(0)]
    public uint Sid;
    [Key(1)]
    public int SlotIndex;     // 开枪时用的槽位（接收端按它让对应武器的枪口闪）

    // ⚠ 下面三个是**开火瞬间的射击方向**（世界空间、已归一化）。
    //   只有 SlotIndex 时，接收端只能用"枪口当前朝向"发子弹；而盟友模型只同步了 yaw、没有俯仰
    //   ⇒ 弹道永远是水平的（抬头打空中目标时明显不对）。零向量 = 旧版发送端 ⇒ 接收端退回枪口朝向。
    [Key(2)] public float DirX;
    [Key(3)] public float DirY;
    [Key(4)] public float DirZ;

    // ⚠ 这三个是**目标点**（开枪者准心实际指到的那一点，世界空间）。
    //   ▍为什么光有方向不够：接收端的"表现弹"是**本地模拟**的（`SpawnVisualBullet`），只给方向时它会沿那把枪的
    //   枪口方向飞，落点取决于本端地形与枪口偏移 ⇒ 与开枪者看到的落点不一致（2026-10-07 用户实测
    //   "盟友开枪的目标点没有同步成功"）。带上目标点后，接收端把方向改成"枪口 → 目标点"
    //   ⇒ 弹道**穿过同一个点**，落点看起来就对了。(0,0,0) = 旧版发送端 ⇒ 退回方向。
    [Key(5)] public float HitX;
    [Key(6)] public float HitY;
    [Key(7)] public float HitZ;
}

/// <summary>
/// 呼叫战备（局内释放一次战备）：成员 -> 房主（<c>AirdropCallNtf</c>）/ 房主 -> 全体（<c>AirdropCallSync</c>）。
///
/// <para>▍为什么要同步：战备是**各端各自模拟**的（<c>AirdropController</c> 只管本机玩家那份 <c>useAd</c>），
/// 所以"我呼叫了一次战备"对别人是完全不可见的 —— 别人看不到信标、看不到空投舱
/// （2026-10-07 用户实测"呼叫战备没有同步"）。这里只同步**结果**（哪个战备 + 落在哪），
/// 各端用 <c>BattleManager.ReleaseAirdrop(point, id)</c> 就地复现（那条路 <c>owner = null</c> ⇒ 不计呼叫次数）。</para>
/// </summary>
[MessagePackObject]
public class AirdropCallMsg
{
    /// <summary>权威标识：0 = 房主自己（与其它 Ntf 同口径）。接收端用它丢掉"自己发的那条"。</summary>
    [Key(0)]
    public uint Sid;
    /// <summary>战备 id（<c>AirdropData_SO.ID</c> / <c>ResSvc.airdropDic</c> 的键）。</summary>
    [Key(1)]
    public int AirdropId;
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
}

/// <summary>
/// 某成员离开（房主 -> 全体）。**只带 sid、不重排名单** —— 战斗期名单是冻结的
/// （重排会让 <c>players</c> 下标漂移，而 BattleData/战备窗都按下标假设），
/// 但"人走了"必须让各端知道：销毁那个盟友实例、HUD 那行要跟着变
/// （2026-10-07 实测："战斗中其他客户端强退后模型没消失"）。
/// </summary>
[MessagePackObject]
public class PlayerLeftMsg
{
    /// <summary>离开者的会话 sid（= 各端 <c>PlayerInfo.Sid</c> / 盟友实例的 key）。</summary>
    [Key(0)]
    public uint Sid;
}

/// <summary>
/// 角色喊话（<c>SpeechTypeEnum</c>）：成员 -> 房主（<c>SpeechNtf</c>）/ 房主 -> 全体（<c>SpeechSync</c>）。
///
/// <para>▍为什么要同步：喊话是纯本机表现（<c>PlayerSpeechManager</c> 直接 <c>GlobalEventSub.ActorSpeech</c>），
/// 而它同时决定"字幕 + 语音"，别人听不见就成了各说各话（2026-10-07 用户实测"角色的喊话没有同步"）。
/// 这里只同步**类型**：具体哪条由接收端用**喊话者那个角色的** <c>RoleData_SO.SpeechGroup(type).Get(位置)</c> 取
/// （各端配置一致 ⇒ 取到同一条）。</para>
/// </summary>
[MessagePackObject]
public class PlayerSpeechMsg
{
    /// <summary>权威标识：0 = 房主自己（与其它 Ntf 同口径）。接收端用它丢掉"自己发的那条"。</summary>
    [Key(0)]
    public uint Sid;
    /// <summary><c>SpeechTypeEnum</c> 的 int（02_Net 看不见玩法层枚举）。</summary>
    [Key(1)]
    public int Speech;
}

/// <summary>
/// 【场景 actor 快照】参与联机时由**后加入的一方**拉取：房主把场上"非玩家单位"
/// （<c>ActorsManager.SpecUnits</c>：NPC / 特殊单位这类由场景生成、会自己走动的东西）的位置与朝向发过去。
///
/// <para>▍为什么需要：每个客户端各自模拟世界，玩家位姿有 20Hz 连续同步，而这些场景单位**只在各端本地走动**
/// （<c>NPCWalk</c> 随机游荡）⇒ 后加入的人会看到 NPC 停在出生点，与房主那边完全对不上。</para>
///
/// <para>▍只发一条、不做持续同步：场景单位是"氛围物"，不需要毫秒级一致；
/// 真正要一致的（玩家）已经有位姿通道。这样既解决了"刚进来看到的位置全错"，也不引入新流量。</para>
/// </summary>
[MessagePackObject]
public class SceneActorEntry
{
    [Key(0)] public string Id;        // 身份（= Actor.Id，跨端稳定：来自角色/模型配置）⇒ **匹配主键**
    [Key(1)] public int IndexID;      // ⚠ 各端自增/注册序，**不保证跨端一致**（同 KeyScreenControl 的注释）⇒ 只作参考
    [Key(2)] public float X;
    [Key(3)] public float Y;
    [Key(4)] public float Z;
    [Key(5)] public float Yaw;
}

/// <summary>场景 actor 快照（同型双向：成员请求 / 房主回应）。</summary>
[MessagePackObject]
public class SceneActorSync
{
    [Key(0)] public SceneActorEntry[] Actors;
}

/// <summary>
/// 玩家生命状态（血 / 盾 / 是否倒地）：成员 -> 房主 / 房主 -> 全体（同型双向）。
///
/// <para>▍为什么同步的是"数值 + 上限"而不是比例：各端用自己的角色配置算出的上限**可能不同**（强化/装备差异），
/// 传比例会让"盟友头像血条"与他自己看到的不一致；直接传绝对值 + 上限，两端显示必然相同。</para>
///
/// <para>▍为什么只同步"表现所需"：伤害与死亡判定由**各自客户端**权威（本地玩家的血量是自己算的），
/// 这里只把结果播给同房间的人看 —— 盟友实体在别人机器上不会被本地敌人扣血（它没有 <c>Damageable</c>）。</para>
/// </summary>
[MessagePackObject]
public class PlayerVital
{
    [Key(0)] public uint Sid;         // 权威标识（房主填：0 = 房主自己）
    [Key(1)] public float Hp;
    [Key(2)] public float HpMax;
    [Key(3)] public float Shield;
    [Key(4)] public float ShieldMax;
    [Key(5)] public bool Down;        // 倒地（= 本人 ActorState.Dead），驱动盟友的倒地姿态
}

/// <summary>
/// 玩家弹药系数（0~1）：成员 -> 房主 / 房主 -> 全体（同型双向）。
///
/// <para>▍为什么**单独一条**、不并进 <see cref="PlayerVital"/>：它的变化节奏和血盾完全不同 ——
/// 每开一枪就变一次，而血盾必须"变化即达"（倒地要立刻看见）。合并就得在一条通道里塞两套节流规则，
/// 分开后：血盾走变化即发 + 0.5s 对账，弹药走 10Hz 上限 + 变化判定，互不打扰。</para>
///
/// <para>▍口径：<c>PlayerWeaponsManager.TotalRemainAmmoRatio()</c>（全部槽位的剩余弹药/总容量），
/// 与 <c>PlayerWnd</c> 里本机自己那根弹药条同源 ⇒ 盟友条与自己的条口径一致。</para>
/// </summary>
[MessagePackObject]
public class PlayerAmmo
{
    [Key(0)] public uint Sid;
    [Key(1)] public float Ratio;      // 总剩余弹药比例 0~1
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

/// <summary>【成员 → 房主】本机打中了某只怪 ⇒ 上报房主结算（血量/死亡以房主为唯一权威）。</summary>
[MessagePackObject]
public class EnemyHitMsg
{
    [Key(0)] public uint Sid;
    [Key(1)] public int NetId;
    [Key(2)] public int Damage;
}

/// <summary>【房主 → 全体】某只怪死亡 ⇒ 成员干掉自己那份副本（保证在场敌人集合一致）。</summary>
[MessagePackObject]
public class EnemyDiedMsg
{
    [Key(0)] public int NetId;
}

/// <summary>选择全队强化：成员 -> 房主。</summary>
[MessagePackObject]
public class PlayerBoosterNtf
{
    [Key(0)]
    public int PlayerIndex;
    [Key(1)]
    public int BoosterId;
    [Key(2)]
    public uint Sid;
}

/// <summary>全队强化同步：房主 -> 全体。</summary>
[MessagePackObject]
public class PlayerBoosterSync
{
    [Key(0)]
    public int PlayerIndex;
    [Key(1)]
    public int BoosterId;
    [Key(2)]
    public uint Sid;
}

/// <summary>
/// 准备状态：成员 -> 房主。
/// 成员点击"准备/取消准备"时上报，房主汇总后广播 PlayerListSync。
/// </summary>
[MessagePackObject]
public class ReadyState
{
    [Key(0)]
    public bool IsReady;      // true=准备，false=取消准备
}

/// <summary>
/// **本局配置确认**：房主 -> 全体。
///
/// <para>▍阶段位置（用户 2026-10-06 口径）：`Bridge`（选任务）→ **本消息** → `Ready`（等所有人就位，**双方都还在舰桥**）
/// → `Armament`（舰桥里各自选战备）→ `Transition`（同时加载战斗场景，见 <see cref="TransitionNtf"/>）→ `Game`。
/// ⇒ 它**不是"开始游戏"**，成员收到后只落配置（`SetSeed` + `SetTask`），**不加载场景**。</para>
///
/// <para>▍为什么要带这些字段：成员要复现房主那一局，得知道难度 / 任务下标 /
/// 额外难度 / 随机种子（<c>TaskManager.SetTask(mapId, taskIndex, difficulty, extraDiff, playMode)</c>）。
/// 新字段**追加在末尾**并取下一个 Key，缺 key 的旧版本端会拿到默认值（MessagePack 语义）。</para>
/// </summary>
[MessagePackObject]
public class TaskConfirmNtf
{
    [Key(0)]
    public string MapName;    // 要加载的地图
    [Key(1)]
    public int Difficulty = -1;   // DifficultyEnum 的 int；-1 = 未指定（旧版房主）
    [Key(2)]
    public int TaskIndex = -1;    // 该地图上的任务下标（TaskManager.TaskCfgs[mapIndex, taskIndex]）
    [Key(3)]
    public int[] ExtraDiff;       // 额外难度（固定 4 项，见 TaskState.ExtraDifficulty）
    [Key(4)]
    public int Seed;              // 随机种子（同局所有玩家一致，供 BattleRandom）
    [Key(5)]
    public int PlayMode = 2;      // 0=加入 1=公开 2=单人（对应 SelectMapWnd.SelectPlayMode）
    [Key(6)]
    public int TaskFingerprint;   // 房主"选中项 TaskCfg"的指纹（0 = 未提供/旧版房主）⇒ 成员比对，不一致就明确报错
    [Key(7)]
    public int MatchId;           // 本局局号（房主每次"确认本局配置"时自增，0 = 未提供）⇒ 幂等 + 后续战斗消息的局标识
    [Key(8)]
    public TaskCfgDto Cfg;        // ★ 选中项 TaskCfg 的**内容**（null = 旧版房主 ⇒ 成员退回"按本地表下标取"，跨窗口会错位；见 TaskCfgDto）
}

/// <summary>
/// **本局任务配置的"内容"**（房主 → 成员，随 <see cref="TaskConfirmNtf.Cfg"/> 下发）。
///
/// <para>▍为什么必须下发"内容"而不是只要"下标"：<c>TaskManager.TaskCfgs</c> 是**各端本地**按
/// 时间窗口（发布版 30 分钟）生成的临时物 —— 跨窗口、或刚刷过表时，同一个 <c>TaskIndex</c>
/// 会指向**另一个任务**（静默错位，2026-10-06 实测踩到："任务表不同步，已中止开局"）。
/// 而本局配置一旦确认就该**冻结**：后进者即使本地表里已经没有这个任务，也要能照打
/// （经 <c>TaskManager.SetTask(..., remoteCfg)</c> 的"以内容为准"分支复现）。</para>
///
/// <para>▍为什么字段全是 int / int[] 而不是 <c>MissionEnum</c> 等：02_Net 的 asmdef
/// <c>references</c> 为空，**看不见** <c>TaskCfg</c>/<c>MissionEnum</c>/<c>TerrainType</c>
/// （它们在 06_Gameplay / 04_Data）⇒ DTO 必须自足，由 09_Managers 侧做
/// <c>TaskManager.ToDto</c> / <c>TaskManager.FromDto</c> 的转换。</para>
/// </summary>
[MessagePackObject]
public class TaskCfgDto
{
    [Key(0)]
    public int Main = -1;         // MissionEnum；-1 = 未提供（旧版房主）⇒ 成员按本地表下标取
    [Key(1)]
    public int[] Extra;           // MissionEnum[]（额外任务，长度 = 主任务的 sizeType 档位）
    [Key(2)]
    public int[] NestCount;       // int[3]（巢穴数，固定 3 档）
    [Key(3)]
    public int Seed;              // TaskCfg.seed（**任务自身**种子，≠ TaskConfirmNtf.Seed 那个"本局权威种子"）
    [Key(4)]
    public float Scale;           // 奖励缩放
    [Key(5)]
    public int Terrain;           // TerrainType
    [Key(6)]
    public int EnemyVariety;      // EnemyVarietyType
    [Key(7)]
    public bool Enable;           // 该任务项是否启用（展示用）
    [Key(8)]
    public string Name;           // 任务名（展示用，形如"深渊回响"）
}

/// <summary>
/// **进入 Transition**（Armament 结束，**同时开始加载战斗场景**）：房主 -> 全体。
///
/// <para>▍为什么需要它：Armament 阶段"所有人都准备"原本是**每端各自**从同步到的准备状态推出来的
/// （<c>ArmamentWnd</c> 播 Exit → 动画事件 <c>FinishReady()</c> → <c>GameState = Transition</c>）。
/// 房主再补一发权威通知，避免某一端因为"战备窗没开 / 准备状态没收全"卡在舰桥。</para>
///
/// <para>▍成员收到后做什么：**只把自己的 <c>GameState</c> 推到 <c>Transition</c>**，
/// 加载由大厅既有的 <c>GameStateController</c>（<c>state: 8</c>）→ <c>TransSceneController.StartLoad()</c>
/// 承接（自己再调 <c>AsyncLoadScene</c> 就是双加载）。<c>GameRoot.GameState</c> 的 setter 自带
/// "值没变就不发事件"判定 ⇒ 两条路径谁先到都不会重复加载。</para>
/// </summary>
[MessagePackObject]
public class TransitionNtf
{
    /// <summary>本局局号（= 刚才那条 <see cref="TaskConfirmNtf.MatchId"/>；0 = 未提供）。仅用于日志与幂等判定。</summary>
    [Key(0)]
    public int MatchId;
}
}
