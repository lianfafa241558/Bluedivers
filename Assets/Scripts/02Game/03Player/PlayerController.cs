using System.Collections.Generic;
using Core;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using Utils;

/// <summary>
/// 主控制器
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
public class PlayerController : MonoBehaviour
{



    public Vector3 CenterPos => m_Actor.CenterPos;
    public string PlayerName=> m_Actor.ShowName;
    public string Id => m_Actor.Id;
    public Sprite Portrait => m_Actor.Portrait;
    public Sprite Halo => m_Actor.ExtraPortrait;
    public Color Color => m_Actor.Color;

    [Foldout("玩家", true)]
    [CustomLabel("玩家摄像机")]
    public Camera PlayerCamera;
    [SerializeField]
    [CustomLabel("玩家倒地摄像机")]
    private Camera PlayerDownCamera;

    public Transform ModleRoot;

    [DisplayField]
    [CustomLabel("所属玩家")]
    public int PlayerIndex;


    [CustomLabel("音频源")]
    public AudioSource AudioSource;

    [Foldout("一般", true)]
    [CustomLabel("重力")]
    public float GravityDownForce = 10f;

    [CustomLabel("触底距离")]
    public float GroundCheckDistance = 0.05f;

    [Foldout("移动", true)]
    [CustomLabel("落地时的最大移动速度（非短跑时）")]
    public float MaxSpeedOnGround = 10f;

    [CustomLabel("(移动平滑度)落地时动作的锐度，低值会使玩家缓慢加速和减速，高值则相反")]
    public float MovementSharpnessOnGround = 15;

    [CustomLabel("蹲下时的最大移动速度")]
    [Range(0, 1)]
    public float MaxSpeedCrouchedRatio = 0.5f;

    [CustomLabel("未接地时的最大移动速度")]
    public float MaxSpeedInAir = 10f;

    [CustomLabel("空中加速速度")]
    /// <summary>空中加速速度</summary>
    public float AccelerationSpeedInAir = 25f;


    [CustomLabel("移动时上下晃动幅度")]
    [Range(0, 0.1f)]
    public float ShakeSpeed = 0f;

    [Foldout("旋转", true)]
    [CustomLabel("镜头旋转速度")]
    public float RotationSpeed = 200f;

    [Range(0.1f, 1f)]
    [CustomLabel("瞄准时的旋转速度倍率")]
    public float AimingRotationMultiplier = 0.4f;

    [Foldout("跳跃", true)]
    [CustomLabel("跳跃强度")]
    public float JumpForce = 9f;
    [CustomLabel("允许跳跃次数")]
    public int AllowJumpCount = 1;

    [Foldout("冲刺", true)]
    [CustomLabel("冲刺持续时间")]
    /// <summary>冲刺持续时间</summary>
    public float SprintDuration = 1f;

    [CustomLabel("冲刺倍数(地面)")]
    /// <summary>冲刺倍数(地面)</summary>
    public float SprintSpeedGroundModifier = 3f;

    [CustomLabel("冲刺倍数(空中)")]
    /// <summary>冲刺倍数(空中)</summary>
    public float SprintSpeedAirModifier = 1.5f;

    [CustomLabel("冲刺冷却")]
    /// <summary>冲刺冷却</summary>
    public float SprintCool = 15f;

    [CustomLabel("冲刺反重力程度")]
    /// <summary>冲刺反重力程度</summary>
    [Range(0, 1f)]
    public float Sprintantigravity = 0.5f;


    [Foldout("站立", true)]
    [Range(0, 1f)]
    [CustomLabel("摄像机所在位置的角色高度比")]
    public float CameraHeightRatio = 0.9f;

    [CustomLabel("站立高度")]
    public float CapsuleHeightStanding = 1.8f;

    [CustomLabel("下蹲高度")]
    public float CapsuleHeightCrouching = 0.9f;

    [CustomLabel("蹲下过渡的速度")]
    public float CrouchingSharpness = 10f;

