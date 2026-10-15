using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 一个模型预制体的"量尺"结果（车头 / 车厢 / 轨道通用）：长轴长度与轴向、把长轴转到 +Z 的偏航、
    /// 底面到 pivot 的高度。铺轨、摆车厢、算清物走廊都按它来 ⇒ 换模型不用改代码。
    /// </summary>
    internal struct RailMetrics
    {
        /// <summary>长轴长度（米）= 一节的默认跨度</summary>
        public float Length;
        /// <summary>短轴长度（米）= 轨道宽度（清物走廊 / 路基内框按它算）</summary>
        public float Width;
        /// <summary>长轴是哪根轴：0 = local X，2 = local Z</summary>
        public int LengthAxis;
        public float YawOffset;
        public float PivotToBottom;

        /// <summary>
        /// 量一个模型预制体（车头/车厢/轨道通用）：长轴 / 短轴长度、长轴是哪根轴、
        /// 把长轴转到 +Z 需要的偏航、底面到 pivot 的高度。
        /// <para>⚠ <b>绝不能 Instantiate 探针</b>：那会跑 prefab 上 <c>Actor.Awake</c> ⇒ 派发 <c>EnemyCreate</c> ⇒
        /// HpWnd 给"探针"建一个 boss 血条，而我们马上把它销毁 ⇒ 血条抱着已销毁的 Actor 每帧抛
        /// MissingReferenceException（2026-10-15 实测）。这里直接用<b>资产自身的层级</b>算根空间包围盒，零副作用。</para>
        /// <para>⚠ 不假设美术资源的朝向与尺度：换模型不必改代码。</para>
        /// </summary>
        public static RailMetrics Measure(GameObject prefab)
        {
            RailMetrics metrics = new() { Length = 1f, Width = 1f, LengthAxis = 2, YawOffset = 0f, PivotToBottom = 0f };
            Matrix4x4 rootToWorld = prefab.transform.localToWorldMatrix;
            Matrix4x4 worldToRoot = rootToWorld.inverse;
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            bool has = false;
            Bounds bounds = default;
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 toRoot = worldToRoot * filters[i].transform.localToWorldMatrix;
                Bounds local = mesh.bounds;
                Vector3 c = local.center;
                Vector3 e = local.extents;
                for (int s = 0; s < 8; s++)
                {
                    Vector3 corner = c + new Vector3(
                        (s & 1) == 0 ? -e.x : e.x,
                        (s & 2) == 0 ? -e.y : e.y,
                        (s & 4) == 0 ? -e.z : e.z);
                    Vector3 p = toRoot.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }
            if (!has)
            {
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].localBounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].localBounds);
                    has = true;
                }
            }
            if (!has) return metrics;

            metrics.Length = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
            metrics.Width = Mathf.Max(0.01f, Mathf.Min(bounds.size.x, bounds.size.z));
            metrics.LengthAxis = bounds.size.x >= bounds.size.z ? 0 : 2;
            metrics.YawOffset = metrics.LengthAxis == 0 ? -90f : 0f;   // Unity 的 forward 是 +Z，长轴在 X 上要转 -90°
            metrics.PivotToBottom = bounds.center.y - bounds.extents.y; // 只有偏航旋转 ⇒ 这正是世界最低点相对 pivot 的高度
            return metrics;
        }
    }
}