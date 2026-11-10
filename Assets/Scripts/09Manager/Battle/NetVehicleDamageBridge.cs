using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Net;
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **载具伤害**桥（09）：一处收口"机甲 / 可驾驶炮台这类载具"的掉血与生死。
    /// <para>▍2026-10-27 扩到**空投支援单位**（炮台 / 治疗无人机 / 火箭包）：它们的处境完全一样
    /// ——两端各 Instantiate 一份、各自挨子弹、成员还会把这一扣上报 ⇒ 2 倍。判据见
    /// <see cref="Actor.HostAuthorityUnit"/>，与载具共用同一道门。</para>
    ///
    /// <para>▍键 = 载具 <c>Actor.NetId</c>（房主随 4034 分配、两端一致，见 <c>ActorsManager.VehicleNetIdBase</c>）；
    /// 通道 = 既有统一伤害（成员 4053 上报 / 房主 4054 下发，死亡靠同一类的 <c>IsDeath</c>）。</para>
    ///
    /// <para>▍**下行刻意不在这里**：<c>EnemyNetBridge</c> 的应用路径是按 <c>NetId</c> 在 <c>ActorsManager.Actors</c>
    /// 全表里找、**不按 <c>UnitTypeEnum</c> 过滤** ⇒ 载具天然吃得下（含死亡那条 <c>ApplyRemoteDeath</c>）。
    /// 本桥只补两件真正缺的东西：</para>
    /// <list type="number">
    ///   <item><b>上行</b>（2026-11-10 已迁出）：现在统一由 <c>NetUnitDamageReporter</c> 报
    ///         （判据 = 有 <c>Actor.NetId</c> 且这一发带真伤害或非零成分，与物件类型无关）；</item>
    ///   <item><b>权威门</b>：两端各自模拟子弹（含敌人 AI 的子弹）⇒ 各扣一次必然分叉。门插在伤害入口
    ///         <c>Damageable.LocalDamageGate</c>（在 <c>InflictDamage</c> 最前面），口径见
    ///         <c>BattleState.IsVehicleDamageAuthority</c>。</item>
    /// </list>
    ///
    /// <para>▍每局一份，由 <c>WaveManager</c> Install/Uninstall（同 <c>EnemyNetBridge</c> 模式）。</para>
    /// </summary>
    public static class NetVehicleDamageBridge
    {
        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            Damageable.LocalDamageGate = Gate;
            // ⚠ 上行（本端掉血 ⇒ 上报/下发）已收口到 NetUnitDamageReporter（2026-11-11），本桥只剩权威门
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;
            Damageable.LocalDamageGate = null;
        }

        // ==================== 权威门：这笔伤害该不该在本端落地 ====================

        /// <summary>见 <c>Damageable.LocalDamageGate</c>。</summary>
        static bool Gate(Damageable d, DamagePacket p)
        {
            // 只管"两端各生成一份"的单位（载具 + 空投支援单位）：别的单位（敌人 / 玩家 / 盟友 / 可破坏物）
            // 走各自既有通道，一律放行
            if (!IsAuthorityGated(d)) return true;

            // 房主 / 单机：本地就是权威 ⇒ 照常落地（随后会在 OnLocalDamaged 里广播出去）
            if (FPSGame.Data.BattleState.IsVehicleDamageAuthority) return true;

            // 正在应用远端下发的那笔（EnemyNetBridge.ApplyRemoteDamage）⇒ 必须落地，否则永远收不到血
            if (EnemyController.ApplyingRemoteDamage) return true;

            // 非权威端：**玩家侧来源**（本机玩家/盟友）落地 + 上报（那笔房主算不出来）；
            //             **AI 来源**（敌人）不落地 —— 那笔由房主那台算并广播下来（两端各模拟一遍 ⇒ 只认房主那份）。
            return IsPlayerSide(p.DamageOrigin) || IsPlayerSide(p.DamageCaused);
        }

        static bool IsAuthorityGated(Damageable d)
        {
            if (d == null) return false;
            return IsAuthorityGatedActor(d.GetComponentInParent<Actor>());
        }

        /// <summary>这个单位该不该"只认房主那份伤害"。
        /// <para>▍两类：① 载具（<c>VehicleController</c>）；② 空投出来的支援单位（炮台/无人机/火箭包，
        /// <see cref="Actor.HostAuthorityUnit"/>）。共同点 = **两端各 Instantiate 一份、各自跑 AI、各自挨一份子弹**
        /// ⇒ 各扣一次必然分叉，成员端还会上报一次 ⇒ 2 倍。</para></summary>
        static bool IsAuthorityGatedActor(Actor actor)
            => actor != null && (actor.HostAuthorityUnit || actor.GetComponent<VehicleController>() != null);

        /// <summary>这个 GameObject 是不是"玩家侧"（玩家 / 盟友）。
        /// <para>⚠ 部署物（哨戒炮 / 地雷这类 <c>SpecUnit</c>）刻意**不算**：它们两端各有一份、也各自开火
        /// ⇒ 与 AI 同口径（只认房主那份），否则同一发炮弹会被两端各扣一次。</para></summary>
        static bool IsPlayerSide(GameObject go)
        {
            if (go == null) return false;
            var a = go.GetComponent<Actor>();
            if (a == null) a = go.GetComponentInParent<Actor>();
            return a != null && (a.Type == UnitTypeEnum.Player || a.Type == UnitTypeEnum.Friend);
        }

        // ==================== 上行 ====================
        // ⚠ 本端掉血后的"上报 / 下发"已收口到 NetUnitDamageReporter（判据：有 Actor.NetId + 带真伤害或非零成分）
        //   ⇒ 敌人 / 载具 / 空投支援单位 / 凯伊盾 / 场景可破坏物走的是同一份实现，这里不再各写一遍。
    }
}
