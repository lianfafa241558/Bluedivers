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

        /// <summary>状态的 int 编码（可选，2026-10-11 追加）。接收端**优先**用 <see cref="StateKey"/> 自己解析，
        /// 这个只在对方没带 Key 时兜底。默认 0 ⇒ 旧发送端 = "无状态负载"。</summary>
        [Key(2)] public int State;
        /// <summary>状态的原始键（可选，如管道的 <c>PipeWait</c>/<c>PipeError</c>）。
        /// <para>▍非空 = 这是一条**状态**同步（不是操作重放）：接收端走 <c>ApplyRemoteState</c>，**不调用</b> <c>Operate()</c>。
        /// 用于"改了状态但没有人交互"的场合（任务把管道打成 PipeError）。</para></summary>
        [Key(3)] public string StateKey;
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

}

