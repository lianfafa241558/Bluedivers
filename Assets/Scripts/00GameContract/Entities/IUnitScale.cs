namespace FPSGame.GameContract
{
    /// <summary>
    /// 单位"视觉体型"契约（2026-09-30 为拆出 <c>05_UnitCore</c> 而建，**接口隔离**的产物）。
    ///
    /// <para>▍为什么不用现成的 <c>IUnit</c>：<c>IUnit</c>（在 <c>02Game/AI/Controller/AIController.cs</c>）
    /// 的成员签名带 <c>GameAttribute</c> ⇒ 让单位内核引用它，就会连带把**整套武器/单位属性系统**
    /// （<c>GameAttribute</c>/<c>AttrTag</c>/<c>ModifierType</c>/<c>WeaponAttrType</c>/<c>UnitAttrType</c>）一起拖到下层。
    /// 而 <c>Health_AboState</c> 只想按体型缩放一个特效 ⇒ 只暴露这一个值即可。</para>
    ///
    /// <para>▍实现方：<c>AIController</c> / <c>BaseSelfController</c>（它们本就实现了 <c>IUnit</c>，属性查询留在上层）。
    /// 若将来下层真的需要整套属性系统，再走"把 <c>GameAttribute</c> 一族下沉"的路线（与 P2 延后的
    /// <c>ModifyAttrData</c> 下沉是同一件事）。</para>
    /// </summary>
    public interface IUnitScale
    {
        /// <summary>体型系数（默认 1；查不到属性时也返回 1）。</summary>
        float VisualScale { get; }
    }
}
