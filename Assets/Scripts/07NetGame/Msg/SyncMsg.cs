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

    /// <summary>家具交互（双向）。<c>SyncId</c> = FNV1a(Id + 位置量化)，创建时算一次并缓存。</summary>
    [MessagePackObject]
    public class FurnitureOperateMsg
    {
        /// <summary>权威标识：操作者会话号（房主 = 0）。远端据此解析"操作者"（Friend 实例）。</summary>
        [Key(0)] public uint Sid;
        [Key(1)] public int SyncId;
    }

    /// <summary>标记点位（双向；表现类，可不可靠）。</summary>
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
}
