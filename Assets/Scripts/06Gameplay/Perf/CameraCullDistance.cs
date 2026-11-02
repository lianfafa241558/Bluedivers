using System;
using FPSGame.Attributes;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 按**图层**给这台相机设剔除距离（引擎侧裁剪，零每帧成本）。
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
    /// <para>▍⚠ 挂多个相机会各自生效；Overlay 相机（WeaponCamera / UIFxCamera）也吃自己的那份数组。
    /// 给 UI 相机配了短距离会把 UI 裁没，注意别乱挂。</para>
    /// </summary>
    [AddComponentMenu("性能/按层相机剔除距离")]
    [RequireComponent(typeof(Camera))]
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

        private Camera _cam;
        private float _lastFar = -1f;

        private void OnEnable()
        {
            _cam = GetComponent<Camera>();
            Apply();
        }

        private void OnValidate()
        {
            // 编辑器里改参数即时生效（Play 中调试很方便）
            if (!isActiveAndEnabled) return;
            if (_cam == null) _cam = GetComponent<Camera>();
            Apply();
        }

        private void Update()
        {
            if (_cam == null) return;
            // 只在 far 被别的逻辑改过时重算（比如换场景/换相机设置）
            if (Mathf.Abs(_cam.farClipPlane - _lastFar) < 0.01f) return;
            Apply();
        }

        private void OnDisable()
        {
            // 组件被关掉时还原成"全部按 far"，否则关掉后场上还是静止在旧的裁剪距离里
            if (_cam == null) return;
            float[] reset = new float[32];
            for (int i = 0; i < 32; ++i) reset[i] = _cam.farClipPlane;
            _cam.layerCullDistances = reset;
            _lastFar = -1f;
        }

        /// <summary>重新计算并写入 <see cref="Camera.layerCullDistances"/>。</summary>
        public void Apply()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;

            _lastFar = _cam.farClipPlane;

            // ⚠ 必须先整表填 far：数组里 0 表示"该层完全不渲染"，留 0 会把没配置的层全部裁没
            float[] distances = new float[32];
            for (int i = 0; i < 32; ++i) distances[i] = _lastFar;

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
                        distances[layer] = Mathf.Min(entry.Distance, _lastFar);
                    }
                }
            }

            _cam.layerCullDistances = distances;
        }

        /// <summary>排查用：当前这台相机每一层的实际剔除距离（0~31，只列被改过的）。</summary>
        public string Dump()
        {
            if (_cam == null) return "CameraCullDistance: 没挂 Camera";

            float[] d = _cam.layerCullDistances;
            if (d == null || d.Length == 0) return "CameraCullDistance: 未生效（用 far）";

            var sb = new System.Text.StringBuilder();
            sb.Append("CameraCullDistance far=").Append(_cam.farClipPlane.ToString("F0")).Append("  ");
            for (int layer = 0; layer < 32; ++layer)
            {
                if (Mathf.Abs(d[layer] - _cam.farClipPlane) < 0.01f) continue;
                sb.Append(UnityEngine.LayerMask.LayerToName(layer)).Append("=").Append(d[layer].ToString("F0")).Append("m ");
            }
            return sb.ToString();
        }
    }
}
