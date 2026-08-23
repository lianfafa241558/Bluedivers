using System.Collections.Generic;
using Core;
using Core.Interface;
using FPSGame.Attribute;
using GameContract;
using PEMaths;
using RootMotion.FinalIK;

using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using Utils;

/// <summary>
/// 主控制器
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
public partial class PlayerController : BaseSelfMoveableController
{

    [Foldout("一般", true)]

    [SerializeField]
    [InspectorName("玩家倒地摄像机")]
    private Camera PlayerDownCamera;

    private Vector3 _downCameraVelocity;

    [Range(0, 1f)]
    [InspectorName("摄像机所在位置的角色高度")]
    public float CameraHeightRatio = 0.9f;
    [InspectorName("站立高度")]
    public float CapsuleHeightStanding = 1.8f;

    [Foldout("第三人称", true)]
    [InspectorName("第三人称相机基准点")]
    [Tooltip("控制第三人称相机的偏移位置，相对于角色Transform")]
    [SerializeField] private Transform _thirdPersonCameraPoint;

    [InspectorName("第三人称瞄准相机点")]
    [Tooltip("瞄准时相机的偏移位置")]
    [SerializeField] private Transform _thirdPersonAimCameraPoint;

    [InspectorName("相机跟随平滑度")]
    [Range(1f, 20f)]
    [SerializeField] private float _thirdPersonSmoothSpeed = 8f;

    [InspectorName("遮挡检测层")]
    [SerializeField] private LayerMask _thirdPersonOcclusionLayers = -1;

    [InspectorName("遮挡时最小距离")]
    [Range(0.5f, 3f)]
    [SerializeField] private float _thirdPersonMinDistance = 1f;

    [InspectorName("最大上仰角")]
    [Range(0f, 89f)]
    [SerializeField] private float _thirdPersonUpperLimit = 70f;

    [InspectorName("最大下俯角")]
    [Range(0f, 89f)]
    [SerializeField] private float _thirdPersonLowerLimit = 70f;

