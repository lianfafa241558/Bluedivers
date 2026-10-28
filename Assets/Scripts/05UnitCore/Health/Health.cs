using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Core;
using FPSGame.Data;
using FPSGame.GameContract;
using FPSGame.Utils;
using PEMaths;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.UI.Image;

namespace FPSGame.Game {
    public abstract partial class Health : MonoBehaviour, IHealth
    {




        /// <summary>最大生命值</summary>
        [InspectorName("最大生命值")]
        public int MaxHealth = 100;
        /// <summary>最大生命值</summary>
        [InspectorName("最大护盾值")] 
        public int MaxShield;

        /// <summary>整体伤害抗性（各类型减伤乘区，1=无减伤）。动能/爆炸由护甲等级AP判定，此处不含这两项</summary>
        [Header("伤害抗性")]
        [SerializeField]
        [InspectorName("伤害抗性")]
        private List<SKVP<DamageTypeEnum, float>> showArmorLists = new();



        /// <summary>主体部位</summary>
        [InspectorName("主体部位")]
        public Damageable MainPart;

        /// <summary>
        /// 拆毁值：伤害的拆毁值大于此值则直接秒杀。默认 -1 视为无法被拆毁（不受秒杀）。
        /// </summary>
        [SerializeField]
        [InspectorName("拆毁值")]
        protected int demolishValue = -1;

        /// <summary>
        /// 死亡后自动摧毁自身物体）
        /// </summary>
        [SerializeField]
        [InspectorName("死亡后自动摧毁自身物体）")]
        protected bool AutoDestroy;

        [Space]
        [DisplayField]
        [InspectorName("剩余生命值")]
        [SerializeField]
        public int showHealth;

        //剩余血量
        public PEInt CurrentHealth { get; set; }
        //剩余护盾
        public PEInt CurrentShield { get; set; }


        #region 事件

        /// <summary>收到伤害时：值、**直接来源**（显示/AI）、**起源**（统计）、受击点、无源伤害
        /// <para>⚠ 用 Action 而非 UnityAction：后者最多只支持 4 个泛型参数。</para></summary>
        public event Action<PEInt, GameObject, GameObject, Collider, bool> OnDamaged;

        /// <summary>被击中时 第 1 参 = **直接来源**（显示口径），受击点,法线，是弱点</summary>
        public event UnityAction<GameObject, Vector3,Vector3, bool> OnHit;

        /// <summary>是否是破盾伤害（ShieldBehaviour 等外挂护盾逻辑需要订阅，故为 public）</summary>
        public event UnityAction<bool> OnShieldDamaged;

        /// <summary>收到治疗时 治疗值</summary>
        public event UnityAction<PEInt> OnHealed;

        /// <summary>恢复护盾时 恢复值</summary>
        public event UnityAction<PEInt> OnRestoreShield;

        /// <summary>死亡时：**直接来源**（显示/AI）、**起源**（统计）</summary>
        public event UnityAction<GameObject, GameObject> OnDie;

        /// <summary>复活时</summary>
        public event UnityAction OnRevive;


        #endregion

        public bool CanPickup() => CurrentHealth < MaxHealth;

        public float GetHpRatio() => CurrentHealth.RawFloat / (MaxHealth+0f);

        public float GetShieldRatio() => CurrentShield.RawFloat / (MaxShield+0f);

        public float GetHpMax() => MaxHealth;
        public float GetHpCurrent() => CurrentHealth.RawFloat;

        public float GetShieldMax() => MaxShield;
        public float GetShieldCurrent() => CurrentShield.RawFloat;

        public IDamageable GetMainPart()=>MainPart;

        [DisplayField]
        [SerializeField]
        protected bool m_IsDead;

        /// <summary>是否已死亡（只读，供护盾等外挂逻辑判断）</summary>
        public bool IsDead => m_IsDead;

        /// <summary>所属单位（无敌标记的唯一来源，Awake 缓存）</summary>
        protected Actor m_Actor;

        protected float m_LastHitTime = Mathf.NegativeInfinity;

        /// <summary>最近一次受击时间（只读，护盾恢复延迟等需要）</summary>
        public float LastHitTime => m_LastHitTime;

        private float m_Time;

