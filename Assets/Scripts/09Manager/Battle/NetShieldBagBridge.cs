using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Net;
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **背包护盾（<c>ShieldBag</c> 的罩子）伤害**桥（09）：把"打到别人护盾上的那一笔"送到**归属者本机**落地。
    ///
    /// <para>▍为什么必须单开一条：罩子（<c>Sphere</c>，带 <c>Damageable</c> + <c>HealthShield</c>）是**独立可破坏单位**，
    /// 但它没有 <c>Actor</c> / <c>NetId</c> ⇒ 统一伤害通道（4025/4040）按号寻址，认不到它；而子弹只在**开枪那台**
    /// 上模拟（玩家血量各自机器权威，见 <c>topics/net-battle.md</c>）⇒ 不打这条，就是"我打你的盾，掉的是我这儿那份副本、
    /// 你的屏幕纹丝不动"（2026-10-21 实测）。</para>
    ///
    /// <para>▍归属者判定：背包被远端重放装到盟友实例上时 <c>BagBase.IsLocalDriver == false</c>，
    /// 它的 <c>Owner</c> 上挂 <c>FriendController</c>（有 <c>NetSid</c>）⇒ 目标就是那个 sid。本机自己那件
    /// （<c>IsLocalDriver == true</c>）**一律不上报**：本机就是权威，伤害已经真扣过了。</para>
    ///
    /// <para>▍自举：<c>[RuntimeInitializeOnLoadMethod]</c>（同 <c>NetVehicleBridge</c> / <c>NetPlayerStateBridge</c>）。</para>
    /// </summary>
    public static class NetShieldBagBridge
    {
        static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (installed) return;
            installed = true;

            UnitEventBus.OnUnitDamaged += OnLocalDamaged;   // 本端真掉血了（含"打到别人护盾副本"这一种）
            NetRoomFlow.OnShieldDamaged += OnRemoteDamaged; // 本机就是归属者 ⇒ 落地
        }

        // ==================== 本端 -> 网络 ====================

        /// <summary>本端有一笔伤害落在护盾罩子上 ⇒ 若不是本机自己的护盾，就把它送给归属者本机。</summary>
        static void OnLocalDamaged(GameObject victim, PEInt damage, GameObject caused, Collider hitBody, GameObject origin)
        {
            if (damage.RawFloat <= 0f) return;
            if (victim == null) return;
            if (NetApplyGuard.Applying) return;      // 远端应用里又派发了一次 ⇒ 别再往回发

            var bag = victim.GetComponentInParent<ShieldBag>();
            if (bag == null) return;                 // 只管背包的罩子（Boss / 神器护盾不在本桥职责内）

            // 本机就是归属者 ⇒ 本地已经真扣过（破盾/重塑/HUD 都在本机链上跑过），不用转发
            if (bag.IsLocalDriver) return;

            // 还没装到任何人身上（空投落地 / 被丢下）：没有归属者要通知，各端各自结算就行
            if (bag.Owner == null) return;

            // 归属者 = 这件背包装着的那个盟友实例（它没有 PlayerController，只有 FriendController）
            var owner = bag.Owner as Component;
            var friend = owner != null ? owner.GetComponent<FriendController>() : null;
            if (friend == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("护盾/伤害·发",
                    $"{bag.name} 装在盟友身上但取不到 FriendController ⇒ 这笔 {damage.RawFloat:F1} 护盾伤害不会同步");
                return;
            }

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;   // 单机 / 不在房：两端本来就一致

            FPSGame.Utils.NetSyncLog.SyncLog("护盾/伤害·发",
                $"归属 sid={friend.NetSid} 伤害={damage.RawFloat:F1} 落点={victim.transform.position:F2}");
            flow.SendShieldDamage(friend.NetSid, damage.RawFloat, victim.transform.position);
        }

        // ==================== 网络 -> 本端 ====================

        /// <summary>本机就是归属者 ⇒ 在本机这份罩子上真扣一次（事件只在目标是本机时到达，见 <c>NetRoomFlow.HandleShieldDamageNtf</c>）。</summary>
        static void OnRemoteDamaged(ShieldDamageMsg m)
        {
            if (m == null || m.Damage <= 0f) return;

            var player = ActorsManager.Player as Component;   // IActor 是接口，取组件得先转 Component
            if (player == null) return;                       // 单机还没建出玩家 / 已阵亡清场

            var bag = player.GetComponentInChildren<ShieldBag>(true);
            if (bag == null || !bag.IsLocalDriver)
            {
                FPSGame.Utils.NetSyncLog.Warn("护盾/伤害·收", $"收到 {m.Damage:F1} 点护盾伤害，但本机玩家身上没有已装备的护盾 ⇒ 丢弃");
                return;
            }

            NetApplyGuard.Begin();
            try
            {
                bag.ApplyRemoteDamage(m.Damage, new Vector3(m.X, m.Y, m.Z));
            }
            finally
            {
                NetApplyGuard.End();
            }

            FPSGame.Utils.NetSyncLog.SyncLog("护盾/伤害·收", $"扣 {m.Damage:F1}（落点={new Vector3(m.X, m.Y, m.Z):F2}）");
        }
    }
}
