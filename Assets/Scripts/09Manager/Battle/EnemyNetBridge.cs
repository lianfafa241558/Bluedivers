using System.Collections.Generic;
using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Net;   // DamageMsg（伤害/生死族合并后的统一 DTO）
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机战斗同步桥（09）：把玩法层（06）的"移动意图 / 命中"送上网，把网络下发的"意图 / 死亡"落回玩法层。
    /// 每局一份，由 <see cref="WaveManager"/> 在 Awake/OnDestroy 里 Install/Uninstall。
    ///
    /// <para>▍分工：**房主**是意图与生死的权威（发移动目标、结算伤害、广播死亡）；成员只做三件事 ——
    /// 应用房主给的目标点（本地各自算路径）、上报自己的命中、按死亡广播干掉副本。</para>
    /// </summary>
    public static class EnemyNetBridge
    {
        static bool installed;

        /// <summary>NetId → 还没落到单位身上的移动目标（"消息比副本先到"时暂存，见 <see cref="OnRemoteMove"/>）。</summary>
        static readonly Dictionary<int, Vector3> _pendingMoves = new Dictionary<int, Vector3>();

        /// <summary>暂存时刻（<c>Time.unscaledTime</c>）—— 超过 <see cref="PendingTtl"/> 还落不下去就丢弃并告警。</summary>
        static readonly Dictionary<int, float> _pendingTime = new Dictionary<int, float>();

        /// <summary>暂存的有效期：够长（覆盖"成员开波比房主晚半步"），又不至于永久堆积。</summary>
        const float PendingTtl = 5f;

        /// <summary>遍历暂存表时的临时键列表（避免边遍历边删除）。</summary>
        static readonly List<int> _flushScratch = new List<int>();

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _pendingMoves.Clear();
            _pendingTime.Clear();

            BattleEventBus.OnEnemyMove += OnLocalMove;

            BattleEventBus.OnEnemyHit += OnLocalHit;
            BattleEventBus.OnEnemyDamaged += OnLocalDamaged;
            UnitEventBus.OnEnemyDead += OnLocalDeath;
            UnitEventBus.OnEnemyCreate += OnEnemyCreated;
            // ★ 特殊单位（炮塔/探照灯…）走同一套：它们现在也有 NetId（独立号段，见 ActorsManager.SpecNetIdBase）
            UnitEventBus.OnSpecUnitDead += OnLocalDeath;
            UnitEventBus.OnSpecUnitCreate += OnEnemyCreated;

            FPSGame.Net.NetRoomFlow.OnEnemyMove += OnRemoteMove;
            FPSGame.Net.NetRoomFlow.OnEnemyHitUp += OnRemoteHit;
            FPSGame.Net.NetRoomFlow.OnEnemyDamaged += OnRemoteDamaged;
            // ⚠ 死亡已并入 4040（DamageMsg.IsDeath）⇒ 不再订阅 OnEnemyDied（4026 已不外发）
            FPSGame.Net.NetRoomFlow.OnHostLost += OnHostLost;   // ★ 房主掉了 ⇒ 恢复本端自主
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;
            _pendingMoves.Clear();
            _pendingTime.Clear();

            BattleEventBus.OnEnemyMove -= OnLocalMove;
            BattleEventBus.OnEnemyHit -= OnLocalHit;
            BattleEventBus.OnEnemyDamaged -= OnLocalDamaged;
            UnitEventBus.OnEnemyDead -= OnLocalDeath;
            UnitEventBus.OnEnemyCreate -= OnEnemyCreated;
            UnitEventBus.OnSpecUnitDead -= OnLocalDeath;
            UnitEventBus.OnSpecUnitCreate -= OnEnemyCreated;

            FPSGame.Net.NetRoomFlow.OnEnemyMove -= OnRemoteMove;
            FPSGame.Net.NetRoomFlow.OnEnemyHitUp -= OnRemoteHit;
            FPSGame.Net.NetRoomFlow.OnEnemyDamaged -= OnRemoteDamaged;
            FPSGame.Net.NetRoomFlow.OnHostLost -= OnHostLost;
        }

        /// <summary>
        /// 房主断开（退出 / 崩溃 / 断网）⇒ **恢复本端自主决策**。
        ///
        /// <para>▍为什么必须做（2026-10-10 用户实测）：房主崩了以后没人告诉成员，<c>RemoteDrivenMovement</c>
        /// 还是 true ⇒ 敌人永远等房主下发（再也不动），而本端 AI 每次决策都被作废、每帧刷一条
        /// 「[AI/移动·本端作废]」。恢复自主后本局与房主的那份世界不再一致 —— 这是**预期**
        /// （房主已经没了，能继续玩下去比一致更重要）。</para>
        /// </summary>
        static void OnHostLost()
        {
            EnemyController.RemoteDrivenMovement = false;
            // 房主没了 ⇒ tick 伤害也没人广播了 ⇒ 本端重新变成 tick 权威（否则怪再也不流血/不掉异常血）
            FPSGame.Data.BattleState.IsTickDamageAuthority = true;
            _pendingMoves.Clear();
            FPSGame.Utils.NetSyncLog.Warn("房主断开", "已恢复本端自主移动决策 + tick 伤害权威（本局不再与房主一致，属预期）");
        }

        #region 本端 → 网络

        static void OnLocalMove(int netId, Vector3 destination)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            // ⚠ 限流 2s/单位：这条是"**每次目标变化**一条"，战斗中一只怪能有几十条 ⇒ 不限流会把 Console 刷掉
            //   （2026-10-10 用户口径"没用的内容先不打印"）。
            //   另：原来的 `有人在房={(SelfSid != 0u)}` 在**房主端恒为 False**（房主就是 sid=0）⇒ 标签误导，已去掉。
            FPSGame.Utils.NetSyncLog.AiLog("敌人移动·发",
                $"netId={netId} 目标={destination:F2} 是房主={(flow != null && flow.IsHost)} 本机sid={(flow != null ? flow.SelfSid.ToString() : "-")}",
                netId, 2f);
            flow?.SendEnemyMove(netId, destination);   // 内部判 IsHost
        }

        static void OnLocalHit(int netId, int damage, int attackerNetId, float[] groups, int part)
        {
            // 数据说话：本端上报的每一发 —— 与房主侧的「远端伤害落地」配对，中间没有别的步骤，
            // 两条日志一对一出现才说明"客机打中 ⇒ 主机真扣血"这条链是通的
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            FPSGame.Utils.NetSyncLog.BulletLog("命中上报·发", $"netId={netId} 伤害={damage} 攻击者号={attackerNetId} 部位={part} 成分={GroupText(groups)} 是房主={(flow != null && flow.IsHost)} 本机sid={(flow != null ? flow.SelfSid.ToString() : "-")}");
            flow?.SendEnemyHit(netId, damage, attackerNetId, groups, EncodePart(part));   // 内部判"成员才发"
        }

        static void OnLocalDamaged(int netId, int damage, int attackerNetId, float[] groups, int part)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            FPSGame.Utils.NetSyncLog.BulletLog("伤害下发·发", $"netId={netId} 伤害={damage} 攻击者号={attackerNetId} 部位={part} 成分={GroupText(groups)} 是房主={(flow != null && flow.IsHost)}");
            flow?.SendEnemyDamaged(netId, damage, 0u, attackerNetId, groups, EncodePart(part));   // 内部判"房主才发"
        }

        /// <summary>部位下标 → 线上编码（<c>Part</c> 用"+1"表示：<c>0</c> = 未指定 ⇒ 接收端自己挑；
        /// 因为 <c>0</c> 本身是**合法下标**，不能用它当"没值"）。</summary>
        static int EncodePart(int part) => part >= 0 ? part + 1 : 0;

        /// <summary>线上 <c>Part</c> → 部位下标（<c>-1</c> = 未指定）。</summary>
        static int DecodePart(int part) => part > 0 ? part - 1 : -1;

        static void OnLocalDeath(Actor actor)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (actor == null || flow == null || actor.NetId == 0) return;
            flow.SendEnemyDied(actor.NetId);                                       // 内部判 IsHost
        }

        #endregion

        #region 网络 → 本端

        static void OnRemoteMove(int netId, Vector3 destination)
        {
            FlushPendingMoves();   // ★ 先兜底清一遍历史暂存（见 FlushPendingMoves 的注释：这是"单位永久不动"的修法）

            var enemy = Find(netId);
            if (enemy != null)
            {
                FPSGame.Utils.NetSyncLog.AiLog("敌人移动·收", $"netId={netId} 目标={destination:F2} 副本=已找到 ⇒ 应用", netId, 2f);
                enemy.ApplyRemoteDestination(destination);
                return;
            }

            // 本端这份副本还没建出来（成员靠 WaveStartSync 才生成本波单位，比房主晚半步）⇒ 暂存。
            // ⚠ 不能直接丢：房主只在"目标变化"时发（<1m 去重），丢了就没有第二条 ⇒ 那只怪会一直原地不动。
            if (netId != 0)
            {
                if (!_pendingMoves.ContainsKey(netId))   // 同一个 netId 反复暂存时只打一次（否则又是一只怪一行）
                    FPSGame.Utils.NetSyncLog.Warn("敌人移动·收", $"netId={netId} 目标={destination:F2} 本端还没有这份副本 ⇒ 暂存（等副本建出来补发）");
                _pendingMoves[netId] = destination;
                _pendingTime[netId] = Time.unscaledTime;
            }
        }

        /// <summary>
        /// 把暂存的移动目标**再试一遍**（找到副本就应用、超时就丢弃并告警）。
        ///
        /// <para>▍为什么不能只在 <see cref="OnEnemyCreated"/> 里补发（2026-10-10 修）：那条路只在"创建事件发生的
        /// **那一刻**拿 <c>actor.NetId</c> 去查表"——若 <c>NetId</c> 是创建**之后**才写上的、或该 netId 的副本
        /// 根本没被本端建出来，这条暂存就**永远不会被应用**。表现就是"房主端那只怪在跑、成员端它原地不动"
        /// （= 位置不同步里最刺眼的一种，用户 2026-10-10 报"大量单位位置不同步"）。
        /// 现在改成：每收到一条移动消息就兜底清一遍 + 超时丢弃并告警 ⇒ 把"静默永久卡死"降级为"最多慢一条消息"。</para>
        /// </summary>
        static void FlushPendingMoves()
        {
            if (_pendingMoves.Count == 0) return;

            float now = Time.unscaledTime;
            _flushScratch.Clear();
            foreach (var kv in _pendingMoves) _flushScratch.Add(kv.Key);

            for (int i = 0; i < _flushScratch.Count; ++i)
            {
                int id = _flushScratch[i];
                var enemy = Find(id);
                if (enemy != null)
                {
                    Vector3 dest;
                    if (!_pendingMoves.TryGetValue(id, out dest)) continue;
                    _pendingMoves.Remove(id);
                    _pendingTime.Remove(id);
                    FPSGame.Utils.NetSyncLog.AiLog("敌人移动·补发", $"netId={id} 目标={dest:F2}（兜底清暂存）", id, 2f);
                    enemy.ApplyRemoteDestination(dest);
                    continue;
                }

                float t;
                if (_pendingTime.TryGetValue(id, out t) && now - t > PendingTtl)
                {
                    _pendingMoves.Remove(id);
                    _pendingTime.Remove(id);
                    FPSGame.Utils.NetSyncLog.Warn("敌人移动·放弃",
                        $"netId={id} 暂存超时 {PendingTtl:0.#}s 仍没有本端副本 ⇒ 丢弃（该单位在本端不会动：房主端可能开了本端没复刻的波次）");
                }
            }
            _flushScratch.Clear();
        }

        /// <summary>本端把这只怪建出来了 ⇒ 把"早到"的移动目标补上（同帧应用，不必等房主下一次下发）。</summary>
        static void OnEnemyCreated(Actor actor)
        {
            if (actor == null || _pendingMoves.Count == 0) return;

            Vector3 destination;
            if (_pendingMoves.TryGetValue(actor.NetId, out destination))
            {
                _pendingMoves.Remove(actor.NetId);
                _pendingTime.Remove(actor.NetId);
                var enemy = actor.GetComponent<EnemyController>();
                FPSGame.Utils.NetSyncLog.AiLog("敌人移动·补发", $"netId={actor.NetId} 目标={destination:F2} 本端该怪={actor.Id} 找到控制器={(enemy != null)}", actor.NetId, 1f);
                if (enemy != null) enemy.ApplyRemoteDestination(destination);
            }

            // ⚠ 同一个创建事件也可能是**别的** netId 的补发时机（NetId 写上的早晚不定）⇒ 顺带全清一遍
            FlushPendingMoves();
        }

        /// <summary>房主侧处理成员上报的"打中 / 打死"（统一的 <c>DamageMsg</c>）。
        /// <para>走正常伤害链 ⇒ 打出致死伤害时本端自己会进 <c>OnEnemyDead</c>，再由 <see cref="OnLocalDeath"/> 广播"死了"。</para></summary>
        static void OnRemoteHit(DamageMsg m)
        {
            if (m == null) return;

            // "只报死了"的那种（Damage = 0 + IsDeath，原 4026 的语义）⇒ 本地照样跑死亡链
            if (m.Damage <= 0)
            {
                if (m.IsDeath) ApplyRemoteDeath(m);
                return;
            }

            ApplyRemoteDamage(m.Sid, m.NetId, m.Damage, m.AttackerNetId, m.Groups, DecodePart(m.Part));

            // ★ 结算完**再转发给除来源外的其他成员**：原来应用完就结束了（`ApplyingRemoteDamage` 把回传也挡了）
            //   ⇒ 成员 A 打出的伤害**从不到达成员 B**：B 那边这只怪的血一动不动，直到死亡消息才突然消失
            //   （≥3 人必现；2 人时 B 就是房主，所以测不出来）。⚠ 必须排除来源端（它本地已扣过一次）。
            FPSGame.Net.NetRoomFlow.Instance?.SendEnemyDamaged(m.NetId, m.Damage, m.Sid, m.AttackerNetId, m.Groups, m.Part);
        }

        /// <summary>成员侧应用房主下发的"打中 / 打死"（房主是血量权威）。⚠ 死亡是"打到 0 血"的特例，同一条消息带来。</summary>
        static void OnRemoteDamaged(DamageMsg m)
        {
            if (m == null) return;

            if (m.Damage > 0) ApplyRemoteDamage(m.Sid, m.NetId, m.Damage, m.AttackerNetId, m.Groups, DecodePart(m.Part));
            if (m.IsDeath) ApplyRemoteDeath(m);
        }

        /// <summary>
        /// 把远端伤害落到本端那份副本上，并**当作一次正常命中**跑完整条链（受击特效 / 血条 / 仇恨 / 命中统计）。
        ///
        /// <para>▍为什么必须给 <c>DamageSource</c>：老实现是 <c>DamageSource=null + NoSource=true</c> ⇒ 那只怪
        /// **静默掉血** —— 对端屏幕上没有受击表现、血条也不刷新，看起来就是"这边命中、那边没命中"
        /// （2026-10-09 用户实测）。传上"开枪者那个单位的实例"后，走的正是本端自己挨打时的同一条链。</para>
        ///
        /// <para>⚠ 解析不到开枪者（盟友实例还没建出来 / 已离开）⇒ 退回静默结算：宁可不表现，也别报错。</para>
        /// </summary>
        static void ApplyRemoteDamage(uint attackerSid, int netId, int damage, int attackerNetId = 0, float[] groups = null, int part = 0)
        {
            var actor = FindActor(netId);
            if (actor == null || damage <= 0)
            {
                // 数据说话：远端报了命中，却在本端找不到对应副本（或伤害<=0）⇒ **静默丢弃**，对方那边是白打的
                FPSGame.Utils.NetSyncLog.Warn("远端伤害落地", $"找不到 netId={netId} 的本端副本（或伤害={damage}<=0）⇒ 这一发被静默丢弃");
                return;
            }

            var dmg = ResolveDamageable(actor, part);
            if (dmg == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("远端伤害落地", $"netId={netId} 的副本找不到任何 IDamageable ⇒ 这一发被静默丢弃（本端该怪={actor.Id}）");
                return;
            }

            // ★ 来源**三级解析**：① 攻击者单位的号（怪 / 炮塔 / 凯伊这类非玩家来源）→ ② 上报者的盟友实例（玩家）
            //   → ③ null（NoSource）。⚠ 顺序不能反：只按 sid 找会把"炮塔打怪"误算成"上报者那个玩家"，
            //   对端 DetectionModule 的阵营过滤/受击改目标就会跟开枪端不一致。
            GameObject source = null;
            var srcActor = attackerNetId != 0 ? FindActor(attackerNetId) : null;
            if (srcActor != null)
            {
                var srcComp = srcActor as Component;
                if (srcComp != null) source = srcComp.gameObject;
            }
            if (source == null) source = ResolveRemoteUser(attackerSid);
            // ★ 受击体：远端这条链没有真实命中信息，而 `Health.TakeDamage` 要拿它的 bounds 算受击法线
            //   （不传 ⇒ 那边 `damageAffected.bounds` 直接 NRE，整条消息处理失败；2026-10-10 用户实测）。
            var hitCollider = ResolveHitCollider(dmg, actor.transform.position);

            FPSGame.Utils.NetSyncLog.AiLog("远端伤害落地", $"netId={netId} 伤害={damage} 开枪者sid={attackerSid} 本端实例={(source != null ? source.name : "<没解析到，将静默结算>")} 受击体={(hitCollider != null ? hitCollider.name : "<无>")} 落点={actor.transform.position:F2}");
            EnemyController.ApplyingRemoteDamage = true;
            try
            {
                dmg.InflictDamage(new DamagePacket
                {
                    Damage = (PEInt)damage,
                    // ⚠ DamageGroups 不能空（InflictDamage 见空就直接返回）⇒ 拿不到成分时给一条中性的普通伤害成分。
                    // ★ 能拿到就用**开枪端的原始成分**：接收端要靠它补"护甲破坏"（Damageable.ArmorDestruction，含破甲后的流血）
                    //   与"异常状态积蓄"（Health.HandleDamage → AddAboGauge）—— 只给总伤害时这两条分支整段被跳过，
                    //   表现就是"主机把怪冻住/破甲了，成员那边它照常跑、护甲还在"（2026-10-12 修）。
                    DamageGroups = UnpackDamageGroups(groups) ?? NeutralGroups(),
                    DamageSource = source,
                    NoSource = source == null,
                    Pos = actor.transform.position,
                    damageAffected = hitCollider,
                    // ★ `damage` 是**开枪端算完护甲/爆炸抗性的最终值**（来自 `EnemyController._OnDamaged` 的上报）
                    //   ⇒ 本端跳过那两个乘区，否则护甲被减两次（两端血量/生死不一致，2026-10-11 修）。
                    Resolved = true,
                });
            }
            finally { EnemyController.ApplyingRemoteDamage = false; }

            // 数据说话：落完之后本端这份副本的血量 —— 与"开枪端自己看到的血量"对照，一眼看出两边是否同步
            var hp = actor.GetComponent<IHealth>();
            FPSGame.Utils.NetSyncLog.BulletLog("远端伤害落地后", $"netId={netId} 扣={damage} 本端该怪={actor.Id} 血量=" +
                (hp != null ? hp.GetHpCurrent().ToString("F0") + "/" + hp.GetHpMax().ToString("F0") : "<无生命组件>"));
        }

        /// <summary>解开枪端摊平的伤害成分 <c>[count, type, coeff, ...]</c>（见 <c>EnemyController.PackDamageGroups</c>）；拿不到 ⇒ null。</summary>
        static List<SKVP<DamageTypeEnum, float>> UnpackDamageGroups(float[] flat)
        {
            if (flat == null || flat.Length < 1) return null;

            int count = (int)flat[0];
            if (count <= 0 || flat.Length < 1 + count * 2) return null;

            var list = new List<SKVP<DamageTypeEnum, float>>(count);
            for (int i = 0; i < count; ++i)
                list.Add(new SKVP<DamageTypeEnum, float>((DamageTypeEnum)(int)flat[1 + i * 2], flat[2 + i * 2]));
            return list;
        }

        /// <summary>没有原始成分时的中性成分（Gun × 1）—— 与老行为一致（只扣总伤害，不动护甲/积蓄槽）。</summary>
        static List<SKVP<DamageTypeEnum, float>> NeutralGroups()
            => new List<SKVP<DamageTypeEnum, float>> { new SKVP<DamageTypeEnum, float>(default, 1f) };

        /// <summary>日志用：把摊平的成分写成 `Gun×1+Burn×0.3` 这种可读串。</summary>
        static string GroupText(float[] flat)
        {
            if (flat == null || flat.Length < 1) return "-";

            int count = Mathf.Clamp((int)flat[0], 0, (flat.Length - 1) / 2);
            if (count <= 0) return "-";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < count; ++i)
            {
                if (i > 0) sb.Append('+');
                sb.Append((DamageTypeEnum)(int)flat[1 + i * 2]).Append('×').Append(flat[2 + i * 2].ToString("0.##"));
            }
            return sb.ToString();
        }

        static NetFriendBridge _friendBridge;

        /// <summary>
        /// 取这份副本的"可伤害组件"。
        ///
        /// <para>▍为什么不能只 <c>actor.GetComponent&lt;IDamageable&gt;()</c>：不少敌人的伤害体挂在**子物体**上
        /// （子物体上的 <c>Damageable</c>／HitBox；<c>Actor.Awake</c> 也是用
        /// <c>GetComponentsInChildren&lt;Damageable&gt;()</c> 收的），根上取不到
        /// ⇒ 远端伤害被静默丢弃。表现就是「客机把怪打死了、主机这只怪毫发无伤」（2026-10-10 实测：
        /// 主机日志 `[异常/远端伤害落地] netId=15 的副本没有 IDamageable`）。</para>
        ///
        /// <para>▍顺序：主躯干（<c>Actor.MainDamageable</c> = <c>IHealth.GetMainPart()</c>）→ 根上组件 →
        /// <c>Actor.Damageables</c>（预制体里配好的肢体表）→ 子物体里的任意 <c>IDamageable</c>（含 inactive）。
        /// 选"主躯干"优先是为了让远端伤害打在**伤害结算该打的那一部件**上（弱点倍率/装甲都挂在它身上）。</para>
        /// </summary>
        static IDamageable ResolveDamageable(Actor actor, int part = 0)
        {
            if (actor == null) return null;

            // ★ 远端带了命中部位下标（`DamageMsg.Part` 已解码成下标；<c>-1</c> = 未指定）⇒ 优先按它取 ——
            //   两端"挑部位"的算法不完全等价（装甲/弱点结算会差一点），传下标才能打在同一部位上。
            if (part >= 0)
            {
                var partList = actor.Damageables;
                if (partList != null && part < partList.Length && partList[part] != null) return partList[part];
            }

            var main = actor.MainDamageable;
            if (main != null) return main;

            var own = actor.GetComponent<IDamageable>();
            if (own != null) return own;

            var parts = actor.Damageables;
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; ++i)
                {
                    if (parts[i] != null) return parts[i];
                }
            }

            var all = actor.GetComponentsInChildren<IDamageable>(true);
            for (int i = 0; i < all.Length; ++i)
            {
                if (all[i] != null) return all[i];
            }
            return null;
        }

        /// <summary>
        /// 给远端伤害找一个"受击体"（<c>DamagePacket.damageAffected</c>）：优先这个伤害体自己身上的碰撞体，
        /// 其次它的子物体。找不到给 null —— 引擎侧（<c>Health.HitNormal</c>）已能容忍 null，只是法线兜底朝上。
        /// <para>▍为什么要传：受击表现（火花/弹痕朝向、<c>OnHit</c> 订阅者）要拿它的 bounds 算，传 null 会让
        /// 表现链退化。取"最近的那个"没必要 —— 这是同步伤害，本来就没有真实命中点。</para>
        /// </summary>
        static Collider ResolveHitCollider(IDamageable dmg, Vector3 pos)
        {
            var mono = dmg as Component;
            if (mono == null) return null;

            var own = mono.GetComponent<Collider>();
            return own != null ? own : mono.GetComponentInChildren<Collider>();
        }

        /// <summary>权威 sid → 本端那个玩家的实例（<c>0</c> = 房主）。解析不到给 null。</summary>
        static GameObject ResolveRemoteUser(uint sid)
        {
            // ⚠ NetFriendBridge 没有静态 Instance（挂在常驻 GameRoot 上的 I_GlobaManager）⇒ 惰性解析一次并缓存
            if (_friendBridge == null) _friendBridge = UnityEngine.Object.FindObjectOfType<NetFriendBridge>();
            return _friendBridge != null && _friendBridge.TryGetFriendObject(sid, out GameObject go) ? go : null;
        }

        /// <summary>【远端说"它死了"】在本端干掉那份副本（<c>DamageMsg.IsDeath</c>；原 4026 / 4043 的语义）。
        /// <para>▍寻址：<c>NetId</c> 优先；为 0 时退回无号可破坏物的兜底键（<c>Key</c> = FNV(Id+位置0.1m)）。</para>
        /// <para>⚠ 与"这一发打到 0"不同：这里只保证死（血量不是权威，可能还是满的）。</para></summary>
        static void ApplyRemoteDeath(DamageMsg m)
        {
            var actor = m.NetId != 0 ? FindActor(m.NetId) : NetDestructibleBridge.FindByKey(m.Key);
            if (actor == null)
            {
                if (m.NetId == 0 && m.Key != 0) return;   // 无号可破坏物：本端没有这件（已销毁/不在同一场景）⇒ 正常，静默
                FPSGame.Utils.NetSyncLog.Warn("远端死亡落地", $"找不到 netId={m.NetId} key={m.Key} 的本端副本 ⇒ 这条死亡消息被丢弃");
                return;
            }

            // ★ 先把血抹 0 再杀：本端这份副本的血量**不是权威**（可能还是满血），而 `Actor.Kill()` 的实现
            //   就是 `OnDie(null)`、**完全不碰血量** ⇒ `Actor.OnDie` 里那条"死亡时生命值>0"的守卫会误报
            //   （那条打印是给**真异常**用的，不该被这条正常同步路径刷）。
            //   ⚠ 改走 `IHealth.Kill()`：它内部先 `CurrentHealth = 0` 再 `HandleDeath(null)`，
            //   派发的还是同一个 `OnDie` ⇒ 掉落 / 特效 / 波次账本照常跟着走，只是血量已归零。
            var hp = actor.GetComponent<IHealth>();
            if (hp != null) hp.Kill();
            else actor.Kill();   // 没有生命组件（极少见）：退回原来的直接死
        }

        #endregion

        #region 查表

        static EnemyController Find(int netId)
        {
            var actor = FindActor(netId);
            return actor != null ? actor.GetComponent<EnemyController>() : null;
        }

        /// <summary>按 NetId 找本端那份副本（**统一在 <c>Actors</c> 全表里找**）。
        /// <para>▍为什么不分开查 <c>Enemys</c>/<c>SpecUnits</c>：号段本身不相交（敌人从 1 起、特殊单位从
        /// <c>ActorsManager.SpecNetIdBase</c> 起）⇒ 一个号只会命中一个单位，分表查只会多一份漏查的风险
        /// （历史上 <c>Enemys</c> 还漏了跨局复位、会残留上一局的条目）。</para></summary>
        static Actor FindActor(int netId)
        {
            if (netId == 0) return null;

            var list = ActorsManager.Actors;
            for (int i = 0; i < list.Count; ++i)
            {
                if (list[i] is Actor a && a != null && a.NetId == netId) return a;
            }
            return null;
        }

        #endregion
    }
}
