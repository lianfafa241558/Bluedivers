using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 列车控制器（挂在车头上）：沿 <see cref="RailTrack"/> 行驶，车厢按弧长跟随。
    /// <para>⚠ 直接写 Transform，不走 NavMeshAgent：轨道是预先铺好的曲线，不需要寻路；
    /// 车厢只是"视觉 + 伤害转发"，不是单位。</para>
    /// </summary>
    [AddComponentMenu("任务/火车/列车控制器", 31)]
    public class TrainUnit : MonoBehaviour
    {
        /// <summary>到站（跑完整条轨道）时回调一次</summary>
        public event Action<TrainUnit> OnArrived;

        /// <summary>远端弧长偏差超过它就直接吸附（首次收包 / 掉线重连 / 真脱节）。</summary>
        private const float SnapDistance = 15f;
        /// <summary>追赶速度 = 基础速度 × 它（下限见 <see cref="MinCorrectionSpeed"/>）。</summary>
        private const float CorrectionSpeedFactor = 2f;
        /// <summary>追赶速度下限（米/秒）：基础速度被调得很低时也要追得上。</summary>
        private const float MinCorrectionSpeed = 4f;
        private const float CorrectionEpsilon = 0.01f;

        private RailTrack _track;
        private float _speed = 8f;
        private float _distance;
        private float _heightOffset;
        private float _yawOffset;
        private bool _arrived;

        /// <summary>本趟班次（0 基，= 任务的发车序号）：联机时作为"同一趟车"的键（见 4067）。</summary>
        public int Tour { get; private set; }

        /// <summary>待消化的远端弧长偏差（米）：**按帧限速**逼近，不直接赋 <c>_distance</c>。</summary>
        private float _correction;
        /// <summary>是否收到过房主的弧长（false 时第一包走吸附，别从 0 米慢慢追）。</summary>
        private bool _hasRemote;

        private readonly List<Transform> _cars = new();
        private readonly List<float> _carOffsets = new();
        private readonly List<float> _carHeights = new();

        public bool Arrived => _arrived;
        public RailTrack Track => _track;

        /// <summary>已行驶弧长（米）</summary>
        public float Distance => _distance;

        /// <summary>当前世界 XZ</summary>
        public Vector2 Position => _track != null ? _track.SampleXZ(_distance) : Vector2.zero;

        /// <summary>
        /// 由任务在生成时注入。
        /// </summary>
        /// <param name="track">行驶轨道</param>
        /// <param name="speed">速度（米/秒）</param>
        /// <param name="heightOffset">车头 pivot 相对轨面的高度（= -底面到 pivot）</param>
        /// <param name="yawOffset">模型长轴修正（与铺轨同一套）</param>
        /// <param name="cars">车厢 Transform（可为空）</param>
        /// <param name="carOffsets">各车厢相对车头的弧长偏移（米，正数 = 在后面）</param>
        /// <param name="carHeightOffsets">各车厢 pivot 相对轨面的高度</param>
        /// <param name="tour">本趟班次（0 基，联机按它对同一趟车）</param>
        public void Init(RailTrack track, float speed, float heightOffset, float yawOffset,
            List<Transform> cars, List<float> carOffsets, List<float> carHeightOffsets, int tour = 0)
        {
            _track = track;
            _speed = Mathf.Max(0.1f, speed);
            _heightOffset = heightOffset;
            _yawOffset = yawOffset;
            Tour = tour;
            _correction = 0f;
            _hasRemote = false;
            _cars.Clear();
            _carOffsets.Clear();
            _carHeights.Clear();
            if (cars != null)
            {
                for (int i = 0; i < cars.Count; i++)
                {
                    if (cars[i] == null) continue;
                    _cars.Add(cars[i]);
                    _carOffsets.Add(carOffsets != null && i < carOffsets.Count ? carOffsets[i] : 0f);
                    _carHeights.Add(carHeightOffsets != null && i < carHeightOffsets.Count ? carHeightOffsets[i] : 0f);
                }
            }
            Apply();
        }

        private void Update()
        {
            if (_track == null || _arrived) return;

            // 远端校正：房主每 ~0.5s 给一次弧长，这里把差值**按限速**消化掉（几帧走完）。
            // ⚠ 不能收到就赋 _distance：那就是"每个包跳一下"（同 NetTransformView 的结论，载具实测过）。
            if (Mathf.Abs(_correction) > CorrectionEpsilon)
            {
                float maxFix = Mathf.Max(MinCorrectionSpeed, _speed * CorrectionSpeedFactor) * Time.deltaTime;
                float fix = Mathf.Clamp(_correction, -maxFix, maxFix);
                _distance += fix;
                _correction -= fix;
            }

            _distance += _speed * Time.deltaTime;
            if (_distance < 0f) _distance = 0f;
            if (_distance >= _track.TotalLength)
            {
                _distance = _track.TotalLength;
                _arrived = true;
                Apply();
                OnArrived?.Invoke(this);
                return;
            }
            Apply();
        }

        /// <summary>
        /// 【成员】应用房主下发的弧长（见 <c>CmdId.TrainPoseSync</c>）。
        /// <para>▍为什么只同步弧长：两端轨道是同一份确定性规划 ⇒ 弧长唯一决定车头 + 全部车厢的位姿，
        /// 传位姿反而要多一份"车头/车厢各自摆"的账。</para>
        /// <para>▍大偏差直接吸附：本端刚建车、或长时间丢包/卡顿后再收到时差值可能有几十米，
        /// 让它在轨道上"加速滑过去"比瞬移更怪。</para>
        /// </summary>
        public void ApplyRemoteDistance(float distance)
        {
            if (_track == null || _arrived) return;

            float target = Mathf.Clamp(distance, 0f, _track.TotalLength);
            float diff = target - _distance;
            if (!_hasRemote || Mathf.Abs(diff) > SnapDistance)
            {
                _hasRemote = true;
                _correction = 0f;
                _distance = target;
                Apply();
                return;
            }
            _correction = diff;
        }

        private void Apply()
        {
            Place(transform, _distance, _heightOffset);
            for (int i = 0; i < _cars.Count; i++)
            {
                if (_cars[i] == null) continue;
                Place(_cars[i], Mathf.Max(0f, _distance - _carOffsets[i]), _carHeights[i]);
            }
        }

        private void Place(Transform target, float s, float heightOffset)
        {
            Vector2 p = _track.SampleXZ(s);
            Vector3 forward = _track.SampleForward(s);
            target.SetPositionAndRotation(
                new Vector3(p.x, _track.BedHeight(s) + heightOffset, p.y),
                Quaternion.LookRotation(forward) * Quaternion.Euler(0f, _yawOffset, 0f));
        }
    }
}
