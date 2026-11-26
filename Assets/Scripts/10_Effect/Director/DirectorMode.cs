using System.Collections;
using UnityEngine;
using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Data;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Managers;

namespace FPSGame.Effect
{
/// <summary>导演模式的三态：自由飞行取景 / 固定机位 / 解锁鼠标选点。</summary>
public enum DirectorState
{
    Fly = 0,
    Shot = 1,
    Pointer = 2,
}

/// <summary>
/// PV 导演模式核心：机位、时间控制、地面选点、定点生成单位与战备。
/// <para>▍为什么单独一个类而不是塞进 <c>FreeCameraWnd</c>：世界操作要调 09/06/05 三层，
/// UI 只做键位转发与面板刷新；下期加"指挥已有单位"时不用碰 UI。</para>
/// </summary>
[AddComponentMenu("特效/导演模式")]
public class DirectorMode : MonoBehaviour
{
    private const int ShotCount = 9;

    /// <summary>机位过渡的**基准**时长（秒）。实际时长还会按相机移动速度缩放，见 <see cref="BlendDuration"/>。</summary>
    private const float DefaultBlendBase = 0.35f;
    private const float MinBlendBase = 0.05f;
    private const float MaxBlendBase = 1.5f;

    /// <summary>相机默认移动速度：处在这个速度时，实际过渡时长 == 基准值。</summary>
    private const float ReferenceSpeed = 4f;

    /// <summary>倍率档位。默认取 0.2：原来的 0.005 几乎静止，取景与布置都看不出变化。</summary>
    private static readonly float[] RateSteps = { 1f, 0.5f, 0.2f, 0.05f, 0.005f };

    private const int DefaultRateIndex = 2;
    private const float PickDistance = 2000f;

    public struct CameraShot
    {
        public bool valid;
        public Vector3 position;
        public Vector3 euler;
        public float fov;
    }

    private readonly CameraShot[] _shots = new CameraShot[ShotCount];

    private FreeCameraController _controller;
    private Camera _camera;
    private DirectorMarker _marker;

    private int _rateIndex = DefaultRateIndex;
    private bool _paused;
    private DirectorState _stateBeforePointer = DirectorState.Fly;
    private bool _markerVisible = true;
    private float _blendBase = DefaultBlendBase;

    private Coroutine _blendRoutine;
    private Coroutine _stepRoutine;

    public DirectorState State { get; private set; } = DirectorState.Fly;

    /// <summary>当前实际倍率（暂停 = 0）。</summary>
    public float Rate => _paused ? 0f : RateSteps[_rateIndex];

    public bool Paused => _paused;

    public bool HasMarker => _marker != null && _marker.HasPoint;

    public Vector3 MarkerPoint => _marker != null ? _marker.Point : default;

    public FreeCameraController Controller => _controller;

    /// <summary>由 <c>FreeCameraWnd</c> 首次开窗时装配（prefab 里没有本组件，避免改 prefab 层级）。</summary>
    public void Init(FreeCameraController controller, Camera camera)
    {
        _controller = controller;
        _camera = camera;

        // 标记挂在场景根：Window 的根是 RectTransform，世界物体挂在 Canvas 下会被 UI 变换带偏
        var markerGo = new GameObject("DirectorMarker");
        _marker = markerGo.AddComponent<DirectorMarker>();
    }

    private void OnDestroy()
    {
        if (_blendRoutine != null) StopCoroutine(_blendRoutine);
        if (_stepRoutine != null) StopCoroutine(_stepRoutine);
        if (_marker != null) Destroy(_marker.gameObject);
    }

    #region 进入 / 退出

    public void Enter()
    {
        _rateIndex = DefaultRateIndex;
        _paused = false;
        ApplyRate();
        EnterFly();
        ClearMarker();
        SetMarkerVisible(true);
    }

    public void Exit()
    {
        if (_blendRoutine != null)
        {
            StopCoroutine(_blendRoutine);
            _blendRoutine = null;
        }
        if (_stepRoutine != null)
        {
            StopCoroutine(_stepRoutine);
            _stepRoutine = null;
        }

        _paused = false;
        GameRoot.SetTimeScale(1f, false);
        ClearMarker();
        SetMarkerVisible(true);

        State = DirectorState.Fly;
        ApplyStateControl();
        LockCursor(true);
    }

    #endregion

    #region 状态切换

    public void EnterFly()
    {
        State = DirectorState.Fly;
        ApplyStateControl();
        LockCursor(true);
    }

    /// <summary>Tab：在"选点"与进入前的状态（飞行 / 固定机位）之间来回。</summary>
    public void TogglePointer()
    {
        if (State == DirectorState.Pointer)
        {
            if (_stateBeforePointer == DirectorState.Shot) EnterShotFrozen();
            else EnterFly();
            return;
        }

        _stateBeforePointer = State;
        State = DirectorState.Pointer;
        ApplyStateControl();
        LockCursor(false);
    }

