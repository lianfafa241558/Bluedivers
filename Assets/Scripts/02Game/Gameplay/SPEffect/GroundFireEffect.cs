using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 地面火焰持续效果。激活时与附近同类型效果做重叠去重：以自身中心为圆心、
    /// 基类 DamageData 的 ExplosionRange 为半径检测，若该范围内已存在其他 GroundFireEffect，
    /// 则停止自身粒子系统（视为隐藏）而不登记入缓存，避免重复堆叠。为避免每次激活遍历全部缓存实例，
    /// 使用基于网格的空间分区，仅查询当前及相邻分区内的对象。
    /// </summary>
    [AddComponentMenu("持续效果/地面火焰")]
    public class GroundFireEffect : SustainedEffect
    {
        /// <summary>网格单元尺寸。建议略大于常见 ExplosionRange，使大多数查询只需扫描 3x3 分区。</summary>
        const float CellSize = 10f;
        const float InverseCellSize = 1f / CellSize;

        /// <summary>空间分区网格：键为打包后的单元格坐标，值为该格内未隐藏的实例列表。</summary>
        static readonly Dictionary<long, List<GroundFireEffect>> s_Grid = new();

        /// <summary>当前未隐藏（粒子系统正在播放）的实例数量。</summary>
        static int s_ActiveCount;

        /// <summary>获取当前未隐藏的实例数量。</summary>
        public static int ActiveCount => s_ActiveCount;

        ParticleSystem _particleSystem;
        Vector3 _cachedCenter;
        long _cellKey;
        bool _registered;

        void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Activate();
        }

        void OnDisable()
        {
            Unregister();
        }

        /// <summary>
        /// 激活：先查询当前及相邻分区内是否已有圆心落入自身检测半径的同类型对象，
        /// 若存在则停止自身粒子系统（视为隐藏）且不登记入缓存；否则登记入缓存。
        /// </summary>
        void Activate()
        {
            // 假设地面火焰为静止对象，激活时缓存中心即可。
            _cachedCenter = DamageAnchor ? DamageAnchor.position : transform.position;

            int cx = Mathf.FloorToInt(_cachedCenter.x * InverseCellSize);
            int cy = Mathf.FloorToInt(_cachedCenter.z * InverseCellSize);
            _cellKey = PackCell(cx, cy);

            float range = GetExplosionRange();

            // 先查后登记：避免把自身计入查询结果，也避免无效实例污染缓存。
            if (HasNearbyInstance(cx, cy, range))
            {
                StopAndHide();
                return;
            }

            Register();
        }

        /// <summary>
        /// 在以 (centerX, centerY) 为中心、覆盖检测半径所需格数范围内，
        /// 查询是否存在圆心落入自身检测半径的其他 GroundFireEffect。
        /// 顺带清理已销毁的无效引用。
        /// </summary>
        bool HasNearbyInstance(int centerX, int centerY, float range)
        {
            if (range <= 0f) return false;

            // 覆盖检测圆所需扫描的格数：floor(range/CellSize)+1 保证不遗漏边缘格内的对象。
            int cellRadius = Mathf.FloorToInt(range * InverseCellSize) + 1;
            float rangeSqr = range * range;
            Vector3 myCenter = _cachedCenter;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    if (!s_Grid.TryGetValue(PackCell(centerX + dx, centerY + dy), out var cell))
                        continue;

                    // 倒序遍历：便于就地清理无效引用。
                    for (int i = cell.Count - 1; i >= 0; i--)
                    {
                        GroundFireEffect other = cell[i];
                        if (!other)
                        {
                            cell.RemoveAt(i);
                            continue;
                        }
                        if (ReferenceEquals(other, this)) continue;

                        Vector3 offset = other._cachedCenter - myCenter;
                        if (offset.sqrMagnitude <= rangeSqr)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>停止自身粒子系统并从缓存移除（视为隐藏）。</summary>
        void StopAndHide()
        {
            if (_particleSystem)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            Unregister();
        }

        /// <summary>将自身加入当前所在单元格。</summary>
        void Register()
        {
            if (_registered) return;
            int cx = Mathf.FloorToInt(_cachedCenter.x * InverseCellSize);
            int cy = Mathf.FloorToInt(_cachedCenter.z * InverseCellSize);
            _cellKey = PackCell(cx, cy);

            if (!s_Grid.TryGetValue(_cellKey, out var cell))
            {
                cell = new List<GroundFireEffect>();
                s_Grid[_cellKey] = cell;
            }
            cell.Add(this);
            _registered = true;
            s_ActiveCount++;
        }

        /// <summary>将自身从所在单元格移除。</summary>
        void Unregister()
        {
            if (!_registered) return;
            if (s_Grid.TryGetValue(_cellKey, out var cell))
            {
                cell.Remove(this);
                if (cell.Count == 0) s_Grid.Remove(_cellKey);
            }
            _registered = false;
            s_ActiveCount--;
        }

        /// <summary>获取检测半径（伤害外半径）。</summary>
        float GetExplosionRange()
        {
            if (DamageData != null && DamageData.IsValid())
            {
                return DamageData.GetDamageOuterRadius(1).RawFloat;
            }
            return 0f;
        }

        /// <summary>将单元格坐标打包为字典键。</summary>
        static long PackCell(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }
    }
}
