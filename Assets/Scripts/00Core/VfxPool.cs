using UnityEngine;
// 写入方是"池宿主"= 09_Managers 的 VFXManager；下层只能调（与 LogicFrame/ServiceLocator 同一套硬化手法）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]



namespace FPSGame.Core
{
    /// <summary>
    /// 特效 / 抛射物**池入口原语**（2026-10-01 取代 ServiceLocator 的 <c>Vfx</c> 槽）。
    ///
    /// <para>▍它为什么归 <c>00_Core</c>：它前置的设施（对象池类型
    /// <c>AutoDicPool</c>/<c>DicObjectPool</c>/<c>AutoObjectPool</c>）本来就定义在 <c>00_Core</c>，
    /// 而 <c>VFXManager</c> 用的两个池（<c>AutoDicPool&lt;GameObject,GameObject&gt;</c>、
    /// <c>DicObjectPool&lt;ProjectileBase,ProjectileBase&gt;</c>）也来自那里
    /// ⇒ 本原语的语义是"**全局对象池的访问入口**"，不是"表现层能力"。</para>
    ///
    /// <para>▍为什么必须有个静态入口（不能注入）：① <c>Creat</c> 必须**同步返回实例**
    /// （调用方要持有 / 读 <c>.transform</c> / 稍后 <c>Release</c> 回池）⇒ 事件做不到；
    /// ② 消费者里有**静态工具类** <c>FpsHelper_Hit</c>、以及 <c>Release(this)</c> 的**对象自我归还**
    /// ⇒ 没有实例可以注入。</para>
    ///
    /// <para>▍形态：静态能力入口 + 宿主接管（`Sink`）。<c>VFXManager</c> 在 <c>Init()</c> 里
    /// <c>VfxPool.Sink = this</c>，在自己的 <c>OnDestroy()</c> 里按身份判定归还 null。
    /// <c>VFXManager</c> 是**跨场景常驻**的（在 <c>GameRoot</c> 层级下，随 <c>DontDestroyOnLoad</c> 常驻）
    /// ⇒ <c>Sink</c> 在整个 Play 期间常驻，只有整局结束才会归还。</para>
    ///
    /// <para>▍未接管时的语义（= 原 <c>NullVfxService</c>，行为等价）：<c>Creat</c> 返回 null、
    /// <c>Release</c> 空操作。⚠ 与原来不同的是：原来 <c>BattleHub.Dump()</c> 还能看出槽位指向哪个空对象，
    /// 现在改看 <see cref="Dump"/>；且 <c>Creat</c> 在**开发构建**下会打**一次性**告警（热路径，防刷屏）。</para>
    ///
    /// <para>▍写入保护：<c>Sink</c> 为 <c>internal set</c> + 本程序集对 <c>09_Managers</c> 开放
    /// <c>InternalsVisibleTo</c>（见文件头）⇒ 只有宿主能写，其它层只能读。</para>
    ///
    /// <para>▍退出条件：等显式注入 / 装配根落地后，本原语与定位器一起重估；
    /// ⚠ 但消费者里的**静态工具类**（<c>FpsHelper_Hit</c>）无法注入 ⇒ 该静态入口大概率**长期保留**。</para>
    /// </summary>
    public static class VfxPool
    {
        /// <summary>池宿主（实现方：<c>VFXManager</c>）。未接管时为 null。</summary>
        public static IVfxSink Sink { get; internal set; }

        /// <summary>按预制体模板池化生成特效实例（<paramref name="parent"/> 为空则挂到当前场景）。
        /// <para>⚠ 未接管时返回 null；调用方沿用原有判空（本原语与原空对象语义一致）。</para></summary>
        public static GameObject Creat(GameObject tmp, Vector3 pos = default, Quaternion rotation = default, Transform parent = default)
        {
            if (Sink == null) { WarnOnce(); return null; }
            return Sink.Creat(tmp, pos, rotation, parent);
        }

        /// <summary>回收由 <see cref="Creat(GameObject,Vector3,Quaternion,Transform)"/> 生成的实例。</summary>
        public static void Release(GameObject go)
        {
            if (go == null || Sink == null) return;
            Sink.Release(go);
        }

        /// <summary>按**组件**模板池化生成（目前只支持 <c>ProjectileBase</c>）。
        /// <para>未接管时返回 null。</para></summary>
        public static T Creat<T>(T template, Vector3 pos, Quaternion rotation) where T : Component
        {
            if (Sink == null) { WarnOnce(); return null; }
            return Sink.Creat(template, pos, rotation);
        }

        /// <summary>回收"按组件模板生成"的实例（与其配对：抛射物回抛射物池，其余回普通池）。</summary>
        public static void Release(Component instance)
        {
            if (instance == null || Sink == null) return;
            Sink.Release(instance);
        }

        /// <summary>排查用：当前宿主是谁（取代原来从 <c>BattleHub.Dump()</c> 看槽位）。</summary>
        public static string Dump()
        {
            return "VfxPool.Sink = " + (Sink == null ? "<none>" : Sink.GetType().Name);
        }

        // ⚠ 只提示一次：这些调用都在开火/受击等**热路径**上；场景切换瞬间 Sink 短暂为空时的偶发调用
        //    也不该刷屏。只在编辑器 / 开发构建里出声，正式包静默（与"静默失败"的既有语义一致）。
        private static void WarnOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (s_warned) return;
            s_warned = true;
            Debug.LogWarning("[VfxPool] 池宿主尚未接管（VFXManager.Init 没跑？）⇒ 本次 Creat 返回 null、Release 为空操作。仅提示一次。");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 字段也放进条件编译：否则正式包里它无人使用，会留下 CS0169 警告。
        private static bool s_warned;
#endif
    }

    /// <summary>
    /// 池宿主的**窄接口**：只有生成 / 回收两件事，刻意不掺任何业务语义。
    /// 由 <c>VFXManager</c> 实现；与 <see cref="VfxPool"/> 同住最底层，避免方案里出现中间层。
    /// </summary>
    public interface IVfxSink
    {
        /// <summary>按预制体模板生成（返回实例根物体，供稍后 <see cref="Release(GameObject)"/>）。</summary>
        GameObject Creat(GameObject tmp, Vector3 pos, Quaternion rotation, Transform parent);

        /// <summary>回收预制体模板生成的实例。</summary>
        void Release(GameObject go);

        /// <summary>按组件模板生成（目前只支持 <c>ProjectileBase</c>）。</summary>
        T Creat<T>(T template, Vector3 pos, Quaternion rotation) where T : Component;

        /// <summary>回收组件模板生成的实例。</summary>
        void Release(Component instance);
    }
}
