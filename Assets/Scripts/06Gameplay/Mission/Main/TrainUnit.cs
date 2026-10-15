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

        private RailTrack _track;
        private float _speed = 8f;
        private float _distance;
        private float _heightOffset;
        private float _yawOffset;
        private bool _arrived;

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
        public void Init(RailTrack track, float speed, float heightOffset, float yawOffset,
            List<Transform> cars, List<float> carOffsets, List<float> carHeightOffsets)
        {
            _track = track;
            _speed = Mathf.Max(0.1f, speed);
            _heightOffset = heightOffset;
            _yawOffset = yawOffset;
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
            _distance += _speed * Time.deltaTime;
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
