using System;
using FPSGame.Attributes;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 按**图层**给一台相机设剔除距离（引擎侧裁剪，零每帧成本）。
    ///
    /// <para>▍典型用法：Default（道具）180 m、Unit 250 m、**Ground 不列出来** ⇒ 地面/悬崖照常用相机 far 看得很远。</para>
    ///
    /// <para>▍判定口径是"相机到物体**包围盒**的距离"，所以天空球/云层这种中心在几百米外、
    /// 包围盒却罩住整屏的物体不会被误裁（比按中心点判定的手写剔除更安全）。</para>
    ///
    /// <para>▍⚠ 它的短板是**只能按层一刀切**：同一层里既有 30 m 近景也有 300 m 远景时没法分档。
    /// 需要更细的粒度就用 <see cref="RenderChunkCuller"/>（32 m 区块），两者可以叠加：
    /// 本组件管"整层粗裁"，区块组件管"同层内细分"。</para>
    ///
    /// <para>▍⚠ 配置写在**相机本体**上（<c>Camera.layerCullDistances</c>），所以"谁在出图"必须是活的：
    /// 玩家倒地时主相机被整台 SetActive(false)、换观察相机出图 ⇒ 配置要跟着转移、旧相机要还原。
    /// 见 <see cref="_followGameplayCamera"/> 与 <see cref="GameplayCamera"/>。</para>
    /// </summary>
    [AddComponentMenu("性能/按层相机剔除距离")]
    public class CameraCullDistance : MonoBehaviour
    {
        /// <summary>一条"这些层 → 裁到多远"。</summary>
        [Serializable]
        public class LayerCullEntry
        {
            [InspectorName("图层")]
            [Tooltip("想一起裁的层可以多选")]
            public LayerMask Layers = 1;

            [InspectorName("剔除距离(米)")]
            [Tooltip("相机到物体包围盒超过这个距离就不渲染。设得比相机 far 大没有意义（会被夹到 far）")]
            [Min(1f)]
            public float Distance = 180f;
        }

        [InspectorName("按图层的剔除距离")]
        [Tooltip("没列在这的层一律按相机 farClipPlane 处理（= 不额外裁）")]
        public LayerCullEntry[] Entries =
        {
            new LayerCullEntry { Layers = 1 << 0, Distance = 180f },          // Default：场景道具
            new LayerCullEntry { Layers = 1 << 6, Distance = 250f },          // Unit：远处单位
        };

        [SerializeField]
        [InspectorName("目标相机（留空=本物体）")]
        [Tooltip("配置写给哪台相机。留空取本物体上的 Camera")]
        private Camera _targetCamera;

        [SerializeField]
        [InspectorName("跟随玩法相机")]
        [Tooltip("自己那台没在出图（倒地切观察相机 / 被自由相机顶掉）时，把配置转给当前出图的玩法相机；" +
                 "挂在 UI / 武器相机上要关掉")]
        private bool _followGameplayCamera = true;

        /// <summary>配置**真正**写在哪台相机上。还原必须按它来，不能按"此刻解析出来的那台"。</summary>
        private Camera _applied;
        /// <summary>复用的 32 层数组（<c>layerCullDistances</c> 赋值会拷进原生侧，不必每次 new）。</summary>
        private float[] _distances;
        private Camera _self;
        private float _lastFar = -1f;

        private void Awake()
        {
            _self = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (_self == null) _self = GetComponent<Camera>();
            Apply();
        }

        private void OnDisable()
        {
            // 组件被关掉时把它改过的相机还原，否则那台相机会一直停在旧配置里
            RestoreApplied();
            _lastFar = -1f;
        }

        private void OnValidate()
        {
            // 编辑器里改参数即时生效（Play 中调试很方便）
            if (!isActiveAndEnabled) return;
            if (_self == null) _self = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            Camera target = ResolveTarget();
            if (target == null)
            {
                // 相机没了（玩家销毁 / 换场景）：把上一台还原，别留着过期距离
                RestoreApplied();
                return;
            }

            if (target != _applied)
            {
                // 出图相机换了（倒地 ↔ 复活）：旧相机先还原，否则它再启用时还带着这份配置
                RestoreApplied();
                ApplyTo(target);
                return;
            }

            // 只在 far 被别的逻辑改过时重算（换场景 / 换相机设置）
            if (Mathf.Abs(target.farClipPlane - _lastFar) < 0.01f) return;
            ApplyTo(target);
        }

        /// <summary>配置该写给哪台相机：显式指定 → 本物体 → 当前出图的玩法相机。</summary>
        private Camera ResolveTarget()
        {
            Camera own = _targetCamera != null ? _targetCamera : _self;

            // 自己那台正在出图 ⇒ 就是它（绝大多数情况，一次引用比较）
            if (!_followGameplayCamera || (own != null && own.isActiveAndEnabled)) return own;

            Camera gameplay = GameplayCamera.Current;
            return gameplay != null ? gameplay : own;
        }

        /// <summary>重新计算并写一次（外部改完配置 / 换完相机时调）。</summary>
        public void Apply()
        {
            Camera target = ResolveTarget();
            if (target == null) return;
            if (target != _applied) RestoreApplied();
            ApplyTo(target);
        }

        private void ApplyTo(Camera cam)
        {
            float far = cam.farClipPlane;
            _lastFar = far;

            // ⚠ 必须先整表填 far：数组里 0 表示"该层完全不渲染"，留 0 会把没配置的层全部裁没
            float[] distances = EnsureDistances();
            for (int i = 0; i < distances.Length; ++i) distances[i] = far;

            if (Entries != null)
            {
                for (int e = 0; e < Entries.Length; ++e)
                {
                    LayerCullEntry entry = Entries[e];
                    if (entry == null) continue;

                    int mask = entry.Layers.value;
                    for (int layer = 0; layer < 32; ++layer)
                    {
                        if ((mask & (1 << layer)) == 0) continue;
                        distances[layer] = Mathf.Min(entry.Distance, far);
                    }
                }
            }

            cam.layerCullDistances = distances;
            _applied = cam;
        }

        /// <summary>把配置从相机上撤掉（全部层回到 far）—— 换相机 / 禁用组件时必须做。</summary>
        private void RestoreApplied()
        {
            if (_applied == null) return;      // 已销毁 / 未写过

            float far = _applied.farClipPlane;
            float[] distances = EnsureDistances();
            for (int i = 0; i < distances.Length; ++i) distances[i] = far;
            _applied.layerCullDistances = distances;
            _applied = null;
        }

        private float[] EnsureDistances()
        {
            if (_distances == null || _distances.Length != 32) _distances = new float[32];
            return _distances;
        }

        /// <summary>排查用：当前生效的相机 + 它每一层的实际剔除距离（只列被改过的）。</summary>
        public string Dump()
        {
            if (_applied == null) return "CameraCullDistance: 未生效（没有可用的相机）";

            float[] d = _applied.layerCullDistances;
            if (d == null || d.Length == 0) return $"CameraCullDistance[{_applied.name}]: 未生效（用 far）";

            float far = _applied.farClipPlane;
            var sb = new System.Text.StringBuilder();
            sb.Append("CameraCullDistance[").Append(_applied.name).Append("] far=")
              .Append(far.ToString("F0")).Append("  ");
            for (int layer = 0; layer < 32 && layer < d.Length; ++layer)
            {
                if (Mathf.Abs(d[layer] - far) < 0.01f) continue;
                sb.Append(UnityEngine.LayerMask.LayerToName(layer)).Append("=").Append(d[layer].ToString("F0")).Append("m ");
            }
            return sb.ToString();
        }
    }
}
