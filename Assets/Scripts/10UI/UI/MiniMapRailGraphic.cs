using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FPSGame.UI
{
    /// <summary>
    /// 小地图上的铁轨路线（UI 折线，虚线）。
    /// <para>▍两层处理：① <b>分桶平均</b>把轨道点阵（A* + 软边平滑后的 ~6.5m 密点）压成"一段一段直线"；
    /// ② 再沿弧长按 <see cref="dashLength"/> / <see cref="gapLength"/> 铺虚线（每个实线小段一个四边形，线宽固定像素）。</para>
    /// <para>▍为什么要有：轨道是小地图上唯一"不是点、而是一条线"的信息，用图标拼不出来（点数太多且描不直），
    /// 所以直接生成网格。</para>
    /// <para>▍为什么要自己裁剪：本组件铺满整张地图（512×512），而玩家不在图中央时，
    /// 轨道两端的投影点会跑出矩形（甚至跑出屏幕）⇒ 每段先按矩形做 Liang–Barsky 参数裁剪，只画可见部分。
    /// 另外小地图根节点上已有 <c>Mask</c>，超出贴图 alpha 的部分也会被挡掉，双保险。</para>
    /// <para>▍坐标口径：<see cref="SetRoutes"/> 的投影函数返回"父节点锚点坐标"（与 <c>MiniMapWnd.WorldPosToMapPos</c>
    /// 同口径，原点 = 小地图左下角）。本组件与父节点同尺寸、枢轴 0.5 ⇒ 局部坐标 = 投影结果 + <c>rect.min</c>。</para>
    /// </summary>
    [AddComponentMenu("UI/小地图/铁轨路线")]
    public class MiniMapRailGraphic : MaskableGraphic
    {
        /// <summary>顶点硬上限：防止把虚线间距填成 0.1m 这类极端参数时生成出几十万顶点（画面上也不可见）</summary>
        private const int MaxVerts = 40000;

        [SerializeField]
        [InspectorName("线宽（像素）")]
        private float width = 3f;

        [SerializeField]
        [InspectorName("简化步长(米)")]
        private float simplifyStep = 30f;

        [SerializeField]
        [InspectorName("实线长度(米)")]
        private float dashLength = 21f;

        [SerializeField]
        [InspectorName("空隙长度(米)")]
        private float gapLength = 7f;

        /// <summary>路线（世界 XZ 折线；深拷贝持有，调用方之后改原列表不影响这里）</summary>
        private readonly List<List<Vector2>> routes = new();

        /// <summary>简化后的折线（分桶平均的结果，复用以避免每次重建都分配）</summary>
        private readonly List<Vector2> simplified = new();

        /// <summary>下次生成网格后打一条摘要日志（只在新路线喂进来时打，不逐帧刷屏）</summary>
        private bool pendingLog;

        /// <summary>世界 XZ → 父节点锚点坐标</summary>
        private Func<Vector2, Vector2> project;

        /// <summary>线宽（像素）</summary>
        public float Width
        {
            get => width;
            set
            {
                float v = Mathf.Max(1f, value);
                if (Mathf.Approximately(v, width)) return;
                width = v;
                SetVerticesDirty();
            }
        }

        /// <summary>当前路线条数（调用方据此判断要不要重建）</summary>
        public int RouteCount => routes.Count;

        /// <summary>
        /// 喂入路线。<paramref name="project"/> 由小地图提供（世界 XZ → 锚点坐标）。
        /// <para>⚠ 每次调用都会<b>深拷贝</b>折线点：轨道点阵是任务的长期数据，这里不该持有它的引用。</para>
        /// </summary>
        public void SetRoutes(List<List<Vector2>> source, Func<Vector2, Vector2> project)
        {
            this.project = project;
            routes.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    if (source[i] == null || source[i].Count < 2) continue;
                    routes.Add(new List<Vector2>(source[i]));
                }
            }
            pendingLog = true;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (project == null || routes.Count == 0) return;

            // 只在新路线喂进来时打两条日志（开始/完成）：万一以后卡住，日志能一眼区分"是不是卡在画线"
            if (pendingLog) Debug.Log($"[小地图/铁轨] 开始生成网格：路线 {routes.Count} 条");

            Rect rect = rectTransform.rect;
            float half = Mathf.Max(0.5f, width * 0.5f);
            Color32 tint = color;
            float cycle = Mathf.Max(0.5f, dashLength + gapLength);
            float dash = Mathf.Clamp(dashLength, 0.1f, cycle);
            int simplifiedTotal = 0;

            for (int r = 0; r < routes.Count; r++)
            {
                // ① 先把软边平滑出来的密集折点按弧长分桶取平均 ⇒ 屏幕上就是"一段一段的直线"
                BuildStraightRoute(routes[r]);
                simplifiedTotal += simplified.Count;

                // ② 再沿整条路线按**世界长度**铺虚线：与简化步长解耦，改步长不会连带改变虚线节奏
                float phase = 0f;
                for (int i = 1; i < simplified.Count; i++)
                {
                    Vector2 a = simplified[i - 1];
                    Vector2 b = simplified[i];
                    float len = Vector2.Distance(a, b);
                    // 非有限/退化段直接跳过（NaN 会让下面的循环条件失效）
                    if (float.IsNaN(len) || float.IsInfinity(len) || len < 0.01f) continue;

                    // ⚠ 这里必须"按整数周期枚举"，**不能**写成 walked += piece 的自增循环：
                    //   实线终点恰好落在浮点边界时 piece 会小于 walked 的精度 ⇒ walked 不增长 ⇒ 死循环
                    //   （2026-10-19 实测把 Unity 直接卡无响应）。周期数只由 len/cycle 决定，天然有界。
                    float start = phase;
                    float end = phase + len;
                    int firstCycle = Mathf.FloorToInt(start / cycle);
                    int lastCycle = Mathf.FloorToInt(end / cycle);
                    for (int k = firstCycle; k <= lastCycle; k++)
                    {
                        float from = Mathf.Max(k * cycle, start);          // 本周期实线的起点
                        float to = Mathf.Min(k * cycle + dash, end);       // 本周期实线的终点
                        if (to <= from) continue;                          // 这段落在空隙里
                        Vector2 p0 = Vector2.Lerp(a, b, (from - start) / len);
                        Vector2 p1 = Vector2.Lerp(a, b, (to - start) / len);
                        // 投影 → 本组件局部坐标 → 按局部矩形裁剪
                        Vector2 la = project(p0) + rect.min;
                        Vector2 lb = project(p1) + rect.min;
                        if (ClipToRect(ref la, ref lb, rect)) AddSegment(vh, la, lb, half, tint);
                    }
                    phase = end;
                }
            }

            if (pendingLog)
            {
                pendingLog = false;
                Debug.Log($"[小地图/铁轨] 路线 {routes.Count} 条 / 简化后共 {simplifiedTotal} 点 / 网格顶点 {vh.currentVertCount} "
                    + $"（简化步长 {simplifyStep:F0}m，虚线 {dash:F0}/{gapLength:F0}m）");
            }
        }

        /// <summary>
        /// 把一条原始折线压成"一段段直线"：按世界弧长分桶，桶内所有点<b>取平均</b>当作该段的代表点。
        /// <para>▍为什么需要：轨道点阵是 A* + 平滑后的等距密点（约 6.5m 一个），画到小地图上每 5~6px 就一个
        /// 小折角，看起来"碎、毛刺"。分桶平均后拐点少一个量级，视觉上就是一条条干净的直线段。</para>
        /// <para>⚠ 结果放在 <see cref="simplified"/>（复用，不分配）。</para>
        /// </summary>
        private void BuildStraightRoute(List<Vector2> source)
        {
            simplified.Clear();
            if (source == null || source.Count < 2) return;

            float step = Mathf.Max(1f, simplifyStep);
            simplified.Add(source[0]);
            Vector2 sum = source[0];
            int count = 1;
            float walked = 0f;
            for (int i = 1; i < source.Count; i++)
            {
                walked += Vector2.Distance(source[i - 1], source[i]);
                sum += source[i];
                count++;
                if (walked < step && i < source.Count - 1) continue;

                simplified.Add(sum / count);   // 本桶平均值 = 这一段的代表点
                sum = source[i];
                count = 1;
                walked = 0f;
            }

            // 末点用真实终点：最后一桶被平均掉会让轨道"短一截"（车还没到站线就断了）
            Vector2 last = source[source.Count - 1];
            if ((simplified[simplified.Count - 1] - last).sqrMagnitude > 0.0001f) simplified.Add(last);
        }

        /// <summary>把一段直线参数裁剪到矩形内（Liang–Barsky）；整段在外返回 false</summary>
        private static bool ClipToRect(ref Vector2 a, ref Vector2 b, Rect rect)
        {
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            if (!ClipAxis(-d.x, a.x - rect.xMin, ref t0, ref t1)) return false;
            if (!ClipAxis(d.x, rect.xMax - a.x, ref t0, ref t1)) return false;
            if (!ClipAxis(-d.y, a.y - rect.yMin, ref t0, ref t1)) return false;
            if (!ClipAxis(d.y, rect.yMax - a.y, ref t0, ref t1)) return false;

            Vector2 origin = a;
            a = origin + d * t0;
            b = origin + d * t1;
            return true;
        }

        /// <summary>单条边界：p·t ≤ q 形式的裁剪（两端点都在外侧 ⇒ false）</summary>
        private static bool ClipAxis(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Approximately(p, 0f)) return q >= 0f;   // 与该边平行：看是否在内侧
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        /// <summary>一段线 → 一个四边形（不做斜接：3px 的线在拐点重叠一点看不出来）</summary>
        private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float half, Color32 tint)
        {
            if (vh.currentVertCount >= MaxVerts) return;
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return;
            Vector2 n = new Vector2(-dir.y, dir.x) / len * half;

            int index = vh.currentVertCount;
            vh.AddVert(a + n, tint, Vector2.zero);
            vh.AddVert(a - n, tint, Vector2.zero);
            vh.AddVert(b + n, tint, Vector2.zero);
            vh.AddVert(b - n, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 1, index + 3);
        }
    }
}
