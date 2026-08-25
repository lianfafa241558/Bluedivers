using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;
using PEMaths;


using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.Game {
    public abstract partial class Health : MonoBehaviour {




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


        /// <summary>收到伤害时,值，来源，受击点,无源伤害</summary>
        public UnityAction<PEInt, GameObject, Collider,bool> OnDamaged;

        /// <summary>被击中时 来源，受击点</summary>
        public UnityAction<GameObject,Vector3,bool> OnHit;

        /// <summary>是否是破盾伤害</summary>
        public UnityAction<bool> OnShieldDamaged;

        /// <summary>收到治疗时 治疗值</summary>
        public UnityAction<PEInt> OnHealed;
        /// <summary>恢复护盾时 恢复值</summary>
        public UnityAction<PEInt> OnRestoreShield;
        /// <summary>死亡时</summary>
        public UnityAction<GameObject> OnDie;

        /// <summary>复活时</summary>
        public UnityAction OnRevive;

        [Space]
        [DisplayField]
        [InspectorName("剩余生命值")]
        [SerializeField]
        public int showHealth;

        //剩余血量
        public PEInt CurrentHealth { get; set; }
        //剩余护盾
        public PEInt CurrentShield { get; set; }

        public bool CanPickup() => CurrentHealth < MaxHealth;

        public float GetHpRatio() => CurrentHealth.RawFloat / (MaxHealth+0f);

        public float GetShieldRatio() => CurrentShield.RawFloat / (MaxShield+0f);

        [DisplayField]
        [SerializeField]
        protected bool m_IsDead;

        /// <summary>所属单位（无敌标记的唯一来源，Awake 缓存）</summary>
        protected Actor m_Actor;

        protected float m_LastHitTime = Mathf.NegativeInfinity;

        private float m_Time;

        /// <summary>标记当前 TakeDamage 调用来自异常状态 tick，HandleDamage 据此跳过积蓄槽更新</summary>
        private bool _isAboTickDamage;

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
        public void Heal(float healAmount) {
            if (m_IsDead)
                return;

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

        /// <summary>受到伤害</summary>
        public void TakeDamage(List<SKVP<DamageTypeEnum, PEInt>> damageGroups, bool noSource, GameObject damageSource,Collider damageAffected,Vector3 pos,bool response=true,bool isWeakness=false,int demolishValue=-1) {
            if (m_IsDead || (m_Actor != null && m_Actor.HasFlag(ActorFlag.Invincible)))
                return;

            // 拆毁秒杀：自身拆毁值 -1 视为无法被拆毁；否则伤害拆毁值大于自身拆毁值则直接击杀，无视护盾，但受上面无敌标记保护
            if (demolishValue > 0 && this.demolishValue != -1 && demolishValue >= this.demolishValue)
            {
                CurrentHealth = 0;
                showHealth = 0;
                if (response) OnHit?.Invoke(damageSource, pos, isWeakness);
                HandleDeath(damageSource);
                return;
            }

            bool haveshield= CurrentShield > 0, isBreakShield=false;
            PEInt finaldamgage = 0;
            foreach(var item in damageGroups)
            {
                // 整体伤害抗性减伤乘区（未配置的类型为 1，不缩放）
                PEInt resistedValue = PEMath.Max(item.Value, 0) * (PEInt)GetResistance(item.Key);
                var re= HandleDamage(item.Key, resistedValue, damageSource, _isAboTickDamage);
                finaldamgage += re;
#if UNITY_EDITOR
                if (re.RawFloat>=0.5f)
                {
                    Color dmgColor = Color.white;
                    if (ResSvc.aboStateDic != null)
                    {
                        foreach (var grp in damageGroups)
                        {
                            if (ResSvc.aboStateDic.TryGetValue(grp.Key, out var aboState) && aboState != null)
                            {
                                dmgColor = aboState.color;
                                break;
                            }
                        }
                    }
                    if (damageAffected) Tool.DrawLabel(pos + RandomUtils.RandomVector3XZ(), "" + Tool.Round(finaldamgage.RawFloat), 3, dmgColor);
                }
#endif

            }

            //Debug.LogWarning("最终伤害" + finaldamgage + "血量" + CurrentHealth);


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
                OnDamaged?.Invoke(trueDamageAmount, damageSource,damageAffected, noSource);
                if(response)OnHit?.Invoke(damageSource,pos,isWeakness);
                if (haveshield) OnShieldDamaged?.Invoke(isBreakShield);
                //这个是控制hpui的，所以总是响应
                BattleEventSub.UnitHit(gameObject, damageSource);
            }
            showHealth = CurrentHealth.RawInt;
            HandleDeath(damageSource);
        }

        private PEInt HandleDamage(DamageTypeEnum type, PEInt value, GameObject damageSource, bool isAboTick = false) {
            //只有需求特殊处理的才单独写，目前没有状态槽，直接全部默认
            //Debug.LogWarning("对"+gameObject.name+"伤害:类型"+type+"值"+value);
            // 异常状态 tick 伤害：不更新积蓄槽（Current/LastGainTime），直接造成对应类型伤害
            if (isAboTick) {
                return type == DamageTypeEnum.Destruction ? 0 : value;
            }
            // Freeze 满槽：受到的 Gun/Explosion/Real 伤害变为 3 倍
            if (IsAboStateFull(DamageTypeEnum.Freeze)
                && (type == DamageTypeEnum.Gun || type == DamageTypeEnum.Explosion || type == DamageTypeEnum.Real))
            {
                value *= 3;
            }
            switch (type) {
                case DamageTypeEnum.Destruction:
                    return 0;//护甲破坏不计入伤害量
                case DamageTypeEnum.Burn:
                case DamageTypeEnum.Freeze:
                case DamageTypeEnum.Electric:
                case DamageTypeEnum.Radiation:
                    AddAboGauge(type, value, damageSource);
                    return value;//燃烧/冰冻/雷击/辐射 伤害计入伤害量
                case DamageTypeEnum.Toxicity:
                case DamageTypeEnum.Hacker:
                case DamageTypeEnum.Terror:
                case DamageTypeEnum.Vertigo:
                    AddAboGauge(type, value, damageSource);
                    return 0;//毒/骇入/恐慌/眩晕不计入伤害量

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


        /// <summary>复活</summary>
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

            HandleDeath(null);
        }

        /// <summary>死亡处理</summary>
        protected virtual void HandleDeath(GameObject source) {
            if (m_IsDead)
                return;

            // call OnDie action
            if (CurrentHealth <= 0) {
                //Debug.Log(gameObject+"单位死亡",gameObject);
                m_IsDead = true;
                // 确保血量不超过0，防止异常恢复导致僵尸单位
                CurrentHealth = 0;
                showHealth = 0;
                OnDie?.Invoke(source);
            }

        }

    }

}