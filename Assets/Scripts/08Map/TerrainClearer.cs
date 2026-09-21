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
        /// <summary>树使用的原型区间（含头含尾），默认 7-10</summary>
        public static Vector2Int TreePrototypeRange = new Vector2Int(7, 10);

        /// <summary>石块使用的原型区间（含头含尾），默认 0-6</summary>
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

        /// <summary>当前积压的请求数（调试/诊断用）</summary>
        public static int PendingRequests => _requests.Count;

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
        public static void MarkTerrainChanged() => _terrainChanged = true;

        /// <summary>
        /// 立刻处理一次：把积压请求下发 + 兑现重烘（跳过 <see cref="RefreshInterval"/> 限流）。
        /// <para>地形刚被改造完（任务平台生成）时调一次，避免"地面已经变了、树和石块还悬在半空"的中间帧。</para>
        /// <para>平时不需要调：帧末由 <see cref="Driver"/> 自动处理。</para>
        /// </summary>
        public static void Flush() => Process(true);

        #endregion

        #region 内部实现

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
            if (hasRequest) DispatchPending();

            if (!hasRequest && !_terrainChanged) return;

            // 非运行期不做重烘：编辑器里没有每帧驱动，重烘交给各自的编辑器流程（生成地形时自带）
            if (!Application.isPlaying) return;

            if (!force && Time.time - _lastRefreshTime < RefreshInterval) return;

            _lastRefreshTime = Time.time;
            _terrainChanged = false;

            // 顺序很重要：先把待提交的树/草兑现（跳过各自的 4/s、8/s 限流），
            // 再重建树碰撞体、最后重烘 NavMesh —— 否则重烘用的还是旧数据
            TreeDestructor.Flush();
            TerrainDetailEraser.Flush();
            TerrainUtils.RebuildTreeColliders(TerrainUtils.Main);
            if (TerrainUtils.Main != null) TerrainUtils.AsyncRefresh(true);
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
