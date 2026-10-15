using System.Collections;
using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Core;
using FPSGame.MapUtils;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 拦截火车 · 轨道生成（partial）：起终点选点 → A* 规划 → 走廊登记/清物 → 路基统一成型 → 逐节铺轨。
    /// <para>⚠ 规划与"走廊登记"必须在本协程的<b>第一个 yield 之前</b>跑完（见主文件 <c>InitMission</c> 的说明）：
    /// 登记得早，后面创建的其它任务点/兴趣点才会反过来躲开铁轨。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        [Foldout("轨道", true)]
        [InspectorName("轨道条数")]
        [SerializeField] private int trackCount = 3;
        [InspectorName("轨道预制体")]
        [SerializeField] private GameObject railPrefab;
        [InspectorName("起终点最小夹角(度)")]
        [SerializeField] private float minAngleSeparation = 120f;
        [InspectorName("起终点最小弦长(占地图半径比例)")]
        [SerializeField] private float minChordRatio = 0.5f;
        [InspectorName("边缘内缩(米)")]
        [SerializeField] private float edgeInset = 2f;

        [Foldout("轨道规划", true)]
        [InspectorName("参考节距(米)")]
        [SerializeField] private float stepLength = 24f;
        [InspectorName("规划外扩(米)")]
        [SerializeField] private float planExpand = 200f;
        [InspectorName("避让余量(米)")]
        [SerializeField] private float avoidMargin = 15f;
        [InspectorName("坡度代价权重")]
        [SerializeField] private float slopeWeight = 2f;
        [InspectorName("单顶点最大转角(度)")]
        [SerializeField] private float maxVertexTurnDeg = 8f;
        [InspectorName("路径平滑次数")]
        [SerializeField] private int smoothPasses = 4;

        [Foldout("路基", true)]
        [InspectorName("路基取样间距(米)")]
        [SerializeField] private float bedStep = 8f;
        [InspectorName("路基纵坡平滑长度(米)")]
        [SerializeField] private float bedSmoothLength = 60f;
        [InspectorName("路基整平阈值(米)")]
        [SerializeField] private float flattenThreshold = 1.2f;
        [InspectorName("路基淡化带(米)")]
        [SerializeField] private float flattenBlend = 6f;

        [Foldout("地表处理", true)]
        [InspectorName("清物目标")]
        [SerializeField] private TerrainClearTarget clearTarget = TerrainClearTarget.VegetationAndDetail;
        [InspectorName("清物盖章间距(米)")]
        [SerializeField] private float clearStep = 8f;
        [InspectorName("清物余量(米)")]
        [SerializeField] private float clearMargin = 1.5f;
        [InspectorName("清物树冠补偿(米)")]
        [SerializeField] private float clearCanopy = 5f;
        [InspectorName("登记走廊为已占(供后来者避让)")]
        [SerializeField] private bool registerCorridor = true;
        [InspectorName("走廊登记半径(米)")]
        [SerializeField] private float corridorRegisterRadius = 12f;

        /// <summary>规划栅格的节点上限（超了就放大格距）：栅格是 O(格数) 的内存与搜索，别为了一张大图卡住开局</summary>
        private const int MaxPlanCells = 40000;

        /// <summary>本次生成出的所有轨道（顺序 = 生成顺序；<see cref="RailTrack"/> 是独立文件，列车也用它）</summary>
        private readonly List<RailTrack> tracks = new();

        /// <summary>轨道尺寸（由预制体量出来，铺节与清物都用它）</summary>
        private RailMetrics railMetrics;

        /// <summary>取轨道（越界给 null；给火车/时刻表用）</summary>
        private RailTrack GetTrack(int index) => index >= 0 && index < tracks.Count ? tracks[index] : null;

        /// <summary>
        /// 生成全部轨道。<para>顺序很重要：**先把所有轨道规划 + 走廊登记（同步、无 yield）**，
        /// 再逐条做路基整平与铺节（有 yield）——这样"登记"最早完成，后续任务点/兴趣点必然躲得开。</para>
        /// </summary>
        protected IEnumerator BuildAllTracks()
        {
            Terrain terrain = TerrainUtils.Main;
            if (!terrain)
            {
                Debug.LogError("拦截火车：场景里没有主地形", this);
                yield break;
            }
            if (railPrefab == null)
            {
                Debug.LogError("拦截火车：没有配轨道预制体", this);
                yield break;
            }

            railMetrics = MeasureRail(railPrefab);
            Vector3 origin = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            Vector2 mapCenter = new(origin.x + size.x * 0.5f, origin.z + size.z * 0.5f);
            float mapRadius = Mathf.Min(size.x, size.z) * 0.5f - Constants.TaskBorder - edgeInset;
            if (mapRadius <= stepLength)
            {
                Debug.LogError($"拦截火车：地图可用半径过小（{mapRadius:F0}m），铺不出轨道", this);
                yield break;
            }

            // ── 第一阶段（同步，必须没有 yield）：逐条规划 + 登记走廊 + 定发车时刻表 ──
            float clearRadius = railMetrics.Width * 0.5f + clearMargin + clearCanopy
                + (flattenThreshold > 0f ? flattenBlend : 0f);
            tracks.Clear();
            // 已铺轨道的**实际开挖走廊**（半径比登记的 12m 大）——用来保证多条轨道不互相切开对方的走廊
            List<TerrainUtils.AreaCircle> selfCorridors = new();
            int wanted = Mathf.Max(1, trackCount);
            for (int i = 0; i < wanted; i++)
            {
                RailTrack track = PlanTrack(i, mapCenter, mapRadius, origin, size, clearRadius, selfCorridors, out int corridorStamps);
                if (track == null) continue;
                tracks.Add(track);
                NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 规划：路点 {track.Points.Count} / 总长 {track.TotalLength:F0}m / "
                    + $"走廊登记 {corridorStamps} 处（半径 {corridorRegisterRadius:F0}m） / 起 {track.StartPoint} 终 {track.EndPoint}");
            }
            if (tracks.Count == 0)
            {
                Debug.LogError("拦截火车：一条轨道都没规划出来", this);
                yield break;
            }
            BuildDepartureSchedule();
            BuildTurretPlan();   // 附加炮塔也在开局（本协程第一个 yield 之前）一次算好 ⇒ 两端一致（见 Train 分部）
            NetSyncLog.LoadLog("任务", $"[拦截火车] 共 {tracks.Count} 条轨道 / 总长 {TotalTrackLength():F0}m / "
                + $"发车时刻表 [{string.Join(",", departureTracks)}]");

            // ── 第二阶段（有 yield）：逐条整平路基 + 铺轨 ──
            for (int i = 0; i < tracks.Count; i++)
            {
                yield return FinishTrack(tracks[i], clearRadius);
            }
        }

        /// <summary>所有轨道总长（米）</summary>
        private float TotalTrackLength()
        {
            float total = 0f;
            for (int i = 0; i < tracks.Count; i++) total += tracks[i].TotalLength;
            return total;
        }

        /// <summary>
        /// 规划一条轨道（<b>同步、无 yield</b>）：起终点 → A* → 平滑 → 弧长表 → 走廊登记/清物 → 设计路基。
        /// </summary>
        private RailTrack PlanTrack(int index, Vector2 mapCenter, float mapRadius, Vector3 origin, Vector3 size,
            float clearRadius, List<TerrainUtils.AreaCircle> selfCorridors, out int corridorStamps)
        {
            corridorStamps = 0;
            // 避让圆每次都现读：上一条轨道刚登记进去的走廊会被自动算进来（所以三条轨道不会互相压）
            List<TerrainUtils.AreaCircle> obstacles = CollectObstacles(mapRadius);
            // 再加"自家已铺轨道"的**实际开挖走廊**：登记半径(12m)小于开挖半径，只靠登记表会让两条轨道的路基带重叠
            // ⇒ 反复切挖同一片地，看起来就是褶皱（2026-10-15 实测）
            if (selfCorridors != null && selfCorridors.Count > 0) obstacles.AddRange(selfCorridors);
            if (!TryPickEdgePair(mapCenter, mapRadius, obstacles, avoidMargin, true, out Vector2 startPoint, out Vector2 endPoint))
                return null;

            List<Vector2> plan = PlanWithRelaxation(startPoint, endPoint, mapCenter, mapRadius, origin, size, obstacles, out float usedMargin);
            for (int attempt = 1; attempt < 5 && plan == null; attempt++)
            {
                // 还是没路：换一对起终点（原来那对可能整个被占位圆包住了）
                if (!TryPickEdgePair(mapCenter, mapRadius, obstacles, avoidMargin, false, out startPoint, out endPoint)) break;
                plan = PlanWithRelaxation(startPoint, endPoint, mapCenter, mapRadius, origin, size, obstacles, out usedMargin);
            }
            if (plan == null || plan.Count < 2)
            {
                // ⚠ 直线兜底会把两条轨道叠着切开同一片地形（实测表现为"褶皱"），所以这里必须留下告警
                Debug.LogWarning($"拦截火车：轨道{index + 1} 怎么试都规划不出路径，退化成直线（地形会被叠着切挖）", this);
                plan = new List<Vector2> { startPoint, endPoint };
            }

            RailTrack track = new() { Index = index, StartPoint = startPoint, EndPoint = endPoint };
            for (int i = 0; i < plan.Count; i++) track.Points.Add(TerrainUtils.WSToTS(plan[i]));
            SmoothToTurnLimit(track.Points, maxVertexTurnDeg, smoothPasses);
            track.BuildCumulative();

            // ★ 走廊登记 + 清物请求（同步，第一阶段的最后一步）；同时把自己的实际开挖范围记进 selfCorridors
            corridorStamps = StampAlongPath(track, clearRadius, selfCorridors);

            // 设计路基高程：沿弧长采地面高度 → 长窗滑动平均 ⇒ 一条连续纵坡
            track.ProfileStep = Mathf.Max(2f, bedStep);
            track.BedProfile = BuildBedProfile(track, track.ProfileStep, bedSmoothLength);
            NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{index + 1} 用到的避让余量 {usedMargin:F0}m / 避让圆 {obstacles.Count} 个");
            return track;
        }

        /// <summary>设计路基纵坡：沿弧长采地面高度再滑动平均（长窗 = <paramref name="smoothLength"/> 米）</summary>
        private static float[] BuildBedProfile(RailTrack track, float profileStep, float smoothLength)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(track.TotalLength / profileStep) + 1);
            float[] ground = new float[count];
            for (int i = 0; i < count; i++)
            {
                float s = Mathf.Min(track.TotalLength, i * profileStep);
                ground[i] = TerrainUtils.WSToHeight(track.SampleXZ(s));
            }
            float[] profile = new float[count];
            int halfWindow = Mathf.Max(1, Mathf.RoundToInt(smoothLength / profileStep) / 2);
            for (int i = 0; i < count; i++)
            {
                int from = Mathf.Max(0, i - halfWindow);
                int to = Mathf.Min(count - 1, i + halfWindow);
                float sum = 0f;
                for (int j = from; j <= to; j++) sum += ground[j];
                profile[i] = sum / (to - from + 1);
            }
            return profile;
        }

        /// <summary>第二阶段：把一条轨道的路基整平并逐节铺满（有 yield）</summary>
        private IEnumerator FinishTrack(RailTrack track, float clearRadius)
        {
            float totalLength = track.TotalLength;
            if (totalLength <= 1f) yield break;

            // 路基整平（统一成型）：按"设计高程沿弧长连续"一次性切/填，断面横向平滑收边 ⇒ 没有逐块压平的台阶
            bool flattened = false;
            if (flattenThreshold > 0f)
            {
                float centerStep = Mathf.Max(2f, track.ProfileStep * 0.5f);
                int centerCount = Mathf.Max(2, Mathf.CeilToInt(totalLength / centerStep) + 1);
                Vector3[] centerline = new Vector3[centerCount];
                for (int i = 0; i < centerCount; i++)
                {
                    float s = Mathf.Min(totalLength, i * centerStep);
                    Vector2 point = track.SampleXZ(s);
                    centerline[i] = new Vector3(point.x, track.BedHeight(s), point.y);
                }
                // refresh=false：收尾统一 Flush 一次（顺带把 NavMesh 重烘也合并掉）
                yield return TerrainUtils.SmoothCorridorBed(centerline, railMetrics.Width * 0.5f + clearMargin,
                    flattenBlend, flattenThreshold, false);
                flattened = TerrainUtils.LastCorridorPixels > 0;
                NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 路基整平：动土 {TerrainUtils.LastCorridorPixels} 像素 / "
                    + $"最大高差 {TerrainUtils.LastCorridorMaxDelta:F2}m（取样间距 {centerStep:F1}m）");
            }

            // 逐节铺：断面贴设计路基、长轴跟着纵坡俯仰 ⇒ 相邻两节端头落在同一点
            int pieces = 0;
            int count2 = Mathf.Max(1, Mathf.RoundToInt(totalLength / railMetrics.Length));
            float span = totalLength / count2;
            for (int k = 0; k < count2; k++)
            {
                Vector2 p0 = track.SampleXZ(k * span);
                Vector2 p1 = track.SampleXZ(Mathf.Min((k + 1f) * span, totalLength));
                Vector2 flat = p1 - p0;
                if (flat.sqrMagnitude < 0.0001f) continue;
                float len = flat.magnitude;
                Vector2 dir = flat / len;
                Vector2 center = (p0 + p1) * 0.5f;

                float bed0 = track.BedHeight(k * span);
                float bed1 = track.BedHeight(Mathf.Min((k + 1f) * span, totalLength));
                float grade = Mathf.Atan2(bed1 - bed0, len);
                Vector3 forward = new(dir.x * Mathf.Cos(grade), Mathf.Sin(grade), dir.y * Mathf.Cos(grade));
                // 长轴对齐本节弦向（预制体长轴不一定朝 +Z）
                Quaternion rot = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, railMetrics.YawOffset, 0f);
                GameObject rail = Instantiate(railPrefab,
                    new Vector3(center.x, (bed0 + bed1) * 0.5f - railMetrics.PivotToBottom * Mathf.Cos(grade), center.y),
                    rot, transform);

                Vector3 scale = rail.transform.localScale;
                float pieceScale = len / railMetrics.Length;
                if (railMetrics.LengthAxis == 0) scale.x *= pieceScale;
                else scale.z *= pieceScale;
                rail.transform.localScale = scale;

                if ((++pieces & 7) == 7) yield return null;
            }

            // 一次兑现：跳过 4/s、8/s 限流与 RefreshInterval，免得轨道先出来、树还杵在上面
            if (flattened) TerrainClearer.MarkTerrainChanged();
            TerrainClearer.Flush();
            NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 铺完 {pieces} 节 / 长 {totalLength:F0}m / "
                + $"清物半径 {clearRadius:F1}m（树管理器 {(TreeDestructor.Instance != null ? "在" : "不在")}） / 整平 {flattened}");
        }

        #region 起终点

        /// <summary>
        /// 收集要避让的圆：已占圆（任务点等）+ 地表占地圆（巨型悬崖）。
        /// <para>⚠ 半径 ≥ 40% 地图可用半径的圆会被剔除：那种尺寸的圆一旦计入就把整张图堵死
        /// （实测：A* 直接无解 ⇒ 退化直线、看起来"完全没躲避"），而它本身也没法绕开，不如放行。</para>
        /// </summary>
        private static List<TerrainUtils.AreaCircle> CollectObstacles(float mapRadius)
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
        /// 在地图边缘（空气墙圆周）取一对起点/终点：夹角 ≥ <see cref="minAngleSeparation"/>、
        /// 弦长 ≥ 半径 × <see cref="minChordRatio"/>（"距离不少于半径一半"这层口径落在弦长上）。
        /// <para><paramref name="requireFree"/> = 两点还都不能撞避让圆；躲不开时传 false，只保夹角/弦长并挑最平的落点。</para>
        /// </summary>
        private bool TryPickEdgePair(Vector2 center, float radius, List<TerrainUtils.AreaCircle> obstacles, float margin,
            bool requireFree, out Vector2 startPoint, out Vector2 endPoint)
        {
            startPoint = default;
            endPoint = default;
            if (!TryPickEdgePoint(center, radius, -1f, obstacles, margin, requireFree, out startPoint, out float startAngle)) return false;
            if (!TryPickEdgePoint(center, radius, startAngle, obstacles, margin, requireFree, out endPoint, out _)) return false;
            return true;
        }

        /// <summary>
        /// 圆周上取一个边缘点：与 <paramref name="excludeAngle"/> 夹角 ≥ <see cref="minAngleSeparation"/>、
        /// 弦长 ≥ 半径 × <see cref="minChordRatio"/>；<paramref name="requireFree"/> = 还必须不撞避让圆。
        /// 在合格候选里挑<b>最平</b>的一处（边缘多半是缓坡/沙滩，端点压在陡坡上轨道会翘）。
        /// </summary>
        /// <param name="excludeAngle">已选点的角度；&lt; 0 表示本次不校验取舍（第一个点）</param>
        private bool TryPickEdgePoint(Vector2 center, float radius, float excludeAngle,
            List<TerrainUtils.AreaCircle> obstacles, float margin, bool requireFree, out Vector2 point, out float angle)
        {
            point = default;
            angle = 0f;
            float bestSteepness = float.MaxValue;
            bool found = false;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                float a = (float)random.NextDouble() * 360f;
                Vector2 candidate = PointOnEdge(center, radius, a);

                if (excludeAngle >= 0f)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(a, excludeAngle)) < minAngleSeparation) continue;
                    if (Vector2.Distance(candidate, PointOnEdge(center, radius, excludeAngle)) < radius * minChordRatio) continue;
                }
                if (requireFree && HitCircle(obstacles, candidate, margin)) continue;

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
        /// <para>代价 = 距离 ×（1 + <see cref="slopeWeight"/> × 坡度/45°）⇒ 缓坡优先；</para>
        /// <para>不可走 = 出空气墙圆 / 撞已占圆（任务点等，半径 + <see cref="avoidMargin"/>）/ 撞地表占地圆（巨型悬崖）；</para>
        /// <para>规划完"拉直"：只把<b>直连不比绕行贵</b>的那段直连（判据用同一套含坡度的代价）。
        /// ⚠ 拉直判据若只看碰撞，任何地形都会被拉成一条直线（实测踩过）。</para>
        /// </summary>
        /// <returns>世界 XZ 路点（含起终点）；无解返回 null</returns>
        private List<Vector2> PlanPath(Vector2 start, Vector2 end, Vector2 mapCenter, float mapRadius,
            Vector3 origin, Vector3 size, List<TerrainUtils.AreaCircle> obstacles, float margin)
        {
            // 只规划"起终点包围盒 + 外扩"这一块：全图铺格子在大图上纯浪费
            float cell = Mathf.Max(4f, stepLength * 0.5f);
            Vector2 min = Vector2.Max(Vector2.Min(start, end) - Vector2.one * planExpand,
                new Vector2(origin.x + 1f, origin.z + 1f));
            Vector2 max = Vector2.Min(Vector2.Max(start, end) + Vector2.one * planExpand,
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
            // 把端点周围 stepLength 半径内的格子强行放开（2026-10-15：轨道2/3 规划不出来、退化成直线就是这么来的）
            int guard = Mathf.Max(1, Mathf.CeilToInt(stepLength / cell));
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
                    float nextCost = cost[current] + step * (1f + slopeWeight * slope[next] / 45f);
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
        private List<Vector2> PlanWithRelaxation(Vector2 start, Vector2 end, Vector2 mapCenter, float mapRadius,
            Vector3 origin, Vector3 size, List<TerrainUtils.AreaCircle> obstacles, out float usedMargin)
        {
            usedMargin = avoidMargin;
            List<Vector2> plan = PlanPath(start, end, mapCenter, mapRadius, origin, size, obstacles, avoidMargin);
            if (plan != null) return plan;

            usedMargin = avoidMargin * 0.5f;
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
                cost += length / steps * (1f + slopeWeight * slope[index] / 45f);
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
        private static void SmoothToTurnLimit(List<Vector3> points, float maxTurnDeg, int maxPasses)
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

        #region 地表处理与尺寸

        /// <summary>
        /// 沿整条路径盖章：① 登记"已占圆"（半径 <see cref="corridorRegisterRadius"/>，供后来的任务点/兴趣点/场景点避让）
        /// ② 把走廊里的树 / 石块 / 草花排进清除队列（半径 <paramref name="clearRadius"/>）。
        /// <para>⚠ 用 <c>ClearInRadius</c> 盖圆而不是 <c>ClearInRectXZ</c>：后者只支持<b>轴对齐</b>矩形，斜轨道会多清一大片。</para>
        /// <para>⚠ 本方法<b>不含 yield</b>：它要在协程的第一个 yield 之前跑完（走廊登记必须早于后续任务点生成）。</para>
        /// </summary>
        /// <returns>盖章数（日志可见）</returns>
        private int StampAlongPath(RailTrack track, float clearRadius, List<TerrainUtils.AreaCircle> selfCorridors)
        {
            int stamps = Mathf.Max(1, Mathf.CeilToInt(track.TotalLength / Mathf.Max(1f, clearStep)));
            for (int i = 0; i < stamps; i++)
            {
                float s = Mathf.Min(track.TotalLength, (i + 0.5f) * (track.TotalLength / stamps));
                Vector2 center = track.SampleXZ(s);
                TerrainClearer.ClearInRadius(new Vector3(center.x, 0f, center.y), clearRadius, clearTarget);
                // 给"其它系统"（任务点等）登记的是保守小半径；给自家下一条轨道用的是实际开挖半径
                if (registerCorridor) TerrainUtils.AddReservedCircle(center, corridorRegisterRadius);
                if (selfCorridors != null) selfCorridors.Add(new TerrainUtils.AreaCircle(center, clearRadius));
            }
            return stamps;
        }

        /// <summary>世界点的坡度（角度制，0 = 平地，90 = 崖壁）</summary>
        private static float SteepnessDeg(Vector2 worldPos)
        {
            Vector2 uv = TerrainUtils.WSToUV(worldPos);
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            // ⚠ 是 TerrainData.GetSteepness（Terrain 上没有这个方法）：吃"归一化坐标"、返回弧度
            return TerrainUtils.Main.terrainData.GetSteepness(uv.x, uv.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 量一个模型预制体（车头/车厢/轨道通用）：长轴 / 短轴长度、长轴是哪根轴、
        /// 把长轴转到 +Z 需要的偏航、底面到 pivot 的高度。
        /// <para>⚠ <b>绝不能 Instantiate 探针</b>：那会跑 prefab 上 <c>Actor.Awake</c> ⇒ 派发 <c>EnemyCreate</c> ⇒
        /// HpWnd 给"探针"建一个 boss 血条，而我们马上把它销毁 ⇒ 血条抱着已销毁的 Actor 每帧抛
        /// MissingReferenceException（2026-10-15 实测）。这里直接用<b>资产自身的层级</b>算根空间包围盒，零副作用。</para>
        /// <para>⚠ 不假设美术资源的朝向与尺度：换模型不必改代码。</para>
        /// </summary>
        private static RailMetrics MeasureRail(GameObject prefab)
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

        /// <summary>轨道尺寸（由预制体量出来，不写死）</summary>
        private struct RailMetrics
        {
            /// <summary>长轴长度（米）= 一节的默认跨度</summary>
            public float Length;
            /// <summary>短轴长度（米）= 轨道宽度（清物走廊 / 路基内框按它算）</summary>
            public float Width;
            /// <summary>长轴是哪根轴：0 = local X，2 = local Z</summary>
            public int LengthAxis;
            public float YawOffset;
            public float PivotToBottom;
        }

        #endregion
    }
}
