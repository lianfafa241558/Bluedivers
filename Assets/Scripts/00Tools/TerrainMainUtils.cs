using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FPSGame.Utils;
using Unity.AI.Navigation;

using FPSGame.Core;

namespace FPSGame.Utils
{
//using UnityEngine.AI;

public static partial class TerrainUtils
{
    /// <summary>
    /// 地表占地圆（纯数据）：世界 XZ 圆心 + 半径。
    /// <para>地形生成阶段由"地表物体的生产者"（例如 <c>GenerateNoiseTerrain</c> 的巨型悬崖）写入
    /// <see cref="AreaCircles"/>；使用者（任务点/兴趣点、AI 布点等）只依赖这份数据，</para>
    /// <para>不需要认识是谁放的、也不需要引用地形生成器所在程序集。</para>
    /// </summary>
    public struct AreaCircle
    {
        /// <summary>世界坐标 XZ 圆心</summary>
        public Vector2 Center;

        /// <summary>占地半径（米）</summary>
        public float Radius;

        public AreaCircle(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>
    /// 本次地形上"不要占用"的地表占地圆（世界 XZ）。
    /// <para>换地形（<see cref="Main"/> 被赋值）时自动清空，由地表生产者重新注入；读取方只读。</para>
    /// </summary>
    private static readonly List<AreaCircle> areaCircles = new();

    /// <summary>地表占地圆（只读，可能为空）</summary>
    public static IReadOnlyList<AreaCircle> AreaCircles => areaCircles;

    /// <summary>清空地表占地圆（重新生成地形前由生产者调用一次）</summary>
    public static void ClearAreaCircles() => areaCircles.Clear();

    /// <summary>登记一处地表占地圆（半径 &lt;= 0 会被忽略）</summary>
    /// <param name="center">世界坐标 XZ 圆心</param>
    /// <param name="radius">占地半径（米）</param>
    public static void AddAreaCircle(Vector2 center, float radius)
    {
        if (radius <= 0f) return;
        areaCircles.Add(new AreaCircle(center, radius));
    }

    public static Terrain Main
    {
        get => main;
        set
        {
            main = value;
            // 换地形 = 上一局的地表占地圆作废（新地形生成时会重新注入）
            areaCircles.Clear();
            if (value)
            {
                data = value.terrainData;
                nav = value.transform.GetComponentInParent<NavMeshSurface>();
                heightmapRes = value.terrainData.heightmapResolution - 1;
                alphamapRes = value.terrainData.alphamapResolution;
                terrainHeight = (int)value.terrainData.size.y;
                Debug.LogWarning("设置main地形", value);
            }
        }
    }

    /// <summary>
    /// 每帧最长阻塞时 ?
    /// </summary>
    private const float maxTimePerFrame = 0.01f;

    /// <summary>矩形内外框的最小间隔（米）：小于它时按"硬边"处理，避免 <see cref="RectFalloffPower"/> 除零</summary>
    private const float MinFalloffWidth = 0.01f;

    private static Terrain main;
    private static TerrainData data;
    private static NavMeshSurface nav;
    /// <summary>高度贴图实际分辨 ?/summary>
    private static int heightmapRes;
    /// <summary>纹理贴图分辨 ?/summary>
    private static int alphamapRes;
    private static int terrainHeight;
    /// <summary>上次重建地形碰撞体的帧号（同一帧内去重，见 <see cref="RebuildTreeColliders"/>）</summary>
    private static int lastTreeColliderRebuildFrame = -1;

    #region 高度图改动耗时（实测用，由 TerrainClearer 汇总打印）

    /// <summary>最近一次高度图"读取 patch"耗时（ms）</summary>
    public static double LastHeightReadMs { get; private set; }

    /// <summary>最近一次高度图"写入 + SyncHeightmap"耗时（ms）</summary>
    public static double LastHeightWriteMs { get; private set; }

    /// <summary>已统计的高度图修改次数</summary>
    public static int HeightModifyCount { get; private set; }

    private static double sumHeightReadMs;
    private static double sumHeightWriteMs;

    /// <summary>平均"读取 patch"耗时（ms）</summary>
    public static double AvgHeightReadMs => HeightModifyCount > 0 ? sumHeightReadMs / HeightModifyCount : 0d;

    /// <summary>平均"写入 + SyncHeightmap"耗时（ms）</summary>
    public static double AvgHeightWriteMs => HeightModifyCount > 0 ? sumHeightWriteMs / HeightModifyCount : 0d;

    /// <summary>清空高度图耗时统计</summary>
    public static void ResetHeightTimingStats()
    {
        LastHeightReadMs = 0d;
        LastHeightWriteMs = 0d;
        sumHeightReadMs = 0d;
        sumHeightWriteMs = 0d;
        HeightModifyCount = 0;
    }

    /// <summary>与 <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> 配套的毫秒换算（无 GC）</summary>
    private static double ElapsedMs(long startTicks)
        => (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;

    #endregion

    #region 转换方法

    /// <summary>
    /// 获取世界坐标对应的高 ?
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static float WSToHeight(Vector3 pos) => Main.WSToHeight(pos);
    public static float WSToHeight(Vector2 pos) => Main.WSToHeight(pos);
    public static float WSToHeight(float x, float z) => Main.WSToHeight(new Vector3(x, 0, z));


    /// <summary>
    /// 获取世界坐标对应的地面坐 ?
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector3 WSToTS(Vector3 pos) => Main != null ? new Vector3(pos.x, Main.WSToHeight(pos), pos.z) : pos;
    public static Vector3 WSToTS(Vector2 pos) => new(pos.x, Main.WSToHeight(pos), pos.y);
    public static Vector3 WSToTS(float x, float z) => new(x, Main.WSToHeight(new Vector2(z, x)), z);


    /// <summary>
    /// 将世界长 ? ?变为UV[0,1]
    /// </summary>
    public static Vector2 WSToUV(Vector3 pos) => Main.WSToUV(pos);
    public static Vector2 WSToUV(Vector2 pos) => Main.WSToUV(pos);


    /// <summary>
    /// 将世界坐 ? ?变为高度贴图长度(像素)
    /// </summary>
    public static int WRToHR(float lenght) => Main.WRToHR(lenght);

    /// <summary>
    /// 将世界长 ? ?变为纹理贴图长度(像素)
    /// </summary>
    public static int WRToAR(float lenght) => Main.WRToAR(lenght);

    /// <summary>
    /// 将纹理贴图长 ?像素)变为高度贴图长度(像素)
    /// </summary>
    public static int ARToHR(float lenght) => Main.ARToHR(lenght);

    /// <summary>
    /// 高度像素点对应的世界坐标
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector2 HSToWS(int x, int y) => Main.HSToWS(x, y);

    /// <summary>
    /// 纹理像素点对应的世界坐标
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector2 ASToWS(int x, int y) => Main.ASToWS(x, y);


    #endregion

    #region 修改地形
    /// <summary>
    /// 修改高度 ?
    /// </summary>
    /// <param name="uv">标准化之后的坐标[0,1]</param>
    /// <param name="shape">形状，只有圆和方有用</param>
    /// <param name="innerRadius">内半 ? ?/param>
    /// <param name="outerRadius">外半 ? ?/param>
    /// <param name="depth">深度: ?/param>
    /// <param name="isSet">设置/修改</param>
    public static IEnumerator ModifyHeightMap(Vector3 pos, float innerRadius, float outerRadius, float depth, ShapeType shape = ShapeType.Circle, bool isSet = true, bool refresh = true)
    {
        if (!Main) yield break;
        yield return ModifyHeightMap(WSToUV(pos), pos.y,
            new Vector2(innerRadius, innerRadius), new Vector2(outerRadius, outerRadius), depth, shape, isSet, refresh);
    }

    /// <summary>
    /// 修改高度图（矩形：内外框的 X / Z 半长都可以不同，且可绕 Y 旋转）。
    /// <para><paramref name="innerHalfSize"/> = **内框**半长：该范围内的地形完全按目标高度拉平；</para>
    /// <para><paramref name="outerHalfSize"/> = **外框**半长：该范围之外地形不动，两者之间按
    /// "各轴从内框到外框的归一化距离取较大者"线性淡化（等比例内外框时与圆形的标量过渡完全等价）。</para>
    /// <para><paramref name="angleDeg"/> 通常直接传物体的 <c>transform.eulerAngles.y</c>——矩形会跟着物体一起转；
    /// 传 0 就是世界 XZ 轴对齐。</para>
    /// <para>⚠ 注意 <c>TerrainClearer.ClearInRectXZ</c> 目前**只支持轴对齐矩形**，旋转后的矩形要用它清地表物需要另加角度支持。</para>
    /// </summary>
    /// <param name="pos">矩形中心的世界坐标</param>
    /// <param name="innerHalfSize">内框 XZ 半尺寸（米）：x = X 方向半长，y = Z 方向半长</param>
    /// <param name="outerHalfSize">外框 XZ 半尺寸（米，应大于内框）</param>
    /// <param name="depth">深度（米）</param>
    /// <param name="angleDeg">绕 Y 的旋转角（度），与物体世界 Y 旋转一致；0 = 世界轴对齐</param>
    /// <param name="isSet">true = 设置（压到目标高度）；false = 只往下挖，不抬升</param>
    /// <param name="refresh">是否刷新地形与 NavMesh</param>
    public static IEnumerator ModifyHeightMapRect(Vector3 pos, Vector2 innerHalfSize, Vector2 outerHalfSize, float depth, float angleDeg = 0f, bool isSet = true, bool refresh = true)
    {
        if (!Main) yield break;
        yield return ModifyHeightMap(WSToUV(pos), pos.y, innerHalfSize, outerHalfSize, depth, ShapeType.Rectangle, isSet, refresh, angleDeg);
    }

    /// <summary>
    /// 矩形的淡化系数：0 = 外框边缘（地形不动），1 = 内框以内（完全生效）。
    /// <para>两轴各自按"内框半长 → 外框半长"把世界距离归一化，取较大者——即等比例内外框时
    /// 与圆形那条标量式 <c>(1 - d) / (1 - innerScale)</c> 完全等价，但允许内外框长宽比不同。</para>
    /// </summary>
    /// <param name="dxPixel">相对中心的 X 像素偏移</param>
    /// <param name="dzPixel">相对中心的 Z 像素偏移</param>
    /// <param name="worldPerPixel">1 像素对应的世界米数（地形 X/Z 等长，共用一个换算）</param>
    /// <param name="innerHalfSize">内框 XZ 半长（米）</param>
    /// <param name="outerHalfSize">外框 XZ 半长（米）</param>
    private static float RectFalloffPower(float dxPixel, float dzPixel, float worldPerPixel, Vector2 innerHalfSize, Vector2 outerHalfSize)
    {
        //分母兜底：内外框重合（或配反了）时不让它除零炸出 NaN，退化成"硬边"
        float tx = (Mathf.Abs(dxPixel) * worldPerPixel - innerHalfSize.x) / Mathf.Max(MinFalloffWidth, outerHalfSize.x - innerHalfSize.x);
        float tz = (Mathf.Abs(dzPixel) * worldPerPixel - innerHalfSize.y) / Mathf.Max(MinFalloffWidth, outerHalfSize.y - innerHalfSize.y);
        return Mathf.Clamp01(1f - Mathf.Max(tx, tz));
    }

    /// <summary>
    /// 修改高度 ?
    /// </summary>
    /// <param name="uv">标准化之后的坐标[0,1]</param>
    /// <param name="baseHeight">中心点高 ? ?/param>
    /// <param name="shape">形状，只有圆和方有用</param>
    /// <param name="innerRadius">内半 ? ?/param>
    /// <param name="outerRadius">外半 ? ?/param>
    /// <param name="depth">深度: ?/param>
    /// <param name="isSet">设置/修改</param>
    /// <param name="innerHalfSize">内框半长（米）：该范围内完全生效</param>
    /// <param name="outerHalfSize">外框半长（米）：该范围外不动，两者之间线性淡化</param>
    /// <param name="angleDeg">矩形绕 Y 的旋转角（度）；其他形状忽略</param>
    public static IEnumerator ModifyHeightMap(Vector2 uv, float baseHeight, Vector2 innerHalfSize, Vector2 outerHalfSize, float depth, ShapeType shape = ShapeType.Circle, bool isSet = true, bool refresh = true, float angleDeg = 0f)
    {
        if (shape != ShapeType.Circle && shape != ShapeType.Ellipse && shape != ShapeType.Rectangle)
        {
            Debug.LogError("修改地形使用了错误的形状" + shape);

            yield break;
        }
        else
        {
            baseHeight /= terrainHeight;
            var outerRadiusRes = WRToHR(outerHalfSize.x);
            //矩形两轴半长可以不同；圆 / 椭圆（含历史实现）两轴都按 outerHalfSize.x
            var outerRadiusZRes = shape == ShapeType.Rectangle ? WRToHR(outerHalfSize.y) : outerRadiusRes;
            if (outerRadiusRes <= 0)
            {
                Debug.LogError("错误:修改的地形半 ?outerRadius  ?0");
                yield break;
            }
            float invRadius = 1f / outerRadiusRes;//范围的倒数，让dis标准 ?

            if (outerRadiusZRes <= 0)
            {
                Debug.LogError("错误:修改的地形半长(Z) <= 0");
                yield break;
            }
            //圆 / 椭圆的内圈比例（内圈 = 外圈 × 该系数）；矩形不用它，走下面的逐轴 RectFalloffPower
            float innerScale = innerHalfSize.x / (outerHalfSize.x + 0f);
            //像素 → 世界米（地形 X/Z 等长，共用一个换算），矩形按"米"算淡化要用
            float worldPerPixel = data.size.x / heightmapRes;
            //矩形可跟着物体的世界 Y 旋转（兴趣点实例运行时会被随机旋转）；圆 / 椭圆绕中心对称，不需要
            bool rotateRect = shape == ShapeType.Rectangle && !Mathf.Approximately(angleDeg, 0f);
            float cosA = 1f;
            float sinA = 0f;
            if (rotateRect)
            {
                float rad = angleDeg * Mathf.Deg2Rad;
                cosA = Mathf.Cos(rad);
                sinA = Mathf.Sin(rad);
            }
            //补丁本身是正方形：轴对齐时取两轴较大的半长；旋转后要用**外接圆半径**才够覆盖外框，
            //超出的部分都由 normalizedDistance > 1 裁掉
            float patchRadiusRes = rotateRect
                ? Mathf.Sqrt((float)outerRadiusRes * outerRadiusRes + (float)outerRadiusZRes * outerRadiusZRes)
                : Mathf.Max(outerRadiusRes, outerRadiusZRes);
            //地形数据（计时：读取 patch）
            long readTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float[,] heights = GetHeights(uv, patchRadiusRes, out int xBase, out int yBase, out int size, out Vector2 offset);
            LastHeightReadMs = ElapsedMs(readTicks);
            if (size == 0)
            {
                Debug.LogError("错误:修改的地形半径为0");
                yield break;
            }
            yield return null;
            //中心点应该的高度[0,1]
            float centerOldHeight = SampleSmallHeight(heights, size / 2f, size / 2f);
            //float centerHeight = GetMapHeightAtUV(uv)- depth/data.size.y;
            //float centerOldHeight = heights[size / 2f, size / 2f];
            float centerHeight = centerOldHeight - depth / terrainHeight;
            if (!isSet)
            {
                centerHeight = centerOldHeight - Mathf.Max(0, depth / terrainHeight - (baseHeight - centerOldHeight));
            }


            Vector2 center = Vector2.one * size * 0.5f;
            float startTime = Time.realtimeSinceStartup;
            for (int y = 0; y < size; y++)
            {

                for (int x = 0; x < size; x++)
                {
                    //矩形要先把"世界偏移"旋进矩形自己的坐标系（世界 → 本地），这样矩形就跟着物体一起转
                    float px = x - center.x;
                    float pz = y - center.y;
                    float lx = rotateRect ? px * cosA - pz * sinA : px;
                    float lz = rotateRect ? px * sinA + pz * cosA : pz;

                    //标准化之后的距离[0,1]
                    float normalizedDistance = shape switch {
                        ShapeType.Circle => Vector2.Distance(new Vector2(x, y), center) * invRadius,
                        ShapeType.Ellipse => Mathf.Max(Mathf.Abs(x - center.x) * 2, Mathf.Abs(y - center.y) * 2) * invRadius,
                        //矩形：两轴各自按自己的半长归一化（本地坐标），取较大者 = 直角边的正方形/长方形
                        ShapeType.Rectangle => Mathf.Max(Mathf.Abs(lx) / (float)outerRadiusRes, Mathf.Abs(lz) / (float)outerRadiusZRes),
                        _ => 0,
                    };
                    if (normalizedDistance <= 1f)
                    {
                        var height = SampleSmallHeight(heights, y + offset.y, x + offset.x);
                        //在外圈线性[0,1]，内圈直 ?
                        //矩形按内外框的"米"距离逐轴归一化（允许内外框长宽比不同）；其余形状仍走标量比例式
                        float power = shape == ShapeType.Rectangle
                            ? RectFalloffPower(lx, lz, worldPerPixel, innerHalfSize, outerHalfSize)
                            : Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale));
                        if (isSet)
                        {
                            heights[y, x] = Mathf.Lerp(height, centerHeight, power);
                        }
                        else
                        {
                            //最终深度[0,1]
                            float nowDepth = Mathf.Max(0, depth / terrainHeight - (baseHeight - centerOldHeight));
                            heights[y, x] = Mathf.Max(0, Mathf.Min(height, Mathf.Lerp(height, centerOldHeight, power) - nowDepth * power));
                        }
                    }
                }
                // 每行结束后检查时 ?
                if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;  // 让出一 ?
                    //Debug.Log($"循环 {y} :{Time.frameCount}");
                    startTime = Time.realtimeSinceStartup;  // 重置计时 ?
                }
            }

            long writeTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            data.SetHeightsDelayLOD(xBase, yBase, heights); //延迟写入（性能最优）
            data.SyncHeightmap();//同步地形数据
            LastHeightWriteMs = ElapsedMs(writeTicks);
            //实测统计（由 TerrainClearer 汇总打印）；注意：这里没被 TerrainClearer 的合并覆盖，每个弹坑各跑一次
            sumHeightReadMs += LastHeightReadMs;
            sumHeightWriteMs += LastHeightWriteMs;
            HeightModifyCount++;
                                 //这里高度已经被标准化过了
            //ModifyAlphaMap 是协程（迭代器），必须 yield return 驱动，裸调用不会执行
            yield return ModifyAlphaMap(uv, 1 - Mathf.Clamp01((baseHeight - centerOldHeight) / (depth / terrainHeight) - 0.1f), innerHalfSize, outerHalfSize, shape, isSet, angleDeg);
            if (refresh) AsyncRefresh(true);
        }

    }

