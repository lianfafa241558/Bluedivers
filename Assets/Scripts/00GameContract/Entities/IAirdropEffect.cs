using System;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 空投信标特效契约（2026-10-01 为切断 `09Manager → Effect` 的反向引用而建）。
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
        /// 【联机】标记**本次呼叫者**的会话 sid（0 = 房主；房主/成员自己的呼叫走 <see cref="IVfxEffect.SetOrigin"/> 那条路，不用调它）。
        /// <para>▍为什么要它：战备呼叫出来的载具（外骨骼/炮台/运输机）**归呼叫者所有**
        /// （载具改装按呼叫者的配置渲染）—— 远端复现那条路是 <c>BattleManager.ReleaseAirdrop</c> 建的、
        /// 信标没有 owner ⇒ 只能由调用方把 sid 显式带下来。</para>
        /// <para>必须在 <see cref="TmpAirdrop"/> 之前调用（本次呼叫有效，回收时复位）。</para>
        /// </summary>
        void SetCallerSid(uint sid);

        /// <summary>
        /// 【联机】标记本次空投生成的**单位号**（<c>Actor.NetId</c>）；必须在 <see cref="TmpAirdrop"/> 之前调用。
        /// <para>▍覆盖范围（2026-10-27 扩）：载具（<c>SpecUnit</c>）与支援单位（炮台 / 治疗无人机 / 火箭包，
        /// <c>Enemy</c> 类型）—— 它们的共同点是"每端各自 Instantiate"，没有房主定的号就必然分叉。</para>
        /// <para>▍传值语义：<c>&gt; 0</c> = 房主已定号（远端复现 / 房主自己的呼叫）；<c>0</c> = 待房主定号
        /// （成员自己叫的那发 —— 先不领自增号，等 4028 回来由 <c>NetFriendBridge</c> 覆盖）；
        /// **不调用** = 不托管（单机 / 旧版房主 ⇒ 保持原来的自增号行为）。</para>
        /// <para>▍为什么要它：生成物两端各生成一份、时机不同，落点还会差几厘米 ⇒ 号必须显式给（理由同
        /// <see cref="SetCallerSid"/>，见 <c>ActorsManager.VehicleNetIdBase</c>）。</para>
        /// </summary>
        void SetVehicleNetId(int netId);

        /// <summary>
        /// 开始一次空投流程。<paramref name="airdropId"/> 由实现方解析成配置
        /// （原实现是 <c>ResSvc.Instance.GetAirdrop(id)</c>）；
        /// <paramref name="action"/> 在空投舱生成后回调（可为 null）。
        /// </summary>
        void TmpAirdrop(Vector3 point, int airdropId, Action<GameObject> action);
    }
}
