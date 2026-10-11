namespace FPSGame.Managers
{
    /// <summary>
    /// 联机同步桥的"正在应用远端消息"共享门（计数式，可嵌套）。
    ///
    /// <para>▍为什么要共享（而不是每个桥各自持一个 bool）：应用远端消息时常常会**派发本地的玩法事件**
    /// （家具 <c>Operate()</c> → <c>GlobalEventBus.FurnitureOperate</c>），而本端的桥也订阅了同一个事件（用于上报）
    /// ⇒ 不加门就会把"远端来的"当成"本机产生的"再发一次（回环）。跨桥的那条回环单一扇门挡不住 ——
    /// 典型例子：应用终端阶段（<c>NetKeyScreenBridge</c>）会走到 <c>furn.Operate()</c>（<c>NetFurnitureBridge</c> 的订阅点）。</para>
    ///
    /// <para>▍用法：<c>Begin(); try { ...应用... } finally { End(); }</c>。计数式 ⇒ 桥里再调桥也安全。</para>
    /// </summary>
    internal static class NetApplyGuard
    {
        static int _depth;

        /// <summary>true = 当前正在应用远端消息（此时本地事件不许再上行）。</summary>
        internal static bool Applying => _depth > 0;

        internal static void Begin() => ++_depth;

        internal static void End()
        {
            if (_depth > 0) --_depth;
        }
    }
}