        /// <summary>标记当前 TakeDamage 调用来自异常状态 tick，HandleDamage 据此跳过积蓄槽更新</summary>
        private bool _isAboTickDamage;

        /// <summary>**本次伤害的原始成分**（类型 + 系数），由 <c>Damageable.InflictDamage</c> 在结算前写入、结算后清空。
        /// <para>▍给联机上报用（见 <c>EnemyController._OnDamaged</c>）：接收端只带总伤害时，
        /// `Damageable.ArmorDestruction`（护甲破坏/流血）与 `Health.HandleDamage → AddAboGauge`（异常状态积蓄）
        /// 两条分支会被**整段跳过** ⇒ 主机把怪冻住、成员那边它照常跑。</para>
        /// <para>⚠ 直接调 <c>TakeDamage</c> 的路径（流血 / 异常状态 tick）拿到的是 null ⇒ 上报退回中性成分。</para></summary>
        public List<SKVP<DamageTypeEnum, float>> LastRawGroups { get; private set; }

        /// <summary>整体伤害抗性缓存字典（由 showArmorLists 构建，避免每帧遍历列表）</summary>
        private readonly Dictionary<DamageTypeEnum, float> _armorResistance = new();

        /// <summary>获取指定伤害类型的整体减伤乘区（1=无减伤，未配置即 1）</summary>
        public float GetResistance(DamageTypeEnum type)
            => _armorResistance.TryGetValue(type, out var value) ? value : 1f;

        private void OnEnable()
        {
            if (m_IsDead) 
            {
                Revive();
            }
        }

        protected virtual void Awake() {
            m_Actor = GetComponentInParent<Actor>();
            CurrentHealth = showHealth = MaxHealth;
    
            // 构建整体抗性字典
            _armorResistance.Clear();
            if (showArmorLists != null)
            {
                foreach (var item in showArmorLists)
                {
                    _armorResistance[item.Key] = item.Value;
                }
            }
            if(MainPart.IsValid()) MainPart.SetIsMain(this);
            InitAboState();
        }



        /// <summary>受到治疗</summary>
        public void Heal(float healAmount) 
        {
            if (m_IsDead) return;

            PEInt healthBefore = CurrentHealth;
            CurrentHealth += (PEInt)healAmount;
            CurrentHealth = PEMath.Clamp(CurrentHealth, 0, MaxHealth);
            showHealth = CurrentHealth.RawInt;
            // call OnHeal action
            PEInt trueHealAmount = CurrentHealth - healthBefore;
            if (trueHealAmount > 0) {
                OnHealed?.Invoke(trueHealAmount);
            }
        }
        /// <summary>恢复护盾</summary>
        public void RestoreShield(float healAmount)
        {
            PEInt healthBefore = CurrentShield;
            CurrentShield += (PEInt)(healAmount);
            CurrentShield = PEMath.Clamp(CurrentShield, 0, MaxShield);

            PEInt trueHealAmount = CurrentShield - healthBefore;
            if (trueHealAmount > 0)
            {
                OnRestoreShield?.Invoke(trueHealAmount);
            }
        }

