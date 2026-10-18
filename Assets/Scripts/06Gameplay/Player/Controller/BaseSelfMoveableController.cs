using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;
using RootMotion.FinalIK;

using Unity.Burst.CompilerServices;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Utils;

namespace FPSGame.Gameplay
{

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
    /// <summary>重量(受力换算用的质量：Δv = 冲量 / 重量)</summary>
    public float Weight = 1;

    [InspectorName("受力阻尼-接地(1/秒)")]
    /// <summary>接地时受力速度的指数衰减系数，越大停得越快</summary>
    public float GroundDrag = 2f;

    [InspectorName("受力阻尼-空中(1/秒)")]
    /// <summary>空中时受力速度的指数衰减系数</summary>
    public float AirDrag = 1f;

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

    /// <summary>本帧累积的持续力(牛顿)，移动循环里积分成速度后清空</summary>
    private PEVector3 m_PendingForce;


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

    /// <summary>本次腾空期间的最大下坠速度(正数)</summary>
    /// <para>落地伤害必须用它，而不是 <see cref="OnLand"/> 当帧读到的 <see cref="CharacterVelocity"/>：
    /// 落地后竖直速度要经 GroundMove 的 Lerp 好几帧才归零，只要接地判定抖一帧（台阶棱角、坡沿，
    /// 或 GroundCheck 末尾那条 Controller.isGrounded 兜底与胶囊探测口径不一致），下一帧就会拿同一个
    /// 残留速度**再结算一次**落地伤害 ⇒ 反复掉血直到摔死。</para>
    PEInt m_FallPeakSpeed;

    /// <summary>上次结算坠落伤害的时间</summary>
    float m_LastFallDamageTime = -999f;

    /// <summary>在跳跃进过一段时间后才开始检测是否落地</summary>
    const float k_JumpGroundingPreventionTime = 0.2f;

    /// <summary>坠落伤害冷却：接地判定抖动时 `!lastState` 边沿会反复为真，冷却期内不重复扣血</summary>
    const float k_FallDamageCooldown = 0.5f;
    // ⚠ 原 `k_GroundCheckDistanceInAir = 0.07f`（空中把探测距离压小）已于 2026-10-14 删除：
    //   它是"站在地上却被判空中 ⇒ 走不动"的成因之一（见 GroundCheck 里的说明）。
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

    /// <summary>最近一次判为可靠接地的时间（供地面IK权重防抖用）</summary>
    private float _lastGroundedTime = -999f;

    /// <summary>地面IK权重防抖宽限期：楼梯/台阶攀爬时吸附验证会被台阶棱角瞬间挡住，
    /// 接地判定间歇性失败，若直接硬切 IK 权重会导致 Grounder 骨盆/脊柱偏移整帧突变
    /// （表现为 Bip001 Spine 上下抖动、weight 在 0 和 1 之间跳变），故短暂失接保持 IK 开启</summary>
    private const float GrounderGracePeriod = 0.15f;

    /// <summary>地面IK权重过渡速度（每秒变化量）</summary>
    private const float GrounderWeightSpeed = 2f;

    /// <summary>重叠查询复用缓冲</summary>
    /// <para>⚠ 不能太小：NonAlloc 系列**按缓冲区长度截断**，而大单位（机甲）自己的骨/武器碰撞体就有十几个，
    /// 会先把缓冲区占满 ⇒ 后面的真实命中被丢掉（见 <see cref="s_GroundHitBuffer"/> 的坑）。</para>
    static readonly Collider[] s_OverlapBuffer = new Collider[64];

    /// <summary>地面检测命中复用缓冲</summary>
    /// <para>⚠⚠ **不能是 16**（2026-10-14 修"平地走两步就走不动"）：`CapsuleCastNonAlloc` 会**按缓冲区长度截断**，
    /// 而机甲的探测胶囊（半径 1.3）整个套在自己身上 ⇒ 18 个自身碰撞体会以"初始重叠 dist=0"的形式**排在最前**、
    /// 把 16 格全占满 ⇒ **真正的地面命中被截断丢弃** ⇒ `hasGroundHit=false` ⇒ `IsGrounded=false` ⇒ 走 `AirMove`
    /// ⇒ 机甲（`AccelerationSpeedInAir=0`）零水平控制 = 站着不动。实测复算：19 条命中里 [0..16] 全是自身、
    /// 地面在 [17]。PhysX 不保证返回顺序 ⇒ 表现就是**时好时坏**（走两步卡住）。</para>
    static readonly RaycastHit[] s_GroundHitBuffer = new RaycastHit[64];

