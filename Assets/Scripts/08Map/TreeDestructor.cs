using System;
using System.Collections.Generic;
using System.Text;
using FPSGame.Attribute;
using UnityEngine;

namespace FpsGame.MapUtils
{
    /// <summary>
    /// 地形树销毁管理器（A 方案）。
    /// <para>不改树原型、不改树的位置与索引，只把被摧毁的树实例缩放置 0（视觉消失），</para>
    /// <para>并把一帧内的多次改动合并成一次 <see cref="TerrainData.SetTreeInstances"/> 提交，
    /// 避免"炸一棵重建一次地形树数据"。</para>
    /// <para>命中判定不依赖物理：地形树没有碰撞体，这里用生成时记录的"索引 → 世界坐标"表做圆柱相交，</para>
    /// <para>爆炸/穿甲弹都可以直接调 <see cref="DestroyInRadius"/> / <see cref="RaycastTree"/>。</para>
    /// <para>本组件由 <see cref="GenerateNoiseTerrain"/> 在写入树实例后自动创建并重建索引，无需手动挂载。</para>
    /// </summary>
    [AddComponentMenu("地形/树木销毁管理", 40)]
    public class TreeDestructor : MonoBehaviour
    {
        #region 单例

        /// <summary>当前场景的实例（地形生成时自动创建；换局重生成后由 Rebuild 刷新内部数据）</summary>
        public static TreeDestructor Instance { get; private set; }

        #endregion

        #region Inspector

        [Header("参数")]
        [InspectorName("树默认高度（原型读取失败时使用）")]
        [SerializeField] private float _defaultTreeHeight = 8f;

        [InspectorName("树默认半径（原型读取失败时使用）")]
        [SerializeField] private float _defaultTreeRadius = 0.5f;

        [InspectorName("每秒最多提交次数（0=每帧提交）")]
        [SerializeField] private float _maxCommitPerSecond = 4f;

        [InspectorName("打印销毁日志")]
        [SerializeField] private bool _log = false;

        [Header("运行时状态（只读）")]
        [InspectorName("树总数")]
        [SerializeField] private int _treeCount;

        [InspectorName("已摧毁数量")]
        [SerializeField] private int _destroyedCount;

        [InspectorName("树种统计（索引×数量+可毁/免疫）")]
        [SerializeField] private string _prototypeSummary;

        #endregion

        #region 运行时数据

        private Terrain _terrain;
        private TerrainData _data;

        /// <summary>树的完整实例数组，顺序与 TerrainData 内一致（提交时整表回写，索引不变）</summary>
        private TreeInstance[] _instances;

        /// <summary>每棵树根部的世界坐标</summary>
        private Vector3[] _worldPos;

        /// <summary>每棵树的世界高度（已乘 heightScale）</summary>
        private float[] _treeHeight;

        /// <summary>每棵树的世界半径（已乘 widthScale）</summary>
        private float[] _treeRadius;

        /// <summary>原始缩放，供 RestoreAll 还原</summary>
        private float[] _originalHeightScale;
        private float[] _originalWidthScale;

        /// <summary>是否存活（false = 已被摧毁）</summary>
        private bool[] _alive;

        /// <summary>是否已进入待提交队列（防止重复入队）</summary>
        private bool[] _queued;

        /// <summary>每棵树的树种索引（<see cref="TreeInstance.prototypeIndex"/> 的快照）</summary>
        private int[] _treePrototype;

        /// <summary>每棵树是否允许被摧毁（按树种白名单预先算好，避免每次爆炸都查集合）</summary>
        private bool[] _destructible;

        /// <summary>允许被摧毁的树种索引；为空 = 全部树种都可摧毁</summary>
        private readonly HashSet<int> _destructiblePrototypes = new();

        /// <summary>待提交的树索引</summary>
        private readonly List<int> _pending = new();

        /// <summary>树原型尺寸缓存：x = 半径，y = 高度</summary>
        private readonly Dictionary<int, Vector2> _prototypeSize = new();

        private float _lastCommitTime;
        private bool _dirty;

        #endregion

        #region 静态 API

        /// <summary>当前地形的树总数（未初始化时为 0）</summary>
        public static int TreeCount => Instance != null ? Instance._treeCount : 0;