        /// <summary>受到伤害（参数语义见 <see cref="DamageSettleArgs"/>）</summary>
        public void TakeDamage(in DamageSettleArgs args) {
            if (m_IsDead || (m_Actor != null && m_Actor.HasFlag(ActorFlag.Invincible)))
                return;

            // 解到局部：下面 80 行引用密集，且 in 参数的字段不可写（caused 要回落赋值）
            List<SKVP<DamageTypeEnum, PEInt>> damageGroups = args.Groups;
            GameObject damageOrigin = args.Origin;
            Collider damageAffected = args.Affected;
            Vector3 pos = args.Pos;
            bool noSource = args.NoSource, response = args.Response, isWeakness = args.IsWeakness;
            bool ricochet = args.Ricochet;
            int demolishValue = args.DemolishValue;

            // 回落：直接来源缺省时用起源 ⇒ 下游消费点（表现/AI）不必到处判空。
            // ⚠ 必须用 == 判断，别改 ?? ：已销毁对象是 Unity fake-null，?? 不会回落。
            GameObject damageCaused = args.Caused;
            if (damageCaused == null) damageCaused = damageOrigin;

            // 拆毁秒杀：自身拆毁值 -1 视为无法被拆毁；否则伤害拆毁值大于自身拆毁值则直接击杀，无视护盾，但受上面无敌标记保护
            if (this.demolishValue >0 && demolishValue >= this.demolishValue)
            {
                CurrentHealth = 0;
                showHealth = 0;
                if (response) OnHit?.Invoke(damageCaused, pos, HitNormal(pos, damageAffected), isWeakness);
                HandleDeath(damageCaused, damageOrigin);
                return;
            }

            bool haveshield= CurrentShield > 0, isBreakShield=false;
            PEInt finaldamgage = 0;
            foreach(var item in damageGroups)
            {
                // 整体伤害抗性减伤乘区（未配置的类型为 1，不缩放）
                PEInt resistedValue = PEMath.Max(item.Value, 0) * (PEInt)GetResistance(item.Key);
                var re= HandleDamage(item.Key, resistedValue, damageCaused, _isAboTickDamage);
                //Debug.LogError("伤害类型" + item.Key + "值" + item.Value + "剩余值" + resistedValue+"最终计数"+re,gameObject);
                finaldamgage += re;
#if UNITY_EDITOR
                if (re.RawFloat>=0.5f)
                {
                    Color dmgColor = Color.white;
                    if (AboStateData_SO.Dic != null)
                    {
                        foreach (var grp in damageGroups)
                        {
                            if (AboStateData_SO.Dic.TryGetValue(grp.Key, out var aboState) && aboState != null)
                            {
                                dmgColor = aboState.color;
                                break;
                            }
                        }
                    }
                    if (damageAffected) Tool.DrawLabel(pos + RandomUtils.RandomVector3XZ(), "" + Tool.Round(finaldamgage.RawFloat), 3, dmgColor);
                    NetSyncLog.DamageLog("伤害",damageCaused+"对" + m_Actor.ShowName + "造成"+ item.Key+ "伤害" + finaldamgage + "血量" + CurrentHealth, gameObject);
                }
#endif

            }



            PEInt healthBefore = CurrentHealth, shieldBefore = CurrentShield;
            if (shieldBefore > 0) {
                CurrentShield -= finaldamgage;

                if (CurrentShield < 0) {
                    finaldamgage = -CurrentShield;
                    CurrentShield = 0;
                    isBreakShield = true;
                }
                else {
                    finaldamgage = 0;
                }
            }
            if (finaldamgage > 0) {
                CurrentHealth -= finaldamgage;
                CurrentHealth = PEMath.Clamp(CurrentHealth, 0, MaxHealth);
            }


            // call OnDamage action
            PEInt trueDamageAmount = healthBefore - CurrentHealth + shieldBefore - CurrentShield;
            if (trueDamageAmount > 0) {
                m_LastHitTime = Time.time;
                // 原始成分只在这段事件期间有效：联机上报的订阅方是在回调里**同步**读的（敌人 / 载具两条通道）。
                // ⚠ 用 prev 保存而不是置 null：OnDamaged 回调里可能重入再打一次（击杀触发爆炸之类）。
                var prevRaw = LastRawGroups;
                LastRawGroups = args.RawGroups;
                try
                {
                    OnDamaged?.Invoke(trueDamageAmount, damageCaused, damageOrigin, damageAffected, noSource);
                    if(response)OnHit?.Invoke(damageCaused,pos, HitNormal(pos, damageAffected),isWeakness);
                    if (haveshield) OnShieldDamaged?.Invoke(isBreakShield);

                    // ★ 联机伤害通道的上行点：**实际扣减量**（见 UnitEventBus.OnUnitDamaged 的注释）
                    UnitEventBus.UnitDamaged(gameObject, trueDamageAmount, damageCaused, damageAffected, damageOrigin);
                }
                finally { LastRawGroups = prevRaw; }
            }
            //这个是控制hpui的，所以总是响应；跳弹只做准星提示（订阅方自己按"开枪者是不是我"过滤）
            //Debug.LogError("目标"+ gameObject+" "+ ricochet,gameObject);
            UnitEventBus.UnitHit(gameObject, damageCaused, ricochet);
            showHealth = CurrentHealth.RawInt;
            //Debug.LogError("击杀者" + damageCaused + "起源" + damageOrigin);
            HandleDeath(damageCaused, damageOrigin);
        }

