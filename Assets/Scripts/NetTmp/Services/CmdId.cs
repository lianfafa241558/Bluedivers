
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
        // ===== 通用 =====
        public const int PingReq = 1001;
        public const int PingRsp = 1002;

        // ===== 账号模块 =====
        public const int LoginReq = 2001;
        public const int LoginRsp = 2002;

        // ===== 聊天模块 =====
        /// <summary>客户端 -> 服务器</summary>
        public const int ChatSend = 3001;
        /// <summary>服务器 -> 所有客户端</summary>
        public const int ChatBroadcast = 3002;

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
        /// <summary>房主 -> 全体：某只怪死亡 ⇒ 成员干掉自己的副本</summary>
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
        /// <summary>房主 -> 全体：场景单位（NPC 这类由场景摆好、会自己走动的氛围单位）的移动目标 / 停下。
        /// ⚠ 键是 <c>Actor.Id</c>（不是 NetId）：大厅会反复重建，而每局自增的号段依赖"每局归零 + 两端创建顺序一致"。</summary>
        public const int SceneUnitMoveSync = 4032;
        /// <summary>成员 -> 房主：请求场景 actor（NPC/特殊单位）快照</summary>
        public const int SceneActorReq = 4021;
        /// <summary>房主 -> 该成员：场景 actor 快照</summary>
        public const int SceneActorNtf = 4022;

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

        // ===== 战斗·关键物体位姿同步（5000 段；房主权威"聚合 + 广播"）=====
        /// <summary>成员 -> 房主：本机权威实体的位姿（当前：本机玩家）</summary>
        public const int PlayerTransformUp = 5001;
        /// <summary>房主 -> 全体：聚合后的位姿批次（含房主自己；房主本地也自派发一次）</summary>
        public const int TransformBatchSync = 5002;
    }

}