        /// <summary>已摧毁的树数量</summary>
        public static int DestroyedCount => Instance != null ? Instance._destroyedCount : 0;

        /// <summary>指定树种是否允许被摧毁（0 基索引；无管理器时视为允许）</summary>
        /// <param name="prototypeIndex">树种索引，对应 <c>TerrainData.treePrototypes</c> 下标</param>
        public static bool IsPrototypeDestructible(int prototypeIndex)
            => Instance == null || Instance.IsDestructiblePrototypeInternal(prototypeIndex);

        /// <summary>
        /// 为指定地形创建/复用管理器并重建树索引表。
        /// <para>由 <see cref="GenerateNoiseTerrain"/> 在 <c>SetTreeInstances</c> 之后调用。</para>
        /// </summary>
        /// <param name="terrain">目标地形，为空时直接返回</param>
        /// <param name="destructiblePrototypeIndexes">
        /// 允许被摧毁的树种索引（0 基，对应 <c>TerrainData.treePrototypes</c> 下标）；
        /// 传 null 或空集合 = 全部树种都可摧毁。
        /// </param>
        /// <returns>管理器实例</returns>
        public static TreeDestructor Rebuild(Terrain terrain, IList<int> destructiblePrototypeIndexes = null)
        {
            if (terrain == null) return null;
            if (!terrain.TryGetComponent(out TreeDestructor destructor))
            {
                destructor = terrain.gameObject.AddComponent<TreeDestructor>();
            }
            destructor.SetDestructiblePrototypesInternal(destructiblePrototypeIndexes);
            destructor.RebuildInternal();
            return destructor;
        }

        /// <summary>
        /// 运行时修改"可被摧毁的树种白名单"（0 基索引；不传参数 = 恢复为全部可摧毁）。
        /// <para>只重算每棵树的可毁标记，不需要重建地形。</para>
        /// </summary>
        /// <param name="prototypeIndexes">允许被摧毁的树种索引</param>
        public static void SetDestructiblePrototypes(params int[] prototypeIndexes)
        {
            if (Instance == null) return;
            Instance.SetDestructiblePrototypesInternal(prototypeIndexes);
        }

        /// <summary>
        /// 摧毁以 <paramref name="center"/> 为圆心、水平半径 <paramref name="radius"/> 内的所有存活树。
        /// <para>只入队，实际数据改动由 <see cref="LateUpdate"/> 合并提交。</para>
        /// </summary>
        /// <param name="center">爆心/中心点</param>
        /// <param name="radius">水平半径（米）</param>
        /// <param name="yTolerance">Y 轴容差，&lt;=0 时忽略高度差</param>
        /// <returns>本次入队的树数量</returns>
        public static int DestroyInRadius(Vector3 center, float radius, float yTolerance = 0f)
            => Instance != null ? Instance.DestroyInRadiusInternal(center, radius, yTolerance) : 0;

        /// <summary>
        /// 清除区域内指定原型区间的植被（**不看"可被摧毁"白名单**）。
        /// <para>用于地形被挖坑 / 被附加地形覆盖时，把区域内该类植被整片清掉——</para>
        /// <para>例如石块 0-6 保留、树 7-10 清除：<c>ClearInRadius(pos, r, 7, 10)</c>。</para>
        /// </summary>
        /// <param name="center">中心点（世界坐标）</param>
        /// <param name="radius">水平半径（米）</param>
        /// <param name="prototypeMin">原型索引下限（含），传 -1 表示不限</param>
        /// <param name="prototypeMax">原型索引上限（含），传 -1 表示不限</param>
        /// <param name="yTolerance">Y 轴容差，&lt;=0 时忽略高度差</param>
        /// <returns>本次入队的数量</returns>
        public static int ClearInRadius(Vector3 center, float radius, int prototypeMin, int prototypeMax,
            float yTolerance = 0f)
            => Instance != null ? Instance.QueueInRadius(center, radius, yTolerance, prototypeMin, prototypeMax) : 0;

