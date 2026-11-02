using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Core.Interface;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FPSGame.Gameplay
{
    /// <summary>区块的三档状态。</summary>
    public enum RenderChunkTier
    {
        /// <summary>激活：正常渲染 + 正常投影</summary>
        Active,
        /// <summary>预备：仍然渲染，但不投阴影（缓冲带，防止在边界上频繁开关）</summary>
        Ready,
        /// <summary>惰性：完全不渲染</summary>
        Lazy,
    }

    /// <summary>
    /// 按区块做**三档距离剔除**：激活 / 预备 / 惰性。
    ///
    /// <para>▍为什么是三档而不是"一刀切"：一刀切只有一条阈值，玩家站在阈值附近来回微动就会
    /// 让整块物体反复开关（闪烁）。三档之间各有一条 <see cref="Hysteresis"/> 滞回带：
    /// 往远处走要"多走一点"才降档，往近处走要"多走一点"才升档 ⇒ 同一块不会在边界抖动。</para>
    ///
    /// <para>▍距离口径：<b>到区块包围盒边缘的距离</b>（不是中心点），所以跨块的物体不会被误判。</para>
    ///
    /// <para>▍⚠ 三条硬规则：
    /// ① 只切 <c>Renderer.forceRenderingOff</c> / <c>shadowCastingMode</c>，**不关 GameObject、也不动 enabled**
    ///    （关 GameObject 会连 AI/碰撞/事件一起停；动 enabled 会触发 OnEnable/OnDisable 搅乱池化）。
    /// ② 包围半径超过 <see cref="ChunkSize"/> 的物体**不参与剔除**，进常驻列表 —— 天空球 / 云层 / 巨型远景
    ///    的包围盒中心可能在几百米外却覆盖整个屏幕（实测场景里有半径 25000 和 7000 的）。
    /// ③ 单位（<see cref="Actor"/> 底下）与 Ground / Unit 层不参与：单位会移动，按出生点分块会闪；
    ///    地面和大悬崖是玩家要远看的。</para>
    /// </summary>
    [AddComponentMenu("性能/区块分层剔除")]
    public class RenderChunkCuller : MonoBehaviour
    {
        /// <summary>场景里那个实例（手动挂到任意常驻物体上；只认一个）。</summary>
        public static RenderChunkCuller Instance { get; private set; }

        [InspectorName("启用")]
        public bool Enabled = true;

        [InspectorName("区块边长(米)")]
        [Min(4f)] public float ChunkSize = 32f;

        [InspectorName("激活距离(米)")]
        [Tooltip("到区块边缘小于它就正常渲染 + 投影")]
        [Min(1f)] public float ActiveDistance = 110f;

        [InspectorName("预备距离(米)")]
        [Tooltip("激活之外、小于它仍渲染但关掉投影；超过它转惰性（完全不渲染）")]
        [Min(2f)] public float ReadyDistance = 150f;

        [InspectorName("滞回(米)")]
        [Tooltip("降档要多走这么远、升档要多走近这么远，避免在边界上反复开关导致闪烁")]
        [Min(0f)] public float Hysteresis = 10f;

        [InspectorName("重算步长(米)：玩家移动多少才重算一次")]
        [Min(1f)] public float MoveThreshold = 16f;

        [InspectorName("重建间隔(秒)：道具会生灭")]
        [Min(1f)] public float RebuildInterval = 30f;

        [InspectorName("重建每帧处理多少个")]
        [Tooltip("重建要扫全场景（2300+ 渲染器 + 每个判一次是不是单位），必须分帧做，否则每 30 秒一次 2~5ms 的尖峰")]
        [Min(32)] public int RebuildBatchSize = 256;

        [InspectorName("用玩家位置判定")]
        [Tooltip("关掉则用 Main Camera 的位置（相机摆动大时更贴合视野，但更新更频繁）")]
        public bool UsePlayerPosition = true;

        private const int GroundLayer = 3;
        private const int UnitLayer = 6;

        /// <summary>区块里的一个渲染器 + 它原本的投影模式（降档时要还原，不能一律设成 On）。</summary>
        private struct ChunkItem
        {
            public Renderer Renderer;
            public ShadowCastingMode Shadow;
        }

        private sealed class Chunk
        {
            public Vector3 Center;
            public float Radius;
            public readonly List<ChunkItem> Items = new();
            public RenderChunkTier Tier = RenderChunkTier.Active;
        }

        private readonly Dictionary<long, Chunk> _chunks = new();
        /// <summary>超大物体（包围半径 &gt; ChunkSize）：永不剔除</summary>
        private readonly List<Renderer> _always = new();

        private Camera _cam;
        private Vector3 _lastPos = new Vector3(float.MaxValue, 0f, 0f);
        private float _nextRebuild;

        // 分帧重建的游标
        private Renderer[] _rebuildList;
        private int _rebuildIndex;
        private bool _rebuilding;

        private void OnEnable()
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResetState();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreAll();
        }

        private void OnValidate()
        {
            if (ReadyDistance < ActiveDistance + 1f) ReadyDistance = ActiveDistance + 1f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 场景换了：旧引用全废，强制重建并从"全可见"重新开始
            ResetState();
        }

        private void ResetState()
        {
            RestoreAll();
            _chunks.Clear();
            _always.Clear();
            _lastPos = new Vector3(float.MaxValue, 0f, 0f);
            _nextRebuild = 0f;
            _cam = null;
        }

        /// <summary>把所有被本组件关掉/改过投影的渲染器还原（切场景 / 禁用组件时）。</summary>
        private void RestoreAll()
        {
            foreach (var kv in _chunks)
            {
                var list = kv.Value.Items;
                for (int i = 0; i < list.Count; ++i)
                {
                    Renderer r = list[i].Renderer;
                    if (r == null) continue;
                    r.forceRenderingOff = false;
                    r.shadowCastingMode = list[i].Shadow;
                }
            }
        }

        private void Update()
        {
            if (!Enabled)
            {
                if (_chunks.Count > 0) { RestoreAll(); _chunks.Clear(); }
                return;
            }

            Vector3 pos;
            if (!TryGetEvalPosition(out pos)) return;

            // 分帧重建：一帧只处理 RebuildBatchSize 个，避免"每 30 秒一次全场景扫描"的尖峰
            if (_rebuilding)
            {
                StepRebuild();
                return;
            }
            if (Time.unscaledTime >= _nextRebuild)
            {
                _nextRebuild = Time.unscaledTime + RebuildInterval;
                BeginRebuild();
                return;
            }

            if ((pos - _lastPos).sqrMagnitude < MoveThreshold * MoveThreshold) return;
            _lastPos = pos;

            Apply(pos);
        }

        /// <summary>判定用的参考点：优先玩家（相机摆动时不会反复触发更新），退化到相机。</summary>
        private bool TryGetEvalPosition(out Vector3 pos)
        {
            if (UsePlayerPosition)
            {
                var player = ActorsManager.Player;
                if (player != null && player.IsValidMono())
                {
                    pos = player.Pos;
                    return true;
                }
            }

            var cam = _cam != null ? _cam : (_cam = Camera.main);
            if (cam == null)
            {
                pos = Vector3.zero;
                return false;
            }
            pos = cam.transform.position;
            return true;
        }

        /// <summary>开始重建：只做"取全场景列表"这一步，真正的分类分帧做（见 <see cref="StepRebuild"/>）。</summary>
        private void BeginRebuild()
        {
            RestoreAll();          // 先把上一轮关掉的全部还原，再清表，否则那些物体会一直不画
            _chunks.Clear();
            _always.Clear();

            _rebuildList = FindObjectsOfType<Renderer>();
            _rebuildIndex = 0;
            _rebuilding = true;
        }

        /// <summary>分帧推进重建：每帧只处理 <see cref="RebuildBatchSize"/> 个。</summary>
        private void StepRebuild()
        {
            int end = Mathf.Min(_rebuildIndex + RebuildBatchSize, _rebuildList.Length);
            for (; _rebuildIndex < end; ++_rebuildIndex)
            {
                Renderer r = _rebuildList[_rebuildIndex];
                // 只认会进主相机裁剪的两种；粒子/线/拖尾不参与（它们自己有 cullingMode）
                if (r is MeshRenderer || r is SkinnedMeshRenderer) AddRenderer(r);
            }

            if (_rebuildIndex < _rebuildList.Length) return;

            FinalizeChunks();
            _rebuildList = null;
            _rebuilding = false;
            // 重建完立刻按新分块刷一次，别让新加入的物体停在"全渲染"状态
            _lastPos = new Vector3(float.MaxValue, 0f, 0f);
        }

        /// <summary>统计每个区块的包围盒 → 中心与半径（判定用"到区块边缘的距离"，跨块物体不会在边界闪）。</summary>
        private void FinalizeChunks()
        {
            foreach (var kv in _chunks)
            {
                Chunk c = kv.Value;
                if (c.Items.Count == 0) continue;

                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int i = 0; i < c.Items.Count; ++i)
                {
                    Renderer r = c.Items[i].Renderer;
                    if (r == null) continue;
                    Bounds b = r.bounds;
                    min = Vector3.Min(min, b.min);
                    max = Vector3.Max(max, b.max);
                }
                c.Center = (min + max) * 0.5f;
                c.Radius = (max - min).magnitude * 0.5f;
            }
        }

        private void AddRenderer(Renderer r)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return;

            int layer = r.gameObject.layer;
            if (layer == GroundLayer || layer == UnitLayer) return;

            // 单位：位置一直在变，按出生点分块会闪；它的开销交给 Animator 裁剪
            if (r.GetComponentInParent<Actor>(true) != null) return;

            Bounds b = r.bounds;
            if (b.extents.magnitude > ChunkSize)
            {
                // 天空球 / 云层 / 巨型远景：中心可能在几百米外，但覆盖整个屏幕 ⇒ 永不剔除
                r.forceRenderingOff = false;
                _always.Add(r);
                return;
            }

            int ix = Mathf.FloorToInt(b.center.x / ChunkSize);
            int iz = Mathf.FloorToInt(b.center.z / ChunkSize);
            long key = ((long)ix << 32) ^ (uint)iz;
            if (!_chunks.TryGetValue(key, out Chunk chunk))
            {
                chunk = new Chunk();
                _chunks[key] = chunk;
            }

            chunk.Items.Add(new ChunkItem { Renderer = r, Shadow = r.shadowCastingMode });
            chunk.Tier = RenderChunkTier.Active;
            r.forceRenderingOff = false;
        }

        /// <summary>按参考点刷新每个区块的档位（只在玩家移动超过阈值时调用）。</summary>
        private void Apply(Vector3 pos)
        {
            foreach (var kv in _chunks)
            {
                Chunk chunk = kv.Value;
                float d = Vector3.Distance(chunk.Center, pos) - chunk.Radius;

                RenderChunkTier target = EvaluateTier(d, chunk.Tier);
                if (target == chunk.Tier) continue;

                chunk.Tier = target;
                ApplyTier(chunk, target);
            }
        }

        /// <summary>
        /// 单边界滞回：当前"已越过"时，要回到内侧需要多走近 <see cref="Hysteresis"/>；
        /// 当前"未越过"时，要越到外侧需要多走远 <see cref="Hysteresis"/>。
        /// 两个边界各算一次，就是三档之间的两条缓冲带。
        /// </summary>
        private bool OverBoundary(float d, float boundary, bool wasOver)
            => wasOver ? d > boundary - Hysteresis : d > boundary + Hysteresis;

        private RenderChunkTier EvaluateTier(float d, RenderChunkTier current)
        {
            bool overActive = current != RenderChunkTier.Active;   // 当前是否已经在激活带之外
            bool overReady = current == RenderChunkTier.Lazy;      // 当前是否已经在预备带之外

            bool nowOverActive = OverBoundary(d, ActiveDistance, overActive);
            bool nowOverReady = OverBoundary(d, ReadyDistance, overReady);

            if (nowOverReady) return RenderChunkTier.Lazy;
            if (nowOverActive) return RenderChunkTier.Ready;
            return RenderChunkTier.Active;
        }

        private void ApplyTier(Chunk chunk, RenderChunkTier tier)
        {
            bool render = tier != RenderChunkTier.Lazy;
            bool castShadow = tier == RenderChunkTier.Active;

            for (int i = 0; i < chunk.Items.Count; ++i)
            {
                ChunkItem item = chunk.Items[i];
                Renderer r = item.Renderer;
                if (r == null) continue;

                r.forceRenderingOff = !render;
                // 降档只关投影；升回激活档要还原它**原本**的模式（有些物体本来就是 Off）
                r.shadowCastingMode = castShadow ? item.Shadow : ShadowCastingMode.Off;
            }
        }

        /// <summary>排查用：区块数 / 各档数量 / 参与剔除的渲染器数 / 常驻(超大)数。</summary>
        public string Dump()
        {
            int active = 0, ready = 0, lazy = 0, items = 0;
            foreach (var kv in _chunks)
            {
                items += kv.Value.Items.Count;
                switch (kv.Value.Tier)
                {
                    case RenderChunkTier.Active: active++; break;
                    case RenderChunkTier.Ready: ready++; break;
                    default: lazy++; break;
                }
            }
            return $"区块={_chunks.Count}(激活{active}/预备{ready}/惰性{lazy}) 渲染器={items} 常驻={_always.Count} " +
                   $"激活{ActiveDistance}m 预备{ReadyDistance}m 滞回{Hysteresis}m";
        }
    }
}
