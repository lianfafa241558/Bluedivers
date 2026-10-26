using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Data;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Utils;

namespace FPSGame.Game
{


    /// <summary>
    /// 可以被攻击 没有这个组件就不造成伤害)
    /// </summary>
    [AddComponentMenu("单位/肢体", 30)]
    public class Damageable : TickBehaviour, IDamageable
    {
        /// <summary>友伤倍率</summary>
        private const float SensibilityToSelfdamage = 0.5f;
        /// <summary>爆炸抗性达到此值视为完全免疫（跳过爆炸遮挡判定）</summary>
        private const float ExplosionImmunityThreshold = 0.95f;
        /// <summary>穿甲等级不足时，每差 1 级减伤比例</summary>
        private const float ArmorPenaltyPerLevel = 0.4f;
        /// <summary>跳弹阈值：穿甲等级比部位护甲高出这么多级 ⇒ 视为"过穿"（只做表现，不改数值）</summary>
        private const int RicochetArmorGap = 2;

        /// <summary>【联机 · 可注入】"这笔伤害该不该在**本端落地**（真的扣血 / 致死）"。
        /// <para>▍为什么需要这么个注入点：载具血量必须**收敛到房主**（两端各自模拟子弹 ⇒ 同一发被扣 N 份），
        /// 但"哪个单位是载具 / 本端是不是权威 / 这笔是不是远端刚下发的 / 来源是玩家还是 AI"这些知识
        /// <c>05_UnitCore</c> 一个都看不见（<c>VehicleController</c> 在 06、联机角色在 07/09）
        /// ⇒ 这里只留一个谓词，判定由 09 的桥注入（同 <c>NetHostSvc.VehicleNetIdAllocator</c> 的思路）。</para>
        /// <para>返回 <c>false</c> = **不落地**：不扣血 / 不致死 / 也**不派发</c> <c>UnitDamaged</c>（⇒ 不会上报）。
        /// 受击的**视觉**表现由子弹自身的命中特效承担，不依赖这里。</para>
        /// <para><c>null</c>（单机 / 未接入）⇒ 一律落地 = 改动前行为。</para></summary>
        public static System.Func<Damageable, DamagePacket, bool> LocalDamageGate;


        [SerializeField]
        [InspectorName("弱点")]
        private bool isWeakness;

        /// <summary>爆炸抗性（0~1，1=完全免疫）</summary>
        [SerializeField]
        [InspectorName("爆炸抗性")]
        private float explosionResistance;

        /// <summary>护甲等级（绝地潜兵2式，由穿甲等级AP判定减伤）</summary>
        [SerializeField]
        [InspectorName("护甲等级")]
        private int armorLevel;

        
        [InspectorName("基础护甲值")]
        public int armorValue;
        [Header("关联护甲")]
        [SerializeField]
        private List<TransferDamageable> LineArmor;

        [Header("护甲破坏效果")]
        [SerializeField]
        private List<ArmorBreakEffect> armorBreakEffect;
        [InspectorName("流血速度")]
        public int BleedValue;

        public event UnityAction<Damageable> OnDamage;
        public event UnityAction<Damageable> OnDestroyPart;
        public IHealth Health { get; private set; }
        private Actor Actor { get; set; }

        /// <summary>
        /// 伤害转发目标：本肢体<b>不自己扣血</b>，把整包伤害交给 <see cref="relay"/> 的 <see cref="Damageable"/> 结算。
        /// <para>▍用在哪（2026-10-18）：火车车厢 —— 一列车只有车头是 Actor/Health，车厢只是"能被打中的壳"。
        /// 车厢预制体上带的是 <see cref="Damageable"/> 但<b>没有任何 Health</b> ⇒ <see cref="InflictDamage"/>
        /// 会在第一行 return，表现就是"打车厢不掉血、只有车头有效"。</para>
        /// <para>⚠ 别改成"给车厢各加一个 Actor + Health"：那要额外做 NetId 分配、伤害与死亡同步、
        /// 击杀/雷达/小地图登记，而它们本来就该共享一管血。转发 = 一列车一份权威血量，两端自然一致。</para>
        /// <para>⚠ 非序列化：运行时由生成方指定（同 <c>TransferDamageable.source</c>）。</para>
        /// </summary>
        [System.NonSerialized] public Damageable relay;

        /// <summary>指定伤害转发目标（见 <see cref="relay"/>）</summary>
        public void SetRelay(Damageable target) => relay = target;



        /// <summary>护甲破坏(计算流血)</summary>
        private GameObject ArmorBreaker;

        bool IDamageable.IsWeakness => isWeakness;

