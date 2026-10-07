
using FPSGame.Core.Interface;
using System.Collections.Generic;
using System.Linq;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.Weapon;
namespace FPSGame.AI
{
    //[RequireComponent(typeof(EnemyController))]
    /// <summary>
    /// 地面单位的移动状态机（巡逻 / 警惕 / 跟随 / 攻击 / 返回），挂在敌人预制体上。
    /// <para>框架：继承 <see cref="AIInputUnitController{AIState}"/>（状态表 + 切换 + 炮塔瞄准），其基类为
    /// <see cref="AIInputBaseController{T}"/>。视野/目标/伤害判定由 EnemyController 与 DetectionModule 提供，
    /// 本类只负责"去哪、怎么走、朝哪打"。异常状态（AboState）在同目录 partial 文件 EnemyMobile_AboState.cs。</para>
    ///
    /// <para>▍状态表（InitState 注册，AiStateHook 提供 onEnter / onUpdate / onExit）</para>
    /// <list type="bullet">
    /// <item><b>Idle</b>：无巡逻点时进入。有 HomePoint 则继续走向落点，否则停导航；每帧 UpdateAutoRotate 让炮塔自转巡检；
    /// 非固定单位超过 IdleMaxDuration 且最近玩家距离 &gt; 80m 时自毁(Kill)。</item>
    /// <item><b>Patrol</b>：有巡逻点(PatrolPos)时进入。速度按 PatrolSpeed 写入 Extra 差量修饰，退出时取反移除；
    /// 走到巡逻点即自毁（哨兵点；与"到达即待命"的 HomePoint 不同，见 EnemyController）。</item>
    /// <item><b>Follow</b>：已发现目标但未满足攻击条件。两段接近——水平距离 &gt; EncircleDistance 时先前往玩家周围
    /// 随机环绕角(_encircleAngle)的环绕点，否则直接锁玩家位置；当距离小于 停止距离-1 且勾选 MaintainMaxDis 时反向拉开；
    /// 全程调 AimTargrt 瞄准。</item>
    /// <item><b>Attack</b>：Follow 满足"看见目标 + 在攻击范围内 + 炮塔已锁定(IsLockTarget)"后转入。
    /// 停止距离 = AttackStopDistanceRatio × DetectionModule.AttackRange，据此逼近 / 保持 / 站定；
    /// 勾选 AttackStop 且任一武器正在开火(IsFiringNow：蓄力/激光/射击中)时站桩且炮塔保持当前朝向不追踪
    /// （仅对已锁定且 CanFireAt 的炮塔开火，且需过开火延迟）。IsAttackLocked 时只停火、不改移动。</item>
    /// <item><b>Beware</b>：听到枪声/示警(DetectionModule.BewarePoint)或丢失目标后的搜索点(SearchPoint)时进入。
    /// 落点按"搜索点 &gt; 噪声点 &gt; 出生点"取用并消费掉，速度改为 BewareSpeed；
    /// 边走边把炮塔朝向警惕方向（只取水平方向，避免把炮口压到地面落点上）。
    /// 结束条件三条（任一成立即 StopNav → Return）：① 看见目标 → 清警惕点转 Follow；
    /// ② 到达 BewareReachRadius 内，**或**已抵达"能看清此处"的观察位 CanObserveBeware()
    /// （接近到观察距离内 + 到落点上方 _ObserveAimHeight 的视线无遮挡 + 连续满足 ObserveSettleTime）
    /// —— 即不必贴到点脚下；观察距离按落点来源区分：SearchPoint 用 SearchObserveDistance、噪声点用 BewareObserveDistance（&lt;=0 关闭该特性）；
    /// ③ 超过 BewareTimeout 兜底（点不可达/被挡住）。
    /// 前两条进 Return 后会在原地停留 BewareStayDuration 扫视（这期间也算"查看"），再回原点。</item>
    /// <item><b>Return</b>：先原地停留 BewareStayDuration（期间炮塔自转"查看"四周），其后每帧下发原点；
    /// 回到原点 BewareReachRadius 内则按有无巡逻点转 Patrol/Idle，同时记录警惕冷却(BewareCooldown)。</item>
    /// <item><b>Death</b>：OnDie 进入，停止所有武器；此后既不迁移也不执行行为。</item>
    /// </list>
    ///
    /// <para>▍状态迁移（UpdateAiStateTransitions，每帧在行为之前执行）</para>
    /// <list type="bullet">
    /// <item>Death、Vertigo/Terror 期间整体冻结迁移。</item>
    /// <item>Idle/Patrol → Beware：ShouldInvestigateBeware() 为真。判据 = 有警惕点 且 点在 max(听力, 视野) 内
    /// 且 不处于警惕冷却的"同点"范围内（否则一枪会惊动几十米外、或在原地来回跑）。</item>
    /// <item>Beware/Return → Follow：一旦 IsSeeingTarget，清警惕点并转追逐。</item>
    /// <item>Follow → Attack：看见目标 + 在攻击范围内 + 炮塔已锁定；Attack → Follow：失去视线或脱离攻击范围。</item>
    /// </list>
    ///
    /// <para>▍每帧驱动（UpdateCurrentAiState）守卫链，命中即 return</para>
    /// <para>出生未完成(BirthComplete) → Death → 弱点僵直计时清除 → IsMoveLocked（Freeze/Vertigo 强制 StopNav）
    /// → Hacker 锁同队 → IsForcedAttack（Toxicity 乱走 + 无脑开火）→ Terror 逃离 → 查表执行当前状态行为。</para>
    ///
    /// <para>▍感知回调</para>
    /// <list type="bullet">
    /// <item>OnDetectedTarget：清警惕点；仅在 Idle/Patrol/Beware/Return 时转 Follow，并重置开火延迟计时
    /// (m_TimeStartedDetection) 与随机环绕角。已在 Follow/Attack 时**不**重置计时——否则开火延迟反复重算，
    /// mustStop 分支跳过开火，武器卡在 InShoots 无法结束（表现为"开火被打断 / 等换弹完才开火"）。</item>
    /// <item>OnLostTarget：目标最后已知位置写入 SearchPoint 并转 Beware；无警惕/搜索点则回 Patrol，
    /// 无巡逻点时下发原点转 Return；同时停火、停导航。</item>
    /// </list>
    ///
    /// <para>▍炮塔瞄准（LateUpdate → UpdateTurretAiming）</para>
    /// <para>Beware/Follow/Attack 用 turrets.Aiming(开火延迟融合) 追踪；Idle/Patrol/Return 由各自行为的
    /// UpdateAutoRotate 自转巡检；Death 不转。</para>
    ///
    /// <para>▍异常状态（AboState，由 Health.OnAboStateFullChanged 派发，实现见 partial 文件）</para>
    /// <para>Electric：速度 Factor -0.3。Freeze/Vertigo：禁移动禁攻击并清目标与警惕点。Terror：清目标后朝远离伤害源
    /// 方向逃 20m。Toxicity：清目标后每 1.5s 在 5m 半径内随机换点乱走并持续开火。Hacker：每帧锁定 20m 内的同队单位。
    /// 门闩 IsMoveLocked / IsAttackLocked / IsForcedAttack / IsHacked 在守卫链与 Attack 行为中生效。</para>
    ///
    /// <para>▍通用约定</para>
    /// <para>① 所有距离判定用水平距离再减目标 HalfRange（寻路是地面导航，忽略高度差，避免大型单位因身高把距离"抬高"
    /// 而一直往目标脚下冲）；② 速度类状态（Patrol/Beware/Return）进入时按 (目标速度 - FinalValue) 写 Extra 修饰、
    /// 退出取反移除；③ 弱点受击僵直 WeakPointHitStunDuration 由 Health.OnHit(isWeakness) 触发，只禁攻击不禁移动；
    /// ④ "以看代到"只在 Beware 用：视线查询走 DetectionModule.HasLineOfSight（无副作用）。**不要**改用 CanLook ——
    /// 它是 protected，且命中烟幕层时会改写 TimeLastSeenTarget，会污染目标锁定的"最后可见时间"。</para>
    /// </summary>
    [AddComponentMenu("AI/地面单位")]
    public partial class EnemyMobile : AIInputUnitController<EnemyMobile.AIState>
    {
        /// <summary>视线观察时把落点抬高多少米再检测：警惕点多为地面弹着点，直接朝它射线会被地面自身挡住</summary>
        private const float _ObserveAimHeight = 1.5f;

