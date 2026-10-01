// 写入方是"逻辑帧宿主"= 09_Managers 的 NetManager；下层只能读/注册（与 ServiceLocator 同一套硬化手法）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]

namespace FPSGame.Core
{
    /// <summary>
    /// 逻辑帧接入**原语**（2026-10-01 取代 <c>ServiceLocator.Net</c> 槽）。
    ///
    /// <para>▍它为什么不是"服务定位器"：语义单一（只有"接入逻辑帧"一件事）、归最底层 <c>00_Core</c>、
    /// 任何层都看得见 ⇒ 不可能像定位器那样长成"什么都能塞的抽屉"。
    /// ⚠ 澄清（2026-10-01 事实核对）：<c>AudioSvc.ClipLoader</c> / <c>TimerRequest</c> 是 <c>03_Audio</c> 上的
    /// <c>static Func&lt;&gt;</c> **委托字段**（由 <c>AudioEventBridge</c> 注入），形态相近但**不带写入保护**；
    /// 真正与本类同款的（静态类 + <c>Sink</c> 属性 + <c>internal set</c> + <c>InternalsVisibleTo</c>）
    /// 只有本类与 <c>VfxPool</c> —— 本类是**第一例**，别按"已有三四例"去估它的份量。</para>
    ///
    /// <para>▍为什么必须有个静态入口（不能显式注入）：消费者 <see cref="I_Login"/> 的实现方
    /// <c>LogicBehaviour</c> 是**26 个武器/逻辑子类的基类**，实例由 prefab 在数十处不同位置生成
    /// ⇒ 注入要落到每个创建点，不可行。</para>
    ///
    /// <para>▍谁接管：<c>NetManager</c> 在 <c>Init()</c> 里 <c>LogicFrame.Sink = this</c>，
    /// <c>UnInit()</c> 里清空。<c>Sink == null</c> 时注册/摘除都是**空操作**
    /// （等价于原 <c>NullNetService</c> 的"服务未就绪静默跳过"语义）。</para>
    ///
    /// <para>▍写入保护：<c>Sink</c> 为 <c>internal set</c> + 本程序集对 <c>09_Managers</c> 开放
    /// <c>InternalsVisibleTo</c>（见文件头）⇒ 只有注册方能写，其它层只能读。</para>
    ///
    /// <para>▍退出条件：等显式注入 / 装配根落地后，本原语与 <c>I_Login</c> 一起并入那条路线。</para>
    /// </summary>
    public static class LogicFrame
    {
        /// <summary>逻辑帧宿主（实现方：<c>NetManager</c>）。未接管时为 null。</summary>
        public static ILogicFrameSink Sink { get; internal set; }

        /// <summary>把对象接入逻辑帧</summary>
        public static void Register(I_Login obj)
        {
            if (obj == null || Sink == null) return;
            Sink.Add(obj);
        }

        /// <summary>从逻辑帧摘除</summary>
        public static void Unregister(I_Login obj)
        {
            if (obj == null || Sink == null) return;
            Sink.Remove(obj);
        }

        /// <summary>排查用：当前宿主是谁</summary>
        public static string Dump()
        {
            return "LogicFrame.Sink = " + (Sink == null ? "<none>" : Sink.GetType().Name);
        }
    }

    /// <summary>
    /// 逻辑帧宿主的**窄接口**：只有两个动词，刻意不掺任何业务语义。
    /// 由 <c>NetManager</c> 实现；与 <see cref="I_Login"/> 同住最底层，避免方案里出现中间层。
    /// </summary>
    public interface ILogicFrameSink
    {
        void Add(I_Login obj);
        void Remove(I_Login obj);
    }
}
