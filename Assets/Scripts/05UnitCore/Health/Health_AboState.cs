using System;
using System.Collections.Generic;
using FPSGame.Audio;
using FPSGame.Core;
using FPSGame.GameContract;
using PEMaths;
using UnityEngine;
using FPSGame.Data;

namespace FPSGame.Game
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


        private FPSGame.GameContract.IUnitScale m_Unit;

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
            m_Unit = GetComponent<FPSGame.GameContract.IUnitScale>();//只取"体型"：避免单位内核引用 IUnit 而连带整套属性系统
            // ⚠ 这里**刻意不注册**：积蓄槽全 0 时无事可做，交战时上千个单位里常驻挂异常状态的只有个位数，
            //   全注册等于让每个单位每帧白跑一次比较。改为首次积蓄 >0 时才挂上（见 AddAboGauge）。
        }

        private void UninitState()
        {
            ReleaseAboTick();
        }

        /// <summary>是否已挂在逐帧驱动里（只有"身上有非零积蓄"的单位才需要每帧问到点没有）。</summary>
        private bool _aboTickRegistered;

        private bool HasActiveGauge()
        {
            foreach (var pair in AboGauge)
            {
                if (pair.Value != null && pair.Value.Current > 0) return true;
            }
            return false;
        }

        private void EnsureAboTick()
        {
            if (_aboTickRegistered) return;
            _aboTickRegistered = true;
            if (m_Time < Time.time) m_Time = Time.time + _AboTick;
            TickDriver.Register(this);
        }

        private void ReleaseAboTick()
        {
            if (!_aboTickRegistered) return;
            _aboTickRegistered = false;
            TickDriver.Unregister(this);
        }

        /// <summary>统一驱动入口（见 <see cref="TickDriver"/>）：原来这段在 Health.Update 里，
        /// 交战时上千个 Health 各自挂 Update，每次只做一次到点比较 ⇒ 光引擎调度就 ≈0.6ms/帧。</summary>
        void ITickDriverTarget.TickFrame(float now)
        {
            if (!isActiveAndEnabled) return;   // 与引擎"禁用/回收后不再调 Update"保持一致
            if (m_Unit == null) return;
            if (now >= m_Time) AboTick();
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



        /// <summary>获取当前积蓄值不为0的异常状态（清除并填充到给定列表，复用避免 GC）。
        /// <para>⚠ 2026-10-01：只回填**逻辑值**。原先这里顺带查 <c>AboStateData_SO.Dic</c> 取图标/颜色，
        /// 那是**表现层的事**（把 <c>Sprite</c>/<c>Color</c> 拖进了契约签名）⇒ 已移到 <c>HpItemBase</c>（10_UI）。</para></summary>
        public void GetActiveAboStates(List<AboStateGauge> results)
        {
            results.Clear();
            if (AboGauge == null) return;

            foreach (var pair in AboGauge)
            {
                AboGaugeEntry entry = pair.Value;
                if (entry == null || entry.Max <= 0 || entry.Current <= 0) continue;

                results.Add(new AboStateGauge
                {
                    Type = pair.Key,
                    Current = entry.Current.RawFloat,
                    Max = entry.Max.RawFloat,
                });
            }
        }

        /// <summary>增加指定异常类型的积蓄值并刷新上次获得时间</summary>
        /// <param name="damageCaused">**直接来源**（显示/AI 口径）：满槽特效挂载点、Terror 逃逸方向都取它</param>
        private void AddAboGauge(DamageTypeEnum type, PEInt amount, GameObject damageCaused)
        {
            if (amount <= 0) return;
            if (!AboGauge.TryGetValue(type, out AboGaugeEntry entry) || entry.Max <= 0) return;

            entry.Current += amount;
            if (entry.Current > entry.Max) entry.Current = entry.Max;
            entry.LastGainTime = (PEInt)Time.time;
            entry.Source = damageCaused;
            NotifyFullChanged(type, entry);
            EnsureAboTick();   // 首次积蓄 >0：挂上逐帧驱动，开始问到点没有
        }
        
        /// <summary>异常状态 tick：处理衰减、满槽伤害与特效</summary>
        private void AboTick()
        {
            m_Time = Time.time + _AboTick;
            if (AboStateData_SO.Dic == null) return;

            foreach (var pair in AboGauge)
            {
                DamageTypeEnum type = pair.Key;
                AboGaugeEntry entry = pair.Value;
                if (entry.Max <= 0) continue;
                if (!AboStateData_SO.Dic.TryGetValue(type, out AboStateData_SO stateData) || stateData == null)
                    continue;

                if (entry.IsFull)
                {
                    // 满：每次 tick 造成 fullDamage + fullPerDamage/100 的百分比伤害
                    DealAboTickDamage(entry, stateData, true);
                    // 满：挂载异常状态特效到造成伤害者下
                    EnsureAboVfx(entry, stateData);

                    // 超过最短维持时间才开始扣除恢复速度 * _AboTick 的积蓄值
                    // ⚠ 骇入是永久性的：完成骇入后槽不再回落（否则倒戈几秒就被打回原队伍）
                    if (type != DamageTypeEnum.Hacker
                        && (PEInt)Time.time - entry.LastGainTime > (PEInt)stateData.duration)
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

            // 积蓄全回到 0 ⇒ 摘出驱动（下一次中招时 AddAboGauge 会重新挂上）
            if (!HasActiveGauge()) ReleaseAboTick();
        }

        /// <summary>本单位是不是玩家 / 盟友（血量本机权威 ⇒ tick 伤害任何角色下都要本地算）。</summary>
        private bool IsPlayerLike
            => m_Actor != null && (m_Actor.Type == UnitTypeEnum.Player || m_Actor.Type == UnitTypeEnum.Friend);

        /// <summary>异常状态 tick 伤害：满槽造成 fullDamage + 百分比伤害，未满造成 damage，类型为状态自身 typeEnum，不更新积蓄槽</summary>
        private void DealAboTickDamage(AboGaugeEntry entry, AboStateData_SO stateData, bool isFull)
        {
            PEInt dmg = isFull
                ? (PEInt)stateData.fullDamage + (PEInt)(stateData.fullPerDamage / 100f * MaxHealth)
                : (PEInt)stateData.damage;
            if (dmg <= 0) return;

            // ★ 联机（2026-10-12）：异常状态跳伤**只由房主（或单机）结算并广播**（理由同 Damageable.Tick 的流血）——
            //   成员端只保留积蓄槽/特效（那是从已同步的状态推导的表现），血量等伤害广播落地。
            if (!FPSGame.Data.BattleState.ShouldApplyTickDamage(IsPlayerLike)) return;

            _AboDmgBuffer.Clear();
            _AboDmgBuffer.Add(new SKVP<DamageTypeEnum, PEInt>(stateData.typeEnum, dmg));
            // 标记为异常状态 tick 伤害，HandleDamage 据此跳过 AddAboGauge（不更新 Current/LastGainTime）
            _isAboTickDamage = true;
            // 无源 tick：caused = origin = entry.Source（都为空 ⇒ NoSource 分支，行为同改动前）
            var tickArgs = DamageSettleArgs.Of(_AboDmgBuffer, entry.Source == null, entry.Source, null, transform.position);
            tickArgs.Response = stateData.damageTriggeredResponse;
            TakeDamage(in tickArgs);
            _isAboTickDamage = false;
        }

        /// <summary>满槽时异常状态特效挂载到自己身上 + 播一次满槽音效</summary>
        private void EnsureAboVfx(AboGaugeEntry entry, AboStateData_SO stateData)
        {
            // 音效用独立标记：vfx 没配时本方法会每 tick 重进，靠 VfxInstance 判重会让音效跟着每 0.5s 重播一次
            if (!entry.SfxPlayed)
            {
                entry.SfxPlayed = true;
                if (stateData.sfx != null)
                    AudioSvc.PlaySound(new(stateData.sfx, transform.position, 30, AudioGroups.General));
            }

            if (entry.VfxInstance != null) return;
            if (stateData.vfx == null || entry.Source == null) return;
            if (m_Unit == null) return;
            // 走特效服务契约（原先直连 VFXManager ⇒ 单位内核反向依赖 09Manager，拆不动程序集）
            entry.VfxInstance = FPSGame.Core.VfxPool.Creat(
                stateData.vfx,
                transform.position,
                transform.rotation,
                transform);
            entry.VfxInstance.transform.localScale = Vector3.one * m_Unit.VisualScale * 2;
            
        }

        /// <summary>释放已挂载的异常状态特效（并复位"本段满槽已播过音效"）</summary>
        private void ReleaseAboVfx(AboGaugeEntry entry)
        {
            entry.SfxPlayed = false;
            if (entry.VfxInstance == null) return;
            FPSGame.Core.VfxPool.Release(entry.VfxInstance);
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
            /// <summary>本段满槽是否已播过音效（运行时）</summary>
            public bool SfxPlayed;
            /// <summary>上一 tick 是否满槽（用于检测满槽状态变化）</summary>
            public bool WasFull;

            /// <summary>积蓄值是否已满</summary>
            public bool IsFull => Max > 0 && Current >= Max;
        }
    }
}