        /// <summary>开火时停在原地且炮塔停止旋转：任一武器处于开火状态(蓄力/激光/射击/可连射)时，机体站桩、炮塔保持当前朝向不追踪；不开火时正常追敌并追踪瞄准</summary>
        [InspectorName("开火时停在原地")]
        [Tooltip("开启后：只要任一武器在开火(蓄力/激光/射击/可连射)，就停在原地且炮塔停止旋转(保持当前朝向)；不开火时正常追敌并追踪瞄准")]
        public bool AttackStop;
        protected EnemyController m_EnemyController;

        /// <summary>弱点受击僵直时长（秒），命中弱点后短暂无法攻击（不禁移动）</summary>
        [InspectorName("弱点受击僵直时长(秒)")]
        public float WeakPointHitStunDuration = 0.3f;

        /// <summary>弱点受击僵直是否生效</summary>
        private bool _hitStunActive;
        /// <summary>弱点受击僵直结束时间</summary>
        private float _hitStunEndTime;

        /// <summary>环绕角（首次进入战斗时随机生成，单位先前往玩家周围该角度的环绕点，到达后才直接锁玩家）</summary>
        private float _encircleAngle;

        protected Vector3 TargetPosition => m_EnemyController.Target.Pos;

        protected TargetData target;

        public enum AIState
        {
            Idle,
            Patrol,
            Follow,
            Attack,
            Death,
            Beware,
            Return,
        }

        [SerializeField]
        EnemyMobile.AIState showState;

       [InspectorName("停止移动时攻击范围的系数")]
        [Tooltip("攻击范围指侦测模块的攻击范围")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;
        [InspectorName("保持在攻击距离范围上的距离")]//目标靠近就跑，远离就追
        public bool MaintainMaxDis = false;

        [InspectorName("环绕距离(米)")]
        [Tooltip("发现目标后先前往玩家周围该距离的随机角度点，接近到该距离内后才直接锁定玩家位置，避免所有单位直线挤向玩家")]
        public float EncircleDistance = 8f;

        [InspectorName("巡逻速度")]
        public float PatrolSpeed = 2;

        [InspectorName("警惕前往速度")]
        public float BewareSpeed = 3;

        /// <summary>警惕点到达判定半径</summary>
        [InspectorName("警惕到达半径")]
        public float BewareReachRadius = 2f;

        /// <summary>警惕到达后停留时间</summary>
        [InspectorName("警惕停留时间")]
        public float BewareStayDuration = 2f;

        /// <summary>警惕查看超时(秒)：前往警惕点的路上超过该时长仍未到达(目标点不可达/被挡住)就放弃并走返回流程</summary>
        [InspectorName("警惕查看超时(秒)")]
        [Tooltip("前往警惕点途中超过该时长仍未到达(如目标点不可达)则放弃、原地转入返回流程，避免单位永久卡在警惕状态。<=0 表示不限时")]
        public float BewareTimeout = 20f;

        /// <summary>听声查看的视线观察距离(米)：听到噪声后前往查看时，不必走到点脚下——接近到该距离内且能看清该处即算查看过，转入停留扫视后返回。&lt;=0 关闭该特性(退化为必须物理到达)</summary>
        [SerializeField]
        [InspectorName("听声查看观察距离(米)")]
        [Tooltip("听到枪声等噪声后前往查看：接近到该距离内并且视线能看清噪声点就算查看过了，不再走到点脚下。<=0 关闭，退化为必须物理到达")]
        private float BewareObserveDistance = 10f;

        /// <summary>追敌搜索的视线观察距离(米)：丢失目标后前往目标最后已知位置时使用；追人值得走到脚下确认，通常应比听声查看更小。&lt;=0 关闭</summary>
        [SerializeField]
        [InspectorName("追敌观察距离(米)")]
        [Tooltip("丢失目标后前往目标最后已知位置搜索：接近到该距离内并且视线能看清该处就算搜过了。通常应比听声查看更小；<=0 关闭")]
        private float SearchObserveDistance = 4f;

        /// <summary>视线观察稳定时长(秒)：视线需连续满足该时长才认账，避免门框/墙角处时通时断导致状态抖动。&lt;=0 表示不要求稳定</summary>
        [SerializeField]
        [InspectorName("视线观察稳定时长(秒)")]
        [Tooltip("视线连续满足多久才算看清了，防止在门框/墙角处一会通一会断导致单位来回切换。<=0 不要求稳定")]
        private float ObserveSettleTime = 0.4f;

        /// <summary>警惕冷却(秒)：从上一个检查点返回后，这段时间内不再响应"该检查点附近"的枪声</summary>
        [InspectorName("警惕冷却(秒)")]
        [Tooltip("从上一个检查点返回后，这段时间内不再响应该检查点附近的枪声(战斗一直打在原地时不要来回跑)。<=0 表示关闭冷却")]
        public float BewareCooldown = 10f;

        /// <summary>警惕冷却范围(米)：枪声点距"上一次检查点"不超过该值才算"同一个地方"</summary>
        [InspectorName("警惕冷却范围(米)")]
        [Tooltip("枪声点距上一次检查点不超过该值时视为同一个地方，冷却期内会被无视；超出则照常前往查看")]
        public float BewareCooldownRadius = 10f;

        /// <summary>初始巡逻点（返回状态回到这里）</summary>
        private PEVector3 m_OriginPos;

        /// <summary>进入Beware时记录的目标点（也是"上一次检查点"，警惕冷却用）</summary>
        private PEVector3 m_BewareDestination;

        /// <summary>回到起点的时间</summary>
        private float m_ReturnStartTime;

        /// <summary>进入警惕状态的时间（警惕查看超时用）</summary>
        private float m_BewareStartTime;

        /// <summary>本次 Beware 的落点是否来自"追敌最后已知位置"(SearchPoint)：决定用哪个视线观察距离</summary>
        private bool m_BewareIsSearch;

        /// <summary>视线观察条件已连续满足的起始时间（负值 = 当前不满足，用于 ObserveSettleTime 防抖）</summary>
        private float m_ObserveValidSince = -1f;

        /// <summary>警惕冷却结束时间（返回 Idle/Patrol 时按 BewareCooldown 记录）</summary>
        private float m_BewareCooldownEndTime;

        /// <summary>进入Idle的时间</summary>
        private float m_IdleStartTime;

        [InspectorName("Idle最大停留时间（秒）")]
        public float IdleMaxDuration = 120f;

        /// <summary>Speed 属性差值（Patrol/Beware/Return 进入时计算，退出时取反移除）</summary>
        private PEMaths.PEInt speedScale;

        [ContextMenu("重置")]
        private void ResetQu()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                if (turrets[i].barrelSetOffset.w == 0)
                {
                    Debug.LogError("炮塔的w=0    "+i);
                    turrets[i].barrelSetOffset = new Quaternion(turrets[i].barrelSetOffset.x, turrets[i].barrelSetOffset.y, turrets[i].barrelSetOffset.z, 1);
                }
            }
        }

        protected override void Start()
        {
            base.Start();
            m_EnemyController = m_Controller as EnemyController;
            m_OriginPos = (PEVector3)transform.position;

            // 有巡逻点就走巡逻，否则原地不动
            if (m_EnemyController.PatrolPos != default)
            {
                SwitchState(AIState.Patrol);
            }
            else
            {
                SwitchState(AIState.Idle);
            }

            InitAboStateListener();
            InitHitStunListener();
        }

        private void OnDestroy()
        {
            OnDestroyAboState();
            UninitHitStunListener();
        }

        /// <summary>订阅 Health.OnHit，命中弱点时触发受击僵直</summary>
        private void InitHitStunListener()
        {
            var health = m_EnemyController.GetComponent<IHealth>();
            if (health.IsValidMono())
            {
                health.OnHit += OnHitStun;
            }
        }

        private void UninitHitStunListener()
        {
            var health = m_EnemyController != null ? m_EnemyController.GetComponent<IHealth>() : null;
            if (health.IsValidMono())
            {
                health.OnHit -= OnHitStun;
            }
        }

        /// <summary>命中弱点：触发短僵直，期间无法攻击（不禁移动）。仅弱点命中触发</summary>
        private void OnHitStun(GameObject source, Vector3 pos,Vector3 _, bool isWeakness)
        {
            if (!isWeakness) return;
            _hitStunActive = true;
            _hitStunEndTime = Time.time + WeakPointHitStunDuration;
        }

        /// <summary>回到起点后决定是Idle还是Patrol</summary>
        private void TryReturnToIdleOrPatrol()
        {
            // 记冷却：刚落点检查完回到原位，短时间内别被"同一个地方"的枪声再叫过去(战斗还在原地打时来回跑)
            if (BewareCooldown > 0f) m_BewareCooldownEndTime = Time.time + BewareCooldown;

            if (m_EnemyController.PatrolPos != default)
            {
                SwitchState(AIState.Patrol);
            }
            else
            {
                SwitchState(AIState.Idle);
            }
        }

        /// <summary>构建状态表，注册每个状态的 onEnter/onUpdate/onExit</summary>
        protected override Dictionary<AIState, AiStateHook> InitState()
        {
            return new Dictionary<AIState, AiStateHook>
            {
                [AIState.Idle] = new AiStateHook
                {
                    onEnter = EnterIdle,
                    onUpdate = IdleBehavior,
                },
                [AIState.Patrol] = new AiStateHook
                {
                    onEnter = EnterPatrol,
                    onExit = ExitSpeedState,
                    onUpdate = PatrolBehavior,
                },
                [AIState.Follow] = new AiStateHook
                {
                    onUpdate = FollowBehavior,
                },
                [AIState.Attack] = new AiStateHook
                {
                    onUpdate = AttackBehavior,
                },
                [AIState.Death] = new AiStateHook(),
                [AIState.Beware] = new AiStateHook
                {
                    onEnter = EnterBeware,
                    onExit = ExitSpeedState,
                    onUpdate = BewareBehavior,
                },
                [AIState.Return] = new AiStateHook
                {
                    onEnter = EnterReturn,
                    onExit = ExitSpeedState,
                    onUpdate = ReturnBehavior,
                },
            };
        }

        #region 状态 onEnter / onExit

        /// <summary>进入 Idle：记录时间并停止导航；若存在长期落点(HomePoint)则继续走向它，避免中断后丢失目标</summary>
        private void EnterIdle()
        {
            m_IdleStartTime = Time.time;
            if (m_EnemyController.HomePoint != default)
            {
                m_EnemyController.SetNavDestination(m_EnemyController.HomePoint);
            }
            else
            {
                m_EnemyController.StopNav();
            }
        }

        /// <summary>进入 Patrol：设置巡逻速度差修饰</summary>
        private void EnterPatrol()
        {
            if (m_EnemyController.Speed == null) return;
            speedScale = (PEMaths.PEInt)PatrolSpeed - m_EnemyController.Speed.FinalValue;
            m_EnemyController.Speed.AddModifier(ModifierType.Extra, speedScale);
        }

        /// <summary>进入 Beware：决定要去的点(搜索指令 > 听觉噪声点 > 出生点)并消费掉，再设置警惕速度差修饰</summary>
        private void EnterBeware()
        {
            var module = m_EnemyController.DetectionModule;
            // 落点来源决定用哪个视线观察距离：追敌最后已知位置(SearchPoint) vs 听声查看(噪声点/出生点)
            m_BewareIsSearch = module.SearchPoint.HasValue;
            if (m_BewareIsSearch)
            {
                m_BewareDestination = module.SearchPoint.Value;
                module.SearchPoint = null;
            }
            else
            {
                var bewarePoint = module.BewarePoint;
                m_BewareDestination = bewarePoint.HasValue ? bewarePoint.Value : m_OriginPos;
            }
            // 消费掉警惕点：离开警惕/返回后回到 Idle/Patrol 时，不会因为同一个点再次进入 Beware(来回摆动)；
            // 之后新的枪声/示警会重新写入 BewarePoint，再触发一次查看
            module.ClearBeware();

            m_BewareStartTime = Time.time;
            m_ObserveValidSince = -1f;
            if (m_EnemyController.Speed == null) return;
            speedScale = (PEMaths.PEInt)BewareSpeed - m_EnemyController.Speed.FinalValue;
            m_EnemyController.Speed.AddModifier(ModifierType.Extra, speedScale);
        }

        /// <summary>进入 Return：设置返回速度差修饰</summary>
        private void EnterReturn()
        {
            if (m_EnemyController.Speed == null) return;
            speedScale = (PEMaths.PEInt)BewareSpeed - m_EnemyController.Speed.FinalValue;
            m_EnemyController.Speed.AddModifier(ModifierType.Extra, speedScale);
        }

        /// <summary>退出带速度差修饰的状态（Patrol/Beware/Return）：移除该修饰</summary>
        private void ExitSpeedState()
        {
            m_EnemyController.Speed?.AddModifier(ModifierType.Extra, -speedScale);
        }

        #endregion

        #region 状态 onUpdate（各状态每帧行为）

        /// <summary>Idle：空闲炮塔巡逻转动；非固定单位超时且无玩家靠近则自毁</summary>
        private void IdleBehavior()
        {
            // 开启自动巡逻旋转的炮塔（如机枪）在空闲时巡逻转动
            UpdateAutoRotate();

            // 非固定单位Idle超时后移动
            if (!m_EnemyController.IsFixed && Time.time >= m_IdleStartTime + IdleMaxDuration)
            {
                if (ActorsManager.Players.Min(item => Vector3.Distance(item.Pos, m_EnemyController.Pos)) > 80)
                {
                    m_EnemyController.Kill(true);
                }
            }
        }

        /// <summary>Patrol：炮塔巡逻转动；走到巡逻点则自毁（移除）</summary>
        private void PatrolBehavior()
        {
            // 开启自动巡逻旋转的炮塔在巡逻时转动
            UpdateAutoRotate();
            if (m_EnemyController.UpdatePathDestination())
            {
                //移除
                m_EnemyController.Kill(true);
            }
        }

        /// <summary>Beware：前往警惕点查看，炮塔一边走一边朝向警惕方向</summary>
        private void BewareBehavior()
        {
            m_EnemyController.SetNavDestination(m_BewareDestination.RawVector3);

            // 只取水平方向：不要把炮管压到地面上的落点(否则炮口一直朝下)
            Vector3 lookAt = m_BewareDestination.RawVector3;
            lookAt.y = transform.position.y;
            CalculationAimTargrt(lookAt);
        }

        /// <summary>
        /// Return：到达警惕点后原地停留 BewareStayDuration（这段由状态切换里的"停留时间到才下发原点"驱动），
        /// 停留期间炮塔自动巡逻转动"查看"四周；到达原点后由状态切换回到 Idle/Patrol
        /// </summary>
        private void ReturnBehavior()
        {
            UpdateAutoRotate();
        }

        /// <summary>Follow：先前往玩家周围随机角度的环绕点，接近后直接锁玩家位置</summary>
        private void FollowBehavior()
        {
            // 水平距离(寻路是地面导航，忽略高度差)减去目标半径：避免大型单位因身高把距离"抬高"，导致一直往目标脚下冲
            float targetHalfFollow = m_EnemyController.Target.Actor?.HalfRange ?? 0f;
            Vector3 toTargetFollow = TargetPosition - m_EnemyController.Pos;
            toTargetFollow.y = 0f;
            float followDis = toTargetFollow.magnitude - targetHalfFollow;
            float followStopRange = AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange;

            if (followDis > EncircleDistance && toTargetFollow.sqrMagnitude > 0.01f)
            {
                // 阶段1：先前往玩家周围该随机角度的环绕点，从不同方向接近，避免同向单位挤成直线
                Vector3 dir = toTargetFollow.normalized;
                Vector3 encirclePoint = TargetPosition + Quaternion.Euler(0f, _encircleAngle, 0f) * (-dir) * EncircleDistance;
                m_EnemyController.SetNavDestination(encirclePoint);
            }
            else if (followDis >= followStopRange + 1)
            {
                // 阶段2：已进入环绕距离，直接锁玩家位置
                m_EnemyController.SetNavDestination(TargetPosition);
            }
            else if (followDis < followStopRange - 1 && MaintainMaxDis)
            {
                m_EnemyController.SetNavDestination(transform.position + (transform.position - TargetPosition).normalized);
            }
            else
            {
                m_EnemyController.StopNav();
            }

            AimTargrt();
        }