    //规则 ?弹坑/平原/洼地/巢穴/陡坡

    /// <summary>
    /// 修改纹理 ?
    /// </summary>
    /// <param name="uv"></param>
    /// <param name="radius"></param>
    private static IEnumerator ModifyAlphaMap(Vector2 uv, float modifityScale, Vector2 innerHalfSize, Vector2 outerHalfSize, ShapeType shape = ShapeType.Circle, bool isSet = true, float angleDeg = 0f)
    {

        //⚠ 必须用 WRToAR（纹理图像素）：下面的 GetAlphas 是按 alphamapResolution 取 patch 的。
        //原先写的 WRToHR 只是因为本项目当前 heightmapResolution-1(1024) 恰好 == alphamapResolution(1024) 才不出错；
        //一旦两者不再相等（例如只把 heightmapResolution 翻倍），贴图改动范围就会跟着错一个比例。
        var radiusRes = WRToAR(outerHalfSize.x);
        //矩形两轴半长可以不同；圆 / 椭圆（含历史实现）两轴都按 outerHalfSize.x
        var radiusZRes = shape == ShapeType.Rectangle ? WRToAR(outerHalfSize.y) : radiusRes;
        if (radiusRes <= 0 || radiusZRes <= 0) yield break;
        float invRadius = 1f / radiusRes;//范围的倒数，让dis标准 ?
        //圆 / 椭圆的内圈比例；矩形走逐轴 RectFalloffPower
        float innerScale = innerHalfSize.x / (outerHalfSize.x + 0f);
        //像素 → 世界米（纹理图分辨率与高度图不同，各用自己的换算）
        float worldPerPixel = data.size.x / alphamapRes;
        //矩形跟着物体世界 Y 旋转（与高度图那侧同一套算法）
        bool rotateRect = shape == ShapeType.Rectangle && !Mathf.Approximately(angleDeg, 0f);
        float cosA = 1f;
        float sinA = 0f;
        if (rotateRect)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            cosA = Mathf.Cos(rad);
            sinA = Mathf.Sin(rad);
        }
        //补丁本身是正方形：轴对齐取两轴较大的半长；旋转后要用外接圆半径才够覆盖外框
        float patchRadiusRes = rotateRect
            ? Mathf.Sqrt((float)radiusRes * radiusRes + (float)radiusZRes * radiusZRes)
            : Mathf.Max(radiusRes, radiusZRes);
        float[,,] alphas = GetAlphas(uv, patchRadiusRes, out int xBase, out int yBase, out int size, out int layer);
        yield return null;

        Vector2 center = Vector2.one * size * 0.5f;
        float startTime = Time.realtimeSinceStartup;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                //矩形要先把"世界偏移"旋进矩形自己的坐标系（世界 → 本地）
                float px = x - center.x;
                float pz = y - center.y;
                float lx = rotateRect ? px * cosA - pz * sinA : px;
                float lz = rotateRect ? px * sinA + pz * cosA : pz;

                //标准化之后的距离[0,1]
                float normalizedDistance = shape switch {
                    ShapeType.Circle => Vector2.Distance(new Vector2(x, y), center) * invRadius,
                    ShapeType.Ellipse => Mathf.Max(Mathf.Abs(x - center.x) * 2, Mathf.Abs(y - center.y) * 2) * invRadius,
                    //矩形：两轴各自按自己的半长归一化（本地坐标），取较大者 = 直角边的正方形/长方形
                    ShapeType.Rectangle => Mathf.Max(Mathf.Abs(lx) / (float)radiusRes, Mathf.Abs(lz) / (float)radiusZRes),
                    _ => 0,
                };


                if (normalizedDistance <= 1f)
                {
                    if (isSet)
                    {
                        //矩形按内外框的"米"距离逐轴归一化；其余形状仍走标量比例式
                        float power = shape == ShapeType.Rectangle
                            ? RectFalloffPower(lx, lz, worldPerPixel, innerHalfSize, outerHalfSize)
                            : Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale));