        GameObject IDamageable.ActorGo => relay != null ? ((IDamageable)relay).ActorGo : Actor.gameObject;

        public IDamageable Source => this;

        public int ArmorLevel => armorLevel;

        public float ExplosionResistance => explosionResistance;

        /// <summary>
        /// 主体一定过爆炸判定
        /// </summary>
        [HideInInspector]
        public bool isMain;


        private Collider[] m_colliders;//包括关联护甲

       
        public PEInt remainArmor;

        /// <summary>护甲满值（含难度缩放），用于护甲/护盾恢复</summary>
        private PEInt maxArmor;

        [DisplayField]
        [SerializeField]
        private float showArmor;

        /// <summary>获取当前护甲剩余比例（0~1）</summary>
        /// <returns>剩余护甲 / 基础护甲值</returns>
        public float GetArmorRatio() => remainArmor.RawFloat / armorValue;

        /// <summary>标记为部件主体，并在主体死亡时连带销毁该部件</summary>
        /// <param name="health">主体的 Health 组件，其 OnDie 事件将触发部件销毁</param>
        public void SetIsMain(IHealth health)
        {
            isMain = true;
            health.OnDie+= DestroyPart;
        }


        /// <summary>初始化：按任务难度缩放护甲值，收集自身与关联护甲的碰撞体，查找 Health/Actor 引用并记录护甲破坏效果初始状态</summary>
        void Awake()
        {

            // 难度缩放走「数据自持」（2026-10-01 取代 ServiceLocator.Task 槽）：TaskManager 发布、单位内核直读。
            // ⚠ 本处在 Awake 执行、注入来不及；而 TaskState 未就绪时读到的就是中性值（系数 0 / Normal）
            //   ⇒ 原来的 `taskSvc != null ? ... : 0` 判空实为死代码，一并清掉。
            PEInt scale = 1 + (FPSGame.Data.TaskState.ExtraDifficulty[3] * (PEInt)0.1f);
            switch (FPSGame.Data.TaskState.Difficulty)
            {
                case DifficultyEnum.Normal:
                    scale *= (PEInt)0.5f;
                    break;
                case DifficultyEnum.Hard:
                    scale *= (PEInt)0.6f;
                    break;
                case DifficultyEnum.VeryHard:
                    scale *= (PEInt)0.7f;
                    break;
                case DifficultyEnum.HardCode:
                    scale *= (PEInt)0.85f;
                    break;
                case DifficultyEnum.Extreme:
                    scale *= (PEInt)1f;
                    break;
                case DifficultyEnum.Insane:
                    scale *= (PEInt)1.15f;
                    break;
                case DifficultyEnum.Torment:
                    scale *= (PEInt)1.2f;
                    break;
                case DifficultyEnum.Lunatic:
                    scale *= (PEInt)1.35f;
                    break;
            }

            remainArmor = armorValue * scale;
            maxArmor = remainArmor;
            showArmor = remainArmor.RawFloat;
            
            // 记录护甲破坏效果的初始状态，用于护盾恢复
            foreach (var item in armorBreakEffect)
            {
                if (item.go != null)
                {
                    item.originActive = item.go.activeSelf;
                    item.originLocalScale = item.go.transform.localScale;
                }
            }

            List<Collider> list = GetComponents<Collider>().ToList();
            foreach (var item in LineArmor)
            {
                item.source = this;
                list.AddRange(item.GetComponents<Collider>());
            }
            m_colliders = list.ToArray();
            //在awake中会因为接口和具体类型注册顺序不一样导致失败，要么用具体类型，要么放start
            Health = GetComponent<IHealth>();
            if (!Health.IsValidMono())
            {
                Health = GetComponentInParent<IHealth>();
            }
            Actor = GetComponent<Actor>();
            if (!Actor)
            {
                Actor = GetComponentInParent<Actor>();
            }
        }



        /// <summary>获取距指定位置最近的碰撞体（含关联护甲的碰撞体）</summary>
        /// <param name="pos">参考位置（通常为伤害来源点）</param>
        /// <returns>距离最近的 Collider；无碰撞体时返回 null 并报错</returns>
        public Collider ClosestCollider(Vector3 pos) {
            if (m_colliders.Length == 0)
            {
                Debug.LogError("没有碰撞组件"+gameObject.name,gameObject);
                return null;
            }
            if (m_colliders.Length == 1) return m_colliders[0];
            var closest = m_colliders[0];
            //Debug.LogError("可伤害组件"+gameObject+"的碰撞数量"+ m_colliders.Length);
            float minDistance = Vector3.Distance(pos, closest.ClosestPointOnBounds(pos));
            for (int i = 1; i < m_colliders.Length; i++) {
                float currentDistance = Vector3.Distance(pos, m_colliders[i].ClosestPointOnBounds(pos));
                if (currentDistance < minDistance) {
                    minDistance = currentDistance;
                    closest = m_colliders[i];
                }
            }
            return closest;
        }

