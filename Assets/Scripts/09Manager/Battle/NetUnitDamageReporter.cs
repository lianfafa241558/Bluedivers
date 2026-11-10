using FPSGame.AI;
using FPSGame.Game;
using FPSGame.GameContract;
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **"本端刚结算的这一笔伤害要不要报上网络"的唯一判据**，具体发消息交给 <see cref="NetDamageReport"/>。
    ///
    /// <para>▍历史（为什么现在只剩这一处）：这段以前写在 <c>EnemyController._OnSettled</c> 里 —— 上报跟着 AI 控制器走，
    /// 于是"没有 EnemyController 的单位"（凯伊护盾 / 载具 / 空投支援单位 / 场景可破坏物）各自又抄了一份桥
    /// （<c>NetKeiShieldBridge</c> / <c>NetVehicleDamageBridge</c> / <c>NetDestructibleBridge</c> 的伤害段）。
    /// 2026-11-10 先把那几座桥收成"无 AI 单位的上报"，2026-11-11 再把 <c>EnemyController</c> 那份也收进来 ⇒
    /// 现在全仓**只有这一处**会因一次结算去发 4053/4054。</para>
    ///
    /// <para>▍为什么订阅 <c>UnitEventBus.OnUnitSettled</c> 而不是各单位自己的 <c>Health.OnSettled</c>：
    /// 两者**在同一处发布**（<c>Health.TakeDamage</c> 里同一个 <c>rawGroups != null</c> 分支，0 伤害也发），
    /// 所以 tick 伤害 / 无成分伤害一样进不来；而总线只需订阅一次，不用每加一种物件补一条。
    /// 代价是每次命中多一次 <c>TryGetComponent&lt;Actor&gt;</c>（~25ns，实测可忽略）。</para>
    ///
    /// <para>▍判据只有两条（其余都在 <see cref="NetDamageReport.Report"/>：回环门 / 成分 / 部位下标 / 方向）：
    /// **有号** + **这一发带真伤害或非零成分**。无号的场景物走 4058（<c>NetDestructibleBridge</c>），
    /// 背包护盾走"按归属者 sid 定点投递"（<c>NetShieldBagBridge</c>）—— 那两条不是按号寻址的，合并不进来。</para>
    ///
    /// <para>⚠ 判据里**不要**再加"有没有 EnemyController"这类过滤：那等于把"谁该报"重新按物件类型分叉一次，
    /// 正是这次要消掉的东西。</para>
    ///
    /// <para>▍自举：<c>[RuntimeInitializeOnLoadMethod]</c>（同 <c>NetVehicleBridge</c> / <c>NetPlayerStateBridge</c>）。</para>
    /// </summary>
    public static class NetUnitDamageReporter
    {
        static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (installed) return;
            installed = true;

            // 用 Settled（0 伤害也回调）：纯冰冻 / 催化 / 破甲的命中也要过网
            UnitEventBus.OnUnitSettled += OnSettled;
        }

        static void OnSettled(GameObject victimGo, PEInt damage, GameObject caused, Collider hitBody, GameObject origin)
        {
            if (victimGo == null) return;

            // 不是单位（子弹 / 特效），或 Health 与 Actor 不在同一个物体上（弱点这类子物体）⇒ 交给专门通道
            if (!victimGo.TryGetComponent(out Actor actor)) return;

            NetDamageReport.Report(actor, Mathf.RoundToInt(damage.RawFloat), hitBody, caused, origin, "单位·伤");
        }
    }
}
