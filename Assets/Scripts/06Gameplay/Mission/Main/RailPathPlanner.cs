using System.Collections.Generic;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 拦截火车 · 轨道路径规划器：地图边缘选点 → 栅格 A*（含坡度代价与"拉直"）→ Chaikin 平滑，
    /// 外加地形坡度采样。整块都是纯算法、不碰 <c>MonoBehaviour</c> 状态，参数一律由构造注入。
    /// <para>▍为什么独立成类：规划涉及的旋钮（节距 / 外扩 / 坡度权重 / 避让余量 / 夹角弦长）与任务的
    /// 生成流程无关，混在一个 partial 里会让"轨道生成编排"被 400 行算法淹没。</para>
    /// <para>⚠ 只在本端算、结果两端一致（只依赖 <see cref="_random"/> 与两端相同的序列化参数）。</para>
    /// </summary>
    internal sealed class RailPathPlanner
    {
        /// <summary>规划栅格的节点上限（超了就放大格距）：栅格是 O(格数) 的内存与搜索，别为了一张大图卡住开局</summary>
        private const int MaxPlanCells = 40000;

        private readonly float _stepLength;
        private readonly float _planExpand;
        private readonly float _slopeWeight;
        private readonly float _avoidMargin;
        private readonly float _minAngleSeparation;
        private readonly float _minChordRatio;
        private readonly System.Random _random;

        public RailPathPlanner(float stepLength, float planExpand, float slopeWeight, float avoidMargin,
            float minAngleSeparation, float minChordRatio, System.Random random)
        {
            _stepLength = stepLength;
            _planExpand = planExpand;
            _slopeWeight = slopeWeight;
            _avoidMargin = avoidMargin;
            _minAngleSeparation = minAngleSeparation;
            _minChordRatio = minChordRatio;
            _random = random;
        }

        #region 起终点

        /// <summary>
        /// 收集要避让的圆：已占圆（任务点等）+ 地表占地圆（巨型悬崖）。
        /// <para>⚠ 半径 ≥ 40% 地图可用半径的圆会被剔除：那种尺寸的圆一旦计入就把整张图堵死
        /// （实测：A* 直接无解 ⇒ 退化直线、看起来"完全没躲避"），而它本身也没法绕开，不如放行。</para>
        /// </summary>
        public static List<TerrainUtils.AreaCircle> CollectObstacles(float mapRadius)
        {
            List<TerrainUtils.AreaCircle> list = new();
            float maxRadius = mapRadius * 0.4f;
            AppendObstacles(list, TerrainUtils.ReservedCircles, maxRadius);
            AppendObstacles(list, TerrainUtils.AreaCircles, maxRadius);
            return list;
        }

        private static void AppendObstacles(List<TerrainUtils.AreaCircle> list,
            IReadOnlyList<TerrainUtils.AreaCircle> source, float maxRadius)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Radius <= 0f || source[i].Radius >= maxRadius) continue;
                list.Add(source[i]);
            }
        }

        /// <summary>
        /// 在地图边缘（空气墙圆周）取一对起点/终点：夹角 ≥ <see cref="_minAngleSeparation"/>、
        /// 弦长 ≥ 半径 × <see cref="_minChordRatio"/>（"距离不少于半径一半"这层口径落在弦长上）。
        /// <para><paramref name="requireFree"/> = 两点还都不能撞避让圆；躲不开时传 false，只保夹角/弦长并挑最平的落点。</para>
        /// </summary>
        public bool TryPickEdgePair(Vector2 center, float radius, List<TerrainUtils.AreaCircle> obstacles,
            bool requireFree, out Vector2 startPoint, out Vector2 endPoint)
        {
            startPoint = default;
            endPoint = default;
            if (!TryPickEdgePoint(center, radius, -1f, obstacles, requireFree, out startPoint, out float startAngle)) return false;
            if (!TryPickEdgePoint(center, radius, startAngle, obstacles, requireFree, out endPoint, out _)) return false;
            return true;
        }

        /// <summary>
        /// 圆周上取一个边缘点：与 <paramref name="excludeAngle"/> 夹角 ≥ <see cref="_minAngleSeparation"/>、
        /// 弦长 ≥ 半径 × <see cref="_minChordRatio"/>；<paramref name="requireFree"/> = 还必须不撞避让圆。
        /// 在合格候选里挑<b>最平</b>的一处（边缘多半是缓坡/沙滩，端点压在陡坡上轨道会翘）。
        /// </summary>
        /// <param name="excludeAngle">已选点的角度；&lt; 0 表示本次不校验取舍（第一个点）</param>
        private bool TryPickEdgePoint(Vector2 center, float radius, float excludeAngle,
            List<TerrainUtils.AreaCircle> obstacles, bool requireFree, out Vector2 point, out float angle)
        {
            point = default;
            angle = 0f;
            float bestSteepness = float.MaxValue;
            bool found = false;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                float a = (float)_random.NextDouble() * 360f;
                Vector2 candidate = PointOnEdge(center, radius, a);

                if (excludeAngle >= 0f)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(a, excludeAngle)) < _minAngleSeparation) continue;
                    if (Vector2.Distance(candidate, PointOnEdge(center, radius, excludeAngle)) < radius * _minChordRatio) continue;
                }
                if (requireFree && HitCircle(obstacles, candidate, _avoidMargin)) continue;

                float steepness = SteepnessDeg(candidate);
                if (!found || steepness < bestSteepness)
                {
                    found = true;
                    bestSteepness = steepness;
                    angle = a;
                    point = candidate;
                }
                if (bestSteepness <= 5f) break;   // 已经够平，不必再挑
            }
            return found;
        }

        private static Vector2 PointOnEdge(Vector2 center, float radius, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        }

        #endregion

        #region 路径规划（A*）

        /// <summary>
        /// <b>提前规划</b>整条路径：8 邻域 A* 栅格搜索。
        /// <para>代价 = 距离 ×（1 + <see cref="_slopeWeight"/> × 坡度/45°）⇒ 缓坡优先；</para>
        /// <para>不可走 = 出空气墙圆 / 撞已占圆（任务点等，半径 + <see cref="_avoidMargin"/>）/ 撞地表占地圆（巨型悬崖）；</para>
        /// <para>规划完"拉直"：只把<b>直连不比绕行贵</b>的那段直连（判据用同一套含坡度的代价）。
        /// ⚠ 拉直判据若只看碰撞，任何地形都会被拉成一条直线（实测踩过）。</para>
        /// </summary>
        /// <returns>世界 XZ 路点（含起终点）；无解返回 null</returns>
        private List<Vector2> PlanPath(Vector2 start, Vector2 end, Vector2 mapCenter, float mapRadius,
            Vector3 origin, Vector3 size, List<TerrainUtils.AreaCircle> obstacles, float margin)
        {
            // 只规划"起终点包围盒 + 外扩"这一块：全图铺格子在大图上纯浪费
            float cell = Mathf.Max(4f, _stepLength * 0.5f);
            Vector2 min = Vector2.Max(Vector2.Min(start, end) - Vector2.one * _planExpand,
                new Vector2(origin.x + 1f, origin.z + 1f));
            Vector2 max = Vector2.Min(Vector2.Max(start, end) + Vector2.one * _planExpand,
                new Vector2(origin.x + size.x - 1f, origin.z + size.z - 1f));

            int w, h;
            while (true)
            {
                w = Mathf.Max(2, Mathf.CeilToInt((max.x - min.x) / cell) + 1);
                h = Mathf.Max(2, Mathf.CeilToInt((max.y - min.y) / cell) + 1);
                if (w * h <= MaxPlanCells) break;
                cell *= 1.5f;   // 格子太多就粗一点（宁可路糙，也别卡开局）
            }
            int total = w * h;

            bool[] blocked = new bool[total];
            float[] slope = new float[total];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Vector2 p = new(min.x + x * cell, min.y + y * cell);
                    blocked[i] = (p - mapCenter).sqrMagnitude > mapRadius * mapRadius
                        || HitCircle(obstacles, p, margin);
                    if (!blocked[i]) slope[i] = SteepnessDeg(p);
                }
            }

            int startIndex = IndexOf(start, min, cell, w, h);
            int endIndex = IndexOf(end, min, cell, w, h);
            // 起终点本就压在圆边/占位圆边上（端点格允许站住）；但如果四周全被堵死，A* 连第一步都迈不出去 ⇒
            // 把端点周围 _stepLength 半径内的格子强行放开（2026-10-15：轨道2/3 规划不出来、退化成直线就是这么来的）
            int guard = Mathf.Max(1, Mathf.CeilToInt(_stepLength / cell));
            int[] guardCenters = { startIndex, endIndex };
            for (int g = 0; g < guardCenters.Length; g++)
            {
                int gx = guardCenters[g] % w;
                int gy = guardCenters[g] / w;
                for (int dy = -guard; dy <= guard; dy++)
                {
                    int y = gy + dy;
                    if (y < 0 || y >= h) continue;
                    for (int dx = -guard; dx <= guard; dx++)
                    {
                        int x = gx + dx;
                        if (x < 0 || x >= w) continue;
                        int index = y * w + x;
                        if (!blocked[index]) continue;
                        blocked[index] = false;
                        slope[index] = SteepnessDeg(CellCenter(index, min, cell, w));
                    }
                }
            }
            slope[startIndex] = SteepnessDeg(start);
            slope[endIndex] = SteepnessDeg(end);

            float[] cost = new float[total];
            int[] cameFrom = new int[total];
            bool[] closed = new bool[total];
            for (int i = 0; i < total; i++)
            {
                cost[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }
            MinHeap open = new();
            cost[startIndex] = 0f;
            open.Push(Vector2.Distance(CellCenter(startIndex, min, cell, w), end), startIndex);

            int[] offsetX = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] offsetY = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (open.Count > 0)
            {
                int current = open.Pop(out _);
                if (closed[current]) continue;
                closed[current] = true;
                if (current == endIndex) break;

                int cx = current % w;
                int cy = current / w;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + offsetX[k];
                    int ny = cy + offsetY[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int next = ny * w + nx;
                    if (blocked[next]) continue;
                    // 斜向不许"贴着角"钻过两个正交堵格
                    if (k >= 4 && blocked[cy * w + nx] && blocked[ny * w + cx]) continue;

                    float step = k >= 4 ? cell * 1.414214f : cell;
                    float nextCost = cost[current] + step * (1f + _slopeWeight * slope[next] / 45f);
                    if (nextCost >= cost[next]) continue;
                    cost[next] = nextCost;
                    cameFrom[next] = current;
                    open.Push(nextCost + Vector2.Distance(CellCenter(next, min, cell, w), end), next);
                }
            }
            if (float.IsPositiveInfinity(cost[endIndex])) return null;

            List<Vector2> nodes = new();
            List<int> nodeIndices = new();
            for (int at = endIndex; at != -1; at = cameFrom[at])
            {
                nodes.Add(CellCenter(at, min, cell, w));
                nodeIndices.Add(at);
                if (at == startIndex) break;
            }
            if (nodes.Count < 2) return null;
            nodes.Reverse();
            nodeIndices.Reverse();
            nodes[0] = start;
            nodes[nodes.Count - 1] = end;

            // 拉直：几何不穿障碍 + 直连代价不高于绕行代价（+2% 容差）才直连
            List<Vector2> straight = new() { nodes[0] };
            int anchor = 0;
            while (anchor < nodes.Count - 1)
            {
                int next = anchor + 1;
                for (int j = nodes.Count - 1; j > anchor + 1; j--)
                {
                    if (LineBlocked(nodes[anchor], nodes[j], min, cell, w, h, blocked)) continue;
                    float direct = LineCost(nodes[anchor], nodes[j], min, cell, w, h, blocked, slope);
                    float alongPath = cost[nodeIndices[j]] - cost[nodeIndices[anchor]];
                    if (direct <= alongPath * 1.02f)
                    {
                        next = j;
                        break;
                    }
                }
                straight.Add(nodes[next]);
                anchor = next;
            }
            straight[0] = start;
            straight[straight.Count - 1] = end;
            return straight;
        }

        /// <summary>
        /// 按"完全避让 → 余量减半 → 完全不避让"三级试着规划，返回第一条能走通的路径。
        /// <para>为什么要放松：起终点常压在占位圆边上，余量一大就连起步格子都被封死 ⇒ A* 无解。</para>
        /// </summary>
        public List<Vector2> PlanWithRelaxation(Vector2 start, Vector2 end, Vector2 mapCenter, float mapRadius,
            Vector3 origin, Vector3 size, List<TerrainUtils.AreaCircle> obstacles, out float usedMargin)
        {
            usedMargin = _avoidMargin;
            List<Vector2> plan = PlanPath(start, end, mapCenter, mapRadius, origin, size, obstacles, _avoidMargin);
            if (plan != null) return plan;

            usedMargin = _avoidMargin * 0.5f;
            plan = PlanPath(start, end, mapCenter, mapRadius, origin, size, obstacles, usedMargin);
            if (plan != null) return plan;

            usedMargin = 0f;
            return PlanPath(start, end, mapCenter, mapRadius, origin, size, obstacles, 0f);
        }

        /// <summary>线段是否穿过不可走的格子（按半格步长采样）</summary>
        private static bool LineBlocked(Vector2 a, Vector2 b, Vector2 min, float cell, int w, int h, bool[] blocked)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (cell * 0.5f)));
            for (int i = 1; i < steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
                if (blocked[IndexOf(p, min, cell, w, h)]) return true;
            }
            return false;
        }

        /// <summary>
        /// 直线段按<b>同一套代价</b>估费（含坡度；撞到不可走格子直接判无穷）。
        /// ⚠ 不可走格子的坡度是 0，所以必须先判 blocked，否则悬崖会被当平地算成"直连更便宜"。
        /// </summary>
        private float LineCost(Vector2 a, Vector2 b, Vector2 min, float cell, int w, int h, bool[] blocked, float[] slope)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(1f, cell * 0.5f)));
            float cost = 0f;
            for (int i = 0; i < steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, (i + 0.5f) / steps);
                int index = IndexOf(p, min, cell, w, h);
                if (blocked[index]) return float.MaxValue;
                cost += length / steps * (1f + _slopeWeight * slope[index] / 45f);
            }
            return cost;
        }

        private static int IndexOf(Vector2 p, Vector2 min, float cell, int w, int h)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((p.x - min.x) / cell), 0, w - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((p.y - min.y) / cell), 0, h - 1);
            return y * w + x;
        }

        private static Vector2 CellCenter(int index, Vector2 min, float cell, int w)
            => new(min.x + index % w * cell, min.y + index / w * cell);

        /// <summary>点是否落在某个避让圆（半径 + <paramref name="margin"/>）里</summary>
        private static bool HitCircle(IReadOnlyList<TerrainUtils.AreaCircle> circles, Vector2 point, float margin)
        {
            for (int i = 0; i < circles.Count; i++)
            {
                float radius = circles[i].Radius + margin;
                if ((circles[i].Center - point).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        /// <summary>极简二叉最小堆（Unity 的 .NET 里没有 PriorityQueue；只存 (优先级, 索引)）</summary>
        private sealed class MinHeap
        {
            private float[] _priority = new float[256];
            private int[] _index = new int[256];
            private int _count;

            public int Count => _count;

            public void Push(float priority, int index)
            {
                if (_count == _priority.Length)
                {
                    System.Array.Resize(ref _priority, _count * 2);
                    System.Array.Resize(ref _index, _count * 2);
                }
                int i = _count++;
                _priority[i] = priority;
                _index[i] = index;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_priority[parent] <= _priority[i]) break;
                    Swap(i, parent);
                    i = parent;
                }
            }

            public int Pop(out float priority)
            {
                priority = _priority[0];
                int index = _index[0];
                if (--_count > 0)
                {
                    _priority[0] = _priority[_count];
                    _index[0] = _index[_count];
                    int i = 0;
                    while (true)
                    {
                        int left = i * 2 + 1;
                        int right = left + 1;
                        int smallest = i;
                        if (left < _count && _priority[left] < _priority[smallest]) smallest = left;
                        if (right < _count && _priority[right] < _priority[smallest]) smallest = right;
                        if (smallest == i) break;
                        Swap(i, smallest);
                        i = smallest;
                    }
                }
                return index;
            }

            private void Swap(int a, int b)
            {
                (_priority[a], _priority[b]) = (_priority[b], _priority[a]);
                (_index[a], _index[b]) = (_index[b], _index[a]);
            }
        }

        #endregion

        #region 折线工具

        /// <summary>Chaikin 平滑到"每个拐点转角 ≤ <paramref name="maxTurnDeg"/>"为止（也有轮数上限）</summary>
        public static void SmoothToTurnLimit(List<Vector3> points, float maxTurnDeg, int maxPasses)
        {
            for (int pass = 0; pass < maxPasses && points.Count > 2 && points.Count < 4096; pass++)
            {
                if (MaxVertexTurnDeg(points) <= maxTurnDeg) return;
                ChaikinOnce(points);
            }
        }

        /// <summary>折线上最大的拐点转角（度；没有拐点时 0）</summary>
        private static float MaxVertexTurnDeg(List<Vector3> points)
        {
            float max = 0f;
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 inDir = new(points[i].x - points[i - 1].x, points[i].z - points[i - 1].z);
                Vector2 outDir = new(points[i + 1].x - points[i].x, points[i + 1].z - points[i].z);
                if (inDir.sqrMagnitude < 0.0001f || outDir.sqrMagnitude < 0.0001f) continue;
                float turn = Vector2.Angle(inDir, outDir);
                if (turn > max) max = turn;
            }
            return max;
        }

        /// <summary>Chaikin 切角一轮（首尾保留）；每跑一轮，各拐点的转角大致再减半</summary>
        private static void ChaikinOnce(List<Vector3> points)
        {
            List<Vector3> smooth = new(points.Count * 2) { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.25f));
                smooth.Add(Vector3.Lerp(points[i], points[i + 1], 0.75f));
            }
            smooth.Add(points[points.Count - 1]);
            points.Clear();
            points.AddRange(smooth);
        }

        #endregion

        /// <summary>世界点的坡度（角度制，0 = 平地，90 = 崖壁）</summary>
        private static float SteepnessDeg(Vector2 worldPos)
        {
            Vector2 uv = TerrainUtils.WSToUV(worldPos);
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            // ⚠ 是 TerrainData.GetSteepness（Terrain 上没有这个方法）：吃"归一化坐标"、返回弧度
            return TerrainUtils.Main.terrainData.GetSteepness(uv.x, uv.y) * Mathf.Rad2Deg;
        }
    }
}