        /// <summary>
        /// 检查自己是否被遮挡(爆炸判定)
        /// </summary>
        /// <param name="sourcePoint">来源</param>
        /// <returns>是否被遮挡</returns>
        public bool ExplosionBlocking(Vector3 sourcePoint,out Collider collider)
        {
            if (isMain)//主体一定过判定
            {
                collider = m_colliders[0];
                return true;
            }
            collider = null;
            //爆炸抗性接近完全免疫的直接跳过遮挡判定
            if (explosionResistance >= ExplosionImmunityThreshold) return false;

            RaycastHit hit;
            // 批量射线检查
            foreach (var item in m_colliders)
            {
                Vector3 direction = item.bounds.center - sourcePoint;
                if (!Physics.Raycast(sourcePoint, direction, out hit, direction.magnitude))
                {
                    // 射线未命中任何物体，按理来说不该由这个情况，但是应该算没有阻挡
                    collider = item;
                    return true;
                }
                else if (hit.collider == item)
                {
                    // 射线命中目标Collider，未被遮挡
                    collider = item;
                    return true;
                }
                // 其他情况都被遮挡
            }

            return false;
        }




        /// <summary>结算伤害入口：依次处理友军减伤、全队强化、弱点加成、爆炸抗性、穿甲等级，拆分伤害类型后交给 Health 结算，并处理护甲破坏成分</summary>
        /// <param name="packet">伤害数据包。
        /// <para>▍来源有**两个**：<c>DamageOrigin</c>（起源 = 统计口径，本方法所有减伤/友伤判定都用它）
        /// 与 <c>DamageCaused</c>（直接来源 = 显示/AI 口径，原样透传给 <c>Health</c>）。见 <c>DamagePacket</c>。</para></param>
        public void InflictDamage(DamagePacket packet) {
            // 转发肢体（车厢这类"共用车头血量"的壳）：整包交给主体去算，本组件不参与减伤/护甲/死亡
            if (relay != null)
            {
                relay.InflictDamage(packet);
                return;
            }
            if (!Health.IsValidMono())
            {
                // 前置条件不成立就一条日志 + return：原来静默丢弃，表现是"打上去了但不掉血"
                //Debug.LogWarning($"[伤害丢弃] {name} 没有绑定 Health（Damageable.Health 为空）", this);
                return;
            }
            if (!packet.DamageGroups.IsValid()|| packet.DamageGroups.Count==0)
            {
                //Debug.LogWarning($"[伤害丢弃] {name} 的伤害包没有成分(DamageGroups 为空) 值={packet.Damage.RawFloat:F2}", this);
                return;
            }

            // ★ 联机（2026-10-14）：这一笔"该不该在本端落地"由 09 注入的谓词决定
            //   （载具血量收敛到房主；非权威端 AI 来源的载具伤害在这里就被挡掉）。null ⇒ 一律落地。
            if (LocalDamageGate != null && !LocalDamageGate(this, packet)) return;

            // ★ 各端**本地生成**的效果伤害（毒烟/火焰 tick、地雷自爆、空投舱砸击、踩踏动能、荆棘反伤）：
            //   非权威端只让玩家/盟友落地，其余目标等房主结算后的伤害广播 —— 否则 N 个端 = N 倍。
            //   放在这里而不是 FpsHelper 的循环里：直击 / 爆炸 / 范围动能 / 以后新增的任何管线都要经过本方法。
            //   ⚠ 玩家不能一起门掉：盟友实例在别人机器上没有 Damageable，房主替他们扣不了血。
            if (packet.LocalEffect && !FPSGame.Data.BattleState.ShouldApplyTickDamage(IsPlayerLike)) return;

            PEInt damage = packet.Damage;
            // ⚠ 减伤/友伤/归属三件事一律读 **起源**（与拆分前的 `DamageSource` 逐字等价 ⇒ 数值零变化）；
            //   **直接来源**只往下传给 Health 供显示/AI 用。见 DamagePacket 的字段注释。
            GameObject damageOrigin = packet.DamageOrigin;
            GameObject damageCaused = packet.DamageCaused;
            //I_Damagable source = packet.Source;

            Actor SourceActor=null;
            if (damageOrigin)
            {
                SourceActor = damageOrigin.GetComponent<Actor>();
            }

            // 友军减伤
            if (Actor && SourceActor && Actor.Team == SourceActor.Team)
            {
                
                damage *= (PEInt)SensibilityToSelfdamage;
            }
            // 全队强化"友情护盾"：友军伤害再降低 50%（总友伤 25%，仅玩家队伍生效）
            if (Actor && SourceActor && Actor.Team == SourceActor.Team
                && IsFriendlyUnit(Actor)
                && FPSGame.Data.BattleState.HaveBooster(BoosterType.FriendShield))
            {
                damage *= (PEInt)0.5f;
            }
            // 全队强化"生命力强化"：玩家受到的伤害降低 10%
            if (FPSGame.Data.BattleState.HaveBooster(BoosterType.Vitality) && Actor && (Actor.Type== UnitTypeEnum.Player|| Actor.Type == UnitTypeEnum.Friend))
            {
                damage *= (PEInt)0.9f;
            }
            if (isWeakness)
            {
                damage *= (1+ packet.WeaknessBonus);
            }
            //Debug.LogWarning("友军减伤" + damage,gameObject);


            // ⚠ 联机镜像（2026-10-11 修）：`packet.Resolved` = 伤害**已在开枪端算过这些乘区**（上报的是最终值）
            //   ⇒ 这里**不能再乘一遍**，否则护甲/爆炸抗性被二次减免、两端血量与死亡判定不一致。
            //   （远端包没带 AP ⇒ 原来按 AP=0 算出最大减伤；也没带 isDirect ⇒ 爆炸抗性也再算一遍。）
            if (!packet.Resolved)
            {
                //爆炸抗性
                PEInt hitExplosionResistance = PEMath.Clamp(packet.isDirect ? 1 : (1 - (PEInt)explosionResistance),0,1);
                //Debug.LogWarning("爆炸抗性" + hitExplosionResistance, gameObject);

                //穿甲等级
                PEInt armorFactor = PEMath.Clamp(1 - (armorLevel - packet.AP) * (PEInt)ArmorPenaltyPerLevel, 0, 1);
                //Debug.LogWarning("穿甲系数" + armorFactor.RawFloat+"护甲等级"+armorLevel+"穿甲等级"+packet.AP, gameObject);
                damage *= hitExplosionResistance* armorFactor;
            }

            //Debug.LogWarning("AP减伤" + damage, gameObject);

            List<SKVP<DamageTypeEnum, PEInt>> finalDamageGroups = new();
            if (packet.DamageGroups.Count == 0) {
                // 无成分时按动能(Gun)结算
                finalDamageGroups.Add(new(DamageTypeEnum.Gun, (damage)));
            }
            else {
                foreach (var item in packet.DamageGroups) {
                    PEInt value = (damage * (PEInt)item.Value);

                    if (item.Key == DamageTypeEnum.Destruction) {
                        // 护甲破坏归属不显示 ⇒ 用起源
                        ArmorDestruction(value, damageOrigin);
                    }
                    else
                    {
                        finalDamageGroups.Add(new(item.Key, value));
                    }
                }
            }
            
            // 原始成分随包交给 Health 自行挂/清（见 Health.TakeDamage 的事件段）：这里不再写别人的状态
            var settle = DamageSettleArgs.Of(finalDamageGroups, packet.NoSource, damageOrigin, packet.damageAffected, packet.Pos);
            settle.Caused = damageCaused;
            settle.IsWeakness = isWeakness;
            settle.DemolishValue = packet.DemolishValue;
            settle.RawGroups = packet.DamageGroups;
            settle.Ricochet = IsRicochet(packet);
            Health.TakeDamage(in settle);
            
        }