                        int xHeight = ARToHR(xBase + x);
                        int yHeight = ARToHR(yBase + y);
                        var steep = GetSteepness(xHeight, yHeight) / 90;//坡度[0,1]
                        float height = data.GetHeight(yHeight, xHeight) / terrainHeight;//高度[0,1]
                        //巢穴系数不变，弹坑层级归零，剩下的层级分权重
                        float weightSum = 1 - alphas[y, x, 3];

                        //例如:0.3/0.1/0.15/0.2/0.25 剩余权重0.8/(1-0.3)
                        //变成 ?0/0.114/0.171/0.2/0.279

                        //弹坑层归 ?
                        alphas[y, x, 0] = 0;

                        // 陡坡层[22.5 ?67.5度],[0.25,0.75]
                        alphas[y, x, 4] = Mathf.SmoothStep(alphas[y, x, 4], Mathf.Clamp01((steep * 2f - 0.5f) * weightSum), power);

                        // 沙地层（中等高度)在[0,0.5]高度逐步变为[0,1]
                        alphas[y, x, 1] = Mathf.SmoothStep(alphas[y, x, 1], Mathf.Clamp01((height * 2f) * (1 - alphas[y, x, 4]) * weightSum), power);

                        // 侵蚀 ?低洼区域)在[0,0.5]高度逐步变为[1,0]
                        alphas[y, x, 2] = Mathf.SmoothStep(alphas[y, x, 2], Mathf.Clamp01(((1 - height) * 2f) * (1 - alphas[y, x, 4]) * weightSum), power);

