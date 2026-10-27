using MessagePack;

namespace FPSGame.Net
{

    // ============================================================
    // 房间 / 舰桥准备 / 开局配置（房主权威 · 局域网联机）
    // 通信模型：成员 --消息--> 房主，房主决定并转发/广播。
    // 判定口径：**房间期与舰桥**的消息放这里；局内"只同步看得见的"→ BattleMsg，
    //           局内"房主权威的玩法状态"→ SyncMsg。
    // 新增消息时在这里加 [MessagePackObject] 类，并在 CmdId 加常量。
    // ============================================================
    #region 房间相关


    /// <summary>
    /// 开房参数（<see cref="NetHostSvc.StartHost(HostRoomOptions)"/> 的入参）。
    /// </summary>
    public struct HostRoomOptions
    {
        /// <summary>房间名（列表里显示的名字，原样广播）。</summary>
        public string RoomName;
        /// <summary>地图名（原样广播，不再拼后缀）。</summary>
        public string MapName;
        /// <summary>最大人数（含房主）。</summary>
        public int MaxPlayers;
        /// <summary>房间密码（空 = 无密码）。</summary>
        public string Password;
        /// <summary>难度（<see cref="DifficultyEnum"/> 的 int；-1 = 不指定）。</summary>
        public int Difficulty;
        /// <summary>房主玩家名（显示用；广播里的 <c>PlayerNames[0]</c> 仍是用于"排除自己开的房"的合成名）。</summary>
        public string HostName;
        /// <summary>本局任务内容
        public TaskCfgDto TaskCfg;

