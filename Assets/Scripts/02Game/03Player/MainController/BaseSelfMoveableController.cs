using System.Collections.Generic;
using Core;
using Core.Interface;
using FPSGame.Attribute;
using GameContract;
using PEMaths;
using RootMotion.FinalIK;

using Unity.Burst.CompilerServices;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using Utils;

/// <summary>
/// 基础的玩家操作控制器，可以移动和跳跃
/// </summary>
[RequireComponent(typeof(CharacterController),typeof(AudioSource))]
public class BaseSelfMoveableController : BaseSelfController, IPhysical
{
    #region 参数

    [Foldout("一般", true)]
    [InspectorName("玩家摄像机")]
    public Camera PlayerCamera;

    [InspectorName("重力")]
    public float GravityDownForce = 20f;

    [InspectorName("触底距离")]
    public float GroundCheckDistance = 0.1f;//0.05f;

    [Foldout("移动", true)]
    [InspectorName("落地时的最大移动速度（非短跑时）")]
    public float MaxSpeedOnGround = 5.5f;

    [InspectorName("(移动平滑度)落地时动作的锐度，低值会使玩家缓慢加速和减速，高值则相反")]
    public float MovementSharpnessOnGround = 15;

    [InspectorName("未接地时的最大移动速度")]
    public float MaxSpeedInAir = 6f;

    [InspectorName("空中加速速度")]
    /// <summary>空中加速速度</summary>
    public float AccelerationSpeedInAir = 2f;

    [InspectorName("重量")]
    /// <summary>重量</summary>
    public float Weight = 1;

    [InspectorName("移动时上下晃动幅度")]
    [Range(0, 0.1f)]
    public float ShakeSpeed = 0f;

    [Foldout("跳跃", true)]
    [InspectorName("跳跃强度")]
    public int JumpForce = 9;
    [InspectorName("允许跳跃次数")]
    public int AllowJumpCount = 1;

    [Foldout("音效", true)]
    [InspectorName("移动一米时播放的脚步声频率")]
    public float FootstepSfxFrequency = 1f;

    [InspectorName("脚步声")]
    public AudioClip FootstepSfx;

    [InspectorName("跳跃声")]
    public AudioClip JumpSfx;
    [InspectorName("落地声")]
    public AudioClip LandSfx;

    [InspectorName("掉落伤害声")]
    public AudioClip FallDamageSfx;

    [Foldout("掉落伤害", true)]
    [InspectorName("是否收到掉落伤害")]
    public bool RecievesFallDamage;

    [InspectorName("触发时的最低速度")]
    public int MinSpeedForFallDamage = 20;

    [InspectorName("触发计算的最高速度")]
    public int MaxSpeedForFallDamage = 40;

    [InspectorName("以最低速度坠落时受到的伤害")]
    public int FallDamageAtMinSpeed = 10;

    [InspectorName("以最高速度坠落时受到的伤害")]
    public int FallDamageAtMaxSpeed = 30;

    #endregion
    protected Vector3 CameraBasePoint { get; set; }
    public CharacterController Controller { get; private set; }
    /// <summary>物理层输出的移动速度</summary>
    public PEVector3 CharacterVelocity { get; set; }

    /// <summary>物理层受力的移动速度</summary>
    public PEVector3 ApplyForceVelocity { get; set; }


    /// <summary>记录脚步声的行进距离(纯表现层)</summary>
    protected float m_FootstepDistanceCounter;
    [InspectorName("跳跃键被占用")]
    public bool UseUpJump = false;
    /// <summary>是否在这一帧起跳(喷气包用)</summary>
    public bool HasJumpedThisFrame { get; private set; }

    //[HideInInspector]
    public bool IsGrounded;// { get; private set; }
    [SerializeField]
    private int JumpCount;