    [Foldout("音频", true)]
    [CustomLabel("移动一米时播放的脚步声量")]
    public float FootstepSfxFrequency = 1f;

    [CustomLabel("短跑时移动一米时发出的脚步声数量")]
    public float FootstepSfxFrequencyWhileSprinting = 1f;

    [CustomLabel("脚步声")]
    public AudioClip FootstepSfx;

    [CustomLabel("跳跃声")]
    public AudioClip JumpSfx;
    [CustomLabel("落地声")]
    public AudioClip LandSfx;

    [CustomLabel("掉落伤害声")]
    public AudioClip FallDamageSfx;

    [Foldout("掉落伤害", true)]
    [CustomLabel("是否收到掉落伤害")]
    public bool RecievesFallDamage;

    [CustomLabel("触发时的最低速度")]
    public float MinSpeedForFallDamage = 10f;

    [CustomLabel("触发计算的最高速度")]
    public float MaxSpeedForFallDamage = 30f;

    [CustomLabel("以最低速度坠落时受到的伤害")]
    public float FallDamageAtMinSpeed = 10f;

    [CustomLabel("以最高速度坠落时受到的伤害")]
    public float FallDamageAtMaxSpeed = 50f;

    public UnityAction<bool> OnStanceChanged;

    public Vector3 CharacterVelocity{ get; set; }
    public bool IsGrounded { get; private set; }
    public int JumpCount;//{ get; private set; }


    public RoleData_SO Cfg { get; private set; }