    /// <summary>被物体压住/盖住时是否允许挣脱（动态生成物压在玩家身上的兜底）。
    /// 总开关：同时控制悬空自动脱困与跳跃键触发的挣脱；
    /// 出生在载具/运输机等封闭舱室内时应关闭，避免出生后被自动传送到舱体外/机顶</summary>
    [InspectorName("被压住自动挣脱")]
    [SerializeField]
    protected bool _autoEscapeFromOverlap = true;

    /// <summary>实质性重叠累计时间（达到阈值自动触发脱困）</summary>
    private float _overlapStuckTime;

    /// <summary>上次脱困时间（脱困冷却，防止连续瞬移抖动）</summary>
    private float _lastEscapeTime = -10f;

    /// <summary>自身碰撞体忽略关系（见 <see cref="IgnoreSelfCollisions"/>）的复用缓冲，避免每次调用都分配</summary>
    readonly List<Collider> m_SelfIgnoreBuffer = new List<Collider>();

    /// <summary>下次重刷"自身忽略关系"的时间（见 <see cref="IgnoreSelfCollisions"/>）</summary>
    float m_NextSelfIgnoreTime;

    /// <summary>
    /// 让 <see cref="Controller"/> **忽略自身层级里的所有碰撞体**（骨 / 武器 / 背包 / 模型件……）。
    ///
    /// <para>▍为什么必须（2026-10-14 定位到"机甲半悬空 + 平地走不动"）：Unity 的 CC 与"挂在同一层级下的子碰撞体"
    /// **并不默认互斥**，而机甲（`Exosuit`）有 **20 个**自身碰撞体泡在 CC 胶囊里（实测 `ComputePenetration`：
    /// `bone_Root` 深 1.59m、`bone_Leg_L1` 0.32m、`Exo_Fire` 0.27m…）⇒ 两个后果：</para>
    /// <list type="number">
    ///   <item>CC 被自己的腿**架住**、永远沉不到真正的地面：实测机甲 CC 底面离地 0.206m、脚下空无一物，
    ///         但向下 `Move` 连 1mm 都走不动，`flags=Below` / `isGrounded=true` 全是"踩在自己身上"的假象
    ///         = 玩家看到的**半悬空**；</item>
    ///   <item>`GroundCheck` 的吸附验证需要向下 `Move` 一个 0.2m ⇒ 被自己的腿挡住 ⇒ 实际位移 0 ⇒ 判**不接地**
    ///         ⇒ 走 `AirMove` ⇒ 机甲（`AccelerationSpeedInAir=0`）**零水平控制**，只剩惯性滑行 = **走不动**。</item>
    /// </list>
    /// <para>⚠ 实测证据：现场加上这条忽略后，同一帧内 `IsGrounded` 立刻变 true、`CharacterVelocity.y` 从 -42.9 归零。</para>
    ///
    /// <para>▍为什么要**定期**重刷：`Physics.IgnoreCollision` 的关系在**碰撞体被禁用/重新启用后会失效**
    /// （Unity 的既有行为），而运行时挂模型（玩家 `StudentModle`）、换武器都会 enable/disable 碰撞体
    /// ⇒ 每秒重刷一次足够（调用点见 <see cref="GroundCheck"/> 开头），只跳过触发器（触发器本来就不阻挡 CC）。</para>
    /// </summary>
    void IgnoreSelfCollisions()
    {
        if (!Controller) return;
        m_SelfIgnoreBuffer.Clear();
        GetComponentsInChildren(true, m_SelfIgnoreBuffer);
        for (int i = 0; i < m_SelfIgnoreBuffer.Count; i++)
        {
            Collider c = m_SelfIgnoreBuffer[i];
            if (c == null || c == Controller || c.isTrigger) continue;
            Physics.IgnoreCollision(Controller, c, true);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        Controller = GetComponent<CharacterController>();
        if(PlayerCamera) CameraBasePoint = PlayerCamera.transform.localPosition;
        IgnoreSelfCollisions();
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
        if (!FpsHelper.IsMainStage()) return;

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
            float chosenFootstepSfxFrequency = FootstepSfxFrequency / MoveSpeedScale;
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
        // 重刷"CC 忽略自身碰撞体"（`IgnoreCollision` 会被 enable/disable 清掉，见 IgnoreSelfCollisions）。
        // ⚠ 放在这里而不是 `Awake` 一次性搞定：玩家模型/武器是运行时挂上来的，且换武器会 enable/disable 碰撞体。
        if (Time.time >= m_NextSelfIgnoreTime)
        {
            m_NextSelfIgnoreTime = Time.time + 1f;
            IgnoreSelfCollisions();
        }

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


        // ⚠ 空中不再把探测距离压到 0.07（2026-10-14）：CC 天然离地一个 skinWidth(0.1)，且探测只是**一条竖直线**、
        //   而 CC 可能"踩在半径内某处凸起上" ⇒ 探测点下方低几厘米就整段漏掉。现在与接地态同口径取
        //   `skinWidth + GroundCheckDistance`；多出来的那段由下面的 needVerify 吸附验证兜住（吸附被挡住 ⇒ 不算接地）。
        //   （这一条是**加固**；当时"平地走不动"的**主因**是 `s_GroundHitBuffer` 只有 16 格被自身命中占满、地面被截断。）
        float chosenGroundCheckDistance = Controller.skinWidth + GroundCheckDistance;

        //接地检查前重置
        bool lastState = IsGrounded;
        IsGrounded = false;
        _isOnSteepSlope = false;
        m_GroundNormal = Vector3.up;

        // 探测结果提到 if 外面：下面的"射线兜底"要判断"是不是一个候选都没找到"
        RaycastHit groundHit = default;
        bool hasGroundHit = false;

        //只有在距离上次跳跃时间较短的情况下，才尝试探测地面；否则，我们可能会在尝试跳跃后立即倒贴地面
        if (Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
        {

            //如果在地面，尝试获得法线
            //输入胶囊体顶部和底部的点
            int groundHitCount = Physics.CapsuleCastNonAlloc(
                GetCapsuleBottomHemisphere() + Vector3.up * 0.05f, GetCapsuleTopHemisphere(),
                Controller.radius, Vector3.down, s_GroundHitBuffer, chosenGroundCheckDistance + 0.05f,
                LayerDefinition.MoveableLayers, QueryTriggerInteraction.Ignore);

            //从所有命中里挑选"最像地面"的一个：法线越朝上越优先，其次距离越近。
            //不能直接用第一个命中：贴身竖直坎/台阶的侧面或棱角会抢先命中（法线接近水平），
            //导致明明已被 CC 抬上坎沿却被判为悬空，随即滑落又被抬升，
            //产生剧烈上下抖动且始终上不去（中间高度台阶卡死的根源）
            //注意：必须排除自身碰撞体/自身子物体——NonAlloc 版与旧单结果 Cast 不同，
            //会报告初始重叠的碰撞体(探测胶囊整体在自己 CC 内部, dist=0 的假命中)，
            //把自己当成地面会导致空中被判定接地：不掉落、可无限跳
            float bestGroundScore = float.MinValue;
            for (int i = 0; i < groundHitCount; i++)
            {
                RaycastHit candidate = s_GroundHitBuffer[i];
                if (candidate.collider == Controller || candidate.collider.transform.IsChildOf(transform)) continue;
                // NonAlloc 对初始重叠的碰撞体会报 dist=0、hitPoint=(0,0,0) 的假命中（法线不可靠），
                // 当作地面会导致悬空时被误判接地：重力被跳过、跟随下坠被打断
                if (candidate.distance <= 0f || candidate.point == Vector3.zero) continue;
                float score = Vector3.Dot(candidate.normal, transform.up) * 10f - candidate.distance;
                if (score > bestGroundScore)
                {
                    bestGroundScore = score;
                    groundHit = candidate;
                    hasGroundHit = true;
                }
            }

            if (hasGroundHit)
            {

                //存储所找到表面的向上方向
                m_GroundNormal = groundHit.normal;


                //只有当地面法线与角色向上的方向相同时，才认为这是一次有效的落地(斜坡不算)
                //如果倾斜角度低于角色控制器的限制
                if (Vector3.Dot(groundHit.normal, transform.up) > 0f &&
                    IsNormalUnderSlopeLimit(m_GroundNormal))
                {
                    //探测起点比脚底高 0.05。命中距离 ≤ 0.05+蒙皮+余量 说明表面就在脚底高度
                    //(CC 因蒙皮会在 0~蒙皮 间隙内贴合，命中距离也随之抖动)，吸附被挡属于正常贴合，
                    //直接判接地，否则会间歇性误判悬空导致走路滑步；
                    //只有表面明显低于脚底时才验证吸附：吸附位移被挡住(悬在坎沿上方、被棱角顶住，
                    //脚下并无真实支撑)时不能判定接地，否则会以"接地"状态悬浮：重力被跳过不掉落、还能无限起跳
                    //
                    // ★ 吸附分两档（2026-10-14，修"载具/玩家快到地时有快速吸附"）：
                    //   **空中不吸附**。探测距离已放宽到 `skinWidth + GroundCheckDistance`（机甲 0.2m），
                    //   而"提前看到地面"若照旧一次性 `Move(down * distance)`，角色/载具就会在离地十几厘米处
                    //   被瞬移吸到地面 —— 低速下落时肉眼可见（用户 2026-10-14 反馈"快到地有个快速的吸附，违和"）。
                    //   现在空中只"探测"不移动，落地交给重力 + CC 自身碰撞（CC 本来就会停在蒙皮间隙、不会穿地）；
                    //   接地态仍按原样吸附（走路贴地跟随：台阶/起伏不失控、不误判悬空），所以走路手感不变。
                    bool snapGrounded = true;
                    if (groundHit.distance > Controller.skinWidth)
                    {
                        float snapDistance = lastState ? groundHit.distance : 0f;   // 空中 = 不吸
                        Vector3 preSnapPos = transform.position;
                        if (snapDistance > 0f) Move(Vector3.down * snapDistance);
                        float movedDown = preSnapPos.y - transform.position.y;

                        // "表面就在脚底"的判据：
                        //   接地态 = 原口径（0.05 起点偏移 + 蒙皮 + 余量；CC 贴合时这个距离本身就会抖）；
                        //   空中   = 收紧到"真的贴住"（间隙 ≤ 蒙皮 + 0.02）。因为空中已经不吸附了，若沿用宽口径
                        //           会把"离地十几厘米"判成落地，而 `GroundMove` **不加竖直重力** ⇒ 角色会悬着飘。
                        float gapToGround = Mathf.Max(0f, groundHit.distance - 0.05f);
                        bool touching = lastState
                            ? (groundHit.distance - movedDown) <= 0.05f + Controller.skinWidth + 0.02f
                            : gapToGround <= Controller.skinWidth + 0.02f;

                        bool needVerify = snapDistance > 0.05f + Controller.skinWidth + 0.02f;
                        bool gotSupport = !needVerify || movedDown >= snapDistance - 0.02f;
                        snapGrounded = touching && gotSupport;
                    }

                    if (snapGrounded)
                    {
                        if(!lastState) OnLand();
                        IsGrounded = true;
                        JumpCount = 0;
                        _lastGroundedTime = Time.time;
                    }
                }
                else
                {
                    // 陡坡：角色接触了地面但坡度太陡无法站立，标记以便在移动时沿坡面滑下
                    // 阈值只需法线略有向上分量(>0.05，覆盖到约87°的近垂直坡面)：
                    // 接近垂直的陡坡若不标记，重力位移会被 CharacterController 当墙面挡住，
                    // 玩家既不能移动也不能跳跃、也不会滑落，被直接粘在坡上。
                    // 完全垂直的墙面/台阶立面(normal.y≈0)仍被排除，避免贴墙时误判抖动；
                    // 上升阶段(velocity.y>0)不标记：避免贴着陡坡跳跃时起跳速度被投影到坡面上吃掉
                    if (Vector3.Dot(groundHit.normal, transform.up) > 0.05f && CharacterVelocity.y <= 0)
                    {
                        _isOnSteepSlope = true;
                    }
                }
            }
        }

        // 兜底（2026-10-14）：上面那套胶囊探测**没能得出接地结论**、而 CC 自己说"贴在东西上"时，就直接采纳 CC 的结论。
        // 为什么不能只信那套探测：它是"胶囊往下扫一段 + 自己筛候选 + 向下吸附验证"，任何一环出问题都会把
        // "其实站在地上"判成空中 ⇒ 走 `AirMove` ⇒ 机甲（`AccelerationSpeedInAir=0`）零水平控制、平地也走不动。
        // 已知会把好判成坏的两条路（都实测过）：
        //   ① 缓冲区被自身命中截断 ⇒ `hasGroundHit=false`（已把缓冲区加大，见 s_GroundHitBuffer）；
        //   ② 候选合格但**吸附被挡住**（CC 自己的骨碰撞体卡住它，见 IgnoreSelfCollisions）⇒ 位移 0 ⇒ snapGrounded=false。
        // ⇒ 所以这里不再要求"完全没候选"，只要**最终没判接地、也不是陡坡**，就用 `Controller.isGrounded` 兜住。
        // ⚠ 陡坡（`_isOnSteepSlope=true`）仍然排除，滑落行为不变。
        // ⚠ 别再改成"从 `GetCapsuleBottomHemisphere()` 往下打短射线"：那是**底部半球中心**，离地还有一整个
        //   `Controller.radius`（机甲 1.3m），短距离射线根本够不到地面（实测 `no hit`，等于没写）。
        if (!IsGrounded && !_isOnSteepSlope && Controller != null && Controller.isGrounded &&
            Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
        {
            m_GroundNormal = transform.up;
            if (!lastState) OnLand();
            IsGrounded = true;
            JumpCount = 0;
            _lastGroundedTime = Time.time;
        }

        //地面IK权重防抖：不随单帧接地判定硬切，而是在宽限期内保持开启，再平滑过渡到目标值
        if (grounderIK)
        {
            bool ikGrounded = Time.time - _lastGroundedTime <= GrounderGracePeriod;
            grounderIK.weight = Mathf.MoveTowards(grounderIK.weight, ikGrounded ? 1f : 0f, Time.deltaTime * GrounderWeightSpeed);
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
        // 取本次腾空的最大下坠速度并清空：一次下坠只结算一次伤害
        PEInt fallSpeed = m_FallPeakSpeed;
        m_FallPeakSpeed = (PEInt)0;

        //落地伤害
        if (RecievesFallDamage && Time.time >= m_LastFallDamageTime + k_FallDamageCooldown &&
            fallSpeed > (PEInt)MinSpeedForFallDamage)
        {
            PEInt fallSpeedRatio = (fallSpeed - MinSpeedForFallDamage) /
                                    (MaxSpeedForFallDamage - MinSpeedForFallDamage);
            PEInt dmgFromFall = PEMath.Lerp(FallDamageAtMinSpeed, FallDamageAtMaxSpeed, fallSpeedRatio);
            Health.TakeDamage(new() { new(DamageTypeEnum.Real, new(dmgFromFall.RawInt)) }, true, null, null, default,false);
            m_LastFallDamageTime = Time.time;

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
            // 被物体压住/盖住（胶囊被实质穿透、普通跳跃会被 CC 挡死）时，跳跃键改为"向上挣脱"
            if (_autoEscapeFromOverlap && IsPenetrating(transform.position))
            {
                TryEscapeFromOverlap(preferUp: true);
                return;
            }

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
        // 动态生成物压住/盖住玩家（胶囊被实质穿透、任何方向都动不了）时的自动挣脱
        HandleAutoEscapeFromOverlap();

        if (IsGrounded) GroundMove(); //在地上
        else AirMove(); //在空中

        TryJump();

        // 持续力(牛顿) → 速度：Δv = (力 / 重量) · dt
        if (m_PendingForce.Magnitude > (PEInt)0.01f)
        {
            ApplyForceVelocity += m_PendingForce / (PEInt)Mathf.Max(Weight, 0.1f) * (PEInt)Time.deltaTime;
            m_PendingForce = default;
        }

        // 外力速度(冲量：Δv = 冲量/重量)参与本帧位移，并按阻尼指数衰减(接地衰减更快)
        PEVector3 totalVelocity = CharacterVelocity;
        if (ApplyForceVelocity.Magnitude > (PEInt)0.01f)
        {
            totalVelocity += ApplyForceVelocity;
            float drag = IsGrounded ? GroundDrag : AirDrag;
            ApplyForceVelocity = (PEVector3)(ApplyForceVelocity.RawVector3 * Mathf.Exp(-drag * Time.deltaTime));
            if (ApplyForceVelocity.Magnitude <= (PEInt)0.01f) ApplyForceVelocity = default;
        }
        else
        {
            ApplyForceVelocity = default;
        }
        totalVelocity *= (PEInt)Time.deltaTime;

        // 陡坡滑动：将位移投影到坡面上，避免被 CharacterController 当墙挡住
        if (_isOnSteepSlope)
        {
            // 限制滑落时累计的下坠速度：近垂直长陡坡上重力会持续累积，
            // 不限制会越滑越快，导致落地即触发坠落伤害甚至高速穿地
            float maxSlideFallSpeed = -MaxSpeedInAir * 2f;
            if (CharacterVelocity.y.RawFloat < maxSlideFallSpeed)
            {
                CharacterVelocity = new PEVector3(CharacterVelocity.x, (PEInt)maxSlideFallSpeed, CharacterVelocity.z);
            }

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
            if(FPSGame.Data.FlowState.GameState== GameStateEnum.Game) transform.position = transform.position + Vector3.up * (TerrainUtils.WSToHeight(transform.position)+2- transform.position.y);
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

        //记录本次腾空的最大下坠速度（落地结算用，见 m_FallPeakSpeed 的说明）
        PEInt fallSpeed = -CharacterVelocity.y;
        if (fallSpeed > m_FallPeakSpeed) m_FallPeakSpeed = fallSpeed;
    }

    #region 被压住自动脱困（动态物重叠玩家的兜底）

    /// <summary>
    /// 每帧检查：当胶囊体与其他 Moveable 碰撞体发生"实质性重叠"（排除贴地/贴墙的正常接触）并持续一段时间，
    /// 说明玩家被动态生成物（空投、摆件、NPC 等）直接压住/盖住、任何方向都推不动，此时自动尝试挣脱。
    /// </summary>
    private void HandleAutoEscapeFromOverlap()
    {
        if (!_autoEscapeFromOverlap) return;
        if (Time.time - _lastEscapeTime < 1f) return;//脱困冷却

        if (!IsPenetrating(transform.position))
        {
            _overlapStuckTime = 0f;
            return;
        }

        _overlapStuckTime += Time.deltaTime;
        // 持续被实质穿透才判定为"被压住"，避免与墙壁/地面的正常贴碰误触发
        if (_overlapStuckTime >= 0.3f)
        {
            if (TryEscapeFromOverlap(preferUp: false))
            {
                _overlapStuckTime = 0f;
            }
        }
    }

    /// <summary>
    /// 胶囊体是否在 spot 位置与其他可移动碰撞体"实质性重叠"。
    /// 用缩小半径并上抬 skin 的探测胶囊，忽略恰好贴地/贴墙的表面接触，只认定真正的相互穿透。
    /// </summary>
    private bool IsPenetrating(Vector3 spot)
    {
        if (!Controller) return false;
        float queryRadius = Mathf.Max(Controller.radius * 0.8f, 0.05f);
        Vector3 bottom = spot + Vector3.up * (queryRadius + Controller.skinWidth);
        Vector3 top = spot + Vector3.up * (Controller.height - queryRadius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, queryRadius, s_OverlapBuffer,
            LayerDefinition.MoveableLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = s_OverlapBuffer[i];
            if (c == null || c == Controller) continue;
            // ★ 自身 / 自身子物体不算"被压住"。⚠ 载具（机甲这类）的**骨碰撞体**就在自己胶囊里 ⇒
            //   原判据只排根上的 CC ⇒ 恒为 true ⇒ 每 ~1.3s 自动脱困把机体往上抬 Controller.height*1.2 米，
            //   重力再拽回来 = "落到地上又悬浮几米再掉下来"死循环（2026-10-13 实测：Exosuit 胶囊 4.5m ⇒ 抬 ~4m）。
            //   排除口径与 GroundCheck 里那条 `IsChildOf(transform)` 一致。
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 执行脱困：preferUp=true（玩家按跳跃时）先尝试向上抬升从顶部脱出，再水平找空位；
    /// 否则先水平向外找空位，被完全盖住再向上抬升。
    /// </summary>
    private bool TryEscapeFromOverlap(bool preferUp)
    {
        Vector3 origin = transform.position;
        if (preferUp)
        {
            return TryEscapeUpward(origin) || TryEscapeHorizontally(origin);
        }
        return TryEscapeHorizontally(origin) || TryEscapeUpward(origin);
    }

    /// <summary>向上抬升直到胶囊不再与任何物体重叠（被板/箱盖住时抬到其顶面脱困）</summary>
    private bool TryEscapeUpward(Vector3 origin)
    {
        float maxLift = Controller.height * 1.2f;
        for (float lift = 0.15f; lift <= maxLift; lift += 0.1f)
        {
            Vector3 spot = origin + Vector3.up * lift;
            if (!IsPenetrating(spot))
            {
                DoEscapeTeleport(spot);
                return true;
            }
        }
        return false;
    }

    /// <summary>在水平方向就近寻找空位（往旁边走脱困）</summary>
    private bool TryEscapeHorizontally(Vector3 origin)
    {
        for (float radius = 0.2f; radius <= 1.8f; radius += 0.2f)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 spot = origin + dir * radius;
                if (!IsPenetrating(spot))
                {
                    DoEscapeTeleport(spot);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>传送到已验证无重叠的目标点，略上抬让其自然落体贴合地面/遮挡物顶面，并清除竖直速度</summary>
    private void DoEscapeTeleport(Vector3 target)
    {
        transform.position = target + Vector3.up * 0.05f;
        Physics.SyncTransforms();
        CharacterVelocity = new PEVector3(CharacterVelocity.x, 0, CharacterVelocity.z);
        _lastEscapeTime = Time.time;
        Debug.LogWarning($"{name} 被物体压住，已自动挣脱到 {target}", gameObject);
    }

    #endregion

    /// <summary>
    /// 如果存在障碍返回true
    /// </summary>
    bool HaveObstacle()
    {
        int count = Physics.OverlapCapsuleNonAlloc(
            GetCapsuleBottomHemisphere(),
            GetCapsuleTopHemisphere(),
            Controller.radius,
            s_OverlapBuffer,
            LayerDefinition.MoveableLayers,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = s_OverlapBuffer[i];
            if (c == null || c == Controller) continue;
            // ★ 同 IsPenetrating：自己的子碰撞体不是"障碍物"（否则带骨碰撞体的单位永远判"有障碍"⇒ 跳不起来）
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            Debug.LogWarning("障碍物" + c);
            return true;
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

    /// <summary>施加一个持续力(牛顿)：逐帧累积，在移动循环里以 Δv = (力/重量)·dt 积分成速度</summary>
    public void ApplyForce(PEVector3 force)
    {
        m_PendingForce += force;
    }

    /// <summary>施加一个瞬时冲量：Δv = 冲量 / 重量(无速度上限；重量越大被推得越少)</summary>
    public void ApplyImpulse(PEVector3 impulse)
    {
        ApplyForceVelocity += impulse / (PEInt)Mathf.Max(Weight, 0.1f);
    }
    public void ApplyGravity()
    {
        CharacterVelocity += PEVector3.Down * (PEInt)(GravitySpeed * Time.deltaTime);
    }


    #endregion
}
}