    private void EnterShotFrozen()
    {
        State = DirectorState.Shot;
        ApplyStateControl();
        LockCursor(true);
    }

    /// <summary>
    /// 把相机与玩家输入恢复成"当前状态该有的样子"。状态切换与"松开微调"共用同一处，
    /// 避免两处各写一遍导致某条路径漏复位（之前 Pointer 回 Shot 就靠这个方法兜住）。
    /// </summary>
    private void ApplyStateControl()
    {
        if (_controller == null) return;

        switch (State)
        {
            case DirectorState.Shot:
                // 固定机位：相机冻结、世界继续，把角色交还玩家
                _controller.InputEnabled = false;
                _controller.AllowLook = true;
                UIState.PlayerControl = true;
                break;

            case DirectorState.Pointer:
                // 选点：相机锁死（连 WASD 都不挪），避免按方向键时镜头漂移、落点判断跟着错。
                // 需要换取景就按住中键/Alt 临时接管（见 SetCameraOverride）。
                _controller.InputEnabled = false;
                _controller.AllowLook = false;
                UIState.PlayerControl = false;
                break;

            default:
                _controller.InputEnabled = true;
                _controller.AllowLook = true;
                UIState.PlayerControl = false;
                break;
        }
    }

    /// <summary>
    /// 按住中键/Alt 时临时接管相机（固定机位微调、选点态转视角）。
    /// <para>▍为什么接管时必须收回玩家输入：固定机位下玩家本来能走，WASD 会同时驱动相机和角色。</para>
    /// </summary>
    public void SetCameraOverride(bool on)
    {
        if (_controller == null) return;

        if (on)
        {
            _controller.InputEnabled = true;
            _controller.AllowLook = true;
            UIState.PlayerControl = false;
            // 转视角必须锁鼠标；选点态平时是解锁的
            if (State == DirectorState.Pointer) LockCursor(true);
            return;
        }

        ApplyStateControl();
        LockCursor(State != DirectorState.Pointer);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    #endregion

    #region 机位

    public bool HasShot(int index) => index >= 0 && index < ShotCount && _shots[index].valid;

    public CameraShot GetShot(int index) => index >= 0 && index < ShotCount ? _shots[index] : default;

    /// <summary>滑动条可调的基准过渡时长（秒）。</summary>
    public float BlendBase => _blendBase;

    /// <summary>
    /// 当前实际过渡时长：相机飞得越快，跳机位就越干脆（大范围跳转不该慢慢飘）。
    /// <para>公式 = 基准 × (标准速度 / 当前速度)，钳到 [MinBlendBase, 2s]：速度 4 时等于基准。</para>
    /// </summary>
    public float BlendDuration
    {
        get
        {
            float speed = _controller != null ? _controller.Speed : ReferenceSpeed;
            float factor = ReferenceSpeed / Mathf.Max(speed, 0.01f);
            return Mathf.Clamp(_blendBase * factor, MinBlendBase, 2f);
        }
    }

    public void SetBlendBase(float seconds) => _blendBase = Mathf.Clamp(seconds, MinBlendBase, MaxBlendBase);

    /// <summary>把当前位姿存进槽位（1..9）。</summary>
    public void SaveShot(int index)
    {
        if (index < 0 || index >= ShotCount || _controller == null) return;

        var t = _controller.transform;
        _shots[index] = new CameraShot
        {
            valid = true,
            position = t.position,
            euler = t.eulerAngles,
            fov = _controller.Fov,
        };
        Debug.Log($"[导演] 保存机位 {index + 1}：{t.position}");
    }

    /// <summary>跳到槽位；没存过就一条日志返回（不猜、不取当前位姿兜底）。</summary>
    public void GotoShot(int index)
    {
        if (!HasShot(index))
        {
            Debug.LogWarning($"[导演] 机位 {index + 1} 还没保存（Alt+{index + 1} 存当前位姿）");
            return;
        }

        if (_blendRoutine != null) StopCoroutine(_blendRoutine);
        _blendRoutine = StartCoroutine(BlendTo(_shots[index]));
        EnterShotFrozen();
    }

    private IEnumerator BlendTo(CameraShot shot)
    {
        var t = _controller.transform;
        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;
        float fromFov = _controller.Fov;
        Quaternion toRot = Quaternion.Euler(shot.euler);

        // 开始过渡时取一次：中途改速度/滑条不会让进度条跳变
        float duration = BlendDuration;
        float k = 0f;
        while (k < 1f)
        {
            k += Time.unscaledDeltaTime / duration;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
            _controller.SnapTo(Vector3.Lerp(fromPos, shot.position, s),
                               Quaternion.Slerp(fromRot, toRot, s),
                               Mathf.Lerp(fromFov, shot.fov, s));
            yield return null;
        }

        _controller.SnapTo(shot.position, toRot, shot.fov);
        _blendRoutine = null;
    }

    #endregion

    #region 时间

    /// <param name="direction">-1 = 更慢（档位变大），+1 = 更快（档位变小）。档位数组是从快到慢排列的。</param>
    public void CycleRate(int direction)
    {
        _paused = false;
        _rateIndex = Mathf.Clamp(_rateIndex - direction, 0, RateSteps.Length - 1);
        ApplyRate();
    }

    public void TogglePause()
    {
        _paused = !_paused;
        ApplyRate();
    }

    /// <summary>推进一个 50Hz 逻辑帧（暂停时有效）。</summary>
    public void StepOneTick()
    {
        if (!_paused || _stepRoutine != null) return;
        _stepRoutine = StartCoroutine(StepRoutine());
    }

    private IEnumerator StepRoutine()
    {
        // LogicFrameHost 用 Time.deltaTime 累加 ⇒ 让下一帧的 deltaTime 恰好等于一个逻辑帧，
        // 它就只会推进 1 tick（不是"渲染帧"，面板文案也这么写）。
        float step = Constants.LoginFrame.RawFloat;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
        GameRoot.SetTimeScale(step / dt, false);
        yield return null;
        GameRoot.SetTimeScale(Rate, false);
        _stepRoutine = null;
    }

    private void ApplyRate() => GameRoot.SetTimeScale(Rate, false);

    #endregion

    #region 选点

    /// <summary>正在从面板把条目拖到场景（期间只做落点预览，松手才生成）。</summary>
    public bool IsPlacing { get; private set; }

    public void BeginPlacement() => IsPlacing = true;

    public void EndPlacement() => IsPlacing = false;

    /// <summary>屏幕点 → 地面世界点。<c>screenPos</c> 由 UI 层传入，UI 悬停判定也在那一侧做。</summary>
    public bool TryPickScreenPoint(Vector2 screenPos, out Vector3 point)
    {
        point = default;
        if (_camera == null || _controller == null) return false;

        Ray ray = _camera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, PickDistance, _controller.GroundMask)) return false;

