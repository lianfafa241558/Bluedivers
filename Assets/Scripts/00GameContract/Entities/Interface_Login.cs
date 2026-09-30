namespace FPSGame.GameContract
{
    /// <summary>
    /// 逻辑帧接入契约（2026-09-30 从 <c>00Tools/LogicBehaviour.cs</c> 下沉到契约层）。
    ///
    /// <para>▍为什么下沉：<c>NetManager.Add/Remove(I_Login)</c> 要被 <see cref="INetService"/> 引用，
    /// 而实现方 <c>LogicBehaviour</c> 被玩法层（<c>WeaponBaseController</c>）继承
    /// ⇒ 这个接口必须落在"两侧共见"的契约层，否则 P5 拆玩法层时必然编译不过。</para>
    ///
    /// <para>▍成员顺序、签名与原实现（`00Tools/LogicBehaviour.cs`）完全一致，搬迁前后零调用点改动。</para>
    /// </summary>
    public interface I_Login
    {
        void LogicInit();
        void LogicUnInit();

        void LogicTick();
        bool IsActive();
    }
}
