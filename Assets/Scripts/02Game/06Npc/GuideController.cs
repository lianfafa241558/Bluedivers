using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Attribute;
using FPSGame.Furn;
using GameContract;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 单个引导阶段配置：目标地点、是否玩家靠近自动进入下一阶段、阶段开始时播放的音效组。
/// </summary>
[Serializable]
public class GuideInfo
{
    [InspectorName("目标地点")]
    public GameObject targetPoint;

    [InspectorName("玩家靠近自动进入下一阶段")]
    [Tooltip("开启后，玩家靠近目标地点达到判定距离时自动进入下一阶段")]
    public bool autoNextWhenNear;

    [InspectorName("阶段开始音效组")]
    [Tooltip("进入本阶段时播放的语音")]
    public SoundGroup_SO soundGroup;
}

/// <summary>
/// 引导控制器：由脚本驱动，使用 CharacterController 移动到目标点、使用装备、使用 SoundGroup_SO 交谈。
/// 适用于引导/教学场景中的非玩家角色（不依赖 PlayerInputHandler）。
/// 支持多阶段引导（GuideInfo 列表），每阶段移动到一个地点并可播放阶段开始语音。
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(EquipController))]
public class GuideController : Furniture_Base
{
    [Foldout("移动", true)]
    [SerializeField]
    [InspectorName("移动速度")]
    private float _moveSpeed = 5f;

    [SerializeField]
    [InspectorName("旋转速度")]
    private float _rotateSpeed = 360f;

    [SerializeField]
    [InspectorName("到达判定距离")]
    private float _arriveDistance = 0.2f;

    [SerializeField]
    [InspectorName("最终停止距离")]
    [Tooltip("距目标点的最终停下距离，避免与提示物重叠")]
    private float _stopDistance = 1.5f;

    [SerializeField]
    [InspectorName("重力")]
    private float _gravity = 20f;

    [SerializeField]
    [InspectorName("跳跃强度")]
    [Tooltip("遇到小坡/台阶时自动跳跃上坡")]
    private float _jumpForce = 8f;

    [SerializeField]
    [InspectorName("最大可跳台阶高度")]
    private float _maxStepHeight = 1f;

    [Foldout("交谈", true)]
    [SerializeField]
    [InspectorName("交谈冷却时间（秒）")]
    private float _speakCooldown = 1f;

    [Foldout("引导阶段", true)]
    [SerializeField]
    [InspectorName("阶段列表")]
    private List<GuideInfo> _guideInfos = new();

    [SerializeField]
    [InspectorName("玩家靠近自动进入距离")]
    private float _autoNextDistance = 2f;

    [Foldout("提示物", true)]
    [SerializeField]
    [InspectorName("提示物体")]
    [Tooltip("场景中已放置的提示物，切换阶段时将其移动到当前阶段目标点")]
    private GameObject _hint;

    /// <summary>到达目标点时的回调（参数为目标点）</summary>
    public event Action<Vector3> OnArrived;

    /// <summary>装备使用成功时的回调</summary>
    public event Action<IEquippable> OnEquipUsed;

    /// <summary>进入某阶段时的回调（参数为阶段索引）</summary>
    public event Action<int> OnStageStart;

    /// <summary>所有阶段完成时的回调</summary>
    public event Action OnGuideFinished;

    private CharacterController _controller;
    private EquipController _equipController;
    private Animator _animator;
    private Coroutine _moveCoroutine;
    private Coroutine _gravityCoroutine;
    private Coroutine _speakCoroutine;
    private float _lastSpeakTime = Mathf.NegativeInfinity;
    [SerializeField]
    private Transform _playerTransform;
    private int _currentIndex = -1;

    private Vector3[] _pathCorners;
    private int _pathIndex;

    /// <summary>垂直速度（用于重力与跳跃）</summary>
    private float _verticalVelocity;

    /// <summary>是否正在移动到目标点</summary>
    public bool IsMoving => _moveCoroutine != null;

    /// <summary>是否正在播放交谈语音</summary>
    public bool IsSpeaking { get; private set; }

    /// <summary>目标点（移动中有效）</summary>
    public Vector3 TargetPosition { get; private set; }

    /// <summary>当前阶段索引（-1 表示未开始或已结束）</summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>引导是否已开始</summary>
    public bool IsGuiding => _currentIndex >= 0;

    /// <summary>是否还有剩余阶段</summary>
    public bool HasNextStage => _guideInfos != null && _currentIndex + 1 < _guideInfos.Count;

    protected override void Awake()
    {
        base.Awake();
        _controller = GetComponent<CharacterController>();
        _equipController = GetComponent<EquipController>();
        _animator = GetComponent<Animator>();

        // 重力协程始终运行，角色始终受重力（不会卡在空中），并防止掉出世界
        if (_gravityCoroutine == null)
        {
            _gravityCoroutine = StartCoroutine(GravityRoutine());
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        GlobalEventSub.OnPlayerCreate -= OnPlayerCreated;
        GlobalEventSub.OnPlayerCreate += OnPlayerCreated;
        GlobalEventSub.OnSwitchRole -= OnSwitchRole;
        GlobalEventSub.OnSwitchRole += OnSwitchRole;

        // 重力协程持续运行（组件禁用后协程会被停止，重新启用时恢复）
        if (_gravityCoroutine == null)
        {
            _gravityCoroutine = StartCoroutine(GravityRoutine());
        }
    }

    private void Start()
    {
        StartCoroutine(WaitStartGuide());
    }

    /// <summary>
    /// 平滑旋转到目标物体的朝向（仅绕 Y 轴，保持角色不倾斜）
    /// </summary>
    private IEnumerator WaitStartGuide()
    {
        yield return new WaitForSeconds(2f);
        // 初始自动进入第0阶段
        StartGuide();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        GlobalEventSub.OnPlayerCreate -= OnPlayerCreated;
        GlobalEventSub.OnSwitchRole -= OnSwitchRole;
        StopMoving();
        StopSpeak();
        // 组件禁用时 Unity 会停止协程，置空引用以便重新启用时恢复
        _gravityCoroutine = null;
    }

    private void OnPlayerCreated(I_Actor player)
    {
        if (player == null) return;
        _playerTransform = player.transform;
    }

    private void OnSwitchRole(PlayerController newPlayer)
    {
        if (newPlayer == null) return;
        _playerTransform = newPlayer.transform;
    }

    protected override void Update()
    {
        base.Update();
        TryAutoNextStage();
    }

    public override bool CanOperate(GameObject unit)
    {
        return base.CanOperate(unit)&& !IsSpeaking;
    }

    public override void Operate()
    {
        GuideInfo info = _guideInfos[_currentIndex];
        if (info == null) return;

        // 阶段开始播放音效
        if (info.soundGroup != null)
        {
            Speak(info.soundGroup,false);
        }
    }


    /// <summary>
    /// 检测当前阶段是否需要自动进入下一阶段（玩家靠近目标地点时）
    /// </summary>
    private void TryAutoNextStage()
    {
        if (!IsGuiding) return;
        if (_playerTransform == null) return;
        if (_currentIndex >= _guideInfos.Count) return;

        GuideInfo info = _guideInfos[_currentIndex];
        if (info == null || !info.autoNextWhenNear) return;
        if (info.targetPoint == null) return;

        float sqrDistance = (_playerTransform.position - info.targetPoint.transform.position).sqrMagnitude;
        if (sqrDistance <= _autoNextDistance * _autoNextDistance)
        {
            NextStage();
        }
    }

    /// <summary>
    /// 面向目标点旋转
    /// </summary>
    private void FaceTarget(Vector3 point, float rotateSpeed)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 垂直移动协程：管理垂直速度（重力下落 / 跳跃上升），保持贴地。
    /// 重力始终有效，角色不会卡在空中；若掉出世界则刷新回地面。
    /// </summary>
    private IEnumerator GravityRoutine()
    {
        while (true)
        {
            // 掉出世界保护：低于死亡高度时重新采样到地面上
            if (transform.position.y < Constants.KillHeight)
            {
                Vector3 samplePoint = transform.position + Vector3.up * 100f;
                if (NavMesh.SamplePosition(samplePoint, out NavMeshHit hit, 100f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    _verticalVelocity = 0f;
                }
            }

            if (_controller != null)
            {
                if (_controller.isGrounded)
                {
                    // 贴地时轻微下压，保持与地面接触
                    if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                }
                else
                {
                    // 空中时受重力加速下落
                    _verticalVelocity -= _gravity * Time.deltaTime;
                }

                _controller.TryMove(Vector3.up * (_verticalVelocity * Time.deltaTime));
            }
            yield return null;
        }
    }

    /// <summary>
    /// 检测前方是否有可跳跃越过的小坡/台阶（底部有障碍、顶部无障碍）
    /// </summary>
    private bool CanStepUp(Vector3 forwardDir)
    {
        if (_controller == null) return false;

        float probeDistance = _controller.radius + 0.15f;

        // 底部探测：脚底高度处前方是否有台阶
        Vector3 bottomOrigin = transform.position + Vector3.up * 0.1f;
        bool bottomHit = Physics.Raycast(bottomOrigin, forwardDir, out _,
            probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (!bottomHit) return false;

        // 顶部探测：脚底 + maxStepHeight 高度处前方是否无障碍（可越过）
        Vector3 topOrigin = bottomOrigin + Vector3.up * _maxStepHeight;
        bool topHit = Physics.Raycast(topOrigin, forwardDir, out _,
            probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        return !topHit;
    }

    /// <summary>
    /// 执行跳跃：施加向上的垂直速度
    /// </summary>
    private void Jump()
    {
        _verticalVelocity = _jumpForce;
    }

    /// <summary>
    /// 开始引导：从第一个阶段开始执行
    /// </summary>
    public void StartGuide()
    {
        if (_guideInfos == null || _guideInfos.Count == 0) return;
        StartStage(0);
    }

    /// <summary>
    /// 进入下一阶段（供 UnityEvent 绑定触发）
    /// </summary>
    public void NextStage()
    {
        if (!IsGuiding)
        {
            // 尚未开始则从第一段开始
            StartGuide();
            return;
        }
        if (HasNextStage)
        {
            StartStage(_currentIndex + 1);
        }
        else
        {
            // 已到最后阶段，结束引导
            StopMoving();
            StopSpeak();
            _currentIndex = -1;
            OnGuideFinished?.Invoke();
        }
    }

    /// <summary>
    /// 进入指定阶段的引导：移动到该阶段目标地点并播放阶段开始音效
    /// </summary>
    /// <param name="index">阶段索引</param>
    public void StartStage(int index)
    {
        if (_guideInfos == null || index < 0 || index >= _guideInfos.Count) return;

        _currentIndex = index;
        GuideInfo info = _guideInfos[index];
        if (info == null) return;

        // 阶段开始播放音效
        if (info.soundGroup != null)
        {
            Speak(info.soundGroup,true);
        }

        // 移动到目标地点
        if (info.targetPoint != null)
        {
            MoveTo(info.targetPoint);
            // 提示物跟随移动到当前阶段目标点
            MoveHintTo(info.targetPoint);
        }

        OnStageStart?.Invoke(index);
    }

    /// <summary>
    /// 将提示物移动到指定物体所在位置
    /// </summary>
    /// <param name="target">目标物体</param>
    public void MoveHintTo(GameObject target)
    {
        if (target == null) return;
        if (_hint == null) return;
        _hint.transform.position = target.transform.position;
    }

    /// <summary>
    /// 移动到指定目标点（若已有移动目标则覆盖）
    /// </summary>
    /// <param name="target">目标点物体</param>
    public void MoveTo(GameObject target)
    {
        if (target == null) return;

        StopMoving();
        TargetPosition = target.transform.position;

        // 计算目标点的 NavMesh 路径，得到可行的拐点路线
        Vector3 from = transform.position;
        Vector3 to = target.transform.position;

        // 把起点和终点采样到 NavMesh 上，避免 y=0 不在 NavMesh 面导致寻路失败
        if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, 10f, NavMesh.AllAreas))
        {
            fromHit.position = from;
        }
        if (!NavMesh.SamplePosition(to, out NavMeshHit toHit, 10f, NavMesh.AllAreas))
        {
            toHit.position = to;
        }

        NavMeshPath path = new NavMeshPath();
        // 只要有拐点就使用路径（含部分可达），避免退化直线撞墙
        if (NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, path) &&
            path.corners != null && path.corners.Length > 1)
        {
            _pathCorners = path.corners;
            // corners[0] 是起点本身，从第二个拐点开始走
            _pathIndex = 1;
        }
        else
        {
            // 无法计算可行路径，退化为直接朝目标点直线移动
            _pathCorners = new[] { to };
            _pathIndex = 0;
        }

        _moveCoroutine = StartCoroutine(MoveRoutine(target.transform));
        if (_gravityCoroutine == null)
        {
            _gravityCoroutine = StartCoroutine(GravityRoutine());
        }
    }

    /// <summary>
    /// 移动协程：先旋转朝向，再沿 NavMesh 路径拐点依次移动直至到达。
    /// 到达后旋转对齐到目标物体的朝向。
    /// </summary>
    private IEnumerator MoveRoutine(Transform target)
    {
        Vector3 targetPos = target.position;
        // 卡住检测：统计 0.5 秒窗口内实际水平位移，位移过小才判卡住
        float stuckTimer = 0f;
        float accumulatedMove = 0f;
        Vector3 lastPos = transform.position;

        while (true)
        {
            // 获取当前要前往的路径拐点
            Vector3 waypoint = (_pathCorners != null && _pathIndex < _pathCorners.Length)
                ? _pathCorners[_pathIndex]
                : targetPos;

            // 先旋转面向当前拐点，再移动
            FaceTarget(waypoint, _rotateSpeed);

            Vector3 toWaypoint = waypoint - transform.position;
            toWaypoint.y = 0f;
            float horizontalDistance = toWaypoint.magnitude;

            // 当前拐点是否为最终目标点（最后一个拐点）
            bool isFinal = (_pathCorners == null || _pathIndex >= _pathCorners.Length - 1);
            // 到达判定：最终目标点用停止距离（避免与提示物重叠），中间拐点用到达距离
            float arriveThreshold = isFinal ? _stopDistance : _arriveDistance;

            if (horizontalDistance <= arriveThreshold)
            {
                // 已到达当前拐点，前进到下一个路径拐点
                _pathIndex++;
                stuckTimer = 0f;
                accumulatedMove = 0f;
                lastPos = transform.position;
                // 拐点遍历完后即到达最终目标
                if (_pathCorners == null || _pathIndex >= _pathCorners.Length)
                {
                    // 若角色还在空中（跳跃未落地），先等待落地再结束，避免卡在半空
                    if (_controller != null && !_controller.isGrounded)
                    {
                        while (_controller != null && !_controller.isGrounded)
                        {
                            yield return null;
                        }
                    }
                    break;
                }
                continue;
            }

            Vector3 direction = toWaypoint.normalized;
            if (_controller != null)
            {
                _controller.TryMove(direction * (_moveSpeed * Time.deltaTime));
            }

            // 累计实际水平位移
            Vector3 horizontalNow = transform.position;
            horizontalNow.y = 0f;
            Vector3 horizontalLast = lastPos;
            horizontalLast.y = 0f;
            accumulatedMove += Vector3.Distance(horizontalNow, horizontalLast);
            lastPos = transform.position;

            // 卡住检测：0.5 秒窗口内累计位移远小于正常移动量，才视为被障碍阻挡
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= 0.5f)
            {
                // 正常应移动的距离的 30% 作为卡住阈值
                float expectedMove = _moveSpeed * stuckTimer * 0.3f;
                if (accumulatedMove < expectedMove)
                {
                    // 确实被挡住（位移过小），触发处理
                    stuckTimer = 0f;
                    accumulatedMove = 0f;

                    // 优先检测是否为可跳跃越过的小坡/台阶，可跳则跳跃
                    if (CanStepUp(direction))
                    {
                        Jump();
                        continue;
                    }

                    // 否则尝试侧向绕行绕过障碍物
                    yield return StartCoroutine(DetourRoutine(waypoint));
                    // 绕行结束后重新评估；若仍卡住，下轮卡住检测会再次触发
                    continue;
                }
                else
                {
                    // 正常前进，重置窗口
                    stuckTimer = 0f;
                    accumulatedMove = 0f;
                }
            }

            if (_animator != null)
            {
                _animator.SetBool("IsMove", true);
            }

            yield return null;
        }

        if (_animator != null)
        {
            _animator.SetBool("IsMove", false);
        }

        _pathCorners = null;
        _pathIndex = 0;

        _moveCoroutine = null;

        // 到达后平滑旋转对齐到目标物体的朝向，完成后触发到达回调
        // 注意：重力协程持续运行，不在此停止，保证角色始终受重力
        if (target != null)
        {
            yield return StartCoroutine(RotateToRoutine(target));
        }

        OnArrived?.Invoke(targetPos);
    }

    /// <summary>
    /// 侧向绕行协程：被障碍物阻挡时，朝可通行的侧向（含前进分量）移动绕过动态障碍物。
    /// 障碍移除后角色可继续朝拐点前进并自然恢复。
    /// </summary>
    private IEnumerator DetourRoutine(Vector3 waypoint)
    {
        Vector3 dir = waypoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) yield break;
        dir.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

        // 记录绕行开始时到拐点的距离，用于判断是否绕过成功
        Vector3 startToWp = waypoint - transform.position;
        startToWp.y = 0f;
        float startDistance = startToWp.magnitude;

        // 当前绕行方向，每隔一段时间动态重探测，避免固定方向卡死
        Vector3 sideDir = DetectFreeSide(right) ? right : -right;
        float directionTimer = 0f;

        float timer = 0f;
        while (timer < 1.5f)
        {
            // 到拐点距离明显减少，说明已绕过障碍，提前结束
            Vector3 toWp = waypoint - transform.position;
            toWp.y = 0f;
            if (toWp.magnitude < startDistance - 0.2f) break;

            // 每 0.3 秒重新探测可通行侧，动态调整绕行方向
            directionTimer += Time.deltaTime;
            if (directionTimer > 0.3f)
            {
                directionTimer = 0f;
                Vector3 newSide = DetectFreeSide(right) ? right : -right;
                if (newSide != sideDir) sideDir = newSide;
            }

            // 混合方向：侧向为主 + 前进分量，避免原地打转，持续朝拐点推进
            Vector3 moveDir = (sideDir * 0.7f + dir * 0.3f).normalized;

            if (_controller != null)
            {
                _controller.TryMove(moveDir * (_moveSpeed * Time.deltaTime));
            }

            if (_animator != null)
            {
                _animator.SetBool("IsMove", true);
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 探测某一侧方向是否可通行（无碰撞阻挡）
    /// </summary>
    private bool DetectFreeSide(Vector3 sideDir)
    {
        if (_controller == null) return false;

        Vector3 center = transform.position + _controller.center;
        float half = _controller.height * 0.5f - _controller.radius;
        Vector3 bottom = center - Vector3.up * half;
        Vector3 top = center + Vector3.up * half;

        // 探测距离取移动速度一帧的位移 + 半径余量
        float checkDistance = _moveSpeed * Time.deltaTime + _controller.radius;
        return !Physics.CapsuleCast(bottom, top, _controller.radius, sideDir,
            out _, checkDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// 平滑旋转到目标物体的朝向（仅绕 Y 轴，保持角色不倾斜）
    /// </summary>
    private IEnumerator RotateToRoutine(Transform target)
    {
        Quaternion targetRotation = GetTargetRotation(target);
        while (transform.rotation != targetRotation)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotateSpeed * Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>
    /// 计算目标物体的朝向旋转（forward 投影到水平面，保持角色不倾斜）
    /// </summary>
    private Quaternion GetTargetRotation(Transform target)
    {
        if (target == null) return transform.rotation;

        Vector3 forward = target.rotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            return transform.rotation;
        }
        forward.Normalize();
        return Quaternion.LookRotation(forward);
    }

    /// <summary>
    /// 停止移动并复位动画
    /// </summary>
    public void StopMoving()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        // 重力协程持续运行，不在此停止，保证角色始终受重力
        if (_animator != null)
        {
            _animator.SetBool("IsMove", false);
        }
    }

    /// <summary>
    /// 使用装备：对目标家具触发装备交互（该家具需是装备类，如 Furniture_Equip）
    /// </summary>
    /// <param name="equipFurniture">装备家具组件</param>
    /// <returns>是否成功触发</returns>
    public bool UseEquip(IFurniture equipFurniture)
    {
        if (equipFurniture == null) return false;
        if (_equipController == null) return false;
        if (!equipFurniture.CanOperate(gameObject)) return false;

        bool handled = equipFurniture.Handle(gameObject);
        if (!handled) return false;

        // 通知已使用（通过已安装的装备反查，或直接以家具上挂载的 IEquippable 通知）
        IEquippable equip = equipFurniture.gameObject != null
            ? equipFurniture.gameObject.GetComponent<IEquippable>()
            : null;
        OnEquipUsed?.Invoke(equip);
        return true;
    }

    /// <summary>
    /// 使用 SoundGroup_SO 交谈：从语音组取一条语音并通过全局事件广播
    /// </summary>
    /// <param name="soundGroup">语音组配置</param>
    /// <returns>是否成功触发</returns>
    public bool Speak(SoundGroup_SO soundGroup,bool isNecessary)
    {
        if (soundGroup == null) return false;
        if (!isNecessary&&Time.time < _lastSpeakTime + _speakCooldown) return false;

        RuntimeSoundData soundData = soundGroup.Get(transform.position);
        if (soundData.Clip == null) return false;

        GlobalEventSub.ActorSpeech(gameObject, soundData);
        _lastSpeakTime = Time.time;

        float clipLength = soundData.Clip.length;
        if (_speakCoroutine != null)
        {
            StopCoroutine(_speakCoroutine);
        }
        _speakCoroutine = StartCoroutine(SpeakRoutine(clipLength));
        return true;
    }

    /// <summary>
    /// 交谈语音播放协程，播放期间 IsSpeaking 为 true
    /// </summary>
    private IEnumerator SpeakRoutine(float clipLength)
    {
        IsSpeaking = true;
        if (clipLength > 0f)
        {
            yield return new WaitForSeconds(clipLength);
        }
        IsSpeaking = false;
        _speakCoroutine = null;
    }

    /// <summary>
    /// 停止交谈并复位状态
    /// </summary>
    public void StopSpeak()
    {
        if (_speakCoroutine != null)
        {
            StopCoroutine(_speakCoroutine);
            _speakCoroutine = null;
        }
        IsSpeaking = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_guideInfos == null || _guideInfos.Count == 0) return;

        // 用直线连接各阶段的目标地点
        Gizmos.color = Color.cyan;
        Vector3 previous = transform.position;

        for (int i = 0; i < _guideInfos.Count; i++)
        {
            GuideInfo info = _guideInfos[i];
            if (info == null || info.targetPoint == null)
            {
                previous = transform.position;
                continue;
            }

            Vector3 current = info.targetPoint.transform.position;
            Gizmos.DrawLine(previous, current);
            Gizmos.DrawWireSphere(current, 0.3f);
            previous = current;
        }

        // 标记当前阶段的判定距离（玩家靠近自动进入下一阶段）
        if (_currentIndex >= 0 && _currentIndex < _guideInfos.Count)
        {
            GuideInfo currentInfo = _guideInfos[_currentIndex];
            if (currentInfo != null && currentInfo.targetPoint != null && currentInfo.autoNextWhenNear)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentInfo.targetPoint.transform.position, _autoNextDistance);
            }
        }
    }
#endif
}