        /// <summary>跳弹判定（只给准星提示用，**不参与伤害数值**）：直击 + 穿甲比本部位护甲高 <see cref="RicochetArmorGap"/> 级以上。</summary>
        /// <remarks>⚠ 联机镜像包（<c>Resolved</c>）不重算：远端没带 AP，按 0 算会把满伤误判成跳弹。</remarks>
        private bool IsRicochet(in DamagePacket packet)
            => packet.isDirect && !packet.Resolved &&  armorLevel - packet.AP >= RicochetArmorGap;

        /// <summary>是否为玩家阵营单位（玩家/盟友）</summary>
        private static bool IsFriendlyUnit(Actor actor)
            => actor.Type == UnitTypeEnum.Player || actor.Type == UnitTypeEnum.Friend;

        /// <summary>计算护甲破坏：扣减剩余护甲，护甲归零时触发破坏，否则触发受击事件</summary>
        /// <param name="value">破坏类伤害值</param>
        /// <param name="source">伤害来源物体</param>
        private void ArmorDestruction(PEInt value, GameObject source)
        {
            if (remainArmor > 0)
            {
                remainArmor -= value;
                showArmor = remainArmor.RawFloat;
                if (remainArmor <= 0)
                {
                    BreakArmor(source);
                }
                else
                {
                    OnDamage?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// 恢复护甲（护盾类肢体）：护甲回满并还原护甲破坏效果
        /// </summary>
        public void RestoreArmor()
        {
            if (remainArmor > 0) return;

            remainArmor = maxArmor;
            showArmor = remainArmor.RawFloat;
            ArmorBreaker = null;
            foreach (var item in armorBreakEffect)
            {
                if (item.go != null)
                {
                    item.go.SetActive(item.originActive);
                    item.go.transform.localScale = item.originLocalScale;
                }
            }
        }

        /// <summary>
        /// 摧毁护甲
        /// </summary>
        public void BreakArmor(GameObject source)
        {
            foreach (var item in armorBreakEffect)
            {
                if (item.go != null)
                {
                    item.go.SetActive(item.state);
                    item.go.transform.localScale *= item.scale;
                }
            }
            ArmorBreaker = source;
            OnDamage?.Invoke(this);
            DestroyPart(null, null);
        }

        /// <summary>部件销毁处理：广播 OnDestroyPart 事件</summary>
        /// <param name="caused">未使用（用于匹配 Health.OnDie 签名：直接来源）</param>
        /// <param name="origin">未使用（用于匹配 Health.OnDie 签名：起源）</param>
        private void DestroyPart(GameObject caused, GameObject origin)
        {
            OnDestroyPart?.Invoke(this);
        }




        /// <summary>本单位是不是玩家 / 盟友（他们的血量**本机权威** ⇒ tick 伤害任何角色下都要本地算）。
        /// <para>▍公开给伤害管线用：`InflictDamage` 的 `DamagePacket.LocalEffect` 门要逐目标判断
        /// （各端本地生成的效果伤害在非权威端只对玩家/盟友落地）。判据只此一处，</para>
        /// <para>别在 `FpsHelper` 或别的地方再抄一份 Actor.Type 判断。</para></summary>
        public bool IsPlayerLike
            => Actor != null && (Actor.Type == UnitTypeEnum.Player || Actor.Type == UnitTypeEnum.Friend);

        /// <summary>每帧tick：护甲已破坏时按流血速度对自身施加真实伤害（流血效果）</summary>
        /// <returns>是否继续 tick</returns>
        public override bool Tick()
        {
            // 转发肢体没有自己的 Health（血在主体上）⇒ 流血/护甲 tick 一律不跑
            if (!Health.IsValidMono()) return true;
            // ★ 联机（2026-10-12）：tick 伤害**只由房主（或单机）本地结算并广播** —— 若成员端也结算，
            //   它还会把这一拍上报给房主（`EnemyController._OnDamaged`）⇒ 同一段流血被叠加 N 份。
            //   玩家/盟友例外（他们的血本机权威）。见 BattleState.IsTickDamageAuthority。
            if (armorValue > 0 && remainArmor <= 0 && FPSGame.Data.BattleState.ShouldApplyTickDamage(IsPlayerLike))
            {
                // 流血：无源（NoSource=true），来源两头都用"最后打破护甲的那个人"（与改动前同口径）
                var settle = DamageSettleArgs.Of(new() { new(DamageTypeEnum.Real, BleedValue) },
                    true, ArmorBreaker, m_colliders[0], m_colliders[0].bounds.center);
                Health.TakeDamage(in settle);
            }
            return true;
        }

#if UNITY_EDITOR
        /// <summary>编辑器辅助：自动收集子物体中的关联护甲，并在有基础护甲值时添加自身为护甲破坏效果</summary>
        [ContextMenu("自动配置")]
        private void AutoSetting()
        {
            LineArmor = transform.GetComponentsInChildren<TransferDamageable>().ToList();
            if (armorValue > 0) armorBreakEffect.Add(new() { go=gameObject});
        }

#endif


    }
    [System.Serializable]
    public class ArmorBreakEffect {
        public GameObject go;
        public bool state;
        public float scale;

        /// <summary>运行时记录破坏前的激活状态，用于护盾恢复</summary>
        [HideInInspector] public bool originActive;
        /// <summary>运行时记录破坏前的局部缩放，用于护盾恢复</summary>
        [HideInInspector] public Vector3 originLocalScale;
    }

}