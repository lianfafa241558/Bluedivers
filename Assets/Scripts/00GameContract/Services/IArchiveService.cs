namespace FPSGame.GameContract
{
    /// <summary>
    /// 存档设置 服务契约（由 <c>ArchiveSvc</c> 实现并注册，2026-09-30 为 P5 加入）。
    ///
    /// <para>⚠ <b>只开了 <c>GetSetting</c></b>：<c>ArchiveSvc.Archive</c> 的返回类型是 <c>ArchivesData_SO</c>
    /// （在 `02Game/Game/Data`＝玩法层）⇒ **契约层无法命名它** ⇒ 那部分改走「数据自持」
    /// （上层 <c>ArchiveSvc</c> 把 SO 写进玩法侧的持有者，玩法层直接读）。</para>
    /// </summary>
    public interface IArchiveService
    {
        /// <summary>按名字读一项设置（返回 float）。</summary>
        float GetSetting(string name);
    }
}
