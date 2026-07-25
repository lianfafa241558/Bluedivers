using UnityEngine;

namespace EffectComp
{
    /// <summary>
    /// CharacterController 重力模拟组件
    /// 仅在 CharacterController.enabled 为 true 时生效
    /// </summary>
    public class CCGravity : MonoBehaviour
    {
        [InspectorName("重力加速度")]
        [Tooltip("重力加速度值，默认 20")]
        public float Gravity = 20f;

        [InspectorName("最大下落速度")]
        [Tooltip("限制最大下落速度，防止穿透地面")]
        public float MaxFallSpeed = 50f;

        private CharacterController _controller;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_controller == null || !_controller.enabled)
            {
                return;
            }

            // 如果角色在地面上且没有向上速度，重置垂直速度防止无限累积
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            else
            {
                _verticalVelocity -= Gravity * Time.deltaTime;
                _verticalVelocity = Mathf.Max(_verticalVelocity, -MaxFallSpeed);
            }

            Vector3 moveDelta = Vector3.up * _verticalVelocity * Time.deltaTime;
            _controller.Move(moveDelta);
        }

        /// <summary>
        /// 添加垂直速度（用于跳跃等）
        /// </summary>
        public void AddVerticalVelocity(float velocity)
        {
            _verticalVelocity += velocity;
        }

        /// <summary>
        /// 重置垂直速度
        /// </summary>
        public void ResetVerticalVelocity()
        {
            _verticalVelocity = 0f;
        }
    }
}