    public bool HasJumpedThisFrame { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsCrouching { get; private set; }

    public float MoveSpeedScale { get; set; } = 1;

    public float RotationMultiplier
    {
        get
        {
            if (WeaponsManager.IsAiming)
            {
                return AimingRotationMultiplier;
            }

            return 1f;
        }
    }

    public HealthPlayer Health { get; private set; }
    public PlayerInputHandler InputHandler { get; private set; }
    public CharacterController Controller { get; private set; }
    public PlayerWeaponsManager WeaponsManager { get; private set; }

    //RoleData_SO Cfg;
    Animator m_Anim;
    Actor m_Actor;
    Vector3 m_GroundNormal;
    Vector3 m_CharacterVelocity;
    Vector3 m_LatestImpactSpeed;
    float m_LastTimeJumped = 0f;

    float m_CameraHorizontalAngle = 0;//死亡才用

    float m_CameraVerticalAngle = 0f;
    float m_FootstepDistanceCounter;
    float m_TargetCharacterHeight;
    //float m_headBaseAngle;
    float m_printTime;

    public float VerticalNewRecoil, VerticalRecoil;

    /// <summary>
    /// 在跳跃进过一段时间后才开始检测是否落地
    /// </summary>
    const float k_JumpGroundingPreventionTime = 0.2f;
    const float k_GroundCheckDistanceInAir = 0.07f;

    public void Init(int playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    void Awake()
    {
        Controller = GetComponent<CharacterController>();
        InputHandler = GetComponent<PlayerInputHandler>();
        WeaponsManager = GetComponent<PlayerWeaponsManager>();
        Health = GetComponent<HealthPlayer>();
        m_Actor = GetComponent<Actor>();
        
    }

    void Start()
    {

        //PlayerCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().cameraStack.Add(WndManager.UiCamera);
        Controller.enableOverlapRecovery = true;
        Health.OnDie += OnDie;
        Health.OnRevive += OnRevive;
        WeaponsManager.OnShoot += WeapomRecoil;
        //m_headBaseAngle = headRoot.localEulerAngles.z;
        m_printTime = -SprintCool;
        // 启动时强制将蹲姿设置为假
        SetCrouchingState(false, true);
        UpdateCharacterHeight(true);

    }

    public void SetBody(Transform modleRoot, RoleData_SO cfg, List<WeaponPlayerController> extraWeapons)
    {
        if (ModleRoot) {
            DestroyImmediate(ModleRoot.gameObject); 
        }


        ModleRoot = modleRoot;
        BaseObject baseMono = modleRoot.GetComponent<BaseObject>();
        m_Actor.ShowName = baseMono.ShowName;
        m_Actor.Id = baseMono.Id;
        m_Actor.Portrait = baseMono.Portrait;
        m_Actor.ExtraPortrait = baseMono.ExtraPortrait;
        m_Actor.Color = baseMono.Color;
        m_Anim = modleRoot.GetComponent<Animator>();



        Tool.Destroy(baseMono);
        Tool.Destroy(modleRoot.GetComponent<Collider>());
        
        modleRoot.SetParent(transform,false);
        modleRoot.Find(item=>item.name.Contains("Head")).gameObject.layer=LayerMask.NameToLayer("MainCameraIgnore");
        var hand = modleRoot.Find(item => item.name.Contains("Hand"));
        hand.gameObject.layer = LayerMask.NameToLayer("FirstPersonWeapon");
        //使手臂在屏幕外也更新
        hand.GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen = true;

        this.Cfg = cfg;
        modleRoot.GetComponent<RootMotion.FinalIK.LookAtController>().target = PlayerCamera.transform.GetChild(0);
        List<WeaponPlayerController> StartingWeapons=new(cfg.GetStartingWeapons(GameRoot.Archive.GetRoleCfg(m_Actor.Id)));
        StartingWeapons.AddRange(extraWeapons);
        WeaponsManager.SetStatrtWeapon(StartingWeapons);

    }

    private float LastLand = 0;
    void Update()
    {

        //在这一帧起跳
        HasJumpedThisFrame = false;
        bool wasGrounded = IsGrounded;
        GroundCheck();
        UpdateFall(wasGrounded);
        // 下蹲
        //if (InputHandler.GetCrouchInputDown())SetCrouchingState(!IsCrouching, false);
        UpdateCharacterHeight(false);
        UpodateSprint();
        HandleCharacterMovement();
        HandleKei();
    }



    void HandleKei()
    {
       if(!IsDead&&InputHandler.GetMuleDown())
        {
            GlobalEventManager.CallKai(gameObject, transform.position);
            GlobalEventManager.PlayMeetSoeech(gameObject, SpeechTypeEnum.Kei);
        }
    }

    /// <summary>
    /// 使用补给
    /// </summary>
    public void UseSupply()
    {
        WeaponsManager.UseSupply();
        Health.Heal(Health.MaxHealth/2);
    }

    //武器后坐力
    public void WeapomRecoil(WeaponController weapon) {
        var power = (weapon as WeaponPlayerController).GetRecoil() * 1.5f;
        VerticalNewRecoil += power;
    }

    void OnDie(GameObject source)
    {
        IsDead = true;
        PlayerCamera.gameObject.SetActive(false);
        PlayerDownCamera.gameObject.SetActive(true);

        WeaponsManager.SwitchToWeaponIndex("", true,false,true);

        //GlobalEventManager.PlayerDead(m_Actor);

        if (GameRoot.GameState == GameStateEnum.Game) BattleManager.Instance.AddBattleDataItem(PlayerIndex, "死亡次数");
        WeaponsManager.enabled = false;
    }

    void OnRevive()
    {
        IsDead = false;
        PlayerCamera.gameObject.SetActive(true);
        PlayerDownCamera.gameObject.SetActive(false);

        WeaponsManager.enabled = true;
        WeaponsManager.SwitchToWeaponIndex(1, true,false, true);
        GlobalEventManager.PlayMeetSoeech(gameObject, SpeechTypeEnum.Thank);
        m_Actor.ActorState = ActorState.Normal;

        //喘息之时现在免费送
        m_Actor.AddTag(ActorFlag.Invincible);
        GameRoot.CreateTimer(()=>m_Actor.RemoveTag(ActorFlag.Invincible),4);
    }

    /// <summary>
    /// 检测是否落地
    /// </summary>
    void GroundCheck()
    {
        //确保已经在空中时的地面检查距离非常小，以防止突然撞击地面
        float chosenGroundCheckDistance =
            IsGrounded ? (Controller.skinWidth + GroundCheckDistance) : k_GroundCheckDistanceInAir;

        //接地检查前重置值
        IsGrounded = false;
        m_GroundNormal = Vector3.up;

        //只有在距离上次跳跃时间较短的情况下，才尝试探测地面；否则，我们可能会在尝试跳跃后立即倒地
        if (Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
        {
            // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
            if (Physics.CapsuleCast(GetCapsuleBottomHemisphere(), GetCapsuleTopHemisphere(Controller.height),
                Controller.radius, Vector3.down, out RaycastHit hit, chosenGroundCheckDistance, LayerDefinition.MoveableLayers,
                QueryTriggerInteraction.Ignore))
            {
                //存储所找到表面的向上方向
                m_GroundNormal = hit.normal;

                //只有当地面法线与角色向上的方向相同时，才认为这是一次有效的地面打击
                //如果倾斜角度低于角色控制器的限制
                if (Vector3.Dot(hit.normal, transform.up) > 0f &&
                    IsNormalUnderSlopeLimit(m_GroundNormal))
                {
                    IsGrounded = true;
                    JumpCount = 0;

                    // handle snapping to the ground
                    if (hit.distance > Controller.skinWidth)
                    {
                        Controller.Move(Vector3.down * hit.distance);
                    }
                }
            }
        }
    }

    public float speed;
    /// <summary>
    /// 移动控制
    /// </summary>
    void HandleCharacterMovement()
    {
        if (IsDead)
        {
            DownHandleCharacterMovement();
            return;
        }
        //以输入速度围绕其局部Y轴旋转变换
        transform.Rotate(new Vector3(0f, (InputHandler.GetLookInputsHorizontal() * RotationSpeed * RotationMultiplier), 0f), Space.Self);


        //为相机的垂直角度添加垂直输入
        m_CameraVerticalAngle += InputHandler.GetLookInputsVertical() * RotationSpeed * RotationMultiplier;

        //后坐力恢复
        if (VerticalRecoil > 0) {
            var speed = Mathf.Lerp(Time.deltaTime, VerticalRecoil, Time.deltaTime * 2);
            VerticalRecoil -= speed;
            m_CameraVerticalAngle += speed;
        }

        //后坐力
        if (VerticalNewRecoil>0) {
            var speed = Mathf.Lerp(Time.deltaTime, VerticalNewRecoil, Time.deltaTime * 10);
            VerticalNewRecoil -= speed;
            speed = Mathf.Min(speed, 12 - VerticalRecoil);
            VerticalRecoil += speed;
            m_CameraVerticalAngle -= speed;
        }



        m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 70f);
        // 将垂直角度作为局部旋转应用于沿其右轴的相机变换（使其上下枢转）
        PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
        WeaponsManager.FirstPersonSocket.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
        //headRoot.localEulerAngles = new(0, 0, m_headBaseAngle - m_CameraVerticalAngle*0.5f);


        // 角色移动处理
        //bool isSprinting = m_InputHandler.GetSprintInputHeld();
        bool isSprinting = m_printTime > 0;
        {
            if (isSprinting)
            {
                //解除下蹲
                isSprinting = SetCrouchingState(false, false);
            }

            float speedModifier = isSprinting ? SprintSpeedGroundModifier : 1f;
            speed = speedModifier;
            // 根据角色的变换方向将移动输入转换为世界空间向量
            Vector3 worldspaceMoveInput = transform.TransformVector(InputHandler.GetMoveInput());

            /*
            if (isSprinting)//冲刺时保持开冲的方向
            {
                worldspaceMoveInput = Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up).normalized;//在up平面的投影(Y归零)                }
            }*/
            //在地面
            if (IsGrounded)
            {
                //根据输入、最大速度和电流斜率计算所需速度
                Vector3 targetVelocity = worldspaceMoveInput * MaxSpeedOnGround * speedModifier* Mathf.Max(MoveSpeedScale, 0);


                //通过蹲下速度比降低蹲下速度
                if (IsCrouching)
                    targetVelocity *= MaxSpeedCrouchedRatio;
                targetVelocity = GetDirectionReorientedOnSlope(targetVelocity.normalized, m_GroundNormal) *
                                    targetVelocity.magnitude;

                //基于加速度，在当前速度和目标速度之间平滑插值
                CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetVelocity,
                    MovementSharpnessOnGround * Time.deltaTime);

                if (CharacterVelocity.sqrMagnitude>0.5f) {
                    m_Anim.SetBool("IsMove", true);
                    m_Anim.SetFloat("Speed", targetVelocity.magnitude/5 *Mathf.Sign(transform.InverseTransformDirection(CharacterVelocity).z));
                }
                else
                {
                    m_Anim.SetBool("IsMove", false);
                    m_Anim.SetFloat("Speed",1);
                }
                //脚步声
                float chosenFootstepSfxFrequency = (isSprinting ? FootstepSfxFrequencyWhileSprinting : FootstepSfxFrequency);
                if (m_FootstepDistanceCounter >= 1f / chosenFootstepSfxFrequency)
                {
                    m_FootstepDistanceCounter = 0f;
                    AudioSource.PlayOneShot(FootstepSfx);
                }

                //记录脚步声的行进距离
                m_FootstepDistanceCounter += CharacterVelocity.magnitude * Time.deltaTime;
                //上下晃动
                if (targetVelocity.magnitude > 0)
                {
                    PlayerCamera.transform.localPosition = Vector3.Lerp(PlayerCamera.transform.localPosition,
                    new(0, m_TargetCharacterHeight * (CameraHeightRatio + (ShakeSpeed * Mathf.Sin(Time.time * 10))), 0.2f), CrouchingSharpness * Time.deltaTime);
                }
            }
            //在空中
            else
            {
                
                //增加空气加速度(在原来速度不变的情况下略微加速)
                CharacterVelocity += worldspaceMoveInput * AccelerationSpeedInAir * Time.deltaTime * speedModifier;

                //将空气速度限制在最大值，但仅限于水平方向
                float verticalVelocity = CharacterVelocity.y;
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up);//在up平面的投影(Y归零)
                //不会直接咸死速度，但是会lerp缓慢降速
                horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.ClampMagnitude(horizontalVelocity, MaxSpeedInAir * speedModifier* Mathf.Max(MoveSpeedScale, 0)), AccelerationSpeedInAir * Time.deltaTime * speedModifier);

                CharacterVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);

                //添加重力
                //冲刺时忽略一部分重力
        
                CharacterVelocity += Vector3.down * (isSprinting ? 1 - Sprintantigravity : 1) * GravityDownForce * Time.deltaTime;
            }
            // 跳跃
            if (JumpCount < AllowJumpCount && InputHandler.GetJumpInputDown())
            {
                // 强制将蹲伏状态设置为假
                if (SetCrouchingState(false, false))
                {
                    ++JumpCount;
                    //重设速度
                    CharacterVelocity = new Vector3(CharacterVelocity.x, JumpForce, CharacterVelocity.z);

                    AudioSource.PlayOneShot(JumpSfx);

                    //记得上次我们跳的时候，因为我们需要防止在短时间内突然落地
                    m_LastTimeJumped = Time.time;
                    HasJumpedThisFrame = true;

                    //强制接地为假
                    IsGrounded = false;
                    m_GroundNormal = Vector3.up;
                    m_Anim.SetBool("IsMove",false);
                }
                else
                {
                    Debug.LogWarning("尝试跳跃，但是有障碍物");
                }
            }
        }



        //将最终计算出的速度值作为角色移动应用
        Vector3 capsuleBottomBeforeMove = GetCapsuleBottomHemisphere();
        Vector3 capsuleTopBeforeMove = GetCapsuleTopHemisphere(Controller.height);
        Controller.Move(CharacterVelocity * Time.deltaTime);

        //检测障碍物以相应地调整速度
        m_LatestImpactSpeed = Vector3.zero;
        if (Physics.CapsuleCast(capsuleBottomBeforeMove, capsuleTopBeforeMove, Controller.radius,
            CharacterVelocity.normalized, out RaycastHit hit, CharacterVelocity.magnitude * Time.deltaTime, -1,
            QueryTriggerInteraction.Ignore))
        {
            // We remember the last impact speed because the fall damage logic might need it
            m_LatestImpactSpeed = CharacterVelocity;

            CharacterVelocity = Vector3.ProjectOnPlane(CharacterVelocity, hit.normal);
        }
    }
    /// <summary>
    /// 倒地后的移动控制
    /// </summary>
    void DownHandleCharacterMovement()
    {

        float mouseX = InputHandler.GetLookInputsHorizontal();
        float mouseY = InputHandler.GetLookInputsVertical(); // 你需要获取垂直输入

        m_CameraHorizontalAngle += mouseX * RotationSpeed * RotationMultiplier / 2;
        m_CameraVerticalAngle += mouseY * RotationSpeed * RotationMultiplier / 2;
        m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -20f, 70f); // 限制上下角度

        Quaternion rotation = Quaternion.Euler(
            m_CameraVerticalAngle,       // 上下
            transform.eulerAngles.y - 180 + m_CameraHorizontalAngle, // 左右
            0
        );

        Vector3 offsetDir = rotation * Vector3.back;
        Vector3 cameraFinalPos = CenterPos+Vector3.up + offsetDir * 4f;

        // 6. 应用位置 + 注视
        PlayerDownCamera.transform.position =Vector3.Lerp(PlayerDownCamera.transform.position, cameraFinalPos, RotationSpeed * RotationMultiplier*Time.deltaTime/2);
        PlayerDownCamera.transform.LookAt(CenterPos);

        //呼救
        if (InputHandler.GetJumpInputDown())
        {
            GlobalEventManager.CallKai(gameObject, transform.position);
            GlobalEventManager.PlayMeetSoeech(gameObject, SpeechTypeEnum.Help);
        }
    }


    //如果给定法线表示的倾斜角度低于角色控制器的倾斜角度限制，则返回true
    bool IsNormalUnderSlopeLimit(Vector3 normal)
    {
        return Vector3.Angle(transform.up, normal) <= Controller.slopeLimit;
    }

    //获取角色控制器胶囊底部半球的中心点
    Vector3 GetCapsuleBottomHemisphere()
    {
        return transform.position + (transform.up * Controller.radius);
    }

    //获取角色控制器胶囊上半球的中心点
    Vector3 GetCapsuleTopHemisphere(float atHeight)
    {
        return transform.position + (transform.up * (atHeight - Controller.radius));
    }

    //获取与给定坡度相切的重新定向方向
    public Vector3 GetDirectionReorientedOnSlope(Vector3 direction, Vector3 slopeNormal)
    {
        Vector3 directionRight = Vector3.Cross(direction, transform.up);
        return Vector3.Cross(slopeNormal, directionRight).normalized;
    }


    void UpodateSprint()
    {
        // 冲刺
        if ((m_printTime -= Time.deltaTime) < -SprintCool && InputHandler.GetSprintInputDouble())
        {
            m_printTime = SprintDuration;
            //反重力
            float y = CharacterVelocity.y;

            Vector3 worldspaceMoveInput = transform.TransformVector(InputHandler.GetMoveInput());
            if (worldspaceMoveInput == Vector3.zero) worldspaceMoveInput = transform.forward;

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(worldspaceMoveInput, Vector3.up).normalized;//在up平面的投影(Y归零)
            CharacterVelocity = horizontalVelocity * Mathf.Max(MoveSpeedScale, 0) * (IsGrounded ? MaxSpeedOnGround * SprintSpeedGroundModifier : MaxSpeedInAir * SprintSpeedAirModifier) + Vector3.up * Sprintantigravity * y;
        }
    }

    /// <summary>
    /// 更新角色跳跃
    /// </summary>
    void UpdateFall(bool oldState)
    {

        if (!IsDead && transform.position.y < Constants.KillHeight)
        {
            //Health.Kill();
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position+Vector3.up*100, out var hit, 10, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
        }

        //落地
        if (IsGrounded && !oldState)
        {
            //落地伤害
            float fallSpeed = -Mathf.Min(CharacterVelocity.y, m_LatestImpactSpeed.y);
            float fallSpeedRatio = (fallSpeed - MinSpeedForFallDamage) /
                                    (MaxSpeedForFallDamage - MinSpeedForFallDamage);
            if (RecievesFallDamage && fallSpeedRatio > 0f)
            {
                float dmgFromFall = Mathf.Lerp(FallDamageAtMinSpeed, FallDamageAtMaxSpeed, fallSpeedRatio);
                Health.TakeDamage(new() { new(DamageTypeEnum.Real, new(dmgFromFall)) }, true, null, null, default);

                // 落地伤害声
                AudioSource.PlayOneShot(FallDamageSfx);
            }
            else if (Mathf.Abs(LastLand - Time.time) > 0.5f)
            {
                LastLand = Time.time;
                //落地声
                AudioSource.PlayOneShot(LandSfx);
            }
        }

    }


    /// <summary>
    /// 更新角色(自己的)高度(下蹲时使用)
    /// </summary>
    /// <param name="force">立即更新</param>
    void UpdateCharacterHeight(bool force)
    {
        if (force)
        {
            Controller.height = m_TargetCharacterHeight;
            Controller.center = Vector3.up * Controller.height * 0.5f;

            PlayerCamera.transform.localPosition = new(0, m_TargetCharacterHeight * CameraHeightRatio, 0.2f);

            m_Actor.AimPoint.transform.localPosition = Controller.center+0.5f*Vector3.up;
        }
        //平滑的更新
        else if (Controller.height != m_TargetCharacterHeight)
        {
            //调整胶囊大小并调整相机位置
            Controller.height = Mathf.Lerp(Controller.height, m_TargetCharacterHeight, CrouchingSharpness * Time.deltaTime);
            Controller.center = Vector3.up * Controller.height * 0.5f;
            
            PlayerCamera.transform.localPosition = Vector3.Lerp(PlayerCamera.transform.localPosition,
                new(0, m_TargetCharacterHeight * CameraHeightRatio, 0.2f), CrouchingSharpness * Time.deltaTime);

            m_Actor.AimPoint.transform.localPosition = Controller.center + 0.5f * Vector3.up;
        }
    }

    /// <summary>
    /// 设置下蹲状态 如果存在障碍，则返回false
    /// </summary>
    /// <param name="crouched">是否下蹲</param>
    /// <param name="ignoreObstructions">忽略障碍</param>
    bool SetCrouchingState(bool crouched, bool ignoreObstructions)
    {
        // set appropriate heights
        if (crouched)
        {
            m_TargetCharacterHeight = CapsuleHeightCrouching;
        }
        else
        {
            // Detect obstructions
            if (!ignoreObstructions)
            {
                Collider[] standingOverlaps = Physics.OverlapCapsule(
                    GetCapsuleBottomHemisphere(),
                    GetCapsuleTopHemisphere(CapsuleHeightStanding),
                    Controller.radius,
                    LayerDefinition.MoveableLayers,
                    QueryTriggerInteraction.Ignore);
                foreach (Collider c in standingOverlaps)
                {
                    if (c != Controller)
                    {
                        Debug.LogWarning("障碍物" + c);
                        return false;
                    }
                }
            }

            m_TargetCharacterHeight = CapsuleHeightStanding;
        }

        if (OnStanceChanged != null)
        {
            OnStanceChanged.Invoke(crouched);
        }

        IsCrouching = crouched;
        return true;
    }



}

