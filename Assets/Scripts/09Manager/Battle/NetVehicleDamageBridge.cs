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
    ///
    /// <para>▍键 = 载具 <c>Actor.NetId</c>（房主随 4028 分配、两端一致，见 <c>ActorsManager.VehicleNetIdBase</c>）；
    /// 通道 = 既有统一伤害（成员 4025 上报 / 房主 4040 下发，死亡靠同一类的 <c>IsDeath</c>）。</para>
    ///
    /// <para>▍**下行刻意不在这里**：<c>EnemyNetBridge</c> 的应用路径是按 <c>NetId</c> 在 <c>ActorsManager.Actors</c>
    /// 全表里找、**不按 <c>UnitTypeEnum</c> 过滤** ⇒ 载具天然吃得下（含死亡那条 <c>ApplyRemoteDeath</c>）。
    /// 本桥只补两件真正缺的东西：</para>
    /// <list type="number">
    ///   <item><b>上行</b>：载具没有 <c>EnemyController</c>，敌人那条上报链挂不上；而 <c>NetDestructibleBridge</c>
    ///         只认 <c>UnitTypeEnum.Other</c> ⇒ 载具"被打掉血"今天**根本没人报**；</item>
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

        /// <summary>成分摊平的复用缓冲（只在"打包网络消息"内有效 —— 序列化是**同步**的，见 <c>EnemyController</c> 同款做法）。</summary>
        static float[] _groupScratch;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            Damageable.LocalDamageGate = Gate;
            UnitEventBus.OnUnitDamaged += OnLocalDamaged;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;
            Damageable.LocalDamageGate = null;
            UnitEventBus.OnUnitDamaged -= OnLocalDamaged;
        }

        // ==================== 权威门：这笔伤害该不该在本端落地 ====================

        /// <summary>见 <c>Damageable.LocalDamageGate</c>。</summary>
        static bool Gate(Damageable d, DamagePacket p)
        {
            // 只管载具：别的单位（敌人 / 玩家 / 盟友 / 可破坏物）走各自既有通道，一律放行
            if (!IsVehicle(d)) return true;

            // 房主 / 单机：本地就是权威 ⇒ 照常落地（随后会在 OnLocalDamaged 里广播出去）
            if (FPSGame.Data.BattleState.IsVehicleDamageAuthority) return true;

            // 正在应用远端下发的那笔（EnemyNetBridge.ApplyRemoteDamage）⇒ 必须落地，否则永远收不到血
            if (EnemyController.ApplyingRemoteDamage) return true;

            // 非权威端：**玩家侧来源**（本机玩家/盟友）落地 + 上报（那笔房主算不出来）；
            //             **AI 来源**（敌人）不落地 —— 那笔由房主那台算并广播下来（两端各模拟一遍 ⇒ 只认房主那份）。
            return IsPlayerSide(p.DamageOrigin) || IsPlayerSide(p.DamageCaused);
        }

        static bool IsVehicle(Damageable d)
        {
            if (d == null) return false;
            var actor = d.GetComponentInParent<Actor>();
            return IsVehicleActor(actor);
        }

        static bool IsVehicleActor(Actor actor)
            => actor != null && actor.GetComponent<VehicleController>() != null;

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

        // ==================== 上行：本端真掉血了 -> 上报 / 下发 ====================

        /// <summary>载具**真掉血**了（<c>Health.TakeDamage</c> 里真伤害 &gt; 0 时派发）⇒ 成员上报 4025 / 房主下发 4040。
        /// <para>▍与 <c>NetDestructibleBridge.OnLocalDamaged</c> 同款，只换过滤条件为"载具"。</para>
        /// <para>▍"落地了才报"就够：非权威端的 AI 伤害被 <see cref="Gate"/> 挡在伤害入口 ⇒ 根本不会派发这个事件
        /// ⇒ 自动做到"AI 来源的载具伤害只有房主算"。</para>
        /// </summary>
        static void OnLocalDamaged(GameObject victim, PEInt damage, GameObject caused, Collider hitBody, GameObject origin)
        {
            if (EnemyController.ApplyingRemoteDamage) return;   // 本端正在应用远端伤害 ⇒ 挡回环（否则两端互相转发）
            if (victim == null) return;
            if (!victim.TryGetComponent(out Actor actor)) return;
            if (!IsVehicleActor(actor)) return;

            int netId = actor.NetId;
            if (netId == 0)
            {
                // 没号 = 同步不了（多半是那一发空投没带号 / 旧端）⇒ 留痕，别静默
                FPSGame.Utils.NetSyncLog.Warn("载具/血·发", $"{actor.ShowName}({actor.Id}) 没有 NetId ⇒ 载具血量同步不了");
                return;
            }

            int dmg = Mathf.RoundToInt(damage.RawFloat);
            if (dmg <= 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;   // 单机 / 不在房：两端本来就一致

            // 两个来源号一起带（口径同敌人那条）：起源 = 统计、直接来源 = 显示/AI
            var agent = origin != null ? origin.GetComponent<Actor>() : null;
            int attackerNetId = agent != null ? agent.NetId : 0;
            var causer = caused != null ? caused.GetComponent<Actor>() : null;
            int causedNetId = causer != null ? causer.NetId : 0;

            // 部位下标（两端同一把 prefab 同序 ⇒ 稳定；-1 = 认不出，接收端自己挑主躯干）
            int part = actor.IndexOfDamageable(hitBody);

            // ★ 原始伤害成分：接收端要靠它补"护甲破坏（含破甲流血）"与"异常状态积蓄"
            //   （只带总伤害时那两条分支整段被跳过 —— 见 Health.LastRawGroups / §7.2e）
            var hp = actor.GetComponent<IHealth>();
            float[] groups = PackGroups(hp);

            FPSGame.Utils.NetSyncLog.BulletLog("载具/血·发",
                $"{actor.ShowName}({actor.Id}) netId={netId} 伤害={dmg} 起源号={attackerNetId} 直接来源号={causedNetId} 部位={part} 是房主={flow.IsHost}");

            if (flow.IsHost) BattleEventBus.EnemyDamaged(netId, dmg, attackerNetId, groups, part, causedNetId);  // 房主：下发全体
            else BattleEventBus.EnemyHit(netId, dmg, attackerNetId, groups, part, causedNetId);                  // 成员：上报房主（由他转发）
        }

        /// <summary>把 <c>Health.LastRawGroups</c> 摊平成 <c>[count, type, coeff, ...]</c>（口径同 <c>EnemyController.PackDamageGroups</c>）。
        /// <para>⚠ 只在下发/上报的**同步打包**里有效（复用缓冲）；拿不到 ⇒ null（接收端退回中性成分）。</para></summary>
        static float[] PackGroups(IHealth hp)
        {
            var g = hp != null ? hp.LastRawGroups : null;   // 结算期间有效；TakeDamage 返回后就被清空
            if (g == null || g.Count == 0) return null;

            int need = 1 + g.Count * 2;
            if (_groupScratch == null || _groupScratch.Length < need) _groupScratch = new float[need];

            _groupScratch[0] = g.Count;
            for (int i = 0; i < g.Count; ++i)
            {
                _groupScratch[1 + i * 2] = (int)g[i].Key;    // DamageTypeEnum 的 int
                _groupScratch[2 + i * 2] = g[i].Value;       // 系数
            }
            return _groupScratch;
        }
    }
}