    [SerializeField]
    /// <summary>上次起跳时间</summary>
    private float m_LastTimeJumped = 0;
    /// <summary>上次落地时间</summary>
    [SerializeField]
    private float m_LastTimeLand = 0;
    /// <summary>在跳跃进过一段时间后才开始检测是否落地</summary>
    const float k_JumpGroundingPreventionTime = 0.2f;
    const float k_GroundCheckDistanceInAir = 0.07f;
    /// <summary>地面法线</summary>
    [SerializeField]
    Vector3 m_GroundNormal;

    /// <summary>是否处于陡坡（坡度超过 slopeLimit 无法站立但身体接触了地面）</summary>
    private bool _isOnSteepSlope;

    /// <summary>最终重力速度</summary>
    protected virtual float GravitySpeed => GravityDownForce;

    //蹲下惩罚，武器惩罚，冲刺奖励
    /// <summary>移动速度乘数</summary>
    public float MoveSpeedScale { get; set; } = 1;

    protected GrounderFBBIK grounderIK;

    protected override void Awake()
    {
        base.Awake();
        Controller = GetComponent<CharacterController>();
        if(PlayerCamera) CameraBasePoint = PlayerCamera.transform.localPosition;
    }

    protected virtual void Start()
    {
        Controller.enableOverlapRecovery = false;

        grounderIK = GetComponent<GrounderFBBIK>();
    }
    protected override void Update()
    {
        base.Update();
        HasJumpedThisFrame = false;
        GroundCheck();
        HandleCharacterMovement();
    }


    protected virtual void LateUpdate()
    {
        if (GameRoot.GameState != GameStateEnum.Game && GameRoot.GameState != GameStateEnum.Ready && GameRoot.GameState != GameStateEnum.Bridge) return;

        var targetVelocity = CharacterVelocity;
        targetVelocity.y = 0;

        if (PlayerCamera)
        {
            var target = transform.TransformPoint(CameraBasePoint);
            float dis = Vector3.Distance(PlayerCamera.transform.position, target);
            if (dis> ShakeSpeed)
            {
                PlayerCamera.transform.position = Vector3.Lerp(PlayerCamera.transform.position,
                target , Time.deltaTime*5);
            }
        }

        //上下晃动
        if (IsGrounded)
        {
            
            if (targetVelocity.Magnitude > 0 && PlayerCamera)
            {
                PlayerCamera.transform.position = Vector3.Lerp(PlayerCamera.transform.position,
                    transform.TransformPoint(CameraBasePoint + Vector3.up * (ShakeSpeed * Mathf.Sin(Time.time * 10)))
                , ShakeSpeed * Time.deltaTime);
            }


            //记录脚步声的行进距离
            m_FootstepDistanceCounter +=  targetVelocity.Magnitude.RawFloat * Time.deltaTime;

            //脚步声
            float chosenFootstepSfxFrequency = FootstepSfxFrequency * MoveSpeedScale;
            if (m_FootstepDistanceCounter >= 1f / Mathf.Max(chosenFootstepSfxFrequency,0.1f))
            {
                m_FootstepDistanceCounter = 0f;

                AudioSource.PlayOneShot(FootstepSfx);
            }
        }


        if (PlayerCamera)
        {
            PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
        }

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

    }


    #region 跳跃相关


