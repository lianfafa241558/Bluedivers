using System;
using System.Collections.Generic;
using Core;
using PEMaths;
using UnityEngine;

namespace Unity.FPS.Game
{
    public abstract partial class Health
    {
        private const float _AboTick = 0.5f;

        private static List<SKVP<DamageTypeEnum, int>> _DefaultAboGaugeArr = new() {
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Toxicity, 100),
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Burn, 100),
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Freeze, 100),
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Electric, 100),
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Vertigo, 100),
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Terror, 100),
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Radiation, 40),
            new SKVP<DamageTypeEnum,int>(DamageTypeEnum.Hacker, 100),
        };

        [SerializeField]
        [InspectorName("异常状态积蓄槽")]
        private List<SKVP<DamageTypeEnum, int>> AboGaugeArr = new();

        /// <summary>异常状态积蓄槽</summary>
        protected Dictionary<DamageTypeEnum, AboGaugeEntry> AboGauge = new();

        /// <summary>复用的异常状态 tick 伤害缓冲，避免 Update 中分配</summary>
        private static readonly List<SKVP<DamageTypeEnum, PEInt>> _AboDmgBuffer = new ();


        private IUnit m_Unit;

        private void InitAboState()
        {
            AboGauge.Clear();
            foreach (var item in AboGaugeArr)
            {
                AboGauge.Add(item.Key, new AboGaugeEntry { Current = 0, Max = item.Value });
            }
            foreach (var item in _DefaultAboGaugeArr)//没有的用默认值填充
            {
                AboGauge.TryAdd(item.Key, new AboGaugeEntry { Current = 0, Max = item.Value });
            }
            m_Time = Time.time + _AboTick;
            m_Unit = GetComponent<IUnit>();
        }

        private void UninitState()
        {

        }


        /// <summary>检查指定异常状态的积蓄值是否已满</summary>
        public bool IsAboStateFull(DamageTypeEnum type)
        {
            return AboGauge != null
                && AboGauge.TryGetValue(type, out AboGaugeEntry entry)
                && entry.IsFull;
        }

        /// <summary>异常状态满槽事件（参数：异常类型、造成伤害者、是否变为满槽）</summary>
        public event Action<DamageTypeEnum, GameObject, bool> OnAboStateFullChanged;

        /// <summary>触发满槽状态变化事件</summary>
        private void NotifyFullChanged(DamageTypeEnum type, AboGaugeEntry entry)
        {
            bool full = entry.IsFull;
            if (full != entry.WasFull)
            {
                entry.WasFull = full;
                OnAboStateFullChanged?.Invoke(type, entry.Source, full);
            }
        }

        /// <summary>异常状态 UI 展示信息</summary>
        public struct AboStateViewInfo
        {
            public DamageTypeEnum Type;
            /// <summary>当前积蓄值</summary>
            public float Current;
            /// <summary>最大积蓄值</summary>
            public float Max;
            /// <summary>异常状态图标</summary>
            public Sprite Icon;
            /// <summary>异常状态颜色</summary>
            public Color Color;
        }

        /// <summary>获取当前积蓄值不为0的异常状态信息（清除并填充到给定列表，复用避免 GC）</summary>
        public void GetActiveAboStates(List<AboStateViewInfo> results)
        {
            results.Clear();
            if (AboGauge == null) return;

            foreach (var pair in AboGauge)
            {
                AboGaugeEntry entry = pair.Value;
                if (entry == null || entry.Max <= 0 || entry.Current <= 0) continue;

                Sprite icon = null;
                Color color = Color.white;
                if (ResSvc.aboStateDic != null
                    && ResSvc.aboStateDic.TryGetValue(pair.Key, out AboStateData_SO stateData)
                    && stateData != null)
                {
                    icon = stateData.icon;
                    color = stateData.color;
                }

                results.Add(new AboStateViewInfo
                {
                    Type = pair.Key,
                    Current = entry.Current.RawFloat,
                    Max = entry.Max.RawFloat,
                    Icon = icon,
                    Color = color,
                });
            }
        }

        /// <summary>增加指定异常类型的积蓄值并刷新上次获得时间</summary>
        private void AddAboGauge(DamageTypeEnum type, PEInt amount, GameObject source)
        {
            if (amount <= 0) return;
            if (!AboGauge.TryGetValue(type, out AboGaugeEntry entry) || entry.Max <= 0) return;

            entry.Current += amount;
            if (entry.Current > entry.Max) entry.Current = entry.Max;
            entry.LastGainTime = (PEInt)Time.time;
            entry.Source = source;
            NotifyFullChanged(type, entry);
        }
        
        /// <summary>异常状态 tick：处理衰减、满槽伤害与特效</summary>
        private void AboTick()
        {
            m_Time = Time.time + _AboTick;
            if (ResSvc.aboStateDic == null) return;

            foreach (var pair in AboGauge)
            {
                DamageTypeEnum type = pair.Key;
                AboGaugeEntry entry = pair.Value;
                if (entry.Max <= 0) continue;
                if (!ResSvc.aboStateDic.TryGetValue(type, out AboStateData_SO stateData) || stateData == null)
                    continue;

                if (entry.IsFull)
                {
                    // 满：每次 tick 造成 fullDamage + fullPerDamage/100 的百分比伤害
                    DealAboTickDamage(entry, stateData, true);
                    // 满：挂载异常状态特效到造成伤害者下
                    EnsureAboVfx(entry, stateData);

                    // 超过最短维持时间才开始扣除恢复速度 * _AboTick 的积蓄值
                    if ((PEInt)Time.time - entry.LastGainTime > (PEInt)stateData.duration)
                    {
                        entry.Current -= (PEInt)(stateData.recovery * _AboTick / 100f) * entry.Max;
                        if (entry.Current < 0) entry.Current = 0;
                        if (!entry.IsFull) ReleaseAboVfx(entry);
                    }
                }
                else if(entry.Current > 0 && (PEInt)Time.time - entry.LastGainTime > (PEInt)stateData.duration)
                {
                    // 未满：每次 tick 造成 damage 伤害
                    DealAboTickDamage(entry, stateData, false);
                    ReleaseAboVfx(entry);
                    entry.Current -= (PEInt)(stateData.recovery * _AboTick/100f) * entry.Max;
                    if (entry.Current < 0) entry.Current = 0;
                }
                NotifyFullChanged(type, entry);
            }
        }

        /// <summary>异常状态 tick 伤害：满槽造成 fullDamage + 百分比伤害，未满造成 damage，类型为状态自身 typeEnum，不更新积蓄槽</summary>
        private void DealAboTickDamage(AboGaugeEntry entry, AboStateData_SO stateData, bool isFull)
        {
            PEInt dmg = isFull
                ? (PEInt)stateData.fullDamage + (PEInt)(stateData.fullPerDamage / 100f * MaxHealth)
                : (PEInt)stateData.damage;
            if (dmg <= 0) return;

            _AboDmgBuffer.Clear();
            _AboDmgBuffer.Add(new SKVP<DamageTypeEnum, PEInt>(stateData.typeEnum, dmg));
            // 标记为异常状态 tick 伤害，HandleDamage 据此跳过 AddAboGauge（不更新 Current/LastGainTime）
            _isAboTickDamage = true;
            TakeDamage(_AboDmgBuffer, entry.Source == null, entry.Source, null, transform.position, stateData.damageTriggeredResponse);
            _isAboTickDamage = false;
        }

        /// <summary>满槽时异常状态特效挂载到自己身上</summary>
        private void EnsureAboVfx(AboGaugeEntry entry, AboStateData_SO stateData)
        {
            if (entry.VfxInstance != null) return;
            if (stateData.vfx == null || entry.Source == null) return;
            if (m_Unit == null) return;
            var attr = m_Unit.GetAttribute(UnitAttrType.Size);
            if (attr == null) return;
            entry.VfxInstance = VFXManager.Creat(
                stateData.vfx,
                transform.position,
                transform.rotation,
                transform);
            entry.VfxInstance.transform.localScale = Vector3.one * attr.FinalValue.RawFloat*2;
        }

        /// <summary>释放已挂载的异常状态特效</summary>
        private void ReleaseAboVfx(AboGaugeEntry entry)
        {
            if (entry.VfxInstance == null) return;
            VFXManager.Release(entry.VfxInstance);
            entry.VfxInstance = null;
        }



        /// <summary>异常状态积蓄槽条目</summary>
        [System.Serializable]
        public class AboGaugeEntry
        {
            /// <summary>当前积蓄值</summary>
            public PEInt Current;
            /// <summary>最大积蓄值</summary>
            public PEInt Max;
            /// <summary>上次获得积蓄值的时间</summary>
            public PEInt LastGainTime;
            /// <summary>造成伤害者</summary>
            public GameObject Source;
            /// <summary>已挂载的异常状态特效实例（运行时）</summary>
            public GameObject VfxInstance;
            /// <summary>上一 tick 是否满槽（用于检测满槽状态变化）</summary>
            public bool WasFull;

            /// <summary>积蓄值是否已满</summary>
            public bool IsFull => Max > 0 && Current >= Max;
        }
    }
}
