using System;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 空投信标特效契约（2026-10-01 为切断 `01Manager → Effect` 的反向引用而建）。
    ///
    /// <para>▍问题：<c>BattleManager.ReleaseAirdrop</c> 原来写
    /// <c>beacon.GetComponent&lt;VFXAirdropEffect&gt;()?.TmpAirdrop(point, ResSvc.GetAirdrop(id), action)</c>
    /// —— 直接指名 `Effect/VFX` 里的具体组件类型，且传的是 <c>AirdropData_SO</c>（在 `04_Data`）
    /// ⇒ 既让管理器反向依赖表现层，契约层也无法表达那个参数类型。</para>
    ///
    /// <para>▍解法：契约只开**动词 + 基础类型**（<c>int airdropId</c>），配置解析交给实现方
    /// （`VFXAirdropEffect` 在 Effect 层、位于管理器之上 ⇒ 可直连 <c>ResSvc</c>）。
    /// 取组件走 <c>GetComponent&lt;IAirdropEffect&gt;()</c> —— 与既有的
    /// <see cref="IVfxEffect"/>（`AirdropController` 已在用 `GetComponent&lt;IVfxEffect&gt;()`）同一套路。</para>
    /// </summary>
    public interface IAirdropEffect
    {
        /// <summary>
        /// 【联机】标记**本次呼叫者**的会话 sid（0 = 房主；房主/成员自己的呼叫走 <see cref="IVfxEffect.SetOwner"/> 那条路，不用调它）。
        /// <para>▍为什么要它：战备呼叫出来的载具（外骨骼/炮台/运输机）**归呼叫者所有**
        /// （载具改装按呼叫者的配置渲染）—— 远端复现那条路是 <c>BattleManager.ReleaseAirdrop</c> 建的、
        /// 信标没有 owner ⇒ 只能由调用方把 sid 显式带下来。</para>
        /// <para>必须在 <see cref="TmpAirdrop"/> 之前调用（本次呼叫有效，回收时复位）。</para>
        /// </summary>
        void SetCallerSid(uint sid);

        /// <summary>
        /// 开始一次空投流程。<paramref name="airdropId"/> 由实现方解析成配置
        /// （原实现是 <c>ResSvc.Instance.GetAirdrop(id)</c>）；
        /// <paramref name="action"/> 在空投舱生成后回调（可为 null）。
        /// </summary>
        void TmpAirdrop(Vector3 point, int airdropId, Action<GameObject> action);
    }
}