        /// <summary>
        /// 受击法线（表现层用来摆弹痕/火花）：以受击体的包围盒中心为基准。
        ///
        /// <para>▍⚠ <paramref name="damageAffected"/> **可能为 null**：网络同步过来的伤害只有
        /// "伤害值 + 落点"，没有真实命中体（见 <c>EnemyNetBridge.ApplyRemoteDamage</c>）。
        /// 老实现是 <c>damageAffected.bounds.center</c> 直接解引用 ⇒ <c>NullReferenceException</c>
        /// 把整条网络消息的处理打断（2026-10-10 用户实测：客机应用房主下发的 <c>EnemyDamagedSync</c> 时崩）。</para>
        /// <para>▍兜底取 <c>Vector3.up</c>：只影响特效朝向，不影响任何结算数值。</para>
        /// </summary>
        private static Vector3 HitNormal(Vector3 pos, Collider damageAffected)
            => damageAffected != null ? (pos - damageAffected.bounds.center).normalized : Vector3.up;

        private PEInt HandleDamage(DamageTypeEnum type, PEInt value, GameObject damageCaused, bool isAboTick = false) {
            //只有需求特殊处理的才单独写，目前没有状态槽，直接全部默认
            //Debug.LogWarning("对"+gameObject.name+"伤害:类型"+type+"值"+value);
            // 异常状态 tick 伤害：不更新积蓄槽（Current/LastGainTime），直接造成对应类型伤害
            if (isAboTick) {
                return type == DamageTypeEnum.Destruction ? 0 : value;
            }
            // 异常状态易伤（原"冰冻满槽 ⇒ 动能/爆炸/真实 ×3"硬编码）已改为数据驱动：
            // 倍率由武器伤害配置的"易伤成分"给出，统一在 Damageable.InflictDamage 里乘（那边看得到 packet）。
            switch (type) {
                case DamageTypeEnum.Destruction:
                    return 0;//护甲破坏不计入伤害量
                case DamageTypeEnum.Burn:
                
                case DamageTypeEnum.Electric:
                case DamageTypeEnum.Radiation:
                    AddAboGauge(type, value, damageCaused);
                    return value;//燃烧/雷击/辐射 伤害计入伤害量
                case DamageTypeEnum.Freeze:
                case DamageTypeEnum.Toxicity:
                case DamageTypeEnum.Hacker:
                case DamageTypeEnum.Terror:
                case DamageTypeEnum.Vertigo:
                    AddAboGauge(type, value, damageCaused);
                    return 0;//冰冻/毒/骇入/恐慌/眩晕不计入伤害量

                default:
                    return value;
            }
        }

        private void Update()
        {
            if (m_Unit == null) return;
            if (Time.time >= m_Time)
            {
                AboTick();
            }

        }


        /// <summary>复活,要注意这个时候不能获取血量，因为玩家会清空护盾啥的，值会不对</summary>
        public virtual void Revive()
        {
            CurrentHealth = MaxHealth;
            CurrentShield = MaxShield;
            showHealth = CurrentHealth.RawInt;
            m_IsDead = false;
            OnRevive?.Invoke();
        }



        /// <summary>代码杀</summary>
        public void Kill() {
            CurrentHealth = 0;

            // call OnDamage action
            //OnDamaged?.Invoke(MaxHealth, null,null,true);

            HandleDeath(null, null);
        }

        /// <summary>死亡处理</summary>
        /// <param name="caused">**直接来源**（显示/AI）</param>
        /// <param name="origin">**起源**（统计）</param>
        protected virtual void HandleDeath(GameObject caused, GameObject origin) {
            if (m_IsDead)
                return;

            // call OnDie action
            if (CurrentHealth <= 0) {
                //Debug.Log(gameObject+"单位死亡",gameObject);
                m_IsDead = true;
                // 确保血量不超过0，防止异常恢复导致僵尸单位
                CurrentHealth = 0;
                showHealth = 0;
                OnDie?.Invoke(caused, origin);
                if (AutoDestroy) Destroy();
            }

        }

        //检视器用
        public void Destroy()
        {
            Tool.Destroy(gameObject);
        }

    }

}