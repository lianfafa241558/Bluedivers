using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.Utils;
using FPSGame.Weapon;

namespace FPSGame.AI
{
    /// <summary>EnemyMobile 异常状态行为：Electric 减速、Freeze 停滞、Vertigo 停滞禁攻、Terror 逃跑禁攻、Toxicity 乱走乱攻、Hacker 锁同队</summary>
    public partial class EnemyMobile
    {
        /// <summary>Electric 满槽时施加的速度 Factor 修正</summary>
        private const float _ElectricSpeedFactor = -0.3f;

        /// <summary>Electric 减速修饰是否已应用</summary>
        private bool _electricDebuffApplied;
        /// <summary>Freeze 是否生效</summary>
        private bool _freezeActive;
        /// <summary>Vertigo 是否生效</summary>
        private bool _vertigoActive;
        /// <summary>Terror 是否生效</summary>
        private bool _terrorActive;
        /// <summary>Toxicity 是否生效</summary>
        private bool _toxicityActive;
        /// <summary>Toxicity 乱走目标点</summary>
        private Vector3 _toxicityWanderDestination;
        /// <summary>Toxicity 乱走下次换点时间</summary>
        private float _toxicityWanderNextTime;
        /// <summary>Toxicity 乱走换点间隔</summary>
        private const float _ToxicityWanderInterval = 1.5f;
        /// <summary>Toxicity 乱走半径</summary>
        private const float _ToxicityWanderRadius = 5f;
        /// <summary>Hacker 是否生效</summary>
        private bool _hackerActive;
        /// <summary>被骇入的那个单位（改 Team 用；惰性缓存，不与 m_EnemyController 同生命周期）</summary>
        private Actor _hackerActor;
        /// <summary>骇入前的原始队伍，解除/销毁时还原</summary>
        private int _hackerOriginTeam;
        /// <summary>Terror 逃离方向的目标点</summary>
        private Vector3 _terrorFleeDestination;

        /// <summary>订阅异常状态满槽事件</summary>
        private void InitAboStateListener()
        {
            var health = m_EnemyController.GetComponent<IHealth>();
            if (health != null)
            {
                health.OnAboStateFullChanged += OnAboStateFullChanged;
            }

            // 原始队伍必须在这里记：骇入会改写 Team，事后取回来就已经是被改过的值了
            _hackerActor = m_EnemyController.GetComponent<Actor>();
            if (_hackerActor != null) _hackerOriginTeam = _hackerActor.Team;
        }

        private void OnDestroyAboState()
        {
            var health = m_EnemyController != null ? m_EnemyController.GetComponent<IHealth>() : null;
            if (health != null)
            {
                health.OnAboStateFullChanged -= OnAboStateFullChanged;
            }

            // 销毁/回收前把队伍改回去，别让 Team 残留到下一次复用
            RestoreHackerTeam();
        }

        private void OnAboStateFullChanged(DamageTypeEnum type, GameObject source, bool full)
        {
            switch (type)
            {
                case DamageTypeEnum.Electric:
                    HandleElectric(full);
                    break;
                case DamageTypeEnum.Freeze:
                    HandleFreeze(full);
                    break;
                case DamageTypeEnum.Vertigo:
                    HandleVertigo(full);
                    break;
                case DamageTypeEnum.Terror:
                    HandleTerror(full, source);
                    break;
                case DamageTypeEnum.Toxicity:
                    HandleToxicity(full);
                    break;
                case DamageTypeEnum.Hacker:
                    HandleHacker(full, source);
                    break;
            }
        }

        /// <summary>Electric 满：速度 Factor -0.3；解除时移除</summary>
        private void HandleElectric(bool full)
        {
            if (m_EnemyController.Speed == null) return;
            if (full && !_electricDebuffApplied)
            {
                m_EnemyController.Speed.AddModifier(ModifierType.Factor, (PEMaths.PEInt)_ElectricSpeedFactor);
                _electricDebuffApplied = true;
            }
            else if (!full && _electricDebuffApplied)
            {
                m_EnemyController.Speed.AddModifier(ModifierType.Factor, (PEMaths.PEInt)(-_ElectricSpeedFactor));
                _electricDebuffApplied = false;
            }
        }

        /// <summary>Freeze 满：停止一切动作（移动+攻击）；解除时恢复</summary>
        private void HandleFreeze(bool full)
        {
            _freezeActive = full;
            if (full)
            {
                m_EnemyController.DetectionModule.SetTargetActor(null);
                m_EnemyController.DetectionModule.ClearBeware();
                m_EnemyController.StopNav();
            }
        }

        /// <summary>Vertigo 满：不移动、不攻击、丢失目标和警惕点</summary>
        private void HandleVertigo(bool full)
        {
            _vertigoActive = full;
            if (full)
            {
                m_EnemyController.DetectionModule.SetTargetActor(null);
                m_EnemyController.DetectionModule.ClearBeware();
                m_EnemyController.StopNav();
            }
        }

