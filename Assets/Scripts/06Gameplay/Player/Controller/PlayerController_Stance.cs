using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Utils;      // Transform.Find(谓词) 递归查找（找模型里的 Body 网格）
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
        [InspectorName("姿态参数表")]
        [SerializeField] private List<StanceParam> _stanceParams = new();

        /// <summary>当前姿态（只读）。联机上报时按整数值取（见 <see cref="PlayerStance"/>）。</summary>
        public PlayerStance Stance { get; private set; } = PlayerStance.Stand;

        /// <summary>当前姿态的参数（表里漏配就回落到 <see cref="StanceParam.Default"/>）。</summary>
        private StanceParam Param => Resolve(Stance);

        /// <summary>按姿态查参数表。</summary>
        private StanceParam Resolve(PlayerStance stance)
        {
            for (int i = 0; i < _stanceParams.Count; ++i)
            {
                if (_stanceParams[i].Stance == stance) return _stanceParams[i];
            }
            return StanceParam.Default(stance);
        }

        /// <summary>倍率兜底：表里漏填（0）按 1 算 —— 别让一条空数据把移速/散布乘成 0。</summary>
        private static float Scale(float v) => v > 0f ? v : 1f;

        /// <summary>表为空时按默认值填三条：新挂的组件在检视器里不该是空列表（否则看不出有哪些参数可调）。
        /// <para>⚠ 只在编辑态、且表为空时动手，绝不覆盖已配好的值。</para></summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (_stanceParams != null && _stanceParams.Count > 0) return;
            FillDefaultStanceParams();
        }

        /// <summary>按默认值重填姿态表（手改乱了的恢复口；检视器组件右上角菜单里点）。</summary>
        [ContextMenu("按默认值重填姿态参数表")]
        private void FillDefaultStanceParams()
        {
            _stanceParams = new List<StanceParam>
            {
                StanceParam.Default(PlayerStance.Stand),
                StanceParam.Default(PlayerStance.Crouch),
                StanceParam.Default(PlayerStance.Prone),
            };
        }

        /// <summary>姿态散布倍率（<see cref="PlayerWeaponsManager"/> 每帧写给当前武器，1 = 立姿）。</summary>
        public float StanceSpreadScale => Scale(Param.SpreadScale);

        /// <summary>姿态后坐倍率（同上）。</summary>
        public float StanceRecoilScale => Scale(Param.RecoilScale);

        /// <summary>趴姿关掉地面 IK（原因见 <see cref="BaseSelfMoveableController.AllowGroundIK"/>）。
        /// 蹲姿脚仍在自己脚下，Grounder 的假设成立，保持开启。</summary>
        protected override bool AllowGroundIK => Stance != PlayerStance.Prone;

        /// <summary>当前姿态的俯仰限位（度）；0 = 该姿态不限（各自视角用默认值：第一人称 89/70、第三人称表里的上仰/下俯角）。
        /// <para>▍两种视角都生效：趴姿的 25 度在第一人称是为了不穿进身体，第三人称同样不该让镜头/枪口抬到 70 度。</para></summary>
        private float StancePitchLimit => Param.PitchLimit;

        /// <summary>仰角上限（度）：由姿态表决定（趴姿 25，脸贴地时用 89 会穿进身体）。</summary>
        protected override float PitchUpLimit => StancePitchLimit > 0f ? StancePitchLimit : base.PitchUpLimit;

        /// <summary>俯角上限（度）：同上，仰俯用同一个值。</summary>
        protected override float PitchDownLimit => StancePitchLimit > 0f ? StancePitchLimit : base.PitchDownLimit;

        /// <summary>第三人称仰角上限：姿态限位与表里的「最大上仰角」**取更严**。
        /// <para>▍为什么不直接顶替：蹲/立姿 PitchLimit 是 0（不限），顶替等于把第三人称的 25/70 放宽到 89。</para></summary>
        private float ThirdPersonPitchUpLimit =>
            StancePitchLimit > 0f ? Mathf.Min(_thirdPersonUpperLimit, StancePitchLimit) : _thirdPersonUpperLimit;

        /// <summary>第三人称俯角上限：姿态限位与表里的「最大下俯角」取更严。</summary>
        private float ThirdPersonPitchDownLimit =>
            StancePitchLimit > 0f ? Mathf.Min(_thirdPersonLowerLimit, StancePitchLimit) : _thirdPersonLowerLimit;

        /// <summary>是否"趴着 + 第一人称"：趴姿的相机收紧、Body 网格隐藏都以这个为准（第三人称不受影响）。</summary>
        private bool IsProneFirstPerson => Stance == PlayerStance.Prone && !IsThirdPerson;

        /// <summary>姿态移速倍率：只在基类 GroundMove/AirMove 各乘一次，不碰共享的 MoveSpeedScale。</summary>
        protected override float ExtraSpeedScale => Scale(Param.SpeedScale);

        /// <summary>当前姿态的胶囊高度（表里没填 ⇒ 沿用站立基线）。</summary>
        private float StanceHeight => Param.CapsuleHeight > 0f ? Param.CapsuleHeight : _standHeight;

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

        /// <summary>模型里的 Body 网格（第一人称趴姿要临时藏掉，其余时候还原）。</summary>
        private Transform _bodyTrans;
        /// <summary>Body 原本所在的层（一般是 Unit）：从 MainCameraIgnore 还原时用。</summary>
        private int _bodyDefaultLayer;

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
            ApplyStanceDetectScale();   // 站姿基线（1），别让复活/换模型后还挂着上一局的趴姿倍率
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
            // ⚠ 趴姿除了降高度还必须往身前平移：趴伏时头在根节点前方 ~0.30m，只降高度相机就埋在脖子/胸口里
            StanceParam p = Resolve(stance);
            float camY = p.CameraHeight > 0f ? p.CameraHeight : _standCameraBasePoint.y;
            float camForward = p.CameraForward;
            CameraBasePoint = new Vector3(_standCameraBasePoint.x, camY, _standCameraBasePoint.z + camForward);

            // 武器挂点同样要跟着降：FirstPersonSocket 挂在玩家根节点上（y=1.15），武器父/瞄准点全是它的子节点，
            // 不跟着挪就是"人趴下了、枪还端在站立胸口高度"。
            if (WeaponsManager && WeaponsManager.FirstPersonSocket)
            {
                // 前移量与相机同量：枪挂在挂点下，相机往前走了枪不跟着走就跑到镜头后面去了
                WeaponsManager.FirstPersonSocket.localPosition =
                    new Vector3(_standSocketLocalPos.x, _standSocketLocalPos.y - p.SocketDrop, _standSocketLocalPos.z + camForward);
            }

            ApplyStanceAnim();
            ApplyStanceBodyLayer();
            ClampPitchToStance();
            ApplyStanceDetectScale();
        }

        /// <summary>把当前姿态的"被 AI 视觉发现距离倍率"写给 <see cref="Actor"/>：
        /// AI 的 <c>DetectionModule</c> 直接读 <see cref="FPSGame.GameContract.IActor.DetectRangeScale"/>，
        /// 不 GetComponent 也不依赖具体控制器类型。非玩家单位恒 1。</summary>
        private void ApplyStanceDetectScale()
        {
            if (Actor) Actor.DetectRangeScale = Scale(Param.DetectRangeScale);
        }

        /// <summary>换模型后（SetBody）重建"参数是否存在"缓存，并把当前姿态重新下发一次。</summary>
        private void RefreshStanceAnimParams()
        {
            // 换模型要重新找 Body 网格并记住它原本的层（第一人称趴姿会临时把它挪走）
            // ⚠ 名字大小写不统一（Nagisa 是 Nagisa_Battle_body），必须走 MeshNameHas，否则 _bodyTrans 为 null、趴姿什么都不改
            _bodyTrans = ModleRoot ? ModleRoot.Find(t => MeshNameHas(t, "Body")) : null;
            if (_bodyTrans) _bodyDefaultLayer = _bodyTrans.gameObject.layer;
            ApplyStanceBodyLayer();

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

        /// <summary>第一人称趴姿把 Body 网格挪到 MainCameraIgnore（相机贴在脸前，身体会糊在画面里挡视线）；其它情况还原。
        /// <para>▍为什么改层就够：Main Camera 的 cullingMask 本来就不含 MainCameraIgnore ⇒ 改层即不可见，不必动相机掩码。</para>
        /// <para>⚠ 只动 Body：手臂（<c>*_Hand</c>）常驻 FirstPersonWeapon，是第一人称该看见的持枪手臂，不能跟着藏。
        /// 头部则由 <see cref="SetBody"/> 一次性放到 MainCameraIgnore（常驻，靠相机掩码区分第一/第三人称）。</para>
        /// <para>⚠ 第三人称必须还原：那时是要看见自己身体的。</para></summary>
        private void ApplyStanceBodyLayer()
        {
            bool hide = IsProneFirstPerson;
            int ignore = LayerMask.NameToLayer("MainCameraIgnore");
            ApplyLayer(_bodyTrans, _bodyDefaultLayer, hide, ignore);
        }

        /// <summary>按"趴姿隐藏 / 其它还原"改一个网格的层（基准层从字段传，别现查）。</summary>
        private static void ApplyLayer(Transform trans, int defaultLayer, bool hide, int ignoreLayer)
        {
            if (!trans) return;
            int want = hide ? ignoreLayer : defaultLayer;
            if (trans.gameObject.layer != want) trans.gameObject.layer = want;
        }

        /// <summary>限位变化后立刻把当前俯仰角夹一次：否则趴下那一帧还会停在旧的超限角度上。
        /// <para>⚠ 必须按当前视角取限位：第三人称那套比姿态值宽（70/89），用宽的去夹等于没夹。</para></summary>
        private void ClampPitchToStance()
        {
            float up = IsThirdPerson ? ThirdPersonPitchUpLimit : PitchUpLimit;
            float down = IsThirdPerson ? ThirdPersonPitchDownLimit : PitchDownLimit;
            m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -up, down);
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