        /// <summary>
        /// 清除轴对齐矩形区域（XZ）内指定原型区间的植被（**不看"可被摧毁"白名单**）。
        /// </summary>
        /// <param name="center">中心点（世界坐标）</param>
        /// <param name="halfSize">半尺寸（x = X 方向半宽，y = Z 方向半宽）</param>
        /// <param name="prototypeMin">原型索引下限（含），传 -1 表示不限</param>
        /// <param name="prototypeMax">原型索引上限（含），传 -1 表示不限</param>
        /// <param name="yTolerance">Y 轴容差，&lt;=0 时忽略高度差（避免清掉区域正上方悬崖上的树时可传正值）</param>
        /// <returns>本次入队的数量</returns>
        public static int ClearInRectXZ(Vector3 center, Vector2 halfSize, int prototypeMin, int prototypeMax,
            float yTolerance = 0f)
            => Instance != null
                ? Instance.QueueInRectXZ(center, halfSize, prototypeMin, prototypeMax, yTolerance) : 0;

        /// <summary>
        /// 射线与树做圆柱相交（因地形树无碰撞体，需自行调用）。
        /// <para>子弹/炮弹希望"打中树"时，把本方法与物理射线结果比较距离取更近者。</para>
        /// </summary>
        /// <param name="ray">世界空间射线，方向会被归一化</param>
        /// <param name="maxDistance">最大距离</param>
        /// <param name="hitPoint">命中点</param>
        /// <param name="onlyDestructible">true = 只命中白名单内的树种；false = 所有树都算阻挡</param>
        /// <returns>是否命中</returns>
        public static bool RaycastTree(Ray ray, float maxDistance, out Vector3 hitPoint, bool onlyDestructible = true)
        {
            hitPoint = default;
            return Instance != null && Instance.RaycastTreeInternal(ray, maxDistance, out hitPoint, onlyDestructible);
        }

        /// <summary>立即提交所有待处理的销毁（平时无需调用，LateUpdate 会自动提交）</summary>
        public static void Flush()
        {
            if (Instance != null) Instance.FlushPending(true);
        }

        /// <summary>摧毁当前地形的全部树（调试用）</summary>
        /// <param name="includeProtected">是否连"免疫树种"一起摧毁</param>
        /// <returns>摧毁数量</returns>
        public static int DestroyAll(bool includeProtected = false)
            => Instance != null ? Instance.DestroyAllInternal(includeProtected) : 0;

        /// <summary>还原所有被摧毁的树（调试用，依赖原始缩放缓存）</summary>
        public static void RestoreAll()
        {
            if (Instance != null) Instance.RestoreAllInternal();
        }

