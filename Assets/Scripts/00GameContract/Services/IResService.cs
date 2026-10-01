using System;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 资源加载 服务契约（由 <c>ResSvc</c> 实现）。
    ///
    /// <para>▍2026-10-01 起**不再经定位器**：<c>ServiceLocator.Res</c> 槽已删除，本接口改为
    /// **显式注入**——装配根 <c>MissionController</c>（任务实例的唯一创建点）在
    /// <c>Instantiate</c> 后调用 <see cref="IResConsumer.Inject"/> 把实例交给任务。
    /// UI 侧不需要它：<c>10_UI</c> 引用 <c>09_Managers</c>，直接写 <c>ResSvc.Instance</c>（既有多处同款）。</para>
    ///
    /// <para>▍成员是"实测开出来的"：<c>CreatPrefab</c>(4 处任务载具：<c>MissionEvacuateBase/Static/Mobile</c>
    /// 里的 <c>Res.CreatPrefab(...)</c>)。
    /// ⚠ 已删的三个成员都是同一条判据 —— **接口消费点 = 0**（调用方改用直连 <c>ResSvc.Instance</c>）：
    /// <c>AsyncLoadScene</c>/<c>AsyncContinueLoadScene</c>（调用方是 <c>BattleManager</c>/5 个 UI 窗口/
    /// <c>TransSceneController</c>）、<c>LoadSprite</c>（唯一调用点 <c>SelectRoleWnd.LoadModuleFrame</c> 也是直连）。
    /// ⚠ 删的只是**接口声明**，<c>ResSvc</c> 上的 public 方法一个都没动。</para>
    /// </summary>
    public interface IResService
    {
        /// <summary>按路径实例化预制体（<paramref name="cache"/> = 走缓存池）。</summary>
        GameObject CreatPrefab(string path, bool cache = false, Vector3 pos = default(Vector3));
    }

    /// <summary>
    /// 需要 <see cref="IResService"/> 的被注入方（**装配根注入**的接线口，2026-10-01 首次引入）。
    ///
    /// <para>▍为什么是接口而不是直接写在 <c>MissionBase</c> 上：装配根只认这个接线口，
    /// 不必知道具体是谁（现在只有任务实例实现它，将来 <c>Flow</c>/<c>Battle</c> 迁移时按同一形状
    /// 加 <c>IxxConsumer</c>，或收敛成一个容器）。</para>
    /// <para>▍调用约定：创建方在 <c>Instantiate</c> 之后、<c>Init</c> 之前注入
    /// （被注入方**不得在 <c>Awake</c> 里**依赖注入对象 —— <c>Awake</c> 在 <c>Instantiate</c> 内部就跑完）。</para>
    /// </summary>
    public interface IResConsumer
    {
        /// <summary>注入资源加载服务（可重复调用，后注入者生效）。</summary>
        void Inject(IResService res);
    }
}
