namespace FPSGame.GameContract
{
    /// <summary>
    /// 逻辑帧注册 服务契约（由 <c>NetManager</c> 实现并注册，2026-09-30 为 P5 加入）。
    ///
    /// <para>▍为什么需要它：<c>LogicBehaviour</c>（被玩法层 <c>WeaponBaseController</c> 继承）在
    /// `Awake/OnDestroy` 里要把自己注册进 <c>NetManager</c> 的逻辑帧循环 ⇒ 玩法层反向依赖 01Manager。</para>
    ///
    /// <para>⚠ <c>LogicBehaviour</c> 用 <c>ServiceLocator.Net?.Add(this)</c> 调用（服务未就绪时静默跳过，
    /// 原实现是直接 NRE）。</para>
    /// </summary>
    public interface INetService
    {
        /// <summary>把逻辑对象接入逻辑帧。</summary>
        void Add(I_Login obj);

        /// <summary>从逻辑帧摘除。</summary>
        void Remove(I_Login obj);
    }
}
