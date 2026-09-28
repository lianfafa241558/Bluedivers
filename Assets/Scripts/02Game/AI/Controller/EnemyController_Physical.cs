using Core;
using Core.Interface;
using PEMaths;
using UnityEngine;
using UnityEngine.AI;

namespace FPSGame.AI
{
    /// <summary>
    /// EnemyController 的受力分部：让 NavMeshAgent 单位也能被爆炸/踩踏推挤。
    /// 接口语义与 BaseSelfMoveableController 一致：ApplyImpulse 给瞬时 Δv = 冲量/重量，
    /// ApplyForce 给持续力(牛顿)，统一由 <see cref="UpdateKnockback"/> 逐帧施加并指数衰减。
    /// - NavMeshAgent 的位置被约束在 NavMesh 上 ⇒ **飞不起来**，只做水平推挤(丢弃 Y)
    /// - 位移用 NavMeshAgent.Move：贴着导航网格走，不会穿墙/出图
    /// - 推挤期间可选地临时关掉 NavMeshAgent.autoRepath：内建重算**不走 PathRequestManager** 的限流，
    ///   同时推飞多个单位可能同帧触发大量重算，关掉后速度归零再恢复原值
    /// - _knockbackImmune 勾选后本单位**完全不受力**(所有 Apply* 调用直接忽略)
    /// - 死亡时 _OnDie 会禁用 NavMeshAgent，此后自动停止受力
    /// 之所以能"零 prefab 改动"生效：Actor 与本组件同物体(FpsHelper 在 unit.transform 上找 IPhysical)。
    /// </summary>
    public partial class EnemyController : IPhysical
    {
        [Header("受力/击退")]
        [InspectorName("重量(受力换算用)")]
        [SerializeField]
        private float _knockbackWeight = 1f;
        [InspectorName("阻力(1/秒)")]
        [SerializeField]
        private float _knockbackDrag = 3f;
        [InspectorName("最大推挤速度")]
        [SerializeField]
        private float _knockbackMaxSpeed = 20f;
        [InspectorName("推挤期间暂停寻路重算")]
        [Tooltip("受力期间临时把 NavMeshAgent.autoRepath 置 false，避免被推飞时同帧触发大量重算" +
                 "(NavMeshAgent 内建重算不经过 PathRequestManager 的限流)。速度归零后恢复为原值。")]
        [SerializeField]
        private bool _pauseAutoRepathWhileKnocked = true;
        [InspectorName("完全不受力")]
        [Tooltip("勾选后本单位免疫一切外力(爆炸冲击波、踩踏击退等)：IPhysical 的 ApplyForce/ApplyImpulse 全部忽略。")]
        [SerializeField]
        private bool _knockbackImmune = false;

        /// <summary>受力速度(米/秒)，逐帧指数衰减</summary>
        PEVector3 _knockbackVelocity;
        /// <summary>本帧累积的持续力(牛顿)，积分后清空</summary>
        PEVector3 _pendingForce;

        /// <summary>当前是否处于"寻路重算已暂停"状态</summary>
        bool _autoRepathPaused;
        /// <summary>暂停前的 autoRepath 原值：恢复时必须写回它，不能硬写 true(有的单位本来就是 false)</summary>
        bool _autoRepathOriginal;

        /// <summary>施加一个持续力(牛顿)：逐帧累积，按 Δv = (力/重量)·dt 积分</summary>
        public void ApplyForce(PEVector3 force)
        {
            if (_knockbackImmune) return;
            _pendingForce += force;
        }

        /// <summary>施加一个瞬时冲量：Δv = 冲量 / 重量(无速度上限；重量越大被推得越少)</summary>
        public void ApplyImpulse(PEVector3 impulse)
        {
            if (_knockbackImmune) return;
            _knockbackVelocity += impulse / (PEInt)Mathf.Max(_knockbackWeight, 0.1f);
        }

        /// <summary>重力由 NavMeshAgent 自己负责，导航单位不吃外力重力</summary>
        public void ApplyGravity() { }

        /// <summary>逐帧结算受力推挤(由 EnemyController.Update 调用)</summary>
        void UpdateKnockback()
        {
            // 完全不受力：清掉残留受力并恢复寻路(可能在推挤途中才被勾上)
            if (_knockbackImmune)
            {
                _knockbackVelocity = default;
                _pendingForce = default;
                ResumeAutoRepath();
                return;
            }

            if (!FpsHelper.HaveNavMeshAgent(NavMeshAgent))
            {
                _knockbackVelocity = default;
                _pendingForce = default;
                ResumeAutoRepath();
                return;
            }

            // 持续力 → 速度：Δv = (力 / 重量) · dt
            if (_pendingForce.Magnitude > (PEInt)0.01f)
            {
                _knockbackVelocity += _pendingForce / (PEInt)Mathf.Max(_knockbackWeight, 0.1f) * (PEInt)Time.deltaTime;
                _pendingForce = default;
            }

            if (_knockbackVelocity.Magnitude <= (PEInt)0.01f)
            {
                _knockbackVelocity = default;
                ResumeAutoRepath();
                return;
            }

            // 有受力速度在推：暂停寻路重算，避免推挤期间反复重算
            PauseAutoRepath();

            // NavMesh 上只能水平推挤(垂直位移会被约束回导航网格)
            Vector3 velocity = _knockbackVelocity.RawVector3;
            velocity.y = 0f;
            NavMeshAgent.Move(Vector3.ClampMagnitude(velocity, _knockbackMaxSpeed) * Time.deltaTime);

            // 指数阻尼
            _knockbackVelocity = (PEVector3)(_knockbackVelocity.RawVector3 * Mathf.Exp(-_knockbackDrag * Time.deltaTime));
        }

        /// <summary>暂停寻路重算(记录原值，只暂停一次)</summary>
        void PauseAutoRepath()
        {
            if (!_pauseAutoRepathWhileKnocked || _autoRepathPaused || !NavMeshAgent) return;

            _autoRepathOriginal = NavMeshAgent.autoRepath;
            NavMeshAgent.autoRepath = false;
            _autoRepathPaused = true;
        }

        /// <summary>恢复寻路重算(还原为暂停前的原值)</summary>
        void ResumeAutoRepath()
        {
            if (!_autoRepathPaused) return;

            if (NavMeshAgent) NavMeshAgent.autoRepath = _autoRepathOriginal;
            _autoRepathPaused = false;
        }

        private void OnDisable()
        {
            // 带着"重算已暂停"的状态被回收/禁用，会让复用后的单位不再自动找路，这里兜底还原
            ResumeAutoRepath();
            _knockbackVelocity = default;
            _pendingForce = default;
        }
    }
}
