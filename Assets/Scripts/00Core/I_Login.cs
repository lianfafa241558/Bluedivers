namespace FPSGame.Core
{
    /// <summary>
    /// 逻辑帧接入接口（与 <see cref="LogicFrame"/> 原语配套）。
    ///
    /// <para>▍层级沿革：2026-09-30 从 <c>00Tools/LogicBehaviour.cs</c> 下沉到契约层（当时被 <c>INetService</c> 引用）；
    /// 2026-10-01 再从契约层**下沉到 <c>00_Core</c>** —— 因为 <c>INetService</c> 已删除，改为
    /// <see cref="LogicFrame"/> 原语，而原语住在最底层 ⇒ 接口必须同层（契约层在 <c>00_Core</c> 之上）。</para>
    ///
    /// <para>▍为什么放最底层而不是契约层：它描述的是"能接入逻辑帧"这一最基础的能力，
    /// 与任何业务 / 服务无关；放在 <c>00_Core</c> 之后，玩法层（<c>LogicBehaviour</c> 的子类）
    /// 与 <c>09_Managers</c>（<c>LogicFrameHost</c>）都是"向上看见它"，依赖方向最浅。</para>
    ///
    /// <para>▍成员签名与原实现完全一致，搬迁前后**零调用点改动**（调用点只需 <c>using FPSGame.Core;</c>）。</para>
    /// </summary>
    public interface I_Login
    {
        void LogicInit();
        void LogicUnInit();

        void LogicTick();
        bool IsActive();
    }
}