        /// <summary>收集所有存活树的世界坐标（供生成/AI 规避使用）</summary>
        /// <param name="result">结果容器，调用前会先清空</param>
        /// <returns>实际写入数量</returns>
        public static int GetAliveTreePositions(List<Vector3> result)
        {
            if (result == null) return 0;
            result.Clear();
            if (Instance?.ApplyAlivePositions(result) != true) return 0;
            return result.Count;
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

        private void OnDrawGizmosSelected()
        {
            if (_worldPos == null || _alive == null) return;
            Gizmos.color = Color.green;
            for (int i = 0; i < _treeCount; i++)
            {
                if (!_alive[i]) continue;
                Vector3 center = _worldPos[i] + Vector3.up * (_treeHeight[i] * 0.5f);
                Gizmos.DrawWireSphere(center, Mathf.Max(_treeRadius[i], 0.3f));
            }
        }

        #endregion

        #region 内部实现

        /// <summary>重建索引表（换局、地形重生后必须调用一次）</summary>
        private void RebuildInternal()
        {
            _terrain = GetComponent<Terrain>();
            _pending.Clear();
            _prototypeSize.Clear();
            _destroyedCount = 0;
            _dirty = false;
            _lastCommitTime = Time.time;

            if (_terrain == null)
            {
                _data = null;
                _treeCount = 0;
                Debug.LogWarning("[树木] TreeDestructor 挂了但没有 Terrain 组件", this);
                return;
            }

            _data = _terrain.terrainData;
            if (_data == null)
            {
                _treeCount = 0;
                return;
            }

            // 整表拷贝一次：之后所有提交都基于这份数组，不改顺序 => 索引稳定
            _instances = _data.treeInstances;
            _treeCount = _instances.Length;

            _worldPos = new Vector3[_treeCount];
            _treeHeight = new float[_treeCount];
            _treeRadius = new float[_treeCount];
            _originalHeightScale = new float[_treeCount];
            _originalWidthScale = new float[_treeCount];
            _alive = new bool[_treeCount];
            _queued = new bool[_treeCount];
            _treePrototype = new int[_treeCount];
            _destructible = new bool[_treeCount];

            for (int i = 0; i < _treeCount; i++)
            {
                TreeInstance tree = _instances[i];
                Vector2 protoSize = GetPrototypeSize(tree.prototypeIndex);

                _treePrototype[i] = tree.prototypeIndex;
                _worldPos[i] = LocalToWorld(tree.position);
                _treeHeight[i] = protoSize.y * tree.heightScale;
                _treeRadius[i] = protoSize.x * tree.widthScale;
                _originalHeightScale[i] = tree.heightScale;
                _originalWidthScale[i] = tree.widthScale;
                _alive[i] = true;
            }

            RefreshDestructibleFlags();
            RefreshPrototypeSummary();
        }

        /// <summary>读树原型的尺寸（半径/高度），失败时退回 Inspector 配置的默认值</summary>
        private Vector2 GetPrototypeSize(int prototypeIndex)
        {
            if (_prototypeSize.TryGetValue(prototypeIndex, out Vector2 cached)) return cached;

            Vector2 size = new(_defaultTreeRadius, _defaultTreeHeight);
            TreePrototype[] prototypes = _data != null ? _data.treePrototypes : null;
            if (prototypes != null && prototypeIndex >= 0 && prototypeIndex < prototypes.Length)
            {
                GameObject prefab = prototypes[prototypeIndex].prefab;
                if (prefab != null)
                {
                    try
                    {
                        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                        bool hasBounds = false;
                        Bounds bounds = default;
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            if (renderers[i] == null) continue;
                            if (!hasBounds)
                            {
                                bounds = renderers[i].bounds;
                                hasBounds = true;
                            }
                            else
                            {
                                bounds.Encapsulate(renderers[i].bounds);
                            }
                        }
                        if (hasBounds && bounds.size.y > 0.01f)
                        {
                            size = new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.size.y);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[树木] 读取树原型尺寸失败，改用默认尺寸：" + e.Message, this);
                    }
                }
            }

            _prototypeSize[prototypeIndex] = size;
            return size;
        }

        /// <summary>树实例的归一化坐标 → 世界坐标</summary>
        private Vector3 LocalToWorld(Vector3 normalizedPos)
        {
            Vector3 origin = _terrain.transform.position;
            Vector3 size = _data.size;
            return new Vector3(
                origin.x + normalizedPos.x * size.x,
                origin.y + normalizedPos.y * size.y,
                origin.z + normalizedPos.z * size.z);
        }

        private int DestroyInRadiusInternal(Vector3 center, float radius, float yTolerance)
            => QueueInRadius(center, radius, yTolerance, -1, -1);

        /// <summary>
        /// 入队：水平半径内的存活植被。
        /// <para>指定了原型区间（&gt;=0）= "清除"语义，忽略"可被摧毁"白名单；</para>
        /// <para>未指定（-1）= "摧毁"语义，只处理白名单内的树种。</para>
        /// </summary>
        private int QueueInRadius(Vector3 center, float radius, float yTolerance, int protoMin, int protoMax)
        {
            if (_alive == null || _treeCount == 0 || radius <= 0f) return 0;

            float sqrRadius = radius * radius;
            int count = 0;
            for (int i = 0; i < _treeCount; i++)
            {
                if (!IsQueueCandidate(i, protoMin, protoMax)) continue;

                Vector3 pos = _worldPos[i];
                float dx = pos.x - center.x;
                float dz = pos.z - center.z;
                if (dx * dx + dz * dz > sqrRadius) continue;
                if (yTolerance > 0f && Mathf.Abs(pos.y - center.y) > yTolerance) continue;

                QueueTree(i);
                count++;
            }

            if (count > 0) _dirty = true;
            return count;
        }

