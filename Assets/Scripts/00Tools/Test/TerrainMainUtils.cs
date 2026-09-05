using System.Collections;
using UnityEngine;
using Utils;
using Unity.AI.Navigation;

using Core;
//using UnityEngine.AI;

public static partial class TerrainUtils
{
    public static Terrain Main
    {
        get => main;
        set
        {
            main = value;
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

    private static Terrain main;
    private static TerrainData data;
    private static NavMeshSurface nav;
    /// <summary>高度贴图实际分辨 ?/summary>
    private static int heightmapRes;
    /// <summary>纹理贴图分辨 ?/summary>
    private static int alphamapRes;
    private static int terrainHeight;

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

        yield return ModifyHeightMap(WSToUV(pos), pos.y, innerRadius, outerRadius, depth, shape, isSet, refresh);
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
    public static IEnumerator ModifyHeightMap(Vector2 uv, float baseHeight, float innerRadius, float outerRadius, float depth, ShapeType shape = ShapeType.Circle, bool isSet = true, bool refresh = true)
    {
        if (shape != ShapeType.Circle && shape != ShapeType.Ellipse)
        {
            Debug.LogError("修改地形使用了错误的形状" + shape);

        }
        else
        {
            baseHeight /= terrainHeight;
            var outerRadiusRes = WRToHR(outerRadius);
            if (outerRadiusRes <= 0)
            {
                Debug.LogError("错误:修改的地形半 ?outerRadius  ?0");
                yield break;
            }
            float invRadius = 1f / outerRadiusRes;//范围的倒数，让dis标准 ?
            float innerScale = innerRadius / (outerRadius + 0f);//内半径的系数(比如0.8)

            //地形数据
            float[,] heights = GetHeights(uv, outerRadiusRes, out int xBase, out int yBase, out int size, out Vector2 offset);
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
                    //标准化之后的距离[0,1]
                    float normalizedDistance = shape switch {
                        ShapeType.Circle => Vector2.Distance(new Vector2(x, y), center) * invRadius,
                        ShapeType.Ellipse => Mathf.Max(Mathf.Abs(x - center.x) * 2, Mathf.Abs(y - center.y) * 2) * invRadius,
                        _ => 0,
                    };
                    if (normalizedDistance <= 1f)
                    {
                        var height = SampleSmallHeight(heights, y + offset.y, x + offset.x);
                        //在外圈线性[0,1]，内圈直 ?
                        float power = Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale));
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

            data.SetHeightsDelayLOD(xBase, yBase, heights); //延迟写入（性能最优）
            data.SyncHeightmap();//同步地形数据
                                 //这里高度已经被标准化过了
            //ModifyAlphaMap 是协程（迭代器），必须 yield return 驱动，裸调用不会执行
            yield return ModifyAlphaMap(uv, 1 - Mathf.Clamp01((baseHeight - centerOldHeight) / (depth / terrainHeight) - 0.1f), innerRadius, outerRadius, shape, isSet);
            if (refresh) AsyncRefresh(true);
        }

    }

    //规则 ?弹坑/平原/洼地/巢穴/陡坡

    /// <summary>
    /// 修改纹理 ?
    /// </summary>
    /// <param name="uv"></param>
    /// <param name="radius"></param>
    private static IEnumerator ModifyAlphaMap(Vector2 uv, float modifityScale, float innerRadius, float outerRadius, ShapeType shape = ShapeType.Circle, bool isSet = true)
    {

        var radiusRes = WRToHR(outerRadius);
        float invRadius = 1f / radiusRes;//范围的倒数，让dis标准 ?
        float innerScale = innerRadius / (outerRadius + 0f);//内半径的系数(比如0.8)

        float[,,] alphas = GetAlphas(uv, radiusRes, out int xBase, out int yBase, out int size, out int layer);
        yield return null;

        Vector2 center = Vector2.one * size * 0.5f;
        float startTime = Time.realtimeSinceStartup;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                //标准化之后的距离[0,1]
                float normalizedDistance = shape switch {
                    ShapeType.Circle => Vector2.Distance(new Vector2(x, y), center) * invRadius,
                    ShapeType.Ellipse => Mathf.Max(Mathf.Abs(x - center.x) * 2, Mathf.Abs(y - center.y) * 2) * invRadius,
                    _ => 0,
                };


                if (normalizedDistance <= 1f)
                {
                    if (isSet)
                    {
                        float power = Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale));

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

                        float targetWeight = Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale)) * modifityScale;
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
