using System.Collections.Generic;
using FPSGame.Attribute;
using UnityEngine;

namespace FpsGame.MapUtils
{
    /// <summary>
    /// 地形细节（草 / 花）擦除器。
    /// <para>把指定区域内的 Detail 层整片清零——爆炸挖坑、附加地形覆盖时调用，</para>
    /// <para>避免"地形被挖掉/被替换后，草还悬在空中"。</para>
    /// <para>与 <see cref="TreeDestructor"/> 同款"入队 + 按帧合并提交"，由</para>
    /// <para><see cref="GenerateNoiseTerrain"/> 在生成地形后自动创建，无需手动挂载。</para>
    /// </summary>
    [AddComponentMenu("地形/细节擦除", 41)]
    public class TerrainDetailEraser : MonoBehaviour
    {
        /// <summary>一次擦除请求：halfZ &lt; 0 表示圆形（halfX 当半径用），否则为矩形</summary>
        private struct ClearRequest
        {
            public Vector3 Center;
            public float HalfX;
            public float HalfZ;
        }

        #region 单例

        /// <summary>当前场景的实例（地形生成时自动创建）</summary>
        public static TerrainDetailEraser Instance { get; private set; }

        #endregion

        #region Inspector

        [Header("参数")]
        [InspectorName("每秒最多提交次数（0=每帧提交）")]
        [SerializeField] private float _maxCommitPerSecond = 8f;

        [InspectorName("打印擦除日志")]
        [SerializeField] private bool _log = false;

        [Header("运行时状态（只读）")]
        [InspectorName("累计擦除的细节格数")]
        [SerializeField] private int _clearedCells;

        #endregion

        #region 运行时数据

        private Terrain _terrain;
        private TerrainData _data;

        /// <summary>待处理的擦除请求</summary>
        private readonly List<ClearRequest> _pending = new();

        private float _lastCommitTime;

        #endregion

        #region 静态 API

        /// <summary>累计擦除的细节格数</summary>
        public static int ClearedCells => Instance != null ? Instance._clearedCells : 0;

        /// <summary>
        /// 为指定地形创建/复用擦除器并缓存地形数据。
        /// <para>由 <see cref="GenerateNoiseTerrain"/> 在生成地形后调用。</para>
        /// </summary>
        /// <param name="terrain">目标地形，为空时直接返回</param>
        /// <returns>擦除器实例</returns>
        public static TerrainDetailEraser Rebuild(Terrain terrain)
        {
            if (terrain == null) return null;
            if (!terrain.TryGetComponent(out TerrainDetailEraser eraser))
            {
                eraser = terrain.gameObject.AddComponent<TerrainDetailEraser>();
            }

            eraser._terrain = terrain;
            eraser._data = terrain.terrainData;
            eraser._pending.Clear();
            eraser._clearedCells = 0;
            eraser._lastCommitTime = Time.time;
            return eraser;
        }

        /// <summary>
        /// 清除以 <paramref name="center"/> 为圆心、水平半径 <paramref name="radius"/> 内的细节（草/花）。
        /// <para>只入队，实际改动由 <see cref="LateUpdate"/> 合并提交。</para>
        /// </summary>
        /// <param name="center">中心点（世界坐标）</param>
        /// <param name="radius">水平半径（米）</param>
        /// <returns>本次入队影响的细节格数上限（调试用；实际擦除数见 Inspector 累计值）</returns>
        public static int ClearInRadius(Vector3 center, float radius)
            => Instance != null ? Instance.Enqueue(center, radius, -1f) : 0;

        /// <summary>
        /// 清除轴对齐矩形区域（XZ）内的细节（草/花）。
        /// <para>用于"附加地形"这类方块覆盖区，避免用外接圆多擦掉区域外的草。</para>
        /// </summary>
        /// <param name="center">中心点（世界坐标）</param>
        /// <param name="halfSize">半尺寸（x = X 方向半宽，y = Z 方向半宽）</param>
        /// <returns>本次入队影响的细节格数上限（调试用）</returns>
        public static int ClearInRectXZ(Vector3 center, Vector2 halfSize)
            => Instance != null ? Instance.Enqueue(center, halfSize.x, halfSize.y) : 0;

        /// <summary>立即提交所有待处理的擦除（平时无需调用，LateUpdate 会自动提交）</summary>
        public static void Flush()
        {
            if (Instance != null) Instance.FlushPending(true);
        }

        #endregion