        /// <summary>Attack：逼近/保持距离并射击，支持开火站桩与瞄准锁定</summary>
        private void AttackBehavior()
        {
            // 水平距离(寻路是地面导航，忽略高度差)减去目标半径：避免大型单位因身高把距离"抬高"，导致一直往目标脚下冲
            float targetHalfAttack = m_EnemyController.Target.Actor?.HalfRange ?? 0f;
            Vector3 toTargetAttack = TargetPosition - m_EnemyController.Pos;
            toTargetAttack.y = 0f;
            float dis = toTargetAttack.magnitude - targetHalfAttack;
            float stopRange = (AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange);
            bool mustStop = AttackStop && IsFiringNow();
            if (mustStop)
            {
                m_EnemyController.StopNav();
            }
            //如果目标到自己的距离大于停止系数*攻击范围，那就追，到范围就停
            else if (dis >= stopRange + 1)//接近
            {
                m_EnemyController.SetNavDestination(TargetPosition);
            }
            else if (dis < stopRange - 1 && MaintainMaxDis)//保持最大距离的敌人会在目标接近时远离
            {
                m_EnemyController.SetNavDestination(transform.position + (transform.position - TargetPosition).normalized);
            }
            else//原地
            {
                m_EnemyController.StopNav();
            }

            // shoot
            if (IsAttackLocked)
            {
                // Vertigo/Terror：禁止攻击，停止武器
                turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
            }
            else if (mustStop)
            {
                // 开火中且勾选 AttackStop：炮塔保持当前朝向不再追踪(不调 AimTargrt/Look)，
                // 仅对已锁定的炮塔按开火延迟正常开火
                if (IsFireDelayPassed())
                {
                    turrets.ForEach(item => {
                        if (item.weapon && item.IsLockTarget(TargetPosition) && item.CanFireAt(TargetPosition))
                        {
                            m_EnemyController.TryAtack(item.weapon);
                        }
                    });
                }
            }
            else if (AimTargrt())
            {
                turrets.ForEach(item => {
                    if (item.weapon && item.IsLockTarget(TargetPosition) && item.CanFireAt(TargetPosition))
                    {
                        m_EnemyController.TryAtack(item.weapon);
                    }
                });
            }
        }

        #endregion

        #region 状态切换判定（Transitions）

        /// <summary>
        /// 是否已抵达"能看清警惕点"的观察位——到了就不必再走到点脚下，就地停留下查看即可。
        /// 三条同时满足：① 该来源配了观察距离，且水平距离已进入该距离（SearchPoint 用 SearchObserveDistance，
        /// 噪声点用 BewareObserveDistance，&lt;=0 视为关闭该特性）；② 从眼睛到"落点上方 _ObserveAimHeight"的视线无遮挡
        /// （不算视野角度：身体朝向由寻路决定，会抖）；③ 连续满足①②的时长达到 ObserveSettleTime（防门框/墙角处时通时断）。
        /// 注：条件②只判"能不能看清这块地方"，敌人是否还在这里由 IsSeeingTarget 分支负责（看见即转 Follow）。
        /// </summary>
        private bool CanObserveBeware()
        {
            float observeDistance = m_BewareIsSearch ? SearchObserveDistance : BewareObserveDistance;
            if (observeDistance <= 0f)
            {
                m_ObserveValidSince = -1f;
                return false;
            }

            Vector3 point = m_BewareDestination.RawVector3;

            // 水平距离：与项目其它距离判定一致，忽略高度差
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.magnitude > observeDistance)
            {
                m_ObserveValidSince = -1f;
                return false;
            }

            var module = m_EnemyController.DetectionModule;
            if (module == null)
            {
                m_ObserveValidSince = -1f;
                return false;
            }

            // 朝"落点上方的观察高度"看：警惕点多为地面弹着点，直接朝它射线会被地面自身挡住
            if (!module.HasLineOfSight(point + Vector3.up * _ObserveAimHeight))
            {
                m_ObserveValidSince = -1f;
                return false;
            }