        /// <summary>常用默认值：4 人、无密码、难度与任务未知。</summary>
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
                TaskCfg = null,
            };
        }
    }

    /// <summary>
    /// 单个玩家信息（用于玩家列表同步）。
    /// </summary>
    [MessagePackObject]
    public class PlayerInfo
    {
        [Key(0)] public uint Sid;          // 房主分配/持有的会话ID
        [Key(1)] public string PlayerName; // 玩家名
        [Key(2)] public bool IsReady;      // 是否已准备
        [Key(3)] public bool IsHost;       // 是否是房主
    }

    /// <summary>
    /// 加入房间请求：成员 -> 房主。
    /// 成员在局域网发现房间并回连成功后，用它向房主申请进房。
    /// </summary>
    [MessagePackObject]
    public class JoinRoomReq
    {
        [Key(0)] public string PlayerName; // 成员想用的名字
        [Key(1)] public string Password;   // 房间密码（没有则为空）
    }

    /// <summary>
    /// 加入房间响应：房主 -> 成员。
    /// ErrorCode==0 表示成功，否则带失败原因。
    /// </summary>
    [MessagePackObject]
    public class JoinRoomRsp
    {
        [Key(0)] public int ErrorCode;     // 0=成功，其他见错误码
        [Key(1)] public string Reason;     // 失败原因（如"房间已满""密码错误"）
        [Key(2)] public PlayerInfo Self;   // 加入成功后，自己在这个房间里的信息
        [Key(3)] public PlayerInfo[] Players; // 当前房间里所有玩家（含自己）
    }

    /// <summary>
    /// 离开房间通知：成员 -> 房主。
    /// 成员退出时告知房主，房主再广播 PlayerListSync 给其余成员。
    /// </summary>
    [MessagePackObject]
    public class LeaveRoomNtf
    {
        [Key(0)] public uint Sid;          // 离开者的会话ID（房主用它识别是谁）
    }

    /// <summary>
    /// 玩家资料（**网络镜像**，与 <c>FPSGame.Managers.PlayerData</c> 一一对应）。
    /// </summary>
    [MessagePackObject]
    public class PlayerProfile
    {
        [Key(0)] public uint Sid;            // 房主分配的会话 id（房主自己 = 0）
        [Key(1)] public string Name;         // 玩家名
        [Key(2)] public string RoleName;     // 角色 id（"Prefabs/StudentModle/" + RoleName）
        [Key(3)] public int RoleLevel;
        [Key(4)] public float RoleExp;
        [Key(5)] public int[] Airdrop;       // 战备（4 槽）
        [Key(6)] public int BoosterId;       // 全队强化 id（0 = 未选）
        [Key(7)] public bool IsReady;
        [Key(8)] public bool IsHost;
    }

    /// <summary>玩家资料上报：成员 -> 房主（角色切换 / 战备 / 强化变更后都重发自己那份）。</summary>
    [MessagePackObject]
    public class PlayerProfileNtf
    {
        [Key(0)] public PlayerProfile Profile;
    }

    /// <summary>玩家列表同步：房主 -> 全体。
    /// 每当有人加入/离开/改变准备状态时，房主把最新玩家列表广播给所有成员。
    /// <para>▍<c>Profiles</c> 与 <c>Players</c> **严格同序同长**（房主侧 <c>BuildProfileArray</c> 保证，
    /// 没上报资料的成员也给占位项）⇒ 消费方直接按下标取。</para>
    /// </summary>
    [MessagePackObject]
    public class PlayerListSync
    {
        [Key(0)] public PlayerInfo[] Players;
        [Key(1)] public PlayerProfile[] Profiles;
    }


    /// <summary>
    /// 某成员离开（房主 -> 全体）。**只带 sid、不重排名单** —— 战斗期名单是冻结的
    /// </summary>
    [MessagePackObject]
    public class PlayerLeftMsg
    {
        /// <summary>离开者的会话 sid（= 各端 <c>PlayerInfo.Sid</c> / 盟友实例的 key）。</summary>
        [Key(0)] public uint Sid;
    }

    #endregion

    #region NPC同步

    /// <summary>
    /// 【场景 actor 快照】参与联机时由**后加入的一方**拉取：房主把场上"非玩家单位"的位置与朝向发过去
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

    /// <summary>【房主 → 全体】场景单位（NPC 这类由场景摆好、会自己走动的氛围单位）的移动目标 / 停下。
    ///
    /// <para>▍为什么键是 <see cref="Id"/> 而不是 NetId：这类单位只在大厅/场景里摆着，回大厅会**整批重建**，
    /// 而 NetId 的口径是"每局归零 + 两端创建顺序一致"⇒ 在大厅不成立；而 <c>Actor.Id</c> 在两端同一份场景里
    /// 天然一致（现有 <see cref="SceneActorSync"/> 也拿它当"匹配主键"）。</para></summary>
    [MessagePackObject]
    public class SceneUnitMoveMsg
    {
        /// <summary>匹配主键：<c>Actor.Id</c>（如 <c>Noa</c> / <c>Mika</c>）。</summary>
        [Key(0)] public string Id;
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
        /// <summary>true = 就地停下（谈话/剧情定住 NPC）⇒ 接收端 <c>ResetPath</c>，不要"走过去"。</summary>
        [Key(4)] public bool Stop;
    }
    #endregion

    #region 任务相关
    /// <summary>
    /// 准备状态：成员 -> 房主。
    /// 成员点击"准备/取消准备"时上报，房主汇总后广播 PlayerListSync。
    /// </summary>
    [MessagePackObject]
    public class ReadyState
    {
        [Key(0)] public bool IsReady;
    }


    /// <summary>
    /// 本局任务配置广播：`Bridge`（选任务）→ **本消息** 
    /// </summary>
    [MessagePackObject]
    public class TaskConfirmNtf
    {
        [Key(0)] public string MapName;    // 要加载的地图
        [Key(1)] public int Difficulty;        // DifficultyEnum 的 int
        [Key(2)] public int[] ExtraDiff;       // 额外难度（固定 4 项，见 TaskState.ExtraDifficulty）
        [Key(3)] public int Seed;              // 随机种子（同局所有玩家一致，供 BattleRandom）
        [Key(4)] public int PlayMode = 2;      // 0=加入 1=公开 2=单人（对应 SelectMapWnd.SelectPlayMode）
        [Key(5)] public TaskCfgDto Cfg;        // ★ 选中项 TaskCfg 的**内容**（必须有；null / Main<0 = 房主侧 bug，成员直接拒收）
        [Key(6)] public int MatchId;           // 本局局号（房主每次"确认本局配置"时自增）⇒ 幂等 + 后续战斗消息的局标识
        [Key(7)] public int Phase;             // ★ 房主**当前所在阶段**（`GameStateEnum` 的位值：4 = Ready、128 = Armament；0 = 未提供）
    }

    /// <summary>
    /// **本局任务配置的"内容"**（房主 → 成员，随 <see cref="TaskConfirmNtf.Cfg"/> 下发）。
    /// </summary>
    [MessagePackObject]
    public class TaskCfgDto
    {
        [Key(0)] public int Main;              // MissionEnum；必须 >= 0（房主侧由 TaskManager.ToDto 填）
        [Key(1)] public int[] Extra;           // MissionEnum[]（额外任务，长度 = 主任务的 sizeType 档位）
        [Key(2)] public int[] NestCount;       // int[3]（巢穴数，固定 3 档）
        [Key(3)] public int Seed;              // TaskCfg.seed（**任务自身**种子，≠ TaskConfirmNtf.Seed 那个"本局权威种子"）
        [Key(4)] public float Scale;           // 奖励缩放
        [Key(5)] public int Terrain;           // TerrainType
        [Key(6)] public int EnemyVariety;      // EnemyVarietyType
        [Key(7)] public bool Enable;           // 该任务项是否启用（展示用）
        [Key(8)] public string Name;           // 任务名（展示用，形如"深渊回响"）
    }

    /// <summary>
    /// **进入 Transition**（Armament 结束，**同时开始加载战斗场景**）：房主 -> 全体。
    /// 房主补一发权威通知，避免某一端因为"战备窗没开 / 准备状态没收全"卡在舰桥。
    /// 成员收到后做什么：只把自己的 GameState 推到 Transition
    /// </summary>
    [MessagePackObject]
    public class TransitionNtf
    {
        /// <summary>本局局号（= 刚才那条 <see cref="TaskConfirmNtf.MatchId"/>；0 = 未提供）。仅用于日志与幂等判定。</summary>
        [Key(0)] public int MatchId;
    }

    /// <summary>
    /// **本机战斗场景加载完成**（成员 -> 房主）。
    /// <para>▍房主收齐（或超时 / 有人掉线被清退）后广播 <see cref="BattleStartMsg"/> 才真正开打。</para>
    /// </summary>
    [MessagePackObject]
    public class LoadCompleteMsg
    {
        /// <summary>本局局号（= <see cref="TaskConfirmNtf.MatchId"/>；0 = 未提供）。仅用于日志与幂等判定。</summary>
        [Key(0)] public int MatchId;
    }

    /// <summary>
    /// **全体加载完成 ⇒ 一起开打**（房主 -> 全体）。
    /// <para>▍接收端动作：把 <c>BattleManager.IsStartBattle</c>（和自持的 <c>BattleState.IsStartBattle</c>）打开。
    /// 房主自己也收（<see cref="NetHostSvc"/> 广播后会本地自派发一次），判据因此只有一处。</para>
    /// </summary>
    [MessagePackObject]
    public class BattleStartMsg
    {
        /// <summary>本局局号（= <see cref="TaskConfirmNtf.MatchId"/>；0 = 未提供）。</summary>
        [Key(0)] public int MatchId;
    }

    /// <summary>
    /// **单个载具的改装**（联机镜像，与 <c>ArchivesData_SO.ArchVehicleData</c> 一一对应）。
    /// </summary>
    [MessagePackObject]
    public class VehicleCustomDto
    {
        [Key(0)] public string VehicleName;
        [Key(1)] public int LeftWeaponIndex;
        [Key(2)] public int RightWeaponIndex;
        [Key(3)] public int SkinIndex;
        [Key(4)] public int BlendIndex;
        [Key(5)] public float BlendScale;
    }

    /// <summary>
    /// **玩家配置同步**：武器选择 + 武器改装（档位 + 模组）+ 载具改装。
    /// </summary>
    [MessagePackObject]
    public class PlayerLoadoutMsg
    {
        /// <summary>权威标识（房主 = 0，成员 = 其会话 sid；由房主/发送方填，接收端不要采信 DTO 里的值）。</summary>
        [Key(0)] public uint Sid;

        /// <summary>武器改装：每类武器（下标 = <c>WeaponTypeEnum</c>）每档**选中的改装项**；与
        /// <c>PlayerData.Upgrades</c> 同形（-1 = 该档未选）。</summary>
        [Key(1)] public int[][] Upgrades;

        /// <summary>武器模组：每类武器**选中的模组下标**（-1/越界 = 无模组）。
        [Key(2)] public int[] Modules;

        /// <summary>载具改装（按 vehicleName 键控；null/空 = 没同步过 ⇒ 消费方退回本机存档）。</summary>
        [Key(3)] public VehicleCustomDto[] Vehicles;

        /// <summary>武器选择：每类武器（下标 = <c>WeaponTypeEnum</c>）选中的那把武器下标；与
        /// <c>ArchivesData_SO.GetWeaponSelect</c> 同形。
        [Key(4)] public int[] Weapons;
    }


    /// <summary>
    /// **战备项变更**（战备 / 全队强化**共用一条**；靠命令号区分）。
    /// </summary>
    [MessagePackObject]
    public class PlayerSetupMsg
    {
        /// <summary>全队强化占用的槽位号：0-3 = 战备格，4 = 强化。**别散魔法数字**。</summary>
        public const int BoosterSlot = 4;

        /// <summary>权威标识（房主 = 0）。</summary>
        [Key(0)] public uint Sid;
        /// <summary>槽位：0-3 = 战备格 / 4 = 全队强化（见 <see cref="BoosterSlot"/>）。</summary>
        [Key(1)] public int SlotIndex;
        /// <summary>战备 id 或强化 id（0 = 清空该槽）。</summary>
        [Key(2)] public int Id;
    }

    #endregion

}
