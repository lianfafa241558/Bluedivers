using Unity.FPS.Game;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>EnemyControllerFX 异常状态行为：Freeze 满 Animator 速度降至 0.01，解除恢复</summary>
    public abstract partial class EnemyControllerFX
    {
        /// <summary>Freeze 满槽时的 Animator.speed</summary>
        private const float _FreezeAnimSpeed = 0.01f;

        /// <summary>原始 Animator.speed，用于恢复</summary>
        private float _originalAnimSpeed = 1f;

        /// <summary>订阅异常状态满槽事件</summary>
        private void InitAboStateFxListener()
        {
            if (m_Controller != null)
            {
                var health = ((MonoBehaviour)m_Controller).GetComponent<Health>();
                if (health != null)
                {
                    health.OnAboStateFullChanged += OnAboStateFxChanged;
                }
            }
        }

        private void OnDestroyAboStateFx()
        {
            if (m_Controller == null) return;
            var health = ((MonoBehaviour)m_Controller).GetComponent<Health>();
            if (health != null)
            {
                health.OnAboStateFullChanged -= OnAboStateFxChanged;
            }
        }

        private void OnAboStateFxChanged(Core.DamageTypeEnum type, GameObject source, bool full)
        {
            if (type != Core.DamageTypeEnum.Freeze) return;

            if (Animator == null) return;

            if (full)
            {
                if (!_freezeAnimApplied)
                {
                    _originalAnimSpeed = Animator.speed;
                    Animator.speed = _FreezeAnimSpeed;
                    _freezeAnimApplied = true;
                }
            }
            else
            {
                if (_freezeAnimApplied)
                {
                    Animator.speed = _originalAnimSpeed;
                    _freezeAnimApplied = false;
                }
            }
        }

        /// <summary>Freeze 动画减速是否已应用</summary>
        private bool _freezeAnimApplied;




    }
}
