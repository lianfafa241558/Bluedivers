namespace FPSGame.GameContract
{
    /// <summary>
    /// 战备界面接收器（2026-10-01 建；同日从 `WindowRegistry.cs` 拆出独立文件——两者毫无关系）。
    ///
    /// <para>▍为什么需要它：<c>BridgeSys</c>（01Manager）要调 <c>ArmamentWnd</c>（04UI）的 4 个方法
    /// （<c>ReceivePlayerSelectAemament</c>/<c>ReceivePlayerSelectTeamEnhance</c>/<c>ReceivePlayerReady</c>/<c>ReceiveRosterChanged</c>），
    /// 而它原先直接持有 <c>ArmamentWnd</c> 字段 ⇒ 「管理器 → UI」反向依赖。改为对接口编程
    /// （实现方 = <c>ArmamentWnd</c>，登记点仍是 <c>BridgeSys.Instance.armament = this</c>）。</para>
    ///
    /// <para>▍为什么在契约层：两侧（`09_Managers` 与 `10_UI`）互不可见 ⇒ 需要一个共见的接口仲裁，
    /// 符合契约层准入判据的"服务接口"一类。</para>
    /// </summary>
    public interface IBridgeArmamentSink
    {
        /// <summary>某玩家选了战备格。</summary>
        void ReceivePlayerSelectAemament(int playerIndex, int id, int index);

        /// <summary>某玩家切换了全队强化。</summary>
        void ReceivePlayerSelectTeamEnhance(int playerIndex, int id);

        /// <summary>某玩家就绪/取消就绪。</summary>
        void ReceivePlayerReady(int playerIndex, bool state);

        /// <summary>名册变化（有人加入/退出）⇒ 刷新每个槽位（退出的人那一行要消失；名册重排后下标会变）。</summary>
        void ReceiveRosterChanged();
    }
}
