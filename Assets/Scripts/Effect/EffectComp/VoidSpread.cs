using UnityEngine;
using UnityEngine.Events;

namespace EffectComp
{
    /// <summary>
    /// 虚空扩散的当前状态
    /// </summary>
    public enum VoidSpreadState
    {
        /// <summary>空闲：未开始，或已扩散到最大半径，或已收缩到 0</summary>
        Idle,
        /// <summary>扩散中：半径由起始半径增长到最大半径</summary>
        Spreading,
        /// <summary>收缩中：半径衰减到 0</summary>
        Shrinking
    }

    /// <summary>
    /// 扩散所驱动的缩放轴
    /// </summary>
    public enum VoidSpreadAxis
    {
        /// <summary>三轴等比（球形扩散）</summary>
        Uniform,
        /// <summary>仅水平面 X/Z（贴地扩散），Y 轴保持原始缩放</summary>
        HorizontalXZ,
        /// <summary>仅 Y 轴（垂直光柱），X/Z 轴保持原始缩放</summary>
        VerticalY
    }

    /// <summary>
    /// 虚空扩散：让物体半径在「扩散」与「收缩」两个状态间变化。
    /// 半径按**面积**匀速变化：面积以恒定速度增减，半径 = √(面积/π)，
    /// 所以半径越大，每秒的半径变化量越小（越大越慢），观感接近冲击波。
    /// 扩散到最大半径后自动停在最大半径（不自动收缩），由外部通过 UnityEvent 调用 <see cref="SwitchToShrink"/> 切换为收缩，
    /// 收缩到 0 后结束（可选隐藏物体，便于对象池复用）。
    /// 被驱动的轴缩放值 = 2 * 半径（即直径），未被驱动的轴保持物体原始缩放。
    /// </summary>
    [DisallowMultipleComponent]
    public class VoidSpread : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("扩散速度")]
        [Tooltip("扩散状态下【面积】的增长速率（㎡/秒）。半径 = √(面积/π)，故半径越大增长越慢。"
            + "参考：最大半径 10m → 面积约 314㎡，填 100 约 3.1 秒扩散完")]
        private float _spreadSpeed = 100f;

        [SerializeField]
        [InspectorName("收缩速度")]
        [Tooltip("收缩状态下【面积】的衰减速率（㎡/秒）。半径越大衰减越慢，接近 0 时会快速收没")]
        private float _shrinkSpeed = 100f;

        [SerializeField]
        [InspectorName("最大半径")]
        [Tooltip("扩散的半径上限，到达后停止扩散")]
        private float _maxRadius = 10f;

        [SerializeField]
        [InspectorName("起始半径")]
        [Tooltip("扩散的起始半径，每次开始/重启用该值")]
        private float _startRadius = 0f;

        [SerializeField]
        [InspectorName("扩散轴")]
        private VoidSpreadAxis _axis = VoidSpreadAxis.HorizontalXZ;

        [SerializeField]
        [InspectorName("启用时自动扩散")]
        private bool _autoSpreadOnEnable = true;

        [SerializeField]
        [InspectorName("收缩结束后隐藏物体")]
        [Tooltip("收缩到 0 后 SetActive(false)，便于对象池复用")]
        private bool _deactivateOnShrinkEnd;


        private float _area;
        private VoidSpreadState _state = VoidSpreadState.Idle;
        private Vector3 _baseScale = Vector3.one;

        /// <summary>最大半径对应的面积（㎡）</summary>
        private float MaxArea => RadiusToArea(_maxRadius);

        /// <summary>当前状态</summary>
        public VoidSpreadState State => _state;

        /// <summary>当前半径（由面积换算：√(面积/π)）</summary>
        public float Radius => Mathf.Sqrt(_area / Mathf.PI);

        /// <summary>当前面积（㎡）</summary>
        public float Area => _area;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_autoSpreadOnEnable)
            {
                StartSpread();
            }
        }

        private void Update()
        {
            if (_state == VoidSpreadState.Idle)
            {
                return;
            }

            // 面积匀速变化：半径 = √(面积/π)，半径越大每秒变化量越小（越大越慢）
            if (_state == VoidSpreadState.Spreading)
            {
                _area += _spreadSpeed * Time.deltaTime;
                if (_area >= MaxArea)
                {
                    FinishSpread();
                    return;
                }
            }
            else
            {
                _area -= _shrinkSpeed * Time.deltaTime;
                if (_area <= 0f)
                {
                    FinishShrink();
                    return;
                }
            }

            ApplyArea(_area);
        }

        /// <summary>
        /// 开始扩散：面积重置为起始半径对应的面积，并按扩散速度增长至最大半径。
        /// </summary>
        public void StartSpread()
        {
            _area = RadiusToArea(Mathf.Clamp(_startRadius, 0f, _maxRadius));
            _state = VoidSpreadState.Spreading;
            ApplyArea(_area);
        }

        /// <summary>
        /// 切换为收缩状态，面积以收缩速度衰减到 0（半径随之收敛到 0）后结束。供 UnityEvent 挂载调用。
        /// </summary>
        public void SwitchToShrink()
        {
            if (_state == VoidSpreadState.Shrinking)
            {
                return;
            }

            _state = VoidSpreadState.Shrinking;
        }

        /// <summary>
        /// 停止变化并保持当前半径。
        /// </summary>
        public void Stop()
        {
            _state = VoidSpreadState.Idle;
        }

        /// <summary>
        /// 立即设置半径并刷新缩放（会被钳制到 [0, 最大半径]）。
        /// </summary>
        /// <param name="radius">目标半径</param>
        public void SetRadius(float radius)
        {
            _area = RadiusToArea(Mathf.Clamp(radius, 0f, _maxRadius));
            ApplyArea(_area);
        }

        private void FinishSpread()
        {
            _area = MaxArea;
            _state = VoidSpreadState.Idle;
            ApplyArea(_area);
        }

        private void FinishShrink()
        {
            _area = 0f;
            _state = VoidSpreadState.Idle;
            ApplyArea(_area);

            if (_deactivateOnShrinkEnd)
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyArea(float area)
        {
            float diameter = AreaToDiameter(area);

            switch (_axis)
            {
                case VoidSpreadAxis.HorizontalXZ:
                    transform.localScale = new Vector3(diameter, _baseScale.y, diameter);
                    break;
                case VoidSpreadAxis.VerticalY:
                    transform.localScale = new Vector3(_baseScale.x, diameter, _baseScale.z);
                    break;
                default:
                    transform.localScale = new Vector3(diameter, diameter, diameter);
                    break;
            }
        }

        private static float RadiusToArea(float radius)
        {
            return Mathf.PI * radius * radius;
        }

        private static float AreaToDiameter(float area)
        {
            return 2f * Mathf.Sqrt(area / Mathf.PI);
        }
    }
}