        point = hit.point;
        return true;
    }

    public void SetMarker(Vector3 worldPoint)
    {
        if (_marker == null) return;
        // 单位与战备最终都要落在可站立的地面上：先吸到导航网格，吸不到就保持原样（GetNavMeshPoint 内部回落）
        _marker.Show(FpsHelper.GetNavMeshPoint(worldPoint));
        // 面板/标记被 H 藏起来时不要因为选了个点就突然冒出来
        _marker.SetVisualVisible(_markerVisible);
    }

    /// <summary>只切标记显隐、保留落点数据（H 隐藏 UI 面板时一起藏）。</summary>
    public void SetMarkerVisible(bool visible)
    {
        _markerVisible = visible;
        if (_marker != null) _marker.SetVisualVisible(visible);
    }

    public void ClearMarker()
    {
        if (_marker != null) _marker.Hide();
    }

    #endregion

    #region 生成

    /// <summary>在落点生成白名单里的单位。成员端不生成（与 <c>WaveManager.CreatUnit</c> 同一口径）。</summary>
    public GameObject SpawnUnit(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[导演] 没选单位预制体");
            return null;
        }
        if (!HasMarker)
        {
            Debug.LogWarning("[导演] 还没设落点：Tab 解锁鼠标后左键点地面");
            return null;
        }
        if (BattleManager.Instance != null && BattleManager.Instance.IsNetMember)
        {
            Debug.LogWarning("[导演] 联机成员端不生成单位（号段归房主）");
            return null;
        }

        Vector3 pos = FpsHelper.GetNavMeshPoint(MarkerPoint);

        var parent = ActorsManager.Instance != null ? ActorsManager.Instance.transform : null;
        float yaw = _controller != null ? _controller.transform.eulerAngles.y : 0f;
        var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
        go.name = prefab.name;

        // 出生延迟清 0：PV 布景等不起站立动画（与 WaveManager.CreatUnit 的 IsFixed 同口径）
        var ai = go.GetComponent<I_AIController>();
        if (ai != null) ai.BirthDuration = 0f;
        var actor = go.GetComponent<IActor>();
        if (actor != null) actor.IsFixed = true;

        Debug.Log($"[导演] 生成 {go.name} @ {pos}");
        return go;
    }

    /// <summary>在落点释放一次战备（走契约层，不碰 <c>AirdropController</c> 的私有交互链）。</summary>
    public void ReleaseAirdrop(AirdropData_SO data)
    {
        if (data == null)
        {
            Debug.LogWarning("[导演] 未选战备");
            return;
        }
        if (!HasMarker)
        {
            Debug.LogWarning("[导演] 还没设落点：Tab 解锁鼠标后左键点地面");
            return;
        }

        BattleHub.Current.ReleaseAirdrop(MarkerPoint, data.ID);
        Debug.Log($"[导演] 释放战备 {data.showName}({data.ID}) @ {MarkerPoint}");
    }

    #endregion
}
}
