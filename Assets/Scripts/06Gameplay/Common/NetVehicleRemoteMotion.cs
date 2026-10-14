using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 【远端载具 · 表现】用"网络推导出来的每帧位移"驱动载具的移动动画。
    ///
    /// <para>▍为什么需要（2026-10-14 实测："客机看不到机甲走路动画"）：载具的运动栈
    /// （<c>VehicleController</c> → <c>BaseSelfMoveableController</c>）在远端被**冻结**了 ——
    /// 它的 <c>Update</c> 会跑 <c>GroundCheck</c> + 重力 <c>Move</c>，与 09 推出来的位姿打架
    /// （见 <c>NetVehicleBridge.SetRemoteLocomotion</c>）；而"走路动画"恰好挂在它的**同一个 <c>LateUpdate</c>** 里
    /// （读 <c>CharacterVelocity</c>）⇒ 一冻结，动画就永远不播（而且远端也没人动 CC ⇒ 速度恒 0）。</para>
    ///
    /// <para>▍口径**照抄本地那一份**（<c>BaseSelfMoveableController.LateUpdate</c>，与 <c>FriendController.DriveAnim</c> 同款）：
    /// 速度 &gt; 0.5 才算移动；<c>Speed = 速度 ÷ 5 × 前后符号</c>；静止时 <c>Speed = 1</c>。
    /// 位移用**插值后**的 <c>transform</c> 差分 ⇒ 与本地看到的同源（前后/倒退的符号也一并还原）。</para>
    ///
    /// <para>▍与 <c>NetTransformView</c> 的分工：那个（执行序 -50）负责把 <c>transform</c> 平滑摆到位；
    /// 本组件只读它写好的结果（默认执行序）⇒ 顺序天然正确。</para>
    ///
    /// <para>▍挂载方 = <c>NetVehicleBridge</c>（上车时挂上并启用、下车时关掉：
    /// 下车后载具自己的运动栈回来了，动画该由它自己驱动，两边同时写会打架）。</para>
    /// </summary>
    [AddComponentMenu("网络/远端载具动画(NetVehicleRemoteMotion)")]
    public class NetVehicleRemoteMotion : MonoBehaviour
    {
        /// <summary>动画参数名与本地同一套（<c>BaseSelfMoveableController</c> 里是硬编码 "Speed"/"IsMove"）。</summary>
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimIsMove = Animator.StringToHash("IsMove");

        private Animator _anim;

        /// <summary>同物体上的位姿插值组件（由桥一起挂上；读它的"吸附"标记丢假速度）。</summary>
        private NetTransformView _view;

        private Vector3 _lastPos;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            if (_anim == null) _anim = GetComponentInChildren<Animator>(true);   // 少数载具把动画挂在子物体上
            _view = GetComponent<NetTransformView>();
            _lastPos = transform.position;
            Idle();
        }

        private void OnEnable()
        {
            // 重新接管（上车）时把速度归零：否则会拿"停用期间"的旧 _lastPos 差分出一次假速度
            _lastPos = transform.position;
            Idle();
        }

        private void Update()
        {
            if (_anim == null) return;

            // 吸附 / 真传送那一帧的位移是假的（会让动画瞬间飙到最快，同 FriendController 的做法）⇒ 丢掉这一帧
            if (_view != null && _view.ConsumeSnap())
            {
                _lastPos = transform.position;
                Idle();
                return;
            }

            // ⚠ 顺序保证：NetTransformView 标了 [DefaultExecutionOrder(-50)]，一定先写完 transform
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 vel = (transform.position - _lastPos) / dt;
            vel.y = 0f;                                    // 与本地一致：只吃水平速度（上下不算走路）
            _lastPos = transform.position;

            float speed = vel.magnitude;
            if (speed > 0.5f)                              // 与本地同一阈值
            {
                _anim.SetBool(AnimIsMove, true);
                // 前后符号：本地用 InverseTransformDirection(velocity).z 的符号（前进为正 / 倒退为负）
                _anim.SetFloat(AnimSpeed, speed / 5f * Mathf.Sign(transform.InverseTransformDirection(vel).z));
            }
            else
            {
                Idle();
            }
        }

        /// <summary>静止基线 —— 与本地静止时的取值一致（<c>IsMove=false</c>、<c>Speed=1</c>）。</summary>
        private void Idle()
        {
            if (_anim == null) return;
            _anim.SetBool(AnimIsMove, false);
            _anim.SetFloat(AnimSpeed, 1f);
        }
    }
}