        #region 生命周期

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            FlushPending(false);
        }

        #endregion

        #region 内部实现

        private int Enqueue(Vector3 center, float halfX, float halfZ)
        {
            if (halfX <= 0f) return 0;

            _pending.Add(new ClearRequest { Center = center, HalfX = halfX, HalfZ = halfZ });

            // 返回"矩形范围 × 层数"的粗略上限，仅供日志/调试参考
            if (!ResolveTerrain()) return 0;
            int cells = Mathf.CeilToInt(halfX * 2f) * Mathf.CeilToInt((halfZ < 0f ? halfX : halfZ) * 2f);
            return cells * _data.detailPrototypes.Length;
        }

        private bool ResolveTerrain()
        {
            if (_terrain == null)
            {
                _terrain = TerrainUtils.Main != null ? TerrainUtils.Main : Terrain.activeTerrain;
                _data = _terrain != null ? _terrain.terrainData : null;
            }
            return _terrain != null && _data != null;
        }

        /// <summary>合并提交：把待处理区域内的 Detail 层整片清零</summary>
        private void FlushPending(bool force)
        {
            if (_pending.Count == 0) return;
            if (!force && _maxCommitPerSecond > 0f && Time.time - _lastCommitTime < 1f / _maxCommitPerSecond) return;

            if (!ResolveTerrain())
            {
                _pending.Clear();
                return;
            }

            int detailW = _data.detailWidth;
            int detailH = _data.detailHeight;
            int layerCount = _data.detailPrototypes.Length;
            Vector3 origin = _terrain.transform.position;
            Vector3 size = _data.size;
            if (detailW <= 0 || detailH <= 0 || layerCount == 0 || size.x <= 0f || size.z <= 0f)
            {
                _pending.Clear();
                return;
            }

            float cellsPerMeterX = detailW / size.x;
            float cellsPerMeterZ = detailH / size.z;
            int clearedThisTime = 0;

            for (int r = 0; r < _pending.Count; r++)
            {
                ClearRequest req = _pending[r];
                bool circle = req.HalfZ < 0f;
                float halfX = Mathf.Max(req.HalfX, 0.5f);
                float halfZ = circle ? halfX : Mathf.Max(req.HalfZ, 0.5f);

                // 世界坐标 → 细节格坐标（Terrain 的 position 是角点）
                int cx = Mathf.RoundToInt((req.Center.x - origin.x) * cellsPerMeterX);
                int cy = Mathf.RoundToInt((req.Center.z - origin.z) * cellsPerMeterZ);
                int rx = Mathf.CeilToInt(halfX * cellsPerMeterX) + 1;
                int rz = Mathf.CeilToInt(halfZ * cellsPerMeterZ) + 1;

                int xBase = Mathf.Clamp(cx - rx, 0, detailW - 1);
                int yBase = Mathf.Clamp(cy - rz, 0, detailH - 1);
                int w = Mathf.Clamp(cx + rx, xBase, detailW - 1) - xBase + 1;
                int h = Mathf.Clamp(cy + rz, yBase, detailH - 1) - yBase + 1;
                if (w <= 0 || h <= 0) continue;

                float radiusSqr = circle ? req.HalfX * req.HalfX : 0f;

                for (int layer = 0; layer < layerCount; layer++)
                {
                    int[,] map = _data.GetDetailLayer(xBase, yBase, w, h, layer);
                    int changed = 0;

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            if (map[y, x] == 0) continue;

                            if (circle)
                            {
                                // 用格中心反算世界坐标做圆判定，坑边更自然
                                float wx = origin.x + (xBase + x + 0.5f) / cellsPerMeterX;
                                float wz = origin.z + (yBase + y + 0.5f) / cellsPerMeterZ;
                                float dx = wx - req.Center.x;
                                float dz = wz - req.Center.z;
                                if (dx * dx + dz * dz > radiusSqr) continue;
                            }

                            map[y, x] = 0;
                            changed++;
                        }
                    }

                    if (changed == 0) continue;

                    _data.SetDetailLayer(xBase, yBase, layer, map);
                    clearedThisTime += changed;
                }
            }

            _pending.Clear();
            _lastCommitTime = Time.time;

            if (clearedThisTime > 0)
            {
                _clearedCells += clearedThisTime;
                if (_log) Debug.Log($"[细节] 擦除 {clearedThisTime} 格（累计 {_clearedCells}）");
            }
        }

        #endregion
    }
}