                        //巢穴层不 ?
                        alphas[y, x, 3] = Mathf.Clamp01(1 - alphas[y, x, 1] - alphas[y, x, 2] - alphas[y, x, 4]);


                    }
                    else
                    {
                        // 使用平滑曲线计算权重

                        float targetWeight = (shape == ShapeType.Rectangle
                            ? RectFalloffPower(lx, lz, worldPerPixel, innerHalfSize, outerHalfSize)
                            : Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale))) * modifityScale;
                        //总和必须 ?
                        float originalSum = (1 - targetWeight);
                        //例如:0.3/0.1/0.15/0.2/0.25 弹坑权重0.6,残余权重就是(1-0.6)/(1-0.3)
                        //变成 ?0.6/0.057/0.0855/0.1142/0.143
                        var remain = 1f;

                        // 重新分配权重
                        for (int u = 1; u < layer; u++)
                        {
                            alphas[y, x, u] *= originalSum;
                            remain -= alphas[y, x, u];
                        }
                        alphas[y, x, 0] = remain;
                    }

                }
            }
            // 每行结束后检查时 ?
            if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
            {
                yield return null;  // 让出一 ?
                                    //Debug.Log($"循环 {y} :{Time.frameCount}");
                startTime = Time.realtimeSinceStartup;  // 重置计时 ?
            }
        }

        // 防御性检查：确保 alphamap 参数合法
        int maSizeX = alphas.GetLength(0);
        int maSizeY = alphas.GetLength(1);
        int maLayers = alphas.GetLength(2);
        int maRes = data.alphamapResolution;
        int maDataLayers = data.alphamapLayers;
        if (xBase < 0 || yBase < 0 || xBase + maSizeX > maRes || yBase + maSizeY > maRes)
        {
            Debug.LogError($"[ModifyAlphaMap] alphamap区域越界! xBase={xBase}, yBase={yBase}, size=({maSizeX},{maSizeY}), res={maRes}");
        }
        if (maLayers != maDataLayers)
        {
            Debug.LogError($"[ModifyAlphaMap] alphamap层数不匹配! alphas层数={maLayers}, terrainData层数={maDataLayers}");
        }

        data.SetAlphamaps(xBase, yBase, alphas);
        //Main.Flush();
    }

    /// <summary>
    /// 将地形旋转后附加到主地形
    /// 先根据 targetHeight 把主地形过渡到目标高度，再贴上附加地形数据
    /// </summary>
    /// <param name="source">要附加的地形</param>
    /// <param name="transitionDistance">边缘过渡距离</param>
    /// <param name="angle">绕Y轴旋转的角度（单位：度）</param>
    /// <param name="targetHeight">目标基准高度（世界坐标），过渡区主地形由此高度过渡到附加地形</param>
    public static IEnumerator AdditionTerrain(Terrain source, float transitionDistance, float angle, float targetHeight, bool refresh = true)
    {

        TerrainData sourceData = source.terrainData;
        float smallTerrainSize = sourceData.size.x; // 假设地形是正方形，x/z尺寸一致

        // 纹理图分辨率
        int smallAlphaRes = sourceData.alphamapResolution;
        int smallAlphaLayers = sourceData.alphamapLayers;
        // 地形的纹理层权重
        float[,,] smallAlphas = sourceData.GetAlphamaps(0, 0, smallAlphaRes, smallAlphaRes);

        // 计算旋转矩阵（绕Y轴旋转，Unity中Y轴向上，角度转弧度）
        Quaternion rotation = Quaternion.Euler(0, angle, 0);
        // 源地形的中心点（旋转中心）
        Vector3 sourceCenter = source.GetPosition() + Vector3.one / 2 * smallTerrainSize;

        // 主地形实际世界高度 = Main.transform.position.y + normalized * size.y，
        // 因此世界高度转归一化需要先减去 Main 的 Y 偏移
        float mainPosY = Main.transform.position.y;
        float normalizedTargetHeight = (targetHeight - mainPosY) / terrainHeight;
        smallTerrainSize += transitionDistance * 0.5f;//额外的过渡范围

        var uv = WSToUV(sourceCenter);
        var heights = GetHeights(uv, WRToHR(smallTerrainSize / 2), out int xBaseH, out int yBaseH, out int sizeH, out Vector2 heightsOffset);
        float[,,] alphas = GetAlphas(uv, WRToAR(smallTerrainSize / 2), out int xBaseA, out int yBaseA, out int sizeA, out int layer);
        yield return null;

        float startTime = Time.realtimeSinceStartup;
        for (int y = 0; y < sizeH; y++)
        {
            for (int x = 0; x < sizeH; x++)
            {
                // 1. 获取主地形上当前点的世界坐标
                Vector3 ws = HSToWS(xBaseH + x, yBaseH + y).ToVector3();
                // 2. 计算该点相对源地形中心的偏移（用于旋转）
                Vector3 offset = ws - sourceCenter;
                // 3. 绕Y轴旋转偏移量
                Vector3 rotatedOffset = rotation * offset;
                // 4. 得到旋转后的世界坐标（用于从源地形取高度）
                Vector3 rotatedWS = sourceCenter + rotatedOffset;

                // 标准化（距离边缘）的距离[0,1]，0=边缘外, 1=核心区
                float edgeFactor = GetDistanceToTerrainEdge(source, ws, transitionDistance);

                // 主地形当前高度（归一化）
                var mainHeight = SampleSmallHeight(heights, y + heightsOffset.y, x + heightsOffset.x);

                // 第一步：主地形高度 → targetHeight，在过渡区平滑过渡
                float raisedHeight = Mathf.SmoothStep(mainHeight, normalizedTargetHeight, edgeFactor);

                // 第二步：raisedHeight → 附加地形高度，在过渡区平滑过渡
                float sourceHeight = (source.WSToHeight(rotatedWS) - mainPosY) / terrainHeight;
                heights[y, x] = Mathf.SmoothStep(raisedHeight, sourceHeight, edgeFactor);
            }
            // 每行结束后检查时间
            if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
            {
                yield return null;
                startTime = Time.realtimeSinceStartup;
            }
        }
        yield return null;
        startTime = Time.realtimeSinceStartup;

        int smallLayout = smallAlphas.GetLength(2);
        for (int y = 0; y < sizeA; y++)
        {
            for (int x = 0; x < sizeA; x++)
            {
                // 1. 获取主地形上当前纹理点的世界坐标
                Vector3 ws = ASToWS(xBaseA + x, yBaseA + y).ToVector3();

                // 2. 计算相对源地形中心的偏移并旋转
                Vector3 offset = ws - sourceCenter;
                Vector3 rotatedOffset = rotation * offset;
                Vector3 rotatedWS = sourceCenter + rotatedOffset;

                // 标准化（距离边缘）的距离[0,1]
                float edgeFactor = GetDistanceToTerrainEdge(source, ws, transitionDistance);
                // 从旋转后的世界坐标获取源地形的UV
                Vector2 uvSmall = source.WSToUV(rotatedWS);
                uvSmall = new(Mathf.Clamp01(uvSmall.x), Mathf.Clamp01(uvSmall.y));

                // 纹理权重插值
                for (int i = 0; i < layer; ++i)
                {
                    if (i < smallLayout)
                    {
                        float smallAlphaValue = SampleSmallAlphaBilinear(smallAlphas, smallAlphaRes, uvSmall, i);
                        alphas[y, x, i] = Mathf.SmoothStep(alphas[y, x, i], smallAlphaValue, edgeFactor);
                    }
                }
            }
            // 每行结束后检查时间
            if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
            {
                yield return null;
                startTime = Time.realtimeSinceStartup;
            }
        }


        // 写入并同步地形数据
        data.SetHeightsDelayLOD(xBaseH, yBaseH, heights);
        data.SyncHeightmap();

        // 防御性检查：确保 alphamap 参数合法
        int alphaSizeX = alphas.GetLength(0);
        int alphaSizeY = alphas.GetLength(1);
        int alphaLayers = alphas.GetLength(2);
        int currentAlphamapRes = data.alphamapResolution;
        int currentAlphamapLayers = data.alphamapLayers;

        if (xBaseA < 0 || yBaseA < 0 || xBaseA + alphaSizeX > currentAlphamapRes || yBaseA + alphaSizeY > currentAlphamapRes)
        {
            Debug.LogError($"[AdditionTerrain] alphamap区域越界! xBaseA={xBaseA}, yBaseA={yBaseA}, size=({alphaSizeX},{alphaSizeY}), res={currentAlphamapRes}");
        }
        if (alphaLayers != currentAlphamapLayers)
        {
            Debug.LogError($"[AdditionTerrain] alphamap层数不匹配! alphas层数={alphaLayers}, terrainData层数={currentAlphamapLayers}");
        }

        data.SetAlphamaps(xBaseA, yBaseA, alphas);
        if (refresh) AsyncRefresh(true);
    }

    #endregion

    #region 其他方法

    public static void Refresh(bool refreshNav)
    {
        Main.Flush();
        //TODO:这个是异步的
        //if(refreshNav) nav.UpdateNavMesh(nav.navMeshData);
        //先用同步的凑合一 ?
        if (refreshNav) nav.BuildNavMesh();
    }

    public static AsyncOperation AsyncRefresh(bool refreshNav)
    {
        Main.Flush();
        if (refreshNav) return nav.UpdateNavMesh(nav.navMeshData);
        return null;
    }

    /// <summary>
    /// 强制重建地形碰撞体，从而按**当前**树实例重新生成"地形树碰撞体"。
    /// <para>⚠ 实测（Unity 2022.3.62，2026-09-21）：<c>SetTreeInstances</c> 改完树实例后，树碰撞体**不会**自动更新——
    /// 旧位置仍然挡住、新位置完全没有碰撞体，<c>terrain.Flush()</c> 也无效；只有 TerrainCollider 重建时
    /// 才会按当时的树实例生成。"在检视器里随便编辑一下 TerrainData 就正常了"正是因为那次编辑触发了重建。</para>
    /// <para>实现方式：切一次 <c>enabled</c>（这是实测有效的唯一廉价手段）。</para>
    /// <para>代价：重建整个地形碰撞体（含全部树碰撞体，几千棵时较贵），运行时不要频繁调。</para>
    /// <para>注意：树碰撞体只由树 prefab 上的 <b>CapsuleCollider</b> 生成，Box/Sphere/MeshCollider 不参与
    /// （所以石块原型 0-6 即使开着 Enable Tree Colliders 也不会有碰撞体）。</para>
    /// </summary>
    /// <param name="terrain">目标地形；为空或碰撞体已禁用时直接返回</param>
    public static void RebuildTreeColliders(Terrain terrain)
    {
        if (terrain == null) return;
        if (!terrain.TryGetComponent(out TerrainCollider terrainCollider)) return;
        if (!terrainCollider.enabled) return;

        // 同一帧内只重建一次：一次清除会经"腿自己的提交"和"TerrainClearer 的合并重烘"两处调用，
        // 而重建整张地形碰撞体很贵（几千棵树）
        if (Time.frameCount == lastTreeColliderRebuildFrame) return;
        lastTreeColliderRebuildFrame = Time.frameCount;

        terrainCollider.enabled = false;
        terrainCollider.enabled = true;
    }



    /// <summary>
    /// 获取一个矩形的高度 ?
    /// </summary>
    /// <param name="center">中心点的UV</param>
    /// <param name="radius">半径:像素</param>
    /// <param name="xBase">起始X:像素</param>
    /// <param name="yBase">起始Y:像素</param>
    /// <param name="size">尺寸:像素</param>
    /// <returns>高度数组 [0,1]</returns>
    private static float[,] GetHeights(Vector2 center, float radius, out int xBase, out int yBase, out int size, out Vector2 offset)
    {
        int resolution = data.heightmapResolution;
        float cx = center.x * heightmapRes;
        float cy = center.y * heightmapRes;
        xBase = Mathf.Clamp(Mathf.FloorToInt(cx - radius), 0, resolution - 1);
        yBase = Mathf.Clamp(Mathf.FloorToInt(cy - radius), 0, resolution - 1);
        int maxSizeX = resolution - xBase;
        int maxSizeY = resolution - yBase;
        int desiredSize = Mathf.Max(1, Mathf.FloorToInt(2 * radius));
        size = Mathf.Min(desiredSize, maxSizeX, maxSizeY);
        offset = new(cx % 1, cy % 1);
        return data.GetHeights(xBase, yBase, size, size);
    }

    /// <summary>
    /// 获取一个矩形的纹理 ?
    /// </summary>
    /// <param name="center">中心点的UV</param>
    /// <param name="radius">半径:像素</param>
    /// <param name="xBase">起始X:像素</param>
    /// <param name="yBase">起始Y:像素</param>
    /// <param name="size">尺寸:像素</param>
    /// <param name="layout">贴图层数</param>
    /// <returns></returns>
    private static float[,,] GetAlphas(Vector2 center, float radius, out int xBase, out int yBase, out int size, out int layer)
    {
        int res = data.alphamapResolution;
        float cx = center.x * res;
        float cy = center.y * res;
        xBase = Mathf.Clamp(Mathf.FloorToInt(cx - radius), 0, res - 1);
        yBase = Mathf.Clamp(Mathf.FloorToInt(cy - radius), 0, res - 1);
        int maxSizeX = res - xBase;
        int maxSizeY = res - yBase;
        int desiredSize = Mathf.Max(1, Mathf.FloorToInt(2 * radius));
        size = Mathf.Min(desiredSize, maxSizeX, maxSizeY);
        layer = data.alphamapLayers;
        //地形数据
        return data.GetAlphamaps(xBase, yBase, size, size);
    }


    /// <summary>
    /// 根据标准化的UV坐标，获取地形高度贴图对应位置的高度 ?
    /// </summary>
    /// <param name="uv">标准化UV坐标[0,1]</param>
    /// <returns>高度值[0,1]</returns>
    private static float GetMapHeightAtUV(Vector2 uv)
    {
        TerrainData data = Main.terrainData;
        int heightmapRes = data.heightmapResolution - 1;// 高度图的像素数（ ?13 = 512x512网格 + 1 ?

        // 转换公式：像素索 ?= 标准化UV * (分辨 ?- 1)，向下取 ?
        int pixelX = Mathf.FloorToInt(uv.x * heightmapRes);
        int pixelZ = Mathf.FloorToInt(uv.y * heightmapRes);

        // 获取该像素的高度值[0,1]
        float height01 = data.GetHeight(pixelZ, pixelX);

        return height01;
    }

    /// <summary>
    /// 计算高度图中指定点的坡度（角度制 ?
    /// </summary>
    /// <param name="x">查询点的x坐标</param>
    /// <param name="y">查询点的y坐标</param>
    /// <returns>坡度角度 ?-90度）</returns>
    private static float GetSteepness(int x, int y)
    {
        // 边界校验：边缘点无法计算法向量，直接返回0
        if (x <= 0 || x >= heightmapRes - 1 || y <= 0 || y >= heightmapRes - 1)
        {
            return 0;
        }

        // 获取中心点及周边8邻域高度（处理边界时自动使用最近的有效点）
        //float h = data.GetHeight(x, y);
        float h_x0 = data.GetHeight(x - 1, y); //  ?
        float h_x1 = data.GetHeight(x + 1, y); //  ?
        float h_y0 = data.GetHeight(x, y - 1); //  ?
        float h_y1 = data.GetHeight(x, y + 1); //  ?

        // 计算x/z方向的梯度（中心差分法）
        float gradientX = (h_x1 - h_x0) / 2;
        float gradientZ = (h_y1 - h_y0) / 2;

        // 计算坡度角（arctan( ?Dh/Dx2 + Dh/Dz2)) ?
        float slopeRadians = Mathf.Atan(Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ));
        float slopeDegrees = slopeRadians * Mathf.Rad2Deg;

        return Mathf.Clamp(slopeDegrees, 0f, 90f);

    }


    /// <summary>
    /// 计算世界坐标地形的法线向 ?
    /// </summary>
    /// <param name="x">查询点在高度图中的x索引（整数）</param>
    /// <param name="y">查询点在高度图中的y索引（整数）</param>
    /// <returns>归一化的法线向量（Vector3：x=右，y=上，z= ?下）</returns>
    public static Vector3 GetNormal(Vector3 pos)
    {
        if (!Main) return Vector3.up;
        var hs = Main.WSToHS(pos);
        var x = hs.x;
        var y = hs.y;
        // 边界校验：边缘点无法计算法线，返回默认向上法 ?
        if (x <= 0 || x >= heightmapRes - 1 || y <= 0 || y >= heightmapRes - 1)
        {
            return Vector3.up; // 边缘点默认法线向 ?
        }

        // 1. 获取中心点及相邻点高度（和原坡度方法一致的邻域采样 ?
        //float h = data.GetHeight(x, y);
        float h_x0 = data.GetHeight(x - 1, y); //  ?
        float h_x1 = data.GetHeight(x + 1, y); //  ?
        float h_y0 = data.GetHeight(x, y - 1); //  ?
        float h_y1 = data.GetHeight(x, y + 1); //  ?

        // 2. 计算x/z方向的梯度（中心差分法，和坡度方法一致）
        float gradientX = (h_x1 - h_x0) / 2; // x方向高度变化 ?
        float gradientZ = (h_y1 - h_y0) / 2; // z方向高度变化 ?

        // 3. 核心：从梯度推导法线向量
        // 原理：法线是梯度的垂直向量，公式 ?(-dx, 1, -dz)，再归一 ?
        Vector3 normal = new Vector3(-gradientX, 1f, -gradientZ);

        // 4. 归一化法线（确保向量长度 ?，符合法线标准）
        normal.Normalize();

        // 可选：若需要转换为世界空间法线（需结合地形缩放 ?
        // normal = Terrain.activeTerrain.transform.TransformDirection(normal);

        return normal;
    }
    private static float GetDistanceToTerrainEdge(Terrain terrain, Vector3 worldPos, float transRange)
    {
        return GetDistanceToTerrainEdge(terrain, worldPos.ToVector2(), transRange);
    }
    /// <summary>
    /// 计算某逻辑坐标点到地形边缘的系数[0,1]
    /// </summary>
    private static float GetDistanceToTerrainEdge(Terrain terrain, Vector2 worldPos, float transRange)
    {
        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.GetPosition();
        float terrainSize = data.size.x;
        float halfSide = data.size.x / 2;
        //1.27=90%* ?

        // 转换为地形本地坐标（0~terrainSize ?
        float localX = worldPos.x - terrainPos.x;
        float localZ = worldPos.y - terrainPos.z;

        float distX = Mathf.Min(localX, terrainSize - localX);
        float distZ = Mathf.Min(localZ, terrainSize - localZ);
        float minRectDis = Mathf.Min(distX, distZ) / transRange;//最小的矩形系数
        //return minRectDis;

        float cirX = distX - halfSide;
        float cirZ = distZ - halfSide;
        Vector2 dir = new(cirX, cirZ);
        float borderDis = halfSide / Mathf.Max(Mathf.Abs(dir.normalized.x), Mathf.Abs(dir.normalized.y));//该方向到边的距离

        //float minCirDis = 0.6345f* terrainSize- Mathf.Sqrt(cirX * cirX + cirZ * cirZ);//距离半径为对角线90%的圆的距 ?
        //float minCirDis = 0.55f * terrainSize - dir.magnitude;//距离半径为对角线78%的圆的距 ?
        float minCirDis = 1 - (dir.magnitude - 0.8f * halfSide) / (borderDis - 0.8f * halfSide);

        float minDis = Mathf.Min(minRectDis, minCirDis);
        return Mathf.Clamp01(minDis);
    }


    /// <summary>
    /// 双线性插值采样小地形纹理权重
    /// </summary>
    private static float SampleSmallAlphaBilinear(float[,,] smallAlphas, int smallRes, Vector2 uv, int layer)
    {
        // u/v：小地形的归一化UV ?~1），而非像素索引
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0f;

        // 转换为像素坐标（带小数，保留插值信息）
        float pixelX = uv.x * (smallRes - 1);
        float pixelY = uv.y * (smallRes - 1);

        // 计算四个相邻像素的索 ?
        int x0 = Mathf.FloorToInt(pixelX);
        int x1 = Mathf.Min(x0 + 1, smallRes - 1);
        int y0 = Mathf.FloorToInt(pixelY);
        int y1 = Mathf.Min(y0 + 1, smallRes - 1);

        // 计算小数部分（插值权重）
        float tx = pixelX - x0;
        float ty = pixelY - y0;

        //Debug.LogError("查询" + x0 + "," + y0+"  ?+x1+","+y1+"uv"+ uv+"层级");
        // 双线性插值：先插值x方向，再插值y方向
        float val0 = Mathf.Lerp(smallAlphas[y0, x0, layer], smallAlphas[y0, x1, layer], tx);
        float val1 = Mathf.Lerp(smallAlphas[y1, x0, layer], smallAlphas[y1, x1, layer], tx);
        return Mathf.Lerp(val0, val1, ty);
    }


    /// <summary>
    /// 双线性插值采样地形高度权 ?
    /// </summary>
    /// <param name="smallHeight">数组数据</param>
    /// <param name="offect">在这一数组中的偏移(浮点 ?</param>
    /// <returns></returns>
    private static float SampleSmallHeight(float[,] smallHeight, float pixelY, float pixelX)
    {
        int smallRes = smallHeight.GetLength(0);
        // u/v：小地形的归一化UV ?~1），而非像素索引
        if (pixelX < 0 || pixelX > smallRes || pixelY < 0 || pixelY > smallRes) return 0f;

        // 计算四个相邻像素的索 ?
        int x0 = Mathf.FloorToInt(pixelX);
        int x1 = Mathf.Min(x0 + 1, smallRes - 1);
        int y0 = Mathf.FloorToInt(pixelY);
        int y1 = Mathf.Min(y0 + 1, smallRes - 1);

        // 计算小数部分（插值权重）
        float tx = pixelX - x0;
        float ty = pixelY - y0;

        // 双线性插值：先插值x方向，再插值y方向
        float val0 = Mathf.Lerp(smallHeight[y0, x0], smallHeight[y0, x1], tx);
        float val1 = Mathf.Lerp(smallHeight[y1, x0], smallHeight[y1, x1], tx);
        return Mathf.Lerp(val0, val1, ty);
    }
    #endregion
}
}
