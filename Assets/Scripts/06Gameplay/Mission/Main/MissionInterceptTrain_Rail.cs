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
    /// 拦截火车 · 轨道生成（partial）：把"量预制体尺寸 → 选起终点 → 规划 → 走廊登记/清物 → 路基成型 → 逐节铺轨"
    /// 这条流程编排起来；<b>算法部分（边缘选点 / 栅格 A* / 折线平滑）在 <see cref="RailPathPlanner"/>、
    /// 量尺在 <see cref="RailMetrics"/></b>。
    /// <para>⚠ 规划与"走廊登记"必须在本协程的<b>第一个 yield 之前</b>跑完（见主文件 <c>InitMission</c> 的说明）：
    /// 登记得早，后面创建的其它任务点/兴趣点才会反过来躲开铁轨。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        #region 调参常量
        // 这些旋钮（选点 / A* / 路基 / 清物）只在开发期调过一次，之后再没动过 ⇒ 收成常量，
        // Inspector 只留"换地图、换轨道预制体才会改"的几项。要再调就直接改这里（改回字段即可回到 Inspector）。
        private const float MinAngleSeparation = 120f;      // 起终点最小夹角(度)
        private const float MinChordRatio = 0.5f;           // 起终点最小弦长(占地图半径比例)
        private const float StepLength = 24f;               // 参考节距(米)
        private const float PlanExpand = 200f;              // 规划外扩(米)
        private const float AvoidMargin = 15f;              // 避让余量(米)
        private const float SlopeWeight = 2f;               // 坡度代价权重
        private const float MaxVertexTurnDeg = 8f;          // 单顶点最大转角(度)
        private const int SmoothPasses = 3;                 // 路径平滑次数
        private const float BedStep = 8f;                   // 路基取样间距(米)
        private const float BedSmoothLength = 60f;          // 路基纵坡平滑长度(米)
        private const float FlattenThreshold = 1.2f;        // 路基整平阈值(米)
        private const float FlattenBlend = 6f;              // 路基淡化带(米)
        private const float ClearStep = 8f;                 // 清物盖章间距(米)
        private const float ClearMargin = 1.5f;             // 清物余量(米)
        private const float ClearCanopy = 5f;               // 清物树冠补偿(米)
        private const bool RegisterCorridor = true;         // 登记走廊为已占(供后来者避让)
        private const float CorridorRegisterRadius = 12f;   // 走廊登记半径(米)
        // ⚠ 清物目标只给"植被 + 草花"：含 Cover 会把巨型悬崖（RockCoverDestructor）一起清掉。
        //   prefab 里原本存的是 -1（所有位都置上 ⇒ 连悬崖一起清），2026-10-18 一并纠正。
        private const TerrainClearTarget ClearTarget = TerrainClearTarget.VegetationAndDetail;
        #endregion

        [Foldout("轨道", true)]
        [InspectorName("轨道条数")]
        [SerializeField] private int trackCount = 3;
        [InspectorName("轨道预制体")]
        [SerializeField] private GameObject railPrefab;
        [InspectorName("边缘内缩(米)")]
        [SerializeField] private float edgeInset = 5f;

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

            railMetrics = RailMetrics.Measure(railPrefab);
            Vector3 origin = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            Vector2 mapCenter = new(origin.x + size.x * 0.5f, origin.z + size.z * 0.5f);
            float mapRadius = Mathf.Min(size.x, size.z) * 0.5f - Constants.TaskBorder - edgeInset;
            if (mapRadius <= StepLength)
            {
                Debug.LogError($"拦截火车：地图可用半径过小（{mapRadius:F0}m），铺不出轨道", this);
                yield break;
            }

            // ── 第一阶段（同步，必须没有 yield）：逐条规划 + 登记走廊 + 定发车时刻表 ──
            float clearRadius = railMetrics.Width * 0.5f + ClearMargin + ClearCanopy
                + (FlattenThreshold > 0f ? FlattenBlend : 0f);
            tracks.Clear();
            // 已铺轨道的**实际开挖走廊**（半径比登记的 12m 大）——用来保证多条轨道不互相切开对方的走廊
            List<TerrainUtils.AreaCircle> selfCorridors = new();
            int wanted = Mathf.Max(1, trackCount);
            int skipped = 0;
            for (int i = 0; i < wanted; i++)
            {
                RailTrack track = PlanTrack(i, mapCenter, mapRadius, origin, size, clearRadius, selfCorridors, out int corridorStamps);
                if (track == null)
                {
                    skipped++;
                    continue;
                }
                tracks.Add(track);
                NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 规划：路点 {track.Points.Count} / 总长 {track.TotalLength:F0}m / "
                    + $"走廊登记 {corridorStamps} 处（半径 {CorridorRegisterRadius:F0}m） / 起 {track.StartPoint} 终 {track.EndPoint}");
            }
            if (skipped > 0)
            {
                Debug.LogWarning($"拦截火车：计划 {wanted} 条轨道，规划失败跳过 {skipped} 条（实际生成 {tracks.Count} 条）", this);
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
        /// <para><b>返回 null = 本条放弃</b>（试过几对起终点都规划不出路径）：不再退化成直线 —— 直线会叠着切挖别处地形。</para>
        /// </summary>
        private RailTrack PlanTrack(int index, Vector2 mapCenter, float mapRadius, Vector3 origin, Vector3 size,
            float clearRadius, List<TerrainUtils.AreaCircle> selfCorridors, out int corridorStamps)
        {
            corridorStamps = 0;
            // 规划器：边缘选点 / 栅格 A* / 折线平滑整块算法都在 RailPathPlanner.cs（参数注入，不碰本类状态）
            RailPathPlanner planner = new(StepLength, PlanExpand, SlopeWeight, AvoidMargin,
                MinAngleSeparation, MinChordRatio, random);
            // 避让圆每次都现读：上一条轨道刚登记进去的走廊会被自动算进来（所以三条轨道不会互相压）
            List<TerrainUtils.AreaCircle> obstacles = RailPathPlanner.CollectObstacles(mapRadius);
            // 再加"自家已铺轨道"的**实际开挖走廊**：登记半径(12m)小于开挖半径，只靠登记表会让两条轨道的路基带重叠
            // ⇒ 反复切挖同一片地，看起来就是褶皱（2026-10-15 实测）
            if (selfCorridors != null && selfCorridors.Count > 0) obstacles.AddRange(selfCorridors);
            if (!planner.TryPickEdgePair(mapCenter, mapRadius, obstacles, true, out Vector2 startPoint, out Vector2 endPoint))
                return null;

            List<Vector2> plan = planner.PlanWithRelaxation(startPoint, endPoint, mapCenter, mapRadius, origin, size, obstacles, out float usedMargin);
            for (int attempt = 1; attempt < 5 && plan == null; attempt++)
            {
                // 还是没路：换一对起终点（原来那对可能整个被占位圆包住了）
                if (!planner.TryPickEdgePair(mapCenter, mapRadius, obstacles, false, out startPoint, out endPoint)) break;
                plan = planner.PlanWithRelaxation(startPoint, endPoint, mapCenter, mapRadius, origin, size, obstacles, out usedMargin);
            }
            if (plan == null || plan.Count < 2)
            {
                // 规划不出来就**放弃这一条**，不再退化成直线：直线兜底会把两条轨道叠着切开同一片地形
                // （实测表现为"褶皱"），而且退化的那条车还是照跑 ⇒ 宁可少于 trackCount，也不要一条假轨道。
                // 上层 BuildAllTracks 会对 null 直接 continue，时刻表只引用真正建成的轨道。
                Debug.LogWarning($"拦截火车：轨道{index + 1} 怎么试都规划不出路径，本次跳过（不生成直线兜底）", this);
                return null;
            }

            // Index 用"已建成条数"而不是循环序号：中间跳过一条时编号仍然连续（日志/时刻表口径一致）
            RailTrack track = new() { Index = tracks.Count, StartPoint = startPoint, EndPoint = endPoint };
            for (int i = 0; i < plan.Count; i++) track.Points.Add(TerrainUtils.WSToTS(plan[i]));
            RailPathPlanner.SmoothToTurnLimit(track.Points, MaxVertexTurnDeg, SmoothPasses);
            track.BuildCumulative();

            // ★ 走廊登记 + 清物请求（同步，第一阶段的最后一步）；同时把自己的实际开挖范围记进 selfCorridors
            corridorStamps = StampAlongPath(track, clearRadius, selfCorridors);

            // 设计路基高程：沿弧长采地面高度 → 长窗滑动平均 ⇒ 一条连续纵坡
            track.ProfileStep = Mathf.Max(2f, BedStep);
            track.BedProfile = BuildBedProfile(track, track.ProfileStep, BedSmoothLength);
            NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 用到的避让余量 {usedMargin:F0}m / 避让圆 {obstacles.Count} 个");
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
            if (FlattenThreshold > 0f)
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
                yield return TerrainUtils.SmoothCorridorBed(centerline, railMetrics.Width * 0.5f + ClearMargin,
                    FlattenBlend, FlattenThreshold, false);
                flattened = TerrainUtils.LastCorridorPixels > 0;
                NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 路基整平：动土 {TerrainUtils.LastCorridorPixels} 像素 / "
                    + $"最大高差 {TerrainUtils.LastCorridorMaxDelta:F2}m（取样间距 {centerStep:F1}m）");
            }

            // 逐节铺：断面贴设计路基、长轴跟着纵坡俯仰 ⇒ 相邻两节端头落在同一点
            int pieces = 0;
            int count2 = Mathf.Max(1, Mathf.RoundToInt(totalLength / railMetrics.Length));
            float span = totalLength / count2;
            // 方向箭头：节与节的接缝各放一个（prefab 上已配好；没配 ⇒ 整局不放，箭头只是装饰）
            GameObject arrowPrefab = trainArrowPrefab;
            RailMetrics arrowMetrics = arrowPrefab != null ? RailMetrics.Measure(arrowPrefab) : default;
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
                    new Vector3(center.x, (bed0 + bed1) * 0.5f -  Mathf.Cos(grade), center.y),
                    rot, transform);

                Vector3 scale = rail.transform.localScale;
                float pieceScale = len / railMetrics.Length;
                if (railMetrics.LengthAxis == 0) scale.x *= pieceScale;
                else scale.z *= pieceScale;
                rail.transform.localScale = scale;

                // 本节起点 = 上一节与本节的接缝（k = 0 是轨道起点，那里不放箭头）
                if (k > 0 && arrowPrefab != null)
                {
                    SpawnTrackArrow(track, k, p0, bed0 - Mathf.Cos(grade), forward, arrowPrefab, arrowMetrics);
                }

                if ((++pieces & 7) == 7) yield return null;
            }

            SealTrackArrows(track);   // 箭头先全灭（材质资产里的强度是亮的），发车时才点亮
            // 一次兑现：跳过 4/s、8/s 限流与 RefreshInterval，免得轨道先出来、树还杵在上面
            if (flattened) TerrainClearer.MarkTerrainChanged();
            TerrainClearer.Flush();
            NetSyncLog.LoadLog("任务", $"[拦截火车] 轨道{track.Index + 1} 铺完 {pieces} 节 / 长 {totalLength:F0}m / "
                + $"清物半径 {clearRadius:F1}m（树管理器 {(TreeDestructor.Instance != null ? "在" : "不在")}） / 整平 {flattened}");
        }

        #region 地表处理

        /// <summary>
        /// 沿整条路径盖章：① 登记"已占圆"（半径 <see cref="CorridorRegisterRadius"/>，供后来的任务点/兴趣点/场景点避让）
        /// ② 把走廊里的树 / 石块 / 草花排进清除队列（半径 <paramref name="clearRadius"/>）。
        /// <para>⚠ 用 <c>ClearInRadius</c> 盖圆而不是 <c>ClearInRectXZ</c>：后者只支持<b>轴对齐</b>矩形，斜轨道会多清一大片。</para>
        /// <para>⚠ 本方法<b>不含 yield</b>：它要在协程的第一个 yield 之前跑完（走廊登记必须早于后续任务点生成）。</para>
        /// </summary>
        /// <returns>盖章数（日志可见）</returns>
        private int StampAlongPath(RailTrack track, float clearRadius, List<TerrainUtils.AreaCircle> selfCorridors)
        {
            int stamps = Mathf.Max(1, Mathf.CeilToInt(track.TotalLength / Mathf.Max(1f, ClearStep)));
            for (int i = 0; i < stamps; i++)
            {
                float s = Mathf.Min(track.TotalLength, (i + 0.5f) * (track.TotalLength / stamps));
                Vector2 center = track.SampleXZ(s);
                TerrainClearer.ClearInRadius(new Vector3(center.x, 0f, center.y), clearRadius, ClearTarget);
                // 给"其它系统"（任务点等）登记的是保守小半径；给自家下一条轨道用的是实际开挖半径
                if (RegisterCorridor) TerrainUtils.AddReservedCircle(center, CorridorRegisterRadius);
                if (selfCorridors != null) selfCorridors.Add(new TerrainUtils.AreaCircle(center, clearRadius));
            }
            return stamps;
        }

        #endregion
    }
}