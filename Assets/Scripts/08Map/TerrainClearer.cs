using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsGame.MapUtils
{
    /// <summary>要清除哪几类地表物（可组合）</summary>
    [Flags]
    public enum TerrainClearTarget
    {
        None = 0,

        /// <summary>地形树（TerrainData.treeInstances，默认原型区间 7-10）</summary>
        Tree = 1 << 0,

        /// <summary>石块（同样是 treeInstances，默认原型区间 0-6）</summary>
        Rock = 1 << 1,

        /// <summary>细节植被：草 / 花（Detail 层）</summary>
        Detail = 1 << 2,

        /// <summary>地形覆盖物：巨型悬崖等预制体</summary>
        Cover = 1 << 3,

        /// <summary>树 + 石块（"植被"这一坨）</summary>
        Vegetation = Tree | Rock,

        /// <summary>树 + 石块 + 草花（爆炸弹坑的常规组合）</summary>
        VegetationAndDetail = Tree | Rock | Detail,

        /// <summary>全部四类（地形被改造 / 被覆盖时的常规组合）</summary>
        All = Tree | Rock | Detail | Cover,
    }

    /// <summary>
    /// 地表物清除的统一门面 + 逐帧合并处理器。
    /// <para>树 / 石块 / 草花 / 地形覆盖物这四类看起来都是"按范围清掉"，但底层是三套完全不同的数据与提交方式：</para>
    /// <para>① 树与石块 = <see cref="TreeDestructor"/>：树实例整表回写（销毁 = 缩放置 0，索引不变、可还原），限流 4/s；</para>
    /// <para>② 草花 = <see cref="TerrainDetailEraser"/>：逐 Detail 层重写，限流 8/s；</para>
    /// <para>③ 覆盖物 = <see cref="RockCoverDestructor"/>：销毁预制体并撤回 TerrainUtils.AreaCircles，立即生效（不入队）。</para>
    /// <para>调用方（<c>ModifyTerrain</c> / <c>FpsHelper.Hit</c> …）只需要记住这一个调用口。</para>
    ///
    /// <para><b>逐帧合并（本类存在的第二个理由）：</b>与清除相随的是三种"全局昂贵操作"——
    /// ① 树实例整表提交（几千个实例）② 树碰撞体重建（整张地形碰撞体）③ NavMesh 重烘（<c>nav.UpdateNavMesh</c>）。</para>
    /// <para>散点触发时（一炮一个弹坑）它们会被反复执行，所以这里把"一帧内的请求"和"重烘"合并成一个处理点：</para>
    /// <para>· 请求先入队，帧末（<see cref="Driver"/> 的 LateUpdate，或调用方显式 <see cref="Flush"/>）统一下发；</para>
    /// <para>· 昂贵操作按 <see cref="RefreshInterval"/> 限流，且**先强制提交树/草队列、再重建碰撞体、最后重烘 NavMesh**，
    /// 保证重烘看到的一定是最新数据（原来三个限流各走各的时最容易在这里错位）。</para>
    ///
    /// <para>⚠ 两套语义仍然保留：<see cref="ClearInRadius"/> = "清除"（忽略"可被摧毁"白名单，地形被改造时用）；</para>
    /// <para><see cref="DestroyInRadius"/> = "摧毁"（走白名单，爆炸伤害用）。</para>
    /// </summary>
    public static class TerrainClearer
    {
        /// <summary>
        /// 树使用的原型区间（含头含尾）。
        /// <para>⚠ 每张地图的原型数量不同，所以这个值**由 <see cref="GenerateNoiseTerrain"/> 按
        /// <c>MapData_SO</c> 的 <c>stonePrototypes</c>/<c>treePrototypes</c> 数组长度在生成地形时写入**
        /// （石块在前、树紧随其后）；这里的默认值只是"没人写过"时的兜底约定。</para>
        /// <para>为空区间（<c>y &lt; x</c>）= 本图没有树原型。</para>
        /// </summary>
        public static Vector2Int TreePrototypeRange = new Vector2Int(7, 10);

        /// <summary>
        /// 石块使用的原型区间（含头含尾）。
        /// <para>与 <see cref="TreePrototypeRange"/> 同源，由 <see cref="GenerateNoiseTerrain"/> 在地形生成时写入。</para>
        /// </summary>
        public static Vector2Int RockPrototypeRange = new Vector2Int(0, 6);

        /// <summary>
        /// 地形重烘（树碰撞体重建 + NavMesh 重烘）的最小间隔（秒）；0 = 每帧最多一次。
        /// <para>默认 0.25s（≈4/s，与树实例提交的限流同量级）；调大可进一步省性能，调小则弹坑/悬崖的寻路更快生效。</para>
        /// </summary>
        public static float RefreshInterval = 0.25f;

        #region 请求队列

        private enum RequestShape
        {
            Circle,
            Rect,
        }

        /// <summary>一条待处理的清除请求</summary>
        private struct Request
        {
            public RequestShape Shape;
            public Vector3 Center;
            public float Radius;
            public Vector2 HalfSize;
            public TerrainClearTarget Target;
            public Vector2Int TreeRange;
            public Vector2Int RockRange;
            /// <summary>true = "摧毁"语义（走可被摧毁白名单）</summary>
            public bool IsDestroy;
        }

        private static readonly List<Request> _requests = new();

        /// <summary>地形高度被改过（需要重烘 NavMesh）</summary>
        private static bool _terrainChanged;

        private static float _lastRefreshTime = float.NegativeInfinity;

        /// <summary>
        /// NavMesh 重烘的最小间隔（秒），与 <see cref="RefreshInterval"/> 解耦。
        /// <para>NavMesh 是异步的、真实烘焙开销远大于"发起"那几毫秒，所以默认给 1s（实测 5.6ms 只是发起耗时）。</para>
        /// </summary>
        public static float NavRefreshInterval = 1f;

        /// <summary>
        /// 是否在"树/石块真的被清过"后重建树碰撞体。**默认 true**（Terrain 开了"启用树碰撞器"时必须保持 true，
        /// 否则被清掉的树会留下看不见的"隐形墙"）。
        /// <para>⚠ 实测（2026-09-21，桥 + 物理射线）：树的碰撞体确实存在（抽样 6/6 棵树在离地 17~24m 命中
        /// TerrainCollider），重建一次 <b>10.75ms</b>（<c>SetTreeInstances</c> 只要 0.08ms、<c>terrain.Flush</c> 0.69ms）
        /// —— 也就是这笔钱是**必要开销**，只是在"树没有碰撞体"时才可以省。</para>
        /// <para>如果哪天把 Terrain Collider 的"启用树碰撞器"**关掉**了，把它设为 false 可省掉这 10.75ms/次。</para>
        /// </summary>
        public static bool SyncTreeCollidersOnTreeChange = true;

        /// <summary>
        /// true = 地形一改动就重建树碰撞体（更保守）。
        /// <para>默认 false：树碰撞体的位置由树实例决定、**不跟随高度图**，所以"只改高度没清树"的弹坑
        /// 不需要重建（重建了也是同样结果）；只有清掉树/石块才需要。</para>
        /// </summary>
        public static bool RebuildCollidersOnTerrainChange = false;

        /// <summary>
        /// 树碰撞体重建的最小间隔（秒）。默认 0.5s —— 重建一次 10.75ms，没必要跟着 0.25s 的处理点走；
        /// 提交版本号会记住变化，所以延迟重建不会丢更新（代价：被清掉的树最多留 0.5s 的隐形碰撞体）。
        /// </summary>
        public static float ColliderSyncInterval = 0.5f;

        /// <summary>地形被改过、正等着一次 NavMesh 重烘（会一直保留到真的烘完，避免被节流吞掉）</summary>
        private static bool _navDirty;

        /// <summary>正在进行的 NavMesh 重烘（没烘完就不再发下一次）</summary>
        private static AsyncOperation _navOperation;

        private static float _lastNavTime = float.NegativeInfinity;

        /// <summary>上次重建树碰撞体的时间（用于 <see cref="ColliderSyncInterval"/> 限流）</summary>
        private static float _lastColliderSyncTime = float.NegativeInfinity;

        /// <summary>上次重建树碰撞体时的"树提交版本号"（用于判断树到底变没变）</summary>
        private static int _colliderSyncedVersion;

        /// <summary>当前积压的请求数（调试/诊断用）</summary>
        public static int PendingRequests => _requests.Count;

        #endregion

        #region 计时（实测用）

        /// <summary>是否打印各阶段耗时（Play 模式实测用；关掉即零开销，只留一点点 Stopwatch 调用）</summary>
        public static bool LogTiming = true;

        /// <summary>每多少次样本额外打印一次"均值/峰值"（1 = 每次都打）</summary>
        public static int TimingLogEverySamples = 5;

        private enum Phase
        {
            Dispatch = 0,
            TreeFlush,
            DetailFlush,
            TreeCollider,
            NavMesh,
            Total,
            Count,
        }

        private static readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private static readonly double[] _sumMs = new double[(int)Phase.Count];
        private static readonly double[] _maxMs = new double[(int)Phase.Count];
        private static int _sampleCount;

        /// <summary>累计样本数</summary>
        public static int TimingSamples => _sampleCount;

        /// <summary>清空计时统计（重新开一局实测前调一次）</summary>
        public static void ResetTimingStats()
        {
            for (int i = 0; i < (int)Phase.Count; i++)
            {
                _sumMs[i] = 0d;
                _maxMs[i] = 0d;
            }
            _sampleCount = 0;
            TerrainUtils.ResetHeightTimingStats();
        }

        #endregion

        #region 创建

        /// <summary>
        /// 为指定地形创建/复用三个管理器（由 <see cref="GenerateNoiseTerrain"/> 生成地形时各自调用，这里只是汇总）。
        /// <para>⚠ 里面包含 <see cref="RockCoverDestructor.Rebuild"/>，它会把**已登记的地形覆盖物**整批清掉，
        /// 所以只能在"放置覆盖物之前"调用（现流程里 <see cref="GenerateNoiseTerrain"/> 是分别调三个 Rebuild 的，
        /// 因为放置悬崖夹在中间）。</para>
        /// </summary>
        /// <param name="terrain">目标地形</param>
        /// <param name="destructibleTreePrototypeIndexes">
        /// 允许被摧毁的树/石块原型索引；传 null 或空集合 = 全部可摧毁。
        /// </param>
        public static void Rebuild(Terrain terrain, IList<int> destructibleTreePrototypeIndexes = null)
        {
            TreeDestructor.Rebuild(terrain, destructibleTreePrototypeIndexes);
            TerrainDetailEraser.Rebuild(terrain);
            RockCoverDestructor.Rebuild(terrain);
        }

        #endregion

        #region 清除（忽略"可被摧毁"白名单）

        /// <summary>清除圆形范围内的地表物（忽略"可被摧毁"白名单），用于地形被改造 / 被覆盖</summary>
        /// <param name="center">中心点（世界坐标，只取 XZ）</param>
        /// <param name="radius">半径（米）</param>
        /// <param name="target">要清哪几类，默认全部</param>
        public static void ClearInRadius(Vector3 center, float radius, TerrainClearTarget target = TerrainClearTarget.All)
            => ClearInRadius(center, radius, target, TreePrototypeRange, RockPrototypeRange);

        /// <summary>清除圆形范围内的地表物（忽略白名单），并显式指定树 / 石块的原型区间</summary>
        /// <param name="center">中心点（世界坐标，只取 XZ）</param>
        /// <param name="radius">半径（米）</param>
        /// <param name="target">要清哪几类</param>
        /// <param name="treeRange">树原型区间（含头含尾，-1 = 不限）</param>
        /// <param name="rockRange">石块原型区间（含头含尾，-1 = 不限）</param>
        public static void ClearInRadius(Vector3 center, float radius, TerrainClearTarget target,
            Vector2Int treeRange, Vector2Int rockRange)
            => Enqueue(new Request
            {
                Shape = RequestShape.Circle,
                Center = center,
                Radius = radius,
                Target = target,
                TreeRange = treeRange,
                RockRange = rockRange,
            });

        /// <summary>
        /// 清除 XZ 轴对齐矩形范围内的地表物（忽略白名单）。
        /// <para>附加地形是"角点 + XZ 尺寸"的方块、未被旋转，所以用矩形判定更精确（不会多清区域外的物体）；</para>
        /// <para>⚠ 附加地形实际改动的是"矩形 + 过渡带"，调用方应把过渡带算进 <paramref name="halfSize"/>。</para>
        /// </summary>
        /// <param name="center">矩形中心（世界坐标，只取 XZ）</param>
        /// <param name="halfSize">半尺寸（x = X 方向半宽，y = Z 方向半宽）</param>
        /// <param name="target">要清哪几类，默认全部</param>
        public static void ClearInRectXZ(Vector3 center, Vector2 halfSize, TerrainClearTarget target = TerrainClearTarget.All)
            => ClearInRectXZ(center, halfSize, target, TreePrototypeRange, RockPrototypeRange);

        /// <summary>清除 XZ 轴对齐矩形范围内的地表物（忽略白名单），并显式指定树 / 石块的原型区间</summary>
        public static void ClearInRectXZ(Vector3 center, Vector2 halfSize, TerrainClearTarget target,
            Vector2Int treeRange, Vector2Int rockRange)
            => Enqueue(new Request
            {
                Shape = RequestShape.Rect,
                Center = center,
                HalfSize = halfSize,
                Target = target,
                TreeRange = treeRange,
                RockRange = rockRange,
            });

        #endregion

        #region 摧毁（走"可被摧毁"白名单，爆炸伤害用）

        /// <summary>
        /// 按"摧毁"语义清除圆形范围内的树 / 石块（走可被摧毁白名单），草花一并擦除。
        /// <para>与 <see cref="ClearInRadius"/> 的区别只在"树/石块"这一路：这里走白名单，是爆炸伤害的正常路径。</para>
        /// </summary>
        /// <param name="center">爆心 / 中心点（世界坐标，只取 XZ）</param>
        /// <param name="radius">半径（米）</param>
        /// <param name="target">要处理哪几类，默认 树 + 石块 + 草花</param>
        public static void DestroyInRadius(Vector3 center, float radius,
            TerrainClearTarget target = TerrainClearTarget.VegetationAndDetail)
            => Enqueue(new Request
            {
                Shape = RequestShape.Circle,
                Center = center,
                Radius = radius,
                Target = target,
                IsDestroy = true,
            });

        #endregion

        #region 地形改动标记与提交

        /// <summary>
        /// 标记"地形高度被改过"，请求合并重烘（树碰撞体重建 + NavMesh 重烘）。
        /// <para>谁改的高度谁来标：<c>ModifyTerrain</c>（挖坑/附加地形）、<c>FpsHelper.Hit</c>（爆炸弹坑）。</para>
        /// <para>标完不必自己重烘：下一个处理点会按 <see cref="RefreshInterval"/> 合并成一次。</para>
        /// </summary>
        public static void MarkTerrainChanged()
        {
            _terrainChanged = true;
            _navDirty = true;
        }

        /// <summary>
        /// 立刻处理一次：把积压请求下发 + 兑现重烘（跳过 <see cref="RefreshInterval"/> 限流）。
        /// <para>地形刚被改造完（任务平台生成）时调一次，避免"地面已经变了、树和石块还悬在半空"的中间帧。</para>
        /// <para>平时不需要调：帧末由 <see cref="Driver"/> 自动处理。</para>
        /// </summary>
        public static void Flush() => Process(true);

        #endregion

        #region 内部实现

        /// <summary>计时执行一段逻辑，并把它累加进对应阶段的统计</summary>
        private static double Measure(Phase phase, Action action)
        {
            _stopwatch.Restart();
            action();
            double ms = _stopwatch.Elapsed.TotalMilliseconds;
            AddSample(phase, ms);
            return ms;
        }

        private static void AddSample(Phase phase, double ms)
        {
            int index = (int)phase;
            _sumMs[index] += ms;
            if (ms > _maxMs[index]) _maxMs[index] = ms;
        }

        private static double Avg(Phase phase)
            => _sampleCount > 0 ? _sumMs[(int)phase] / _sampleCount : 0d;

        /// <summary>
        /// 打印一次实测数据。
        /// <para>每 <see cref="TimingLogEverySamples"/> 次样本额外打印"均值/峰值"；</para>
        /// <para>⚠ "高度图"那两列来自 <see cref="TerrainUtils"/>（<c>ModifyHeightMap</c> 内部），
        /// 它不在这里的合并范围内 —— 每个弹坑各执行一次，正是本次实测的重点怀疑对象。</para>
        /// </summary>
        private static void LogTimingSample(double dispatchMs, double treeMs, double detailMs,
            double colliderMs, double navMs, double totalMs, string colliderTag, string navTag)
        {

            //Debug.Log(
            //    $"[TerrainClearer] #{_sampleCount} 下发 {dispatchMs:F2} | 树提交 {treeMs:F2} | 草 {detailMs:F2} | "
            //    + $"碰撞体 {colliderMs:F2}{colliderTag} | NavMesh {navMs:F2}{navTag} | 合计 {totalMs:F2} ms"
            //    + $"   ‖ 高度图×{TerrainUtils.HeightModifyCount} 上次 读 {TerrainUtils.LastHeightReadMs:F2} "
            //    + $"写+Sync {TerrainUtils.LastHeightWriteMs:F2} ms"
            //    + $"（均值 读 {TerrainUtils.AvgHeightReadMs:F2} 写+Sync {TerrainUtils.AvgHeightWriteMs:F2}）");

            int every = Mathf.Max(1, TimingLogEverySamples);
            if (_sampleCount % every != 0) return;

            //Debug.Log(
            //    $"[TerrainClearer] 均值(×{_sampleCount}) 下发 {Avg(Phase.Dispatch):F2} | 树提交 {Avg(Phase.TreeFlush):F2} | "
            //    + $"草 {Avg(Phase.DetailFlush):F2} | 碰撞体 {Avg(Phase.TreeCollider):F2} | NavMesh {Avg(Phase.NavMesh):F2} | "
            //    + $"合计 {Avg(Phase.Total):F2} ms");
            //Debug.Log(
            //    $"[TerrainClearer] 峰值 下发 {_maxMs[(int)Phase.Dispatch]:F2} | 树提交 {_maxMs[(int)Phase.TreeFlush]:F2} | "
            //    + $"草 {_maxMs[(int)Phase.DetailFlush]:F2} | 碰撞体 {_maxMs[(int)Phase.TreeCollider]:F2} | "
            //    + $"NavMesh {_maxMs[(int)Phase.NavMesh]:F2} | 合计 {_maxMs[(int)Phase.Total]:F2} ms");
        }

        private static void Enqueue(Request request)
        {
            _requests.Add(request);

            // 非运行期没有每帧驱动（LateUpdate 不跑），立刻下发，避免请求一直积压
            if (!Application.isPlaying) Process(false);
        }

        /// <summary>
        /// 逐帧处理点：① 把一帧内的请求统一下发到三个腿；② 把"提交 + 重建碰撞体 + 重烘 NavMesh"合并成一次。
        /// </summary>
        /// <param name="force">true = 忽略 <see cref="RefreshInterval"/> 限流</param>
        private static void Process(bool force)
        {
            bool hasRequest = _requests.Count > 0;

            double dispatchMs = 0d;
            if (hasRequest) dispatchMs = Measure(Phase.Dispatch, DispatchPending);

            if (!hasRequest && !_terrainChanged && !_navDirty) return;

            // 非运行期不做重烘：编辑器里没有每帧驱动，重烘交给各自的编辑器流程（生成地形时自带）
            if (!Application.isPlaying) return;

            if (!force && Time.time - _lastRefreshTime < RefreshInterval) return;

            bool terrainChangedThisTick = _terrainChanged;
            _lastRefreshTime = Time.time;
            _terrainChanged = false;

            // 顺序很重要：先把待提交的树/草兑现（跳过各自的 4/s、8/s 限流），
            // 再重建树碰撞体、最后重烘 NavMesh —— 否则重烘用的还是旧数据
            double treeMs = Measure(Phase.TreeFlush, TreeDestructor.Flush);
            double detailMs = Measure(Phase.DetailFlush, TerrainDetailEraser.Flush);

            // 树碰撞体：只在"树/石块真的被清过"时重建（重建整张地形碰撞体，实测 10.75ms）。
            // 用提交版本号判断 —— 即使 TreeDestructor 在自己的 LateUpdate 里已经提交过也能准确识别，且不跨帧重复重建。
            // 另外按 ColliderSyncInterval 限流：延迟重建不会丢更新（版本号会记住），代价是被清掉的树多留一会儿隐形碰撞体
            bool treeChanged = TreeDestructor.CommitVersion != _colliderSyncedVersion;
            bool collidersEnabled = SyncTreeCollidersOnTreeChange || RebuildCollidersOnTerrainChange;
            bool colliderDue = force || ColliderSyncInterval <= 0f
                || Time.time - _lastColliderSyncTime >= ColliderSyncInterval;
            bool needCollider = collidersEnabled && colliderDue
                && ((SyncTreeCollidersOnTreeChange && treeChanged)
                    || (RebuildCollidersOnTerrainChange && (treeChanged || terrainChangedThisTick)));

            double colliderMs = 0d;
            string colliderTag = "";
            if (needCollider)
            {
                colliderMs = Measure(Phase.TreeCollider, RebuildTreeCollidersLeg);
                _colliderSyncedVersion = TreeDestructor.CommitVersion;
                _lastColliderSyncTime = Time.time;
            }
            else if (collidersEnabled && treeChanged && !colliderDue)
            {
                colliderTag = "(排队中)"; // 有变化但还没到重建间隔，版本号没消费 ⇒ 下一次一定会补上
            }
            else
            {
                // 区分"没开关（本来就不需要）"和"开了但这次没变化"
                colliderTag = collidersEnabled ? "(跳过)" : "(关闭)";
                _colliderSyncedVersion = TreeDestructor.CommitVersion;
            }

            // NavMesh：独立节流 + "上一次没烘完就不发下一次"；_navDirty 会保留到真的烘完为止
            double navMs = 0d;
            string navTag = "";
            bool navInFlight = _navOperation != null && !_navOperation.isDone;
            bool navDue = force || Time.time - _lastNavTime >= NavRefreshInterval;
            if (_navDirty && navDue && !navInFlight)
            {
                navMs = Measure(Phase.NavMesh, RefreshNavLeg);
                _lastNavTime = Time.time;
                _navDirty = false;
            }
            else if (_navDirty)
            {
                navTag = navInFlight ? "(等上次烘完)" : "(排队中)";
            }

            double totalMs = dispatchMs + treeMs + detailMs + colliderMs + navMs;
            AddSample(Phase.Total, totalMs);
            _sampleCount++;

            if (LogTiming)
                LogTimingSample(dispatchMs, treeMs, detailMs, colliderMs, navMs, totalMs, colliderTag, navTag);
        }

        /// <summary>树碰撞体重建（单独包一层，便于计时）</summary>
        private static void RebuildTreeCollidersLeg() => TerrainUtils.RebuildTreeColliders(TerrainUtils.Main);

        /// <summary>
        /// NavMesh 重烘（单独包一层，便于计时）。
        /// <para>记下这次异步操作，供"上一次没烘完就不发下一次"的判断使用。</para>
        /// </summary>
        private static void RefreshNavLeg()
        {
            if (TerrainUtils.Main == null) return;
            _navOperation = TerrainUtils.AsyncRefresh(true);
        }

        /// <summary>把积压的请求下发到三个腿（它们各自还会做"整表提交"级别的合并与限流）</summary>
        private static void DispatchPending()
        {
            for (int i = 0; i < _requests.Count; i++) Dispatch(_requests[i]);
            _requests.Clear();
        }

        private static void Dispatch(Request request)
        {
            if (request.IsDestroy)
            {
                // "摧毁"语义：树/石块走可被摧毁白名单
                if ((request.Target & TerrainClearTarget.Vegetation) != 0)
                    TreeDestructor.DestroyInRadius(request.Center, request.Radius);
                if ((request.Target & TerrainClearTarget.Detail) != 0)
                    TerrainDetailEraser.ClearInRadius(request.Center, request.Radius);
                if ((request.Target & TerrainClearTarget.Cover) != 0)
                    RockCoverDestructor.RemoveInRadius(request.Center, request.Radius);
                return;
            }

            bool isRect = request.Shape == RequestShape.Rect;
            if ((request.Target & TerrainClearTarget.Tree) != 0)
            {
                if (isRect) TreeDestructor.ClearInRectXZ(request.Center, request.HalfSize, request.TreeRange.x, request.TreeRange.y);
                else TreeDestructor.ClearInRadius(request.Center, request.Radius, request.TreeRange.x, request.TreeRange.y);
            }
            if ((request.Target & TerrainClearTarget.Rock) != 0)
            {
                if (isRect) TreeDestructor.ClearInRectXZ(request.Center, request.HalfSize, request.RockRange.x, request.RockRange.y);
                else TreeDestructor.ClearInRadius(request.Center, request.Radius, request.RockRange.x, request.RockRange.y);
            }
            if ((request.Target & TerrainClearTarget.Detail) != 0)
            {
                if (isRect) TerrainDetailEraser.ClearInRectXZ(request.Center, request.HalfSize);
                else TerrainDetailEraser.ClearInRadius(request.Center, request.Radius);
            }
            if ((request.Target & TerrainClearTarget.Cover) != 0)
            {
                if (isRect) RockCoverDestructor.RemoveInRectXZ(request.Center, request.HalfSize);
                else RockCoverDestructor.RemoveInRadius(request.Center, request.Radius);
            }
        }

        #endregion

        #region 每帧驱动

        /// <summary>门面的逐帧驱动器（隐藏、跨场景常驻；不需要任何场景/prefab 改动）</summary>
        private class Driver : MonoBehaviour
        {
            private void LateUpdate() => Process(false);
        }

        private static Driver _driver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDriver()
        {
            if (_driver != null) return;

            GameObject go = new GameObject("[TerrainClearer]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<Driver>();
        }

        #endregion
    }
}
