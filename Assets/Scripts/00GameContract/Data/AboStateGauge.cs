using FPSGame.Core;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 异常状态的**积蓄值**（纯逻辑值，不含任何表现概念）。
    ///
    /// <para>▍2026-10-01 改造：原名 <c>AboStateViewInfo</c>，带 <c>Sprite Icon</c> + <c>Color Color</c>
    /// —— 表现概念被塞进了契约签名（<c>IHealth.GetActiveAboStates</c>）。而它俩真正的来源是
    /// <c>FPSGame.Data.AboStateData_SO</c>（`04_Data`）。现改为：本结构只承载逻辑值，
    /// **图标/颜色由表现层（<c>HpItemBase</c>，`10_UI`）自己查 <c>AboStateData_SO.Dic</c> 自解**。</para>
    ///
    /// <para>▍收益：① 契约层不再出现 <c>Sprite</c>/<c>Color</c>（符合"三进三出"的准出第 2 条：
    /// 表现概念 ⇒ 拆成"逻辑值 + 表现查表"）；② 逻辑内核 `05_UnitCore` 不再替 UI 取显示数据。</para>
    /// </summary>
    public struct AboStateGauge
    {
        /// <summary>异常类型</summary>
        public DamageTypeEnum Type;

        /// <summary>当前积蓄值</summary>
        public float Current;

        /// <summary>最大积蓄值</summary>
        public float Max;
    }
}
