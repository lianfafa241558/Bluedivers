
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 智能武器锁头
    /// </summary>
    [AddComponentMenu("子弹/锁定子弹", 30)]
    public class ProjectileLockStandard : ProjectileStandard
    {

        [Header("运动")]

        [InspectorName("转向速度")]
        [Tooltip("收敛速率(1/秒)：越大越早咬住目标，与帧率无关")]
        public float SteeringSpeed = 5;

        private Transform _target;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        protected override void OnEnable()
        {
            // ⚠ 池化复用：上一发锁的目标必须在这里清（OnEnable 在池弹出时跑，早于 Boss 的 SetTarget）；
            //   放到 _OnShoot 里清反而会把**本次**刚 Set 的目标抹掉（实际开火是 OnBulletShoot 先、Shoot 后）。
            _target = null;
            base.OnEnable();
        }

        protected override void Update()
        {
            if (m_isStop) return;

            // 穿透命中过单位 ⇒ 立刻脱离锁定，沿命中那一刻的朝向直线飞走。
            // ⚠ 不清的话转向会把它拉回来，子弹绕着目标穿第二圈（像在敌人身边转圈）；
            //   m_hasHits 只登记"打中过的单位"（穿透分支与弹射写），打地形不会误解除。
            if (_target && m_hasHits.Count > 0) _target = null;

            Steer(Time.time - m_lastTime);
            base.Update();
        }

        /// <summary>把飞行方向拧向锁定点，**总速度大小原样保留**。
        /// <para>⚠ 别改回 <c>Vector3.Lerp(速度, 目标方向*速度大小, k)</c>：目标在正背后时两向量反向，
        /// 插值中点会穿过零向量 ⇒ 子弹每帧掉一截速度、方向来回翻，最后原地抽搐（用户实测"背对目标速度很奇怪"）。</para>
        /// </summary>
        private void Steer(float tick)
        {
            if (!_target || tick <= 0f) return;

            Vector3 toTarget = _target.position - transform.position;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            // 实际飞行方向 = m_Velocity + 继承的枪口速度 ⇒ 拧的是"总速度"：
            // 只拧 m_Velocity 的话，那条横向偏移会一直留着，子弹绕着目标画弧。
            Vector3 velocity = m_Velocity + InheritedMuzzleVelocity;
            float speed = velocity.magnitude;
            if (speed < 0.0001f) return;

            Vector3 dir = velocity / speed;
            Vector3 want = toTarget.normalized;

            // 指数收敛：每帧只转过"剩余夹角"的一部分，帧率无关（SteeringSpeed*tick 当 Lerp 系数会随帧率漂）
            float angle = Vector3.Angle(dir, want) * (1f - Mathf.Exp(-SteeringSpeed * tick));

            Vector3 axis = Vector3.Cross(dir, want);
            if (axis.sqrMagnitude < 1e-6f)
            {
                // 正对 ⇒ 不用转；正背 ⇒ 叉积退化，任取一条与当前方向垂直的轴（不然转轴为零向量，速度直接归零）
                if (Vector3.Dot(dir, want) > 0f) return;
                axis = Vector3.Cross(dir, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(dir, Vector3.right);
            }

            Vector3 turned = Quaternion.AngleAxis(angle, axis.normalized) * velocity;
            // 扣回继承速度：Move 里位置是按 (m_Velocity + InheritedMuzzleVelocity) 走的
            m_Velocity = turned - InheritedMuzzleVelocity;
        }

    }
}