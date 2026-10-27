using MessagePack;

namespace FPSGame.Net
{
    // ============================================================
    // 局内**表现**消息（位姿 + 切枪 / 开火 / 生命 / 救援 / 弹药）
    // 判定口径：**只同步"看得见的"** —— 缺了它们玩法照跑；
    //           房主权威的玩法状态（伤害 / 刷怪 / 波次 / 护盾…）在 SyncMsg。
    // ============================================================
    #region 位姿
    /// <summary>
    /// 一个实体的**位姿快照**（联机高频消息里字段要尽量少）。
    /// <para>▍⚠ 还没做量化压缩：<c>float</c> × 5 = 20 字节/实体。按 4 人 × 20Hz ≈ 1.6 KB/s 完全可以接受；
    /// 等实体数量上去（敌人也要同步时）再按位置同步计划的方案换成"定点 int16 + 分桶"。</para>
    /// </summary>
    [MessagePackObject]
    public class PoseSnapshot
    {
        /// <summary>权威标识：玩家 = 会话 sid（房主 = 0）。</summary>
        [Key(0)] public uint Sid;
        [Key(1)] public float X;
        [Key(2)] public float Y;
        [Key(3)] public float Z;
        /// <summary>朝向（Y 轴角度，度）。</summary>
        [Key(4)] public float Yaw;
        /// <summary>上身/武器的俯仰角（度）</summary>
        [Key(5)] public float Pitch;
        /// <summary>是否为传送</summary>
        [Key(6)] public bool Teleport;
    }


    /// <summary>
    /// 位姿**批次**：成员上行时只装 1 项；房主下行时是"聚合后的全体"。
    /// 房主按一定频率批发全部信息，来降低包体数量。
    /// </summary>
    [MessagePackObject]
    public class PoseBatchMsg
    {
        /// <summary>本局局号（0 = 大厅 / 尚未开局；接收端可用它丢弃上一局的残留包）。</summary>
        [Key(0)] public int MatchId;
        [Key(1)] public PoseSnapshot[] Items;
    }

    #endregion


    // ============================================================
    // 局内**表现**同步（ 开火 / 生命 / 救援 / 弹药）
    // 只同步"看得见的东西"：缺了它们玩法照跑，所以不进 SyncMsg 的状态流。
    // ============================================================

    /// <summary>
    /// 局内切枪：成员 -> 房主 / 房主 -> 全体（**同一个 DTO 两个方向**都用，靠命令号区分）。
    /// <para>▍<c>SlotIndex</c> 用 <c>PlayerWeaponsManager</c> 的槽位口径（0-8，类型→槽位映射见其 <c>SlotOf</c>）。</para>
    /// </summary>
    [MessagePackObject]
    public class PlayerWeaponSwitch
    {
        [Key(0)] public uint Sid;          // 权威标识（房主填：0 = 房主自己）
        [Key(1)] public int SlotIndex;     // 当前武器槽位
    }

    /// <summary>
    /// 开火（**只做表现同步**：枪口闪光/音效/枪械动画/表现弹；伤害由开枪者本机结算，这里不重放）。
    /// </summary>
    [MessagePackObject]
    public class PlayerShoot
    {
        [Key(0)] public uint Sid;
        [Key(1)] public int SlotIndex;     // 开枪时用的槽位（接收端按它让对应武器的枪口闪）

        // ⚠ 下面三个是**开火瞬间的射击方向**（世界空间、已归一化）。
        [Key(2)] public float DirX;
        [Key(3)] public float DirY;
        [Key(4)] public float DirZ;

        // ⚠ 这三个是**目标点**（开枪者准心实际指到的那一点，世界空间）。
        [Key(5)] public float HitX;
        [Key(6)] public float HitY;
        [Key(7)] public float HitZ;

        /// <summary>子弹档位</summary>
        [Key(8)] public int DamageIndex;

        /// <summary>第几个发射点</summary>
        [Key(9)] public int MuzzleIndex;
    }

    /// <summary>
    /// 玩家生命状态（血 / 盾 / 是否倒地）：成员 -> 房主 / 房主 -> 全体（同型双向）。
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
    /// 玩家弹药系数（0~1）：成员 -> 房主 / 房主 -> 全体（同型双向）
    /// 它的变化节奏和血盾完全不同
    /// 分开后：血盾走变化即发 + 0.5s 对账，弹药走 10Hz 上限 + 变化判定，互不打扰
    /// </summary>
    [MessagePackObject]
    public class PlayerAmmo
    {
        [Key(0)] public uint Sid;
        [Key(1)] public float Ratio;      // 总剩余弹药比例 0~1
    }

    /// <summary>
    /// 角色喊话（<c>SpeechTypeEnum</c>）：成员 -> 房主（<c>SpeechNtf</c>）/ 房主 -> 全体（<c>SpeechSync</c>）。
    /// </summary>
    [MessagePackObject]
    public class PlayerSpeechMsg
    {
        /// <summary>权威标识：0 = 房主自己（与其它 Ntf 同口径）。接收端用它丢掉"自己发的那条"。</summary>
        [Key(0)] public uint Sid;
        /// <summary>SpeechTypeEnum 的 int </summary>
        [Key(1)] public int Speech;
    }
}