            if (m_ObserveValidSince < 0f) m_ObserveValidSince = Time.time;
            return Time.time - m_ObserveValidSince >= ObserveSettleTime;
        }

        /// <summary>
        /// 是否该为当前警惕点起身查看，三条都要满足：
        /// ① 有警惕点(枪声/示警点)；
        /// ② 点在感知范围(听力/视野**取较大者**)内——枪声写入 BewarePoint 的条件比较宽松
        ///    (距离 - 音半径 不超过 听力距离)，不拦一道的话一枪能惊动几十米外的单位；
        /// ③ 不在警惕冷却里——刚从"同一个地方"检查完返回时，不因为同一处的枪声再跑一趟；
        ///    冷却范围外的枪声照常响应。
        /// </summary>
        private bool ShouldInvestigateBeware()
        {
            var module = m_EnemyController.DetectionModule;
            if (module == null) return false;

            var bewarePoint = module.BewarePoint;
            if (!bewarePoint.HasValue) return false;

            Vector3 point = bewarePoint.Value.RawVector3;
            if (Vector3.Distance(transform.position, point) > Mathf.Max(module.HearingRange, module.DetectionRange)) return false;

            // m_BewareDestination = 上一次去过的检查点（由 TryReturnToIdleOrPatrol 记冷却结束时间）；
            // 从没去过任何检查点(m_BewareDestination 还是 default)时不做冷却判定
            if (BewareCooldown > 0f && Time.time < m_BewareCooldownEndTime && m_BewareDestination != default
                && Vector3.Distance(point, m_BewareDestination.RawVector3) <= BewareCooldownRadius) return false;

            return true;
        }

        /// <summary>状态机切换</summary>
        protected override void UpdateAiStateTransitions()
        {
            // 死亡后不再进行状态切换
            if (AiState == AIState.Death) return;
            // Vertigo/Terror 期间冻结状态切换
            if (_vertigoActive || _terrorActive) return;

            // Handle transitions 
            switch (AiState)
            {
                // 听到枪声/示警：前往警惕点查看，到达后停留再返回原点（警惕点由 DetectionModule.BulletHit 写入）
                case AIState.Idle:
                case AIState.Patrol:
                    if (ShouldInvestigateBeware())
                    {
                        SwitchState(AIState.Beware);
                    }
                    break;

                case AIState.Beware:
                    // 如果发现目标，清除警惕点并转为追逐
                    if (m_EnemyController.IsSeeingTarget)
                    {
                        m_EnemyController.DetectionModule.ClearBeware();
                        SwitchState(AIState.Follow);
                    }
                    // 到达警惕点、或已走到"能看清此处"的观察位(不必贴到点脚下)、或查看超时(目标点不可达/被挡住，
                    // 不能永久卡在这里)后，停留一段时间再返回
                    else if (Vector3.Distance(transform.position, m_BewareDestination.RawVector3) <= BewareReachRadius
                        || CanObserveBeware()
                        || (BewareTimeout > 0 && Time.time - m_BewareStartTime >= BewareTimeout))
                    {
                        m_EnemyController.StopNav();
                        m_ReturnStartTime = Time.time;
                        SwitchState(AIState.Return);
                    }
                    break;

                case AIState.Return:
                    // 返回途中如果发现目标，清除警惕点并转为追逐
                    if (m_EnemyController.IsSeeingTarget)
                    {
                        m_EnemyController.DetectionModule.ClearBeware();
                        SwitchState(AIState.Follow);
                    }
                    // 先判"是否已回到原点"，再判"停留时间到没到"。
                    // ⚠ 原顺序把"停留时间到"写在前面：时间一过该条件永远为真，"到达判定"永远进不去
                    // ⇒ 单位会卡在 Return 里反复请求原点，永远回不到 Idle/Patrol
                    else if (Vector3.Distance(transform.position, m_OriginPos.RawVector3) <= BewareReachRadius)
                    {
                        m_EnemyController.StopNav();
                        TryReturnToIdleOrPatrol();
                    }
                    // 停留时间结束，开始移动回原点（每帧下发由 SetNavDestination 内部去重/节流）
                    else if (Time.time >= m_ReturnStartTime + BewareStayDuration)
                    {
                        m_EnemyController.SetNavDestination(m_OriginPos.RawVector3);
                    }
                    break;

                case AIState.Follow:
                    // 当与目标有视线连接时，转为攻击状态
                    if (m_EnemyController.IsSeeingTarget && m_EnemyController.IsTargetInAttackRange&& IsLockTarget()) {
                        SwitchState(AIState.Attack);
                        //在这里写移动没用，下一帧就改了
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_EnemyController.IsTargetInAttackRange||!m_EnemyController.IsSeeingTarget)
                    {
                        SwitchState(AIState.Follow);
                    }

                    break;
            }
        }

        private bool IsLockTarget() {
            foreach(var item in turrets) {
                if (item.IsLockTarget(TargetPosition)) {
                    return true;
                }
            }
            return false;
        }
        /// <summary>任一武器是否正在开火（蓄力/激光/射击中）。注意：不含"武器就绪可开火"(CanShoot)，
        /// 否则进入射程后 mustStop 恒为 true，炮塔会被永久冻结无法转过去对准目标</summary>
        bool IsFiringNow()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                WeaponEnemyController w = turrets[i].weapon;
                if (w != null && (w.InCharging || w.InLasering || w.InShoots))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>是否已过开火延迟（与 AimTargrt 判定一致：任一炮塔过了它的开火延迟即允许开火）</summary>
        bool IsFireDelayPassed()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                if (Time.time > m_TimeStartedDetection + turrets[i].detectionFireDelay)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        /// <summary>状态机每帧（全局守卫后查表调用当前状态的 onUpdate）</summary>
        protected override void UpdateCurrentAiState()
        {
            if (!m_EnemyController.BirthComplete) return;

            // 死亡后不再执行任何行为
            if (AiState == AIState.Death) return;

            showState = AiState;

            // 弱点受击僵直超时清除
            if (_hitStunActive && Time.time >= _hitStunEndTime)
            {
                _hitStunActive = false;
            }

            // Vertigo：完全禁止移动，强制停止导航
            if (IsMoveLocked)
            {
                m_EnemyController.StopNav();
                return;
            }

            // Hacker：每帧尝试锁定同队伍单位
            UpdateHackerTarget();

            // Toxicity：乱走+持续攻击
            if (IsForcedAttack)
            {
                if (Time.time >= _toxicityWanderNextTime)
                {
                    RefreshToxicityWanderDestination();
                }
                m_EnemyController.SetNavDestination(_toxicityWanderDestination);
                // 持续攻击：无目标也尝试对炮台朝向方向开火
                turrets.ForEach(item =>
                {
                    if (item.weapon != null)
                    {
                        //中毒
                        m_EnemyController.TryAtack(item.weapon);
                    }
                });
                return;
            }

            // Terror：持续往远离伤害源方向跑，跳过原状态机逻辑
            if (_terrorActive)
            {
                m_EnemyController.SetNavDestination(_terrorFleeDestination);
                return;
            }

            // 查表调用当前状态的行为
            InvokeCurrentState();
        }


        protected override void OnDetectedTarget()
        {
            // 不管什么状态，发现目标都清空警惕点
            m_EnemyController.DetectionModule.ClearBeware();
            if (AiState == AIState.Idle || AiState == AIState.Patrol || AiState == AIState.Beware || AiState == AIState.Return)
            {
                SwitchState(AIState.Follow);
                // 首次从非战斗状态进入战斗才重置开火延迟计时
                m_TimeStartedDetection = Time.time;
                // 生成一次随机环绕角：先前往玩家周围该角度的环绕点，接近后才直接锁玩家
                _encircleAngle = Random.Range(0f, 360f);
            }
            // 战斗状态(Follow/Attack)下反复 OnDetect(目标短暂失去视野后重新看见、受击转火等)
            // 不再重置 m_TimeStartedDetection：
            // 否则开火延迟(detectionFireDelay)反复重新计时，期间 IsFireDelayPassed=false，
            // mustStop 分支跳过开火→武器被冻结在射击状态(InShoots)无法结束→卡死，表现为"开火被打断/等换弹完才开火"
        }

        protected override void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                // 丢失目标时，优先前往目标最后已知位置搜索；没有则退回警惕点（枪声/示警点）。
                // 搜索点是"指令"不是"噪声"，单独放 SearchPoint；EnterBeware 按 搜索点 > 噪声点 取用并消费
                var module = m_EnemyController.DetectionModule;
                var lastKnown = module.LastKnownTargetPos;
                if (lastKnown.HasValue)
                {
                    module.SearchPoint = lastKnown.Value;
                }
                if (module.SearchPoint.HasValue || module.BewarePoint.HasValue)
                {
                    SwitchState(AIState.Beware);
                }
                else
                {
                    // 无警惕点：有巡逻点就走巡逻，否则回出生点
                    if (m_EnemyController.PatrolPos != default)
                    {
                        SwitchState(AIState.Patrol);
                    }
                    else
                    {
                        m_EnemyController.SetNavDestination(m_OriginPos.RawVector3);
                        SwitchState(AIState.Return);
                    }
                }
            }

            m_TimeLostDetection = Time.time;
            turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
            m_EnemyController.StopNav();
        }

        /// <summary>
        /// 炮台锁头(LateUpdate)：应用 Look() 渐进算出的目标旋转。
        /// Follow/Attack 追击瞄准、Beware 一边走一边看向警惕方向；
        /// Idle/Patrol/Return 由各自 onUpdate 里的自动巡逻转动(UpdateAutoRotate)驱动，Death 不转。
        /// </summary>
        protected override void UpdateTurretAiming()
        {
            if (AiState == AIState.Beware || AiState == AIState.Follow || AiState == AIState.Attack)
            {
                turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
            }
        }

        /// <summary>对开启自动巡逻旋转的炮塔执行巡逻转动（未开启的自动跳过）</summary>
        private void UpdateAutoRotate()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].AutoRotate(Time.deltaTime);
            }
        }

        protected override bool AimTargrt()
        {
            bool mustShoot = false;
            foreach(var item in turrets){
                if(mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay)break;
            }

            CalculationAimTargrt(TargetPosition);

            return mustShoot;
        }

        protected override void OnDamaged(Collider collider)
        {

        }


        protected override void OnDie()
        {
            SwitchState(AIState.Death);
            turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
        }

    }
}