    [InspectorName("旋转灵敏度")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _thirdPersonRotationSensitivity = 0.5f;

    [DisplayField]
    [InspectorName("所属玩家")]
    public int PlayerIndex;

    /// <summary>
    /// 是否处于第三人称视角
    /// </summary>
    [DisplayField]
    [InspectorName("第三人称")]
    [SerializeField] private bool _isThirdPerson;

    public bool IsThirdPerson
    {
        get => _isThirdPerson;
        private set => _isThirdPerson = value;
    }


    [Foldout("冲刺", true)]
    [InspectorName("冲刺持续时间")]
    /// <summary>冲刺持续时间</summary>
    public float SprintDuration = 1f;

    [InspectorName("冲刺倍数")]
    /// <summary>冲刺倍数</summary>
    public float SprintSpeedModifier = 2f;

    [InspectorName("冲刺冷却")]
    /// <summary>冲刺冷却</summary>
    public float SprintCool = 15f;

    [InspectorName("冲刺时重力程度")]
    /// <summary>冲刺时重力程度</summary>
    [Range(0, 1f)]
    public float SprintGravity = 0.2f;



    public RoleData_SO Cfg { get; private set; }

    public bool IsDead { get; private set; }

    public Transform ModleRoot { get; private set; }

    public PlayerWeaponsManager WeaponsManager { get; private set; }


    float m_sprintTime;//冲刺的剩余时间


    public void Init(int playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    protected override void Awake()
    {
        base.Awake();
        WeaponsManager = GetComponent<PlayerWeaponsManager>();
        WeaponsManager.OnAim+= OnAim;
    }

    private void OnDestroy()
    {
        if (!WeaponsManager) return;
        WeaponsManager.OnAim -= OnAim;
    }

    protected override void Start()
    {
        Controller.enableOverlapRecovery = true;
        Health.OnDie += OnDie;
        Health.OnRevive += OnRevive;
        WeaponsManager.OnShoot += WeapomRecoil;
        m_sprintTime = -SprintCool;
        if (GameRoot.GameState == GameStateEnum.Bridge) Actor.AddTag(ActorFlag.Invincible);

        // 根据存档中的"默认操作视角"设置初始化视角（0=第一人称，1=第三人称）
        bool defaultThirdPerson = ArchiveSvc.GetSetting("默认操作视角") == 1;
        if (IsThirdPerson != defaultThirdPerson)
        {
            IsThirdPerson = defaultThirdPerson;
            ApplyViewMode();
        }
    }

    /// <summary>
    /// 根据 IsThirdPerson 状态应用视角切换效果
    /// </summary>
    private void ApplyViewMode()
    {
        var lookAt = GetComponentInChildren<RootMotion.FinalIK.LookAtController>();
        var lookAtIK = GetComponentInChildren<RootMotion.FinalIK.LookAtIK>();
        if (IsThirdPerson)
        {
            PlayerCamera.cullingMask |= LayerDefinition.FirstPersonIgnoreLayers;
            WeaponsManager.WeaponCamera.enabled = false;
            if (lookAt) lookAt.enabled = false;
            if (lookAtIK) lookAtIK.enabled = false;
        }
        else
        {
            PlayerCamera.cullingMask &= ~LayerDefinition.FirstPersonIgnoreLayers;
            WeaponsManager.WeaponCamera.enabled = true;
            RestoreCameraParent();
            if (lookAt) lookAt.enabled = true;
            if (lookAtIK) lookAtIK.enabled = true;
        }

        GlobalEventSub.ViewSwitch(IsThirdPerson);

        // 视角切换后刷新武器瞄准状态，确保准星等UI正确
        WeaponsManager.RefreshAimState();
    }
    private void OnEnable()
    {
        if (!PlayerCamera) return;

        if (IsThirdPerson)
        {
            // 第三人称：显示 FirstPersonIgnoreLayers（头部等），激活相机
            PlayerCamera.cullingMask |= LayerDefinition.FirstPersonIgnoreLayers;
            if (!PlayerCamera.gameObject.activeSelf)
                PlayerCamera.gameObject.SetActive(true);
        }
        else
        {
            // 第一人称：隐藏 FirstPersonIgnoreLayers
            PlayerCamera.cullingMask &= ~LayerDefinition.FirstPersonIgnoreLayers;
        }
    }

    /// <summary>
    /// 载具等外部系统接管相机时设为 true，跳过 OnDisable 中的相机隐藏
    /// </summary>
    [HideInInspector]
    public bool SkipCameraDeactivateOnDisable;

    private void OnDisable()
    {
        if (!PlayerCamera) return;

        PlayerCamera.cullingMask |= LayerDefinition.FirstPersonIgnoreLayers;

        // 组件关闭时恢复相机父级并重置视角
        if (IsThirdPerson)
        {
            RestoreCameraParent();
            // 第三人称相机已脱离父级，需要手动隐藏，确保 UICamera 能正确切换
            // 载具接管时跳过隐藏，由 VehicleController 管理相机生命周期
            if (!SkipCameraDeactivateOnDisable)
            {
                PlayerCamera.gameObject.SetActive(false);
            }
        }
    }

    public void OnAim(bool state)
    {
        GetAttribute(UnitAttrType.AngularSpeed).AddModifier(ModifierType.Factor, (state?-1:1)*(1-(PEInt)AimingRotationMultiplier));
    }


    public void SetBody(Transform modleRoot, RoleData_SO cfg, List<WeaponPlayerController> extraWeapons)
    {
        if (ModleRoot) {
            DestroyImmediate(ModleRoot.gameObject);
            _aimIK = null;
            _aimController = null;
        }
    


        ModleRoot = modleRoot;
        BaseObject baseMono = modleRoot.GetComponent<BaseObject>();
        Actor.ShowName = baseMono.ShowName;
        Actor.Id = baseMono.Id;
        Actor.Portrait = baseMono.Portrait;
        Actor.ExtraPortrait = baseMono.ExtraPortrait;
        Actor.Color = baseMono.Color;
        m_Anim = modleRoot.GetComponent<Animator>();



        Tool.Destroy(baseMono);
        Tool.Destroy(modleRoot.GetComponent<Collider>());
        
        modleRoot.SetParent(transform,false);
        modleRoot.localPosition = Vector3.zero;
        modleRoot.localEulerAngles = Vector3.zero;
        modleRoot.Find(item=>item.name.Contains("Head")).gameObject.layer=LayerMask.NameToLayer("MainCameraIgnore");
        var hand = modleRoot.Find(item => item.name.Contains("Hand"));
        hand.gameObject.layer = LayerMask.NameToLayer("FirstPersonWeapon");
        //使手臂在屏幕外也更新
        hand.GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen = true;

        this.Cfg = cfg;
        modleRoot.GetComponent<LookAtController>().target = PlayerCamera.transform.GetChild(0);
        List<WeaponPlayerController> StartingWeapons=new(cfg.GetStartingWeapons(ArchiveSvc.Archive.GetRoleCfg(Actor.Id)));
        StartingWeapons.AddRange(extraWeapons);
        WeaponsManager.SetStatrtWeapon(StartingWeapons);

        grounderIK = modleRoot.GetComponent<GrounderFBBIK>();

        // 第三人称时关闭新模型的头部IK组件
        if (IsThirdPerson)
        {
            var lookAt = modleRoot.GetComponent<LookAtController>();
            if (lookAt) lookAt.enabled = false;
            var lookAtIK = modleRoot.GetComponent<LookAtIK>();
            if (lookAtIK) lookAtIK.enabled = false;

            _aimIK = modleRoot.GetComponent<AimIK>();
            if (_aimIK) _aimIK.enabled = false;
            _aimController = modleRoot.GetComponent<AimController>();
            if (_aimController) _aimController.enabled = false;
        }
    }


    public override Vector3 GetInputMove()
    {
        if (IsDead) return Vector3.zero;
        if (IsThirdPerson) return GetInputMoveThirdPerson();
        return base.GetInputMove();
    }

    protected override void Update()
    {
        //在这一帧起床
        DownHandleCharacterMovement();
        base.Update();
        UpdateSprint();
        HandleKei();
        HandleToggleView();

        
    }
    private bool _wasThirdPersonLastFrame;

    protected override void LateUpdate()
    {
        // 刚切换到第三人称时，瞬间跳转到第三人称相机位置
        if (IsThirdPerson && !_wasThirdPersonLastFrame && !IsDead && PlayerCamera && _thirdPersonCameraPoint)
        {
            float xOffset = _thirdPersonCameraPoint.localPosition.x;
            float height = _thirdPersonCameraPoint.localPosition.y;
            float distance = Mathf.Abs(_thirdPersonCameraPoint.localPosition.z);
            Quaternion rotation = Quaternion.Euler(m_CameraVerticalAngle, _cameraYaw, 0);
            Vector3 offset = rotation * new Vector3(xOffset, height, -distance);
            PlayerCamera.transform.position = CenterPos + offset;
            PlayerCamera.transform.rotation = rotation;
        }

        if (IsThirdPerson && !IsDead)
        {
            HandleThirdPersonCamera();
        }
        else
        {
            // 刚从第三人称切回第一人称时，瞬间跳转到第一人称位置，避免看到头部消失
            if (_wasThirdPersonLastFrame && PlayerCamera)
            {
                PlayerCamera.transform.position = transform.TransformPoint(CameraBasePoint);
                PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
            }
            base.LateUpdate();
        }

        // 动画器移动参数（第一/第三人称都需要）
        var targetVelocity = CharacterVelocity;
        targetVelocity.y = 0;
        if (targetVelocity.Magnitude.RawFloat > 0.5f)
        {
            m_Anim?.SetBool("IsMove", true);
            m_Anim?.SetFloat("Speed", targetVelocity.Magnitude.RawFloat / 5 * Mathf.Sign(transform.InverseTransformDirection(CharacterVelocity.RawVector3).z));
        }
        else
        {
            m_Anim?.SetBool("IsMove", false);
            m_Anim?.SetFloat("Speed", 1);
        }

        // 脚步声（第一/第三人称都需要）
        if (IsGrounded)
        {
            m_FootstepDistanceCounter += targetVelocity.Magnitude.RawFloat * Time.deltaTime;
            float chosenFootstepSfxFrequency = FootstepSfxFrequency * MoveSpeedScale;
            if (m_FootstepDistanceCounter >= 1f / Mathf.Max(chosenFootstepSfxFrequency, 0.1f))
            {
                m_FootstepDistanceCounter = 0f;
                AudioSource.PlayOneShot(FootstepSfx);
            }
        }

        if (IsThirdPerson)
        {
            // 第三人称时用世界旋转同步武器朝向（瞄准时瞄准相机方向，非瞄准时跟随角色）
            if (WeaponsManager.IsAiming)
            {
                WeaponsManager.FirstPersonSocket.transform.rotation = Quaternion.Euler(m_CameraVerticalAngle, transform.eulerAngles.y, 0);
            }
        }
        else
        {
            WeaponsManager.FirstPersonSocket.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
        }
        _wasThirdPersonLastFrame = IsThirdPerson;
    }

    protected override void TryJump()
    {
        if (IsDead) return;
        base.TryJump();
    }

    protected override void HandleRotation()
    {
        if (IsDead) return;
        if (IsThirdPerson)
        {
            HandleRotationThirdPerson();
            return;
        }
        base.HandleRotation();
    }
    void UpdateSprint()
    {
        // 冲刺
        if (m_sprintTime < -SprintCool && InputHandler.GetSprintInputDouble())
        {
            m_sprintTime = SprintDuration;
            MoveSpeedScale *= SprintSpeedModifier;
            GravityDownForce *= SprintGravity;
        }
        else if (m_sprintTime > 0 && m_sprintTime < Time.deltaTime)
        {
            MoveSpeedScale /= SprintSpeedModifier;
            GravityDownForce /= SprintGravity;
        }
        m_sprintTime -= Time.deltaTime;
    }

   
    //武器后坐力
    public void WeapomRecoil(WeaponController weapon) {
        var power = (weapon as WeaponPlayerController).GetRecoil() * 1.5f;
        VerticalNewRecoil += power;
    }

    /// <summary>
    /// 倒地后的相机控制（与第三人称一致的轨道旋转方式）
    /// </summary>
    void DownHandleCharacterMovement()
    {
        if (!IsDead) return;

        float angSpeed = GetAttribute(UnitAttrType.AngularSpeed).FinalValue.RawFloat;
        float dt = Time.deltaTime;

        // 更新旋转角度（与第三人称 HandleThirdPersonCamera 一致：累积 _cameraYaw）
        _cameraYaw += InputHandler.GetLookInputsHorizontal() * angSpeed * dt;
        m_CameraVerticalAngle += InputHandler.GetLookInputsVertical() * angSpeed * dt;
        m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -_thirdPersonUpperLimit, _thirdPersonLowerLimit);

        // 相机位置：围绕 CenterPos 轨道旋转（与第三人称一致的公式）
        Quaternion rotation = Quaternion.Euler(m_CameraVerticalAngle, _cameraYaw, 0);
        Vector3 offset = rotation * new Vector3(0, 1f, -4f);
        Vector3 desiredPosition = CenterPos + offset;

        // 遮挡检测（与第三人称一致）
        Vector3 origin = CenterPos;
        Vector3 dir = (desiredPosition - origin).normalized;
        float maxDist = Vector3.Distance(origin, desiredPosition);
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, _thirdPersonOcclusionLayers, QueryTriggerInteraction.Ignore))
        {
            float occludedDist = Mathf.Max(hit.distance - 0.3f, _thirdPersonMinDistance);
            desiredPosition = origin + dir * occludedDist;
        }