    /// <summary>
    /// 检测是否落地
    /// </summary>
    void GroundCheck()
    {
        //防止掉出地面
        if (transform.position.y < Constants.KillHeight)
        {
            //Health.Kill();
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position + Vector3.up * 100, out var hit, 10, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
        }

        //在这一帧起跳
        HasJumpedThisFrame = false;


        //确保已经在空中时的地面检查距离非常小，以防止突然撞击地面
        float chosenGroundCheckDistance =
            IsGrounded ? (Controller.skinWidth + GroundCheckDistance) : k_GroundCheckDistanceInAir;

        //接地检查前重置
        bool lastState = IsGrounded;
        IsGrounded = false;
        _isOnSteepSlope = false;
        m_GroundNormal = Vector3.up;

        //只有在距离上次跳跃时间较短的情况下，才尝试探测地面；否则，我们可能会在尝试跳跃后立即倒贴地面
        if (Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
        {

            //如果在地面，尝试获得法线
            //输入胶囊体顶部和底部的点
            if (Physics.CapsuleCast(GetCapsuleBottomHemisphere() + Vector3.up * 0.05f, GetCapsuleTopHemisphere(),
                Controller.radius, Vector3.down, out RaycastHit hit, chosenGroundCheckDistance + 0.05f, LayerDefinition.MoveableLayers,
                QueryTriggerInteraction.Ignore))
            {

                //存储所找到表面的向上方向
                m_GroundNormal = hit.normal;


                //只有当地面法线与角色向上的方向相同时，才认为这是一次有效的落地(斜坡不算)
                //如果倾斜角度低于角色控制器的限制
                if (Vector3.Dot(hit.normal, transform.up) > 0f &&
                    IsNormalUnderSlopeLimit(m_GroundNormal))
                {
                    if(!lastState) OnLand();
                    IsGrounded = true;
                    JumpCount = 0;
                    if(grounderIK) grounderIK.weight = 1;
                    //如果没有贴地，吸附到地面
                    if (hit.distance > Controller.skinWidth)
                    {
                        Move(Vector3.down * hit.distance);
                    }

                }
                else
                {
                    if (grounderIK) grounderIK.weight = 0;

                    // 陡坡：角色接触了地面但坡度太陡无法站立，标记以便在移动时沿坡面滑下
                    // 只有法线明显向上时才判定为陡坡，避免台阶/墙面被误判为陡坡导致上下抖动
                    if (Vector3.Dot(hit.normal, transform.up) > 0.3f)
                    {
                        _isOnSteepSlope = true;
                    }
                }
            }
        }
    }


    /// <summary>获取角色控制器胶囊底部半球的中心点</summary>
    protected Vector3 GetCapsuleBottomHemisphere()
    {
        return transform.position + (transform.up * Controller.radius);
    }

    /// <summary>获取角色控制器胶囊上半球的中心点</summary>
    protected Vector3 GetCapsuleTopHemisphere()
    {
        return transform.position + (transform.up * (Controller.height - Controller.radius));
    }

    
    /// <summary>如果法线的倾斜角度低于角色控制器的倾斜角度限制，则返回true</summary>
    bool IsNormalUnderSlopeLimit(Vector3 normal)
    {
        return Vector3.Angle(transform.up, normal) <= Controller.slopeLimit;
    }
    /// <summary>落地</summary>
    void OnLand()
    {
       
        //落地伤害
        //float fallSpeed = -Mathf.Min(CharacterVelocity.y, m_LatestImpactSpeed.y);
        PEInt fallSpeed = -CharacterVelocity.y;//下坠的时候速度本身就是负数
        PEInt fallSpeedRatio = (fallSpeed - MinSpeedForFallDamage) /
                                (MaxSpeedForFallDamage - MinSpeedForFallDamage);
        if (RecievesFallDamage && fallSpeedRatio > 0)
        {
            PEInt dmgFromFall = PEMath.Lerp(FallDamageAtMinSpeed, FallDamageAtMaxSpeed, fallSpeedRatio);
            Health.TakeDamage(new() { new(DamageTypeEnum.Real, new(dmgFromFall.RawInt)) }, true, null, null, default,false);

            // 落地伤害
            if (Time.time >= m_LastTimeLand + k_JumpGroundingPreventionTime) AudioSource.PlayOneShot(FallDamageSfx);
        }

        if (Time.time >= m_LastTimeLand + k_JumpGroundingPreventionTime) AudioSource.PlayOneShot(LandSfx);

        m_LastTimeLand = Time.time;
    }

    public void Move(Vector3 pos,bool isTeleport=false)
    {
        Controller.TryMove(pos, isTeleport);
    }


    protected bool JumpInput()
    {
        return UseUpJump ? InputHandler.GetJumpInputUp() : InputHandler.GetJumpInputDown();
    }

    //玩家控制器的时候记得继承然后改成如果没死亡才能执行
    /// <summary>
    /// 尝试跳跃
    /// </summary>
    protected virtual void TryJump()
    {
        if (JumpCount < AllowJumpCount && JumpInput())
        {
            //Debug.LogError(gameObject+"尝试跳跃",gameObject);
            if (!HaveObstacle())
            {
                Jump();
            }
            else
            {
                Debug.LogWarning("尝试跳跃，但是有障碍物");
            }
        }
    }

    protected virtual void Jump()
    {
        ++JumpCount;
        //重设速度
        CharacterVelocity = new PEVector3(CharacterVelocity.x, JumpForce, CharacterVelocity.z);

        AudioSource.PlayOneShot(JumpSfx);

        //记得上次我们跳的时候，因为我们需要防止在短时间内突然落地
        m_LastTimeJumped = Time.time;
        HasJumpedThisFrame = true;

        //强制接地为假
        IsGrounded = false;
        //Debug.LogError("跳跃设置不在地面"+gameObject,gameObject);
        m_GroundNormal = Vector3.up;
        m_Anim?.SetBool("IsMove", false);
    }


    #endregion


    #region

    public Vector3 showInput;
    /// <summary>根据角色的变换方向将移动输入转换为世界空间方向</summary>
    public virtual Vector3 GetInputMove()
    {
        showInput= transform.TransformVector(InputHandler.GetMoveInput());
        return transform.TransformVector(InputHandler.GetMoveInput());
    }

    //不知道为什么在一些尺寸比例下会出现滑动的情形，但是现在玩家的设置刚刚好不??
    /// <summary>
    /// 移动控制
    /// </summary>
    void HandleCharacterMovement()
    {

        if (IsGrounded) GroundMove(); //在地上
        else AirMove(); //在空中

        TryJump();

        // 应用外部冲击力（撞击、爆炸等），并随时间衰减
        PEVector3 totalVelocity = CharacterVelocity;
        if (ApplyForceVelocity.Magnitude > (PEInt)0.01f)
        {
            totalVelocity += ApplyForceVelocity;
            // 衰减（接地时衰减更快)
            float decay = IsGrounded ? 3f : 1f;
            //暂时没办法，PEVector3里面没有lerp
            ApplyForceVelocity = (PEVector3)Vector3.Lerp(ApplyForceVelocity.RawVector3, Vector3.zero, decay * Time.deltaTime);
        }
        else
        {
            ApplyForceVelocity = default;
        }
        totalVelocity *= (PEInt)Time.deltaTime;

        // 陡坡滑动：将位移投影到坡面上，避免被 CharacterController 当墙挡住
        if (_isOnSteepSlope)
        {
            Vector3 projectedDisplacement = Vector3.ProjectOnPlane(totalVelocity.RawVector3, m_GroundNormal);
            totalVelocity = (PEVector3)projectedDisplacement;
        }

        //将最终计算出的速度值作为角色移动应用
        Move(totalVelocity.RawVector3);

        //检测障碍物以相应地调整速度
        if (CharacterVelocity.Magnitude>0&&Physics.CapsuleCast(GetCapsuleBottomHemisphere(), GetCapsuleTopHemisphere(), Controller.radius,
            CharacterVelocity.Normalized.RawVector3, out RaycastHit hit, (totalVelocity.Magnitude.RawFloat * Time.deltaTime), -1,
            QueryTriggerInteraction.Ignore))
        {
            CharacterVelocity = (PEVector3)Vector3.ProjectOnPlane(totalVelocity.RawVector3, hit.normal);
        }
        if (transform.position.y < -50)
        {
            if(GameRoot.GameState== GameStateEnum.Game) transform.position = transform.position + Vector3.up * (TerrainUtils.WSToHeight(transform.position)+2- transform.position.y);
            else transform.position = transform.position + Vector3.up * ( 2 - transform.position.y);
        }
    }
     
    void GroundMove()
    {
        Vector3 worldspaceMoveInput = GetInputMove();

        //根据输入、最大速度和电流斜率计算所需速度
        Vector3 targetVelocity = worldspaceMoveInput * MaxSpeedOnGround * Mathf.Max(MoveSpeedScale, 0);

        targetVelocity = GetDirectionReorientedOnSlope(targetVelocity.normalized, m_GroundNormal) * targetVelocity.magnitude;

        //基于加速度，在当前速度和目标速度之间平滑插值
        //暂时没办法，PEVector3里面没有lerp

        CharacterVelocity = (PEVector3)Vector3.Lerp(CharacterVelocity.RawVector3, targetVelocity, MovementSharpnessOnGround * Time.deltaTime);

    }

    void AirMove()
    {
        PEVector3 worldspaceMoveInput = (PEVector3)GetInputMove();
        //加速度
        var accelerate = AccelerationSpeedInAir * Time.deltaTime * Mathf.Max(MoveSpeedScale, 0);
        //增加空气加速度在原来速度不变的情况下略微加速
        CharacterVelocity += worldspaceMoveInput * (PEInt)accelerate;

        //将空气速度限制在最大值，但仅限于水平方向
        //记录垂直速度
        var verticalVelocity = CharacterVelocity.y;
        //在up平面的投影，Y归零
        PEVector3 horizontalVelocity = (PEVector3)Vector3.ProjectOnPlane(CharacterVelocity.RawVector3, Vector3.up);
        //最大速度
        var maxSpeed = MaxSpeedInAir * Mathf.Max(MoveSpeedScale, 0);
        //不会直接限制速度，但是会lerp缓慢降低
        horizontalVelocity = (PEVector3)Vector3.Lerp(horizontalVelocity.RawVector3, Vector3.ClampMagnitude(horizontalVelocity.RawVector3, maxSpeed), accelerate);
        //重新组合速度
        CharacterVelocity = horizontalVelocity + (PEVector3.Up * verticalVelocity);

        //添加重力
        ApplyGravity();

    }

    /// <summary>
    /// 如果存在障碍返回true
    /// </summary>
    bool HaveObstacle()
    {
        Collider[] standingOverlaps = Physics.OverlapCapsule(
            GetCapsuleBottomHemisphere(),
            GetCapsuleTopHemisphere(),
            Controller.radius,
            LayerDefinition.MoveableLayers,
            QueryTriggerInteraction.Ignore);
        foreach (Collider c in standingOverlaps)
        {
            if (c != Controller)
            {
                Debug.LogWarning("障碍物" + c);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 根据速度方向和地面法线重新定向方??
    /// </summary>
    /// <param name="direction">速度方向</param>
    /// <param name="slopeNormal">地面法线</param>
    protected Vector3 GetDirectionReorientedOnSlope(Vector3 direction, Vector3 slopeNormal)
    {
        //叉积得到垂直两个向量的方向，得到一个移动方向的右方向
        Vector3 directionRight = Vector3.Cross(direction, transform.up);
        //再用这个右方向重新和地面法线叉乘就得到移动方向在地面方向的投影
        return Vector3.Cross(slopeNormal, directionRight).normalized;
    }

    public void ApplyForce(PEVector3 vector)
    {
        if(vector.Magnitude >(PEInt)Weight)
        {
            ApplyForceVelocity += (vector /(PEInt)Mathf.Max(Weight,0.1f));
            ApplyForceVelocity = ApplyForceVelocity.Normalized * PEMath.Min(ApplyForceVelocity.Magnitude,(PEInt)MaxSpeedInAir*3);
        }
    }
    public void ApplyGravity()
    {
        CharacterVelocity += PEVector3.Down * (PEInt)(GravitySpeed * Time.deltaTime);
    }


    #endregion
}