        /// <summary>入队：轴对齐矩形区域（XZ）内的存活植被；原型区间语义同 <see cref="QueueInRadius"/></summary>
        private int QueueInRectXZ(Vector3 center, Vector2 halfSize, int protoMin, int protoMax, float yTolerance)
        {
            if (_alive == null || _treeCount == 0) return 0;
            if (halfSize.x <= 0f || halfSize.y <= 0f) return 0;

            int count = 0;
            for (int i = 0; i < _treeCount; i++)
            {
                if (!IsQueueCandidate(i, protoMin, protoMax)) continue;

                Vector3 pos = _worldPos[i];
                if (Mathf.Abs(pos.x - center.x) > halfSize.x) continue;
                if (Mathf.Abs(pos.z - center.z) > halfSize.y) continue;
                if (yTolerance > 0f && Mathf.Abs(pos.y - center.y) > yTolerance) continue;

                QueueTree(i);
                count++;
            }

            if (count > 0) _dirty = true;
            return count;
        }

        /// <summary>能否入队：存活、未入队，且满足原型区间（或"可被摧毁"白名单）规则</summary>
        private bool IsQueueCandidate(int index, int protoMin, int protoMax)
        {
            if (!_alive[index] || _queued[index]) return false;

            // 没指定原型区间 => 走"可被摧毁"白名单（爆炸伤害的正常路径）
            if (protoMin < 0 && protoMax < 0) return _destructible[index];

            int proto = _treePrototype[index];
            if (protoMin >= 0 && proto < protoMin) return false;
            if (protoMax >= 0 && proto > protoMax) return false;
            return true;
        }

        private void QueueTree(int index)
        {
            _queued[index] = true;
            _pending.Add(index);
        }

        /// <summary>射线 vs 树（圆柱近似：XZ 圆 + Y 高度段）</summary>
        private bool RaycastTreeInternal(Ray ray, float maxDistance, out Vector3 hitPoint, bool onlyDestructible)
        {
            hitPoint = default;
            if (_alive == null || _treeCount == 0 || maxDistance <= 0f) return false;

            Vector3 direction = ray.direction.normalized;
            Vector2 origin2D = new(ray.origin.x, ray.origin.z);
            Vector2 dir2D = new(direction.x, direction.z);

            bool hit = false;
            float nearest = float.MaxValue;

            for (int i = 0; i < _treeCount; i++)
            {
                if (!_alive[i]) continue;
                if (onlyDestructible && !_destructible[i]) continue;

                Vector3 pos = _worldPos[i];
                float radius = _treeRadius[i];
                if (radius <= 0f) continue;

                Vector2 center2D = new(pos.x, pos.z);
                Vector2 oc = origin2D - center2D;

                float a = Vector2.Dot(dir2D, dir2D);
                if (a < 1e-6f) continue; // 垂直射线，不参与（树的碰撞体近似为竖直圆柱）

                float b = 2f * Vector2.Dot(oc, dir2D);
                float c = Vector2.Dot(oc, oc) - radius * radius;
                float disc = b * b - 4f * a * c;
                if (disc < 0f) continue;

                float sqrt = Mathf.Sqrt(disc);
                float t = (-b - sqrt) / (2f * a);
                if (t < 0f) t = (-b + sqrt) / (2f * a);
                if (t < 0f || t > maxDistance || t >= nearest) continue;

                // 命中点高度必须落在树的高度段内
                float y = ray.origin.y + direction.y * t;
                if (y < pos.y - 0.5f || y > pos.y + _treeHeight[i]) continue;

                nearest = t;
                hit = true;
            }

            if (!hit) return false;

            hitPoint = ray.origin + direction * nearest;
            return true;
        }

        /// <summary>合并提交：一帧内所有销毁只重建一次地形树数据</summary>
        private void FlushPending(bool force)
        {
            if (!_dirty || _pending.Count == 0) return;
            if (!force && _maxCommitPerSecond > 0f && Time.time - _lastCommitTime < 1f / _maxCommitPerSecond) return;

            if (_data == null)
            {
                _pending.Clear();
                _dirty = false;
                return;
            }

            for (int p = 0; p < _pending.Count; p++)
            {
                int i = _pending[p];
                if (i < 0 || i >= _treeCount || !_alive[i]) continue;

                TreeInstance tree = _instances[i];
                // ⚠ 不能改 position / prototypeIndex，否则 SetTreeInstance 会抛 ArgumentException；
                //   缩放置 0 => 视觉消失，且索引不变、可还原
                tree.heightScale = 0f;
                tree.widthScale = 0f;
                _instances[i] = tree;
                _alive[i] = false;
                _destroyedCount++;
            }

            _pending.Clear();
            _dirty = false;
            _lastCommitTime = Time.time;

            // 一次整表提交；snapToHeightmap = false，避免被弹坑改过的地形把树重新贴一遍
            _data.SetTreeInstances(_instances, false);
            _terrain.Flush();

            if (_log) Debug.Log($"[树木] 已摧毁 {_destroyedCount}/{_treeCount}");
        }