        /// <summary>Terror 满：不攻击、丢失目标和警惕点、立刻往远离伤害源方向跑</summary>
        private void HandleTerror(bool full, GameObject source)
        {
            _terrorActive = full;
            if (full)
            {
                m_EnemyController.DetectionModule.SetTargetActor(null);
                m_EnemyController.DetectionModule.ClearBeware();

                if (source != null)
                {
                    Vector3 awayDir = (transform.position - source.transform.position).normalized;
                    _terrorFleeDestination = transform.position + awayDir * 20f;
                    m_EnemyController.SetNavDestination(_terrorFleeDestination);
                }
            }
        }

        /// <summary>Toxicity 满：乱走+持续攻击；解除时恢复</summary>
        private void HandleToxicity(bool full)
        {
            _toxicityActive = full;
            if (full)
            {
                m_EnemyController.DetectionModule.SetTargetActor(null);
                m_EnemyController.DetectionModule.ClearBeware();
                RefreshToxicityWanderDestination();
            }
        }

        /// <summary>刷新 Toxicity 乱走目标点（自身周围随机方向）</summary>
        private void RefreshToxicityWanderDestination()
        {
            // ★ 走本单位的确定性流（乱走目标点两端才一致）
            Vector2 rand = FPSGame.AI.EnemyRandom.For(this).InsideUnitCircle().normalized * _ToxicityWanderRadius;
            _toxicityWanderDestination = transform.position + new Vector3(rand.x, 0f, rand.y);
            _toxicityWanderNextTime = Time.time + _ToxicityWanderInterval;
        }

        /// <summary>
        /// Hacker 满：把单位所属改成攻击者的队伍（真倒戈）—— 改完 <c>DetectionModule.CheckNewTarget</c>
        /// 只会找"非本队"，即它的原队友，不用再手动塞目标。解除/销毁时复原。
        ///
        /// <para>⚠ 代价（选 A 方案时就接受的）：玩家打它会走友伤减伤
        /// （<c>Damageable.InflictDamage</c> 按 <c>Actor.Team == SourceActor.Team</c> 乘 <c>SensibilityToSelfdamage</c>）。
        /// 要让它"倒戈但照样吃满伤"，得在伤害链上单独给骇入单位开洞，别在这里绕。</para>
        /// <para>▍槽是永久满的（见 <c>Health.AboTick</c>），所以解除分支基本只在销毁时走到。</para>
        /// </summary>
        /// <param name="full">是否满槽</param>
        /// <param name="source">直接来源（<c>AboGaugeEntry.Source</c>）：倒戈后加入它的队伍</param>
        private void HandleHacker(bool full, GameObject source)
        {
            if (!full)
            {
                RestoreHackerTeam();
                return;
            }
            if (_hackerActive) return;   // 重复满槽不再改（否则会把"当前队伍"当成原队伍记下来）

            if (_hackerActor == null)
            {
                _hackerActor = m_EnemyController != null ? m_EnemyController.GetComponent<Actor>() : null;
                if (_hackerActor == null) return;
                _hackerOriginTeam = _hackerActor.Team;
            }

            Actor sourceActor = null;
            if (source != null)
            {
                sourceActor = source.GetComponent<Actor>();
                if (sourceActor == null) sourceActor = source.GetComponentInParent<Actor>();
            }
            if (sourceActor == null)
            {
                Debug.LogWarning($"[骇入] {name} 的来源 {source} 不是单位，无法倒戈", this);
                return;
            }

            _hackerActive = true;
            _hackerActor.Team = sourceActor.Team;

            // 旧目标（原阵营）现在变同队了，清掉交给感知重找 —— 改 Team 后它只会锁定原队友
            m_EnemyController.DetectionModule.SetTargetActor(null);
            m_EnemyController.DetectionModule.ClearBeware();
        }

        /// <summary>把队伍还原成骇入前的样子（幂等）。</summary>
        private void RestoreHackerTeam()
        {
            if (!_hackerActive) return;
            _hackerActive = false;
            if (_hackerActor != null) _hackerActor.Team = _hackerOriginTeam;
        }

        /// <summary>是否被异常状态禁止攻击（Freeze/Vertigo/Terror），或弱点受击僵直期间</summary>
        public bool IsAttackLocked => _freezeActive || _vertigoActive || _terrorActive || _hitStunActive;

        /// <summary>是否被异常状态禁止移动（Freeze/Vertigo）</summary>
        public bool IsMoveLocked => _freezeActive || _vertigoActive;

        /// <summary>是否被异常状态强制原地乱攻（Toxicity）</summary>
        public bool IsForcedAttack => _toxicityActive;

        /// <summary>是否已被骇入（队伍已被改成攻击者的）</summary>
        public bool IsHacked => _hackerActive;
    }
}

