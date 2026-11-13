using FPSGame.Attributes;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// PlayerController 分部：姿态（站立 / 下蹲 / 卧倒）。
    ///
    /// <para>▍为什么集中在一个分部：姿态要同时改**胶囊高度、镜头高度、移速倍率、动画参数**四处，
    /// 散在 Update / LateUpdate / OnDie 里各自判断，迟早出现"蹲着复活""进载具回来还趴着"这类半截状态。</para>
    ///
    /// <para>▍切换式（按一下切一次），键位见 <c>InputState.Crouching</c>(C) / <c>InputState.Prone</c>(Z)；
    /// 碰撞体只改高不换形 ⇒ 脚底用 <c>center.y = height * 0.5f</c> 钉住
    /// （<c>GetCapsuleBottomHemisphere()</c> 的 <c>position + up*radius</c> 口径依赖这一点）。</para>
    /// </summary>
    public partial class PlayerController
    {
        private static readonly int s_animIsCrouch = Animator.StringToHash("IsCrouch");
        private static readonly int s_animIsProne = Animator.StringToHash("IsProne");

        [Foldout("姿态", true)]
        [InspectorName("下蹲胶囊高度")]
        [SerializeField] private float _crouchHeight = 1.2f;

        [InspectorName("卧倒胶囊高度")]
        [Tooltip("不能小于 radius*2（0.8），否则 Unity 会把胶囊压成球")]
        [SerializeField] private float _proneHeight = 0.8f;

        [InspectorName("下蹲镜头高度")]
        [Tooltip("第一人称相机离地高度（米）：蹲姿模型头顶约 0.85 ⇒ 取 0.82 正好在眼睛高度")]
        [SerializeField] private float _crouchCameraHeight = 0.82f;

        [InspectorName("卧倒镜头高度")]
        [Tooltip("趴姿模型头顶约 0.35 ⇒ 取 0.30，跟画面里贴地的身体对得上")]
        [SerializeField] private float _proneCameraHeight = 0.30f;

        [InspectorName("下蹲武器挂点下沉")]
        [Tooltip("武器挂点(FirstPersonSocket，站立 y=1.15)在蹲姿时往下挪多少米；它挂在玩家根节点、不吃相机高度，必须单独挪")]
        [SerializeField] private float _crouchSocketDrop = 0.60f;

        [InspectorName("卧倒武器挂点下沉")]
        [SerializeField] private float _proneSocketDrop = 1.00f;

        [InspectorName("下蹲移速倍率")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _crouchSpeedScale = 0.5f;

        [InspectorName("卧倒移速倍率")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _proneSpeedScale = 0.2f;

        [InspectorName("下蹲散布倍率")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _crouchSpreadScale = 0.7f;

        [InspectorName("卧倒散布倍率")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _proneSpreadScale = 0.5f;

        [InspectorName("下蹲后坐倍率")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _crouchRecoilScale = 0.75f;

        [InspectorName("卧倒后坐倍率")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _proneRecoilScale = 0.55f;

        /// <summary>当前姿态（只读）。联机上报时按整数值取（见 <see cref="PlayerStance"/>）。</summary>
        public PlayerStance Stance { get; private set; } = PlayerStance.Stand;

        /// <summary>姿态散布倍率（<see cref="PlayerWeaponsManager"/> 每帧写给当前武器，1 = 立姿）。</summary>
        public float StanceSpreadScale => Stance switch
        {
            PlayerStance.Crouch => _crouchSpreadScale,
            PlayerStance.Prone => _proneSpreadScale,
            _ => 1f,
        };

        /// <summary>姿态后坐倍率（同上）。</summary>
        public float StanceRecoilScale => Stance switch
        {
            PlayerStance.Crouch => _crouchRecoilScale,
            PlayerStance.Prone => _proneRecoilScale,
            _ => 1f,
        };

        /// <summary>姿态移速倍率：只在基类 GroundMove/AirMove 各乘一次，不碰共享的 MoveSpeedScale。</summary>
        protected override float ExtraSpeedScale => Stance switch
        {
            PlayerStance.Crouch => _crouchSpeedScale,
            PlayerStance.Prone => _proneSpeedScale,
            _ => 1f,
        };

        /// <summary>当前姿态的胶囊高度。</summary>
        private float StanceHeight => Stance switch
        {
            PlayerStance.Crouch => _crouchHeight,
            PlayerStance.Prone => _proneHeight,
            _ => _standHeight,
        };

        /// <summary>第三人称轨道中心相对 <see cref="BaseSelfController.CenterPos"/> 的竖直偏移（蹲/趴为负）。</summary>
        private float StanceOrbitOffsetY => CameraBasePoint.y - _standCameraBasePoint.y;

        /// <summary>第三人称 / 死亡相机的轨道中心：跟镜头一起降，别让蹲着的人还被镜头盯着头顶。</summary>
        private Vector3 StanceOrbitCenter => CenterPos + Vector3.up * StanceOrbitOffsetY;

        /// <summary>站立胶囊高度（Awake 抄的基线）。</summary>
        private float _standHeight;
        /// <summary>站立时的相机基准点（局部坐标，Awake 抄的基线）。</summary>
        private Vector3 _standCameraBasePoint;
        /// <summary>站立时的武器挂点局部坐标（Awake 抄的基线）。</summary>
        private Vector3 _standSocketLocalPos;
        private bool _stanceReady;

        /// <summary>模型动画参数是否存在：控制器里没接参数时静默跳过，避免每帧刷 Animator warning。</summary>
        private bool _hasCrouchParam;
        private bool _hasProneParam;

        /// <summary>Awake 末尾调用（必须在 base.Awake() 之后：Controller / CameraBasePoint 那时才有值）。</summary>
        private void InitStance()
        {
            _standHeight = Controller.height;
            _standCameraBasePoint = CameraBasePoint;
            if (WeaponsManager && WeaponsManager.FirstPersonSocket)
                _standSocketLocalPos = WeaponsManager.FirstPersonSocket.localPosition;
            _stanceReady = true;
        }

        /// <summary>姿态输入：切换式。C 在站立/下蹲间切、Z 在站立/卧倒间切，两个键互相可顶替。</summary>
        private void HandleStanceInput()
        {
            if (!_stanceReady || IsDead) return;

            if (InputHandler.GetProneToggleDown())
            {
                SetStance(Stance == PlayerStance.Prone ? PlayerStance.Stand : PlayerStance.Prone);
                return;
            }
            if (InputHandler.GetCrouchToggleDown())
            {
                SetStance(Stance == PlayerStance.Crouch ? PlayerStance.Stand : PlayerStance.Crouch);
            }
        }

        /// <summary>切姿态：胶囊、镜头基准、动画参数一次改完（移速/散布是现算属性，不用刷新）。
        /// <para>⚠ 只改高度不动半径：<c>center.y</c> 必须跟着走一半，否则脚底漂移、落地判定口径错位。</para></summary>
        public void SetStance(PlayerStance stance)
        {
            if (Stance == stance) return;
            if (!Controller) return;   // OnDisable 走在销毁路径上时 Controller 可能已经没了
            Stance = stance;

            float height = Mathf.Max(StanceHeight, Controller.radius * 2f);
            Controller.height = height;
            Controller.center = new Vector3(Controller.center.x, height * 0.5f, Controller.center.z);

            // 第一人称镜头高度：蹲/趴各有明确取值（不按胶囊等比算 —— 胶囊下限是 radius*2，比真实趴姿高出半米）
            // 基类 LateUpdate 本来就在往 CameraBasePoint 上 Lerp ⇒ 天然平滑
            float camY = stance switch
            {
                PlayerStance.Crouch => _crouchCameraHeight,
                PlayerStance.Prone => _proneCameraHeight,
                _ => _standCameraBasePoint.y,
            };
            CameraBasePoint = new Vector3(_standCameraBasePoint.x, camY, _standCameraBasePoint.z);

            // 武器挂点同样要跟着降：FirstPersonSocket 挂在玩家根节点上（y=1.15），武器父/瞄准点全是它的子节点，
            // 不跟着挪就是"人趴下了、枪还端在站立胸口高度"。
            if (WeaponsManager && WeaponsManager.FirstPersonSocket)
            {
                float socketDrop = stance switch
                {
                    PlayerStance.Crouch => _crouchSocketDrop,
                    PlayerStance.Prone => _proneSocketDrop,
                    _ => 0f,
                };
                WeaponsManager.FirstPersonSocket.localPosition =
                    new Vector3(_standSocketLocalPos.x, _standSocketLocalPos.y - socketDrop, _standSocketLocalPos.z);
            }

            ApplyStanceAnim();
        }

        /// <summary>换模型后（SetBody）重建"参数是否存在"缓存，并把当前姿态重新下发一次。</summary>
        private void RefreshStanceAnimParams()
        {
            _hasCrouchParam = _hasProneParam = false;
            if (!m_Anim) return;

            var ps = m_Anim.parameters;
            for (int i = 0; i < ps.Length; ++i)
            {
                if (ps[i].nameHash == s_animIsCrouch) _hasCrouchParam = true;
                else if (ps[i].nameHash == s_animIsProne) _hasProneParam = true;
            }
            ApplyStanceAnim();
        }

        /// <summary>姿态动画参数下发（动画资源由美术后续接，这里只保证状态位是对的）。</summary>
        private void ApplyStanceAnim()
        {
            if (!m_Anim) return;
            if (_hasCrouchParam) m_Anim.SetBool(s_animIsCrouch, Stance == PlayerStance.Crouch);
            if (_hasProneParam) m_Anim.SetBool(s_animIsProne, Stance == PlayerStance.Prone);
        }
    }
}
