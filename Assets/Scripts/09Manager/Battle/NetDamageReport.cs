using FPSGame.AI;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · 把"本端刚结算的这一笔伤害"报上网络的**唯一实现**。
    ///
    /// <para>▍为什么收成一处（2026-11-10）：这段逻辑以前在 <c>EnemyController._OnSettled</c> /
    /// <c>NetVehicleDamageBridge</c> / <c>NetKeiShieldBridge</c> / <c>NetDestructibleBridge</c> 各抄一份，
    /// 抄漏一次的表现就是"某个物件血量不同步"，而且每加一种物件就要再抄一份。</para>
    ///
    /// <para>▍方向：成员上报 4053 / 房主下发 4054（判据 <c>EnemyController.RemoteDrivenMovement</c>，与敌人那条一字同源）。
    /// 落地端（<c>EnemyNetBridge.ApplyRemoteDamage</c>）按 <c>NetId</c> 在全表找人、按部位下标取 Damageable ⇒ 与物件类型无关。</para>
    ///
    /// <para>▍调用方只负责"该不该报"的判据：回环门、号是否存在、是否已在别处报过。</para>
    /// </summary>
    public static class NetDamageReport
    {
        /// <summary>成分摊平的复用缓冲（只在同步打包内有效 —— 序列化是**同步**的，同 <c>EnemyController</c> 做法）。</summary>
        private static float[] _scratch;

        /// <summary>把这一笔报到网络。<paramref name="tag"/> 只用于日志分类。
        /// <returns>true = 确实发出去了。</returns></summary>
        public static bool Report(Actor victim, int dmg, Collider hitBody, GameObject caused, GameObject origin, string tag)
        {
            if (victim == null) return false;
            if (EnemyController.ApplyingRemoteDamage) return false;   // 回环门：正在应用远端那笔 ⇒ 别再往回发

            int netId = victim.NetId;
            if (netId == 0)
            {
                // 玩家/盟友的身份是 sid，本来就不参与号空间 ⇒ 静默跳过
                if (victim.Type != FPSGame.Core.UnitTypeEnum.Player && victim.Type != FPSGame.Core.UnitTypeEnum.Friend)
                {
                    // 有 Actor 却不取号 = 配置事故：上报会被 SendEnemyHit 静默挡掉，
                    // 表现就是"本端看得见命中、对端毫无反应" —— 这是排这类问题唯一的线索，别删
                    FPSGame.Utils.NetSyncLog.Warn("本地命中结算",
                        $"「{victim.ShowName}」({victim.Id}) 没有 NetId ⇒ **打了也不会同步到对端** 伤害={dmg}");
                }
                return false;
            }

            var hp = victim.GetComponent<IHealth>();
            // 0 伤害但带**非零成分**（冰冻/催化/破甲）也必须发，否则对端积蓄槽不动；纯被护盾吸收则不发。
            // ⚠ 判"非零值"而不是"成分列表非空"：列表可能带着全 0 的条目（口径与旧 EnemyController._OnSettled 一致）
            if (dmg <= 0 && !HasEffectGroups(hp)) return false;

            float[] groups = PackGroups(hp);

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return false;            // 单机 / 不在房：两端本来就一致

            int attackerNetId = NetIdOf(origin);
            int causedNetId = NetIdOf(caused);
            // 部位下标（两端同一把 prefab 同序 ⇒ 稳定；-1 = 认不出，接收端退回主躯干）
            int part = victim.IndexOfDamageable(hitBody);

            // ⚠ 先判开关再拼串：BulletLog 不是 [Conditional]，插值参数会无条件分配（每次命中 ~300B）
            if (FPSGame.Utils.NetSyncLog.BulletEnabled)
                FPSGame.Utils.NetSyncLog.BulletLog(tag,
                    $"netId={netId} 单位={victim.Id} 伤害={dmg} 起源号={attackerNetId} 直接来源号={causedNetId} " +
                    $"部位={part} 是房主={flow.IsHost} ⇒ {(EnemyController.RemoteDrivenMovement ? "上报房主" : "下发给成员")}");

            // 两个来源号：AttackerNetId = 起源（统计）；CausedNetId = 直接来源（显示 / AI）
            if (EnemyController.RemoteDrivenMovement)
                BattleEventBus.EnemyHit(netId, dmg, attackerNetId, groups, part, causedNetId);
            else
                BattleEventBus.EnemyDamaged(netId, dmg, attackerNetId, groups, part, causedNetId);

            return true;
        }

        /// <summary>这一发是否带**非零成分**（异常状态积蓄 / 破甲都算）：0 伤害但成分非零时也必须过网。
        /// <para>⚠ 口径是"值 &gt; 0"而不是"列表非空"：列表可能带着全 0 的条目（旧 <c>EnemyController._OnSettled</c> 同口径）。</para></summary>
        private static bool HasEffectGroups(IHealth hp)
        {
            var g = hp != null ? hp.LastRawGroups : null;
            if (g == null) return false;

            for (int i = 0, l = g.Count; i < l; ++i)
            {
                var e = g[i];
                if (e != null && e.Value > 0) return true;
            }
            return false;
        }

        /// <summary>取一个物体的单位号（没有 Actor 或没号 ⇒ 0）。</summary>
        public static int NetIdOf(GameObject go)

        {
            if (go == null) return 0;
            var actor = go.GetComponent<Actor>();
            return actor != null ? actor.NetId : 0;
        }

        /// <summary>把 <c>Health.LastRawGroups</c> 摊平成 <c>[count, type, value, ...]</c>（口径同 EnemyNetBridge 的解包）。
        /// <para>⚠ 只在下发/上报的**同步打包**里有效（复用缓冲）；拿不到 ⇒ null（接收端退回中性成分）。</para></summary>
        private static float[] PackGroups(IHealth hp)
        {
            var g = hp != null ? hp.LastRawGroups : null;
            if (g == null || g.Count == 0) return null;

            int need = 1 + g.Count * 2;
            if (_scratch == null || _scratch.Length < need) _scratch = new float[need];

            _scratch[0] = g.Count;
            for (int i = 0; i < g.Count; ++i)
            {
                _scratch[1 + i * 2] = (int)g[i].Key;          // DamageTypeEnum 的 int
                _scratch[2 + i * 2] = g[i].Value.RawFloat;    // 成分**数值**（不是系数）
            }
            return _scratch;
        }
    }
}
