using MessagePack;

namespace FPSGame.Net
{
    /// <summary>
    /// 一个实体的**位姿快照**（联机高频消息里字段要尽量少）。
    ///
    /// <para>▍字段取舍：位置 + 朝向 + 俯仰 + 是否瞬移。
    /// ⚠ **俯仰必须带**（2026-10-07 实测）：玩家抬头/低头时身体只转 yaw、**武器（和枪口）跟着相机俯仰**
    /// ⇒ 不带 pitch 时别人看你永远是"平举枪、只朝前"，连枪口方向都不对（弹道同步也就跟着不准）。
    /// 动画状态 / 装备姿态仍然留给表现层本地推断（那些不影响"看起来像不像在打那个方向"）。</para>
    ///
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
        /// <summary>true = 接收端**不要插值**，直接吸附（首次出现 / 传送 / 复活）。</summary>
        [Key(5)] public bool Teleport;
        /// <summary>**上身/武器的俯仰角**（度）：玩家 = 相机垂直角（<c>BaseSelfController.CameraPitch</c>）；
        /// 接收端把它应用到盟友的武器挂点上（见 <c>FriendWeaponView.SetAimPitch</c>）。0 = 不俯仰（旧版发送端）。</summary>
        [Key(6)] public float Pitch;
    }

    /// <summary>
    /// 位姿**批次**：成员上行时只装 1 项；房主下行时是"聚合后的全体"。
    ///
    /// <para>▍为什么用批次而不是单条：房主的 <c>Update</c> 里按固定频率（15Hz）一次性广播所有实体，
    /// 这样下行报文条数与"人数/实体数"解耦 —— 人数变多时也不会把包数放大。</para>
    /// </summary>
    [MessagePackObject]
    public class PoseBatchMsg
    {
        /// <summary>本局局号（0 = 大厅 / 尚未开局；接收端可用它丢弃上一局的残留包）。</summary>
        [Key(0)] public int MatchId;
        [Key(1)] public PoseSnapshot[] Items;
    }
}
