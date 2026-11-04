using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Net;
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **凯伊护盾**（<see cref="KeiShield"/> 的罩子）伤害同步桥（09）。
    ///
    /// <para>▍为什么需要：罩子是"独立可破坏物件"，但**没有 EnemyController**，所以既不会被
    /// <c>EnemyController</c> 的上报链带上，也不属于 <c>NetDestructibleBridge</c> 认的
    /// <c>UnitTypeEnum.Other</c>（盾的 Actor 是 <c>None</c>，故意避开那条桥的死亡消息）。
    /// ⇒ 不写这条，就是"我这边把盾打破了、你那边还是满盾"。</para>
    ///
    /// <para>▍寻址：盾自带 Actor（<c>type = None</c>，有派生号）⇒ 走与敌人相同的统一伤害通道
    /// （4053 上报 / 4054 下发，键 = <c>Actor.NetId</c>）；落地端 <c>EnemyNetBridge.ApplyRemoteDamage</c>
    /// 用 <c>FindActor</c> 在全表按号找人、再 <c>ResolveDamageable(actor, part)</c> 取盾的 Damageable ⇒ 零改动。</para>
    ///
    /// <para>▍破盾/重塑不需要新消息：两端盾血由伤害同步保持一致 ⇒ 破盾时刻一致，
    /// <c>ShieldRebuild</c> 各自计时 10 秒重塑即可（延迟差只有网络往返那几帧）。</para>
    ///
    /// <para>▍自举：<c>[RuntimeInitializeOnLoadMethod]</c>（同 <c>NetShieldBagBridge</c> / <c>NetVehicleBridge</c>）。</para>
    /// </summary>
    public static class NetKeiShieldBridge
    {
        static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (installed) return;
            installed = true;

            UnitEventBus.OnUnitDamaged += OnLocalDamaged;
        }

        /// <summary>本端有一笔伤害落在凯伊的罩子上 ⇒ 房主下发全体 / 成员上报房主。</summary>
        static void OnLocalDamaged(GameObject victim, PEInt damage, GameObject caused, Collider hitBody, GameObject origin)
        {
            if (victim == null) return;
            // 0 伤害不参与：护盾只吃实打实的伤害（没有异常状态积蓄那套）
            if (damage.RawFloat <= 0f) return;
            // 回环门：本端正在重放远端那笔伤害 ⇒ 不能再往回发（否则两端互相转发）
            if (EnemyController.ApplyingRemoteDamage) return;
            if (!victim.TryGetComponent(out KeiShield _)) return;

            var actor = victim.GetComponent<Actor>();
            if (actor == null || actor.NetId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;   // 单机 / 不在房：两端本来就一致

            int dmg = Mathf.RoundToInt(damage.RawFloat);

            // 两个来源号（口径同 EnemyController._OnDamaged）：
            //   attackerNetId = 起源（统计）；causedNetId = 直接来源（显示/AI）
            var agent = origin != null ? origin.GetComponent<Actor>() : null;
            int attackerNetId = agent != null ? agent.NetId : 0;
            var causer = caused != null ? caused.GetComponent<Actor>() : null;
            int causedNetId = causer != null ? causer.NetId : 0;

            // 命中体 → 部位下标（两端同一把 prefab 同序 ⇒ 稳定；-1 = 认不出，接收端退回主躯干）
            int part = actor.IndexOfDamageable(hitBody);

            FPSGame.Utils.NetSyncLog.BulletLog("凯伊盾·伤",
                $"netId={actor.NetId} 伤害={dmg} 起源号={attackerNetId} 直接来源号={causedNetId} 部位={part} 是房主={flow.IsHost}");

            if (flow.IsHost) BattleEventBus.EnemyDamaged(actor.NetId, dmg, attackerNetId, null, part, causedNetId); // 房主：下发全体
            else BattleEventBus.EnemyHit(actor.NetId, dmg, attackerNetId, null, part, causedNetId);                 // 成员：上报房主（由他转发）
        }
    }
}
