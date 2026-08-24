using System.Collections.Generic;
using Core;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;

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
        /// <summary>Terror 逃离方向的目标点</summary>
        private Vector3 _terrorFleeDestination;

        /// <summary>订阅异常状态满槽事件</summary>
        private void InitAboStateListener()
        {
            var health = m_EnemyController.GetComponent<Health>();
            if (health != null)
            {
                health.OnAboStateFullChanged += OnAboStateFullChanged;
            }
        }

        private void OnDestroyAboState()
        {
            var health = m_EnemyController != null ? m_EnemyController.GetComponent<Health>() : null;
            if (health != null)
            {
                health.OnAboStateFullChanged -= OnAboStateFullChanged;
            }
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
                    HandleHacker(full);
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
            Vector2 rand = Random.insideUnitCircle.normalized * _ToxicityWanderRadius;
            _toxicityWanderDestination = transform.position + new Vector3(rand.x, 0f, rand.y);
            _toxicityWanderNextTime = Time.time + _ToxicityWanderInterval;
        }

        /// <summary>Hacker 满：尝试锁定同队伍单位；解除时清目标</summary>
        private void HandleHacker(bool full)
        {
            _hackerActive = full;
            if (full)
            {
                m_EnemyController.DetectionModule.SetTargetActor(null);
                m_EnemyController.DetectionModule.ClearBeware();
            }
        }

        /// <summary>是否被异常状态禁止攻击（Freeze/Vertigo/Terror），或弱点受击僵直期间</summary>
        public bool IsAttackLocked => _freezeActive || _vertigoActive || _terrorActive || _hitStunActive;

        /// <summary>是否被异常状态禁止移动（Freeze/Vertigo）</summary>
        public bool IsMoveLocked => _freezeActive || _vertigoActive;

        /// <summary>是否被异常状态强制原地乱攻（Toxicity）</summary>
        public bool IsForcedAttack => _toxicityActive;

        /// <summary>是否被异常状态强制锁定同队（Hacker）</summary>
        public bool IsHacked => _hackerActive;

        /// <summary>每帧处理 Hacker：寻找并锁定同队伍单位</summary>
        private void UpdateHackerTarget()
        {
            if (!_hackerActive) return;
            if (!BattleManager.Instance.IsValid()) return;

            var actor = m_EnemyController.GetComponent<Actor>();
            if (actor == null) return;

            var list = BattleManager.Instance.FindUnits(
                new PEMaths.PECircle(actor.LogicPos, (PEMaths.PEInt)20f),
                TargetCfg.EnemyAI,
                item => item.Team == actor.Team && item != (I_Actor)actor && FpsHelper.VaildTarget(item));

            if (list.Count > 0)
            {
                m_EnemyController.DetectionModule.SetTargetActor(list[0]);
            }
        }
    }
}