        // 平滑位置（SmoothDamp 替代 Lerp）
        PlayerDownCamera.transform.position = Vector3.SmoothDamp(
            PlayerDownCamera.transform.position, desiredPosition, ref _downCameraVelocity, 1f / _thirdPersonSmoothSpeed);

        // 平滑旋转（Slerp 替代 LookAt）
        PlayerDownCamera.transform.rotation = Quaternion.Slerp(
            PlayerDownCamera.transform.rotation, rotation, dt * _thirdPersonSmoothSpeed);

        // 呼叫
        if (InputHandler.GetJumpInputUp())
        {
            BattleEventSub.CallKai(gameObject, transform.position);
            GlobalEventSub.PlayMeetSpeech(gameObject, SpeechTypeEnum.Help);
        }
    }

    private bool _wasThirdPersonBeforeDeath;

    void OnDie(GameObject source)
    {
        IsDead = true;
        _wasThirdPersonBeforeDeath = IsThirdPerson;
        _downCameraVelocity = Vector3.zero;

        // 第一人称死亡时 _cameraYaw 未更新，初始化为角色后方
        if (!IsThirdPerson)
        {
            _cameraYaw = transform.eulerAngles.y - 180;
        }

        PlayerCamera.gameObject.SetActive(false);
        PlayerDownCamera.gameObject.SetActive(true);
        m_Anim.SetBool("IsDeath", true);
        WeaponsManager.SwitchToWeaponIndex("", true, false, true);

        if (GameRoot.GameState == GameStateEnum.Game) BattleManager.Instance.AddBattleDataItem(PlayerIndex, "死亡次数");
        WeaponsManager.enabled = false;
    }

    void OnRevive()
    {
        IsDead = false;
        _downCameraVelocity = Vector3.zero;
        PlayerCamera.gameObject.SetActive(true);
        PlayerDownCamera.gameObject.SetActive(false);

        WeaponsManager.enabled = true;
        WeaponsManager.SwitchToWeaponIndex(1, true, false, true);
        GlobalEventSub.PlayMeetSpeech(gameObject, SpeechTypeEnum.Thank);
        Actor.ActorState = ActorState.Normal;
        m_Anim.SetBool("IsDeath",false);
        //喘息之时现在免费送
        Actor.AddTag(ActorFlag.Invincible);
        GameRoot.CreateTimer(() => Actor.RemoveTag(ActorFlag.Invincible), 4);

        // 恢复死亡前的视角状态
        if (_wasThirdPersonBeforeDeath)
        {
            IsThirdPerson = true;
            PlayerCamera.cullingMask |= LayerDefinition.FirstPersonIgnoreLayers;
            WeaponsManager.WeaponCamera.enabled = false;
            GlobalEventSub.ViewSwitch(true);
        }
        else
        {
            IsThirdPerson = false;
            PlayerCamera.cullingMask &= ~LayerDefinition.FirstPersonIgnoreLayers;
        }
        _wasThirdPersonBeforeDeath = false;
    }
    void HandleKei()
    {
        if (!IsDead && InputHandler.GetMuleDown())
        {
            BattleEventSub.CallKai(gameObject, transform.position);
            GlobalEventSub.PlayMeetSpeech(gameObject, SpeechTypeEnum.Kei);
        }
    }

    /// <summary>
    /// 使用补给
    /// </summary>
    public void UseSupply()
    {
        UseCaisson();
        UseMedicalBag();
    }
    /// <summary>
    /// 使用弹药箱
    /// </summary>
    public void UseCaisson()
    {
        WeaponsManager.UseSupply();
    }
    /// <summary>
    /// 使用医疗包
    /// </summary>
    public void UseMedicalBag()
    {
        Health.Heal(Health.MaxHealth / 2);
    }


}