        private int DestroyAllInternal(bool includeProtected)
        {
            if (_alive == null) return 0;

            int count = 0;
            for (int i = 0; i < _treeCount; i++)
            {
                if (!_alive[i] || _queued[i]) continue;
                if (!includeProtected && !_destructible[i]) continue;
                _queued[i] = true;
                _pending.Add(i);
                count++;
            }

            if (count > 0)
            {
                _dirty = true;
                FlushPending(true);
            }
            return count;
        }

        private void RestoreAllInternal()
        {
            if (_data == null || _treeCount == 0) return;
            if (!_dirty && _destroyedCount == 0) return;

            for (int i = 0; i < _treeCount; i++)
            {
                if (_alive[i]) continue;

                TreeInstance tree = _instances[i];
                tree.heightScale = _originalHeightScale[i];
                tree.widthScale = _originalWidthScale[i];
                _instances[i] = tree;
                _alive[i] = true;
                _queued[i] = false;
            }

            _destroyedCount = 0;
            _pending.Clear();
            _dirty = false;
            _lastCommitTime = Time.time;

            _data.SetTreeInstances(_instances, false);
            _terrain.Flush();
        }

        /// <summary>设置白名单（null/空 = 全部可摧毁），并即时重算</summary>
        private void SetDestructiblePrototypesInternal(IList<int> prototypeIndexes)
        {
            _destructiblePrototypes.Clear();
            if (prototypeIndexes != null)
            {
                for (int i = 0; i < prototypeIndexes.Count; i++)
                {
                    _destructiblePrototypes.Add(prototypeIndexes[i]);
                }
            }

            RefreshDestructibleFlags();
            RefreshPrototypeSummary();
        }

        /// <summary>指定树种是否在白名单内（白名单为空 = 全放行）</summary>
        private bool IsDestructiblePrototypeInternal(int prototypeIndex)
            => _destructiblePrototypes.Count == 0 || _destructiblePrototypes.Contains(prototypeIndex);

        /// <summary>按白名单重算每棵树的可摧毁标记</summary>
        private void RefreshDestructibleFlags()
        {
            if (_destructible == null || _treePrototype == null) return;

            for (int i = 0; i < _treeCount; i++)
            {
                _destructible[i] = IsDestructiblePrototypeInternal(_treePrototype[i]);
            }
        }

        /// <summary>刷新 Inspector 的"树种统计"，用于对照 索引 ↔ 数量 ↔ 可毁/免疫</summary>
        private void RefreshPrototypeSummary()
        {
            TreePrototype[] prototypes = _data != null ? _data.treePrototypes : null;
            int prototypeCount = prototypes != null ? prototypes.Length : 0;
            if (prototypeCount == 0 || _treePrototype == null)
            {
                _prototypeSummary = string.Empty;
                return;
            }

            int[] counts = new int[prototypeCount];
            for (int i = 0; i < _treeCount; i++)
            {
                int index = _treePrototype[i];
                if (index >= 0 && index < prototypeCount) counts[index]++;
            }

            StringBuilder builder = new();
            for (int i = 0; i < prototypeCount; i++)
            {
                if (i > 0) builder.Append("  ");
                builder.Append('[').Append(i).Append("]x").Append(counts[i])
                    .Append(IsDestructiblePrototypeInternal(i) ? " 可毁" : " 免疫");
            }
            _prototypeSummary = builder.ToString();
        }

        private bool ApplyAlivePositions(List<Vector3> result)
        {
            if (_alive == null) return false;
            for (int i = 0; i < _treeCount; i++)
            {
                if (_alive[i]) result.Add(_worldPos[i]);
            }
            return true;
        }

        #endregion
    }
}
