using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;
using Utils;
using UnityEngine.AI;

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
            }
        }
    }
    private static Terrain main;
    private static TerrainData data;
    private static NavMeshSurface nav;
    /// <summary>高度贴图实际分辨率</summary>
    private static int heightmapRes;
    /// <summary>纹理贴图分辨率</summary>
    private static int alphamapRes;
    private static int terrainHeight;

    #region 转换方法

    /// <summary>
    /// 获取世界坐标对应的高度
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static float WSToHeight(Vector3 pos) => Main.WSToHeight(pos);
    public static float WSToHeight(Vector2 pos) => Main.WSToHeight(pos);
    public static float WSToHeight(float x, float z) => Main.WSToHeight(new Vector3(x, 0, z));


    /// <summary>
    /// 获取世界坐标对应的地面坐标
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector3 WSToTS(Vector3 pos) => Main!=null?new Vector3(pos.x, Main.WSToHeight(pos), pos.z): pos;
    public static Vector3 WSToTS(Vector2 pos) => new(pos.x, Main.WSToHeight(pos), pos.y);
    public static Vector3 WSToTS(float x, float z) => new(x, Main.WSToHeight(new Vector2(z, x)), z);


    /// <summary>
    /// 将世界长度(米)变为UV[0,1]
    /// </summary>
    public static Vector2 WSToUV(Vector3 pos) => Main.WSToUV(pos);
    public static Vector2 WSToUV(Vector2 pos) => Main.WSToUV(pos);


    /// <summary>
    /// 将世界坐标(米)变为高度贴图长度(像素)
    /// </summary>
    public static int WRToHR(float lenght) => Main.WRToHR(lenght);

    /// <summary>
    /// 将世界长度(米)变为纹理贴图长度(像素)
    /// </summary>
    public static int WRToAR(float lenght) => Main.WRToAR(lenght);

    /// <summary>
    /// 将纹理贴图长度(像素)变为高度贴图长度(像素)
    /// </summary>
    public static int ARToHR(float lenght) => Main.ARToHR(lenght);

    /// <summary>
    /// 高度像素点对应的世界坐标
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector2 HSToWS(int x, int y) => Main.HSToWS(x,y);

    /// <summary>
    /// 纹理像素点对应的世界坐标
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector2 ASToWS(int x, int y) => Main.ASToWS(x, y);


    #endregion

    #region 修改地形
    /// <summary>
    /// 修改高度图
    /// </summary>
    /// <param name="uv">标准化之后的坐标[0,1]</param>
    /// <param name="shape">形状，只有圆和方有用</param>
    /// <param name="innerRadius">内半径:米</param>
    /// <param name="outerRadius">外半径:米</param>
    /// <param name="depth">深度:米</param>
    /// <param name="isSet">设置/修改</param>
    public static void ModifyHeightMap(Vector3 pos, int innerRadius, int outerRadius, float depth, ShapeType shape = ShapeType.Circle, bool isSet = true, bool refresh = true)
    {
        ModifyHeightMap(WSToUV(pos), innerRadius, outerRadius, depth, shape, isSet,refresh);
    }

    /// <summary>
    /// 修改高度图
    /// </summary>
    /// <param name="uv">标准化之后的坐标[0,1]</param>
    /// <param name="shape">形状，只有圆和方有用</param>
    /// <param name="innerRadius">内半径:米</param>
    /// <param name="outerRadius">外半径:米</param>
    /// <param name="depth">深度:米</param>
    /// <param name="isSet">设置/修改</param>
    public static void ModifyHeightMap(Vector2 uv, int innerRadius, int outerRadius, float depth, ShapeType shape = ShapeType.Circle, bool isSet = true,bool refresh=true)
    {
        if (shape != ShapeType.Circle && shape != ShapeType.Ellipse)
        {
            Debug.LogError("修改地形使用了错误的形状" + shape);
            return;
        }
        var outerRadiusRes = WRToHR(outerRadius);
        float invRadius = 1f / outerRadiusRes;//范围的倒数，让dis标准化
        float innerScale = innerRadius / (outerRadius + 0f);//内半径的系数(比如0.8)

        //地形数据
        float[,] heights = GetHeights(uv, outerRadiusRes, out int xBase, out int yBase, out int size);
        if (size==0)
        {
            Debug.LogError("错误:修改的地形半径为0");
            return;
        }
        //中心点应该的高度
        //float centerHeight = GetMapHeightAtUV(uv)- depth/data.size.y;
        float centerOldHeight = heights[size / 2, size / 2];
        float centerHeight = centerOldHeight - depth / terrainHeight;

        Vector2 center = Vector2.one * size * 0.5f;

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
                    //在外圈线性[0,1]，内圈直接1
                    float power = Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale));
                    if (isSet)
                    {
                        heights[y, x] = Mathf.Lerp(heights[y, x], centerHeight, power);
                    }
                    else
                    {
                        heights[y, x] = Mathf.Max(0, Mathf.Min(heights[y, x], Mathf.Lerp(heights[y, x], centerOldHeight, power) - depth * power / terrainHeight));
                    }
                }
            }
        }

        data.SetHeightsDelayLOD(xBase, yBase, heights); //延迟写入（性能最优）
        data.SyncHeightmap();//同步地形数据
        ModifyAlphaMap(uv, innerRadius, outerRadius, shape,isSet);
        if (refresh) Refresh(innerRadius>2);
    }

    //规则是:弹坑/平原/洼地/巢穴/陡坡

    /// <summary>
    /// 修改纹理图
    /// </summary>
    /// <param name="uv"></param>
    /// <param name="radius"></param>
    private static void ModifyAlphaMap(Vector2 uv, int innerRadius, int outerRadius, ShapeType shape = ShapeType.Circle, bool isSet = true)
    {

        var radiusRes = WRToHR(outerRadius);
        float invRadius = 1f / radiusRes;//范围的倒数，让dis标准化
        float innerScale = innerRadius / (outerRadius + 0f);//内半径的系数(比如0.8)

        float[,,] alphas = GetAlphas(uv, radiusRes, out int xBase, out int yBase, out int size, out int layer);


        Vector2 center = Vector2.one * size * 0.5f;

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
                        int xHeight = ARToHR(xBase + x);
                        int yHeight = ARToHR(yBase + y);
                        var steep = GetSteepness(xHeight, yHeight) / 90;//坡度[0,1]
                        float height = data.GetHeight(xHeight, yHeight)/ terrainHeight;//高度[0,1]
                        //巢穴系数不变，弹坑层级归零，剩下的层级分权重
                        float weightSum = 1 - alphas[x, y, 3];

                        //例如:0.3/0.1/0.15/0.2/0.25 剩余权重0.8/(1-0.3)
                        //变成了 0/0.114/0.171/0.2/0.279

                        // 陡坡层[22.5度,67.5度],[0.25,0.75]
                        alphas[y, x, 4] = Mathf.Clamp01(steep * 2f - 0.5f);

                        // 沙地层（中等高度)在[0,0.5]高度逐步变为[0,1]
                        alphas[x, y, 1] = Mathf.Clamp01(height * 2f) * (1 - alphas[x, y, 4]) * weightSum;

                        // 侵蚀层(低洼区域)在[0,0.5]高度逐步变为[1,0]
                        alphas[x, y, 2] = Mathf.Clamp01((1 - height) * 2f) * (1 - alphas[x, y, 4]) * weightSum;

                        alphas[y, x, 4] *= weightSum;

                        //巢穴层不变
                        //alphas[x, y, 3] = 0;
                        //弹坑层归零
                        alphas[x, y, 0] = 0;

                    }
                    else
                    {
                        // 使用平滑曲线计算权重

                        float targetWeight = Mathf.Clamp01((1 - normalizedDistance) / (1 - innerScale));
                        //总和必须是1
                        float originalSum = (1 - targetWeight);
                        //例如:0.3/0.1/0.15/0.2/0.25 弹坑权重0.6,残余权重就是(1-0.6)/(1-0.3)
                        //变成了 0.6/0.057/0.0855/0.1142/0.143
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
        }

        data.SetAlphamaps(xBase, yBase, alphas);
        //Main.Flush();
    }

    /// <summary>
    /// 将地形旋转后附加到主地形上
    /// 这里要解决的一个映射的问题，因为地形不能旋转，所以得先想象一个旋转之后的，然后uv转到对应位置
    /// </summary>
    /// <param name="source">要附加的地形</param>
    /// <param name="transitionDistance">边缘过渡距离</param>
    /// <param name="angle">绕Y轴旋转的角度（单位：度）</param>
    public static void AdditionTerrain(Terrain source, float transitionDistance, float angle, bool refresh = true)
    {
        TerrainData sourceData = source.terrainData;
        float smallTerrainSize = sourceData.size.x; // 假设地形是正方形，x/z尺寸一致

        // 纹理图分辨率
        int smallAlphaRes = sourceData.alphamapResolution;
        // 地形的纹理层权重
        float[,,] smallAlphas = sourceData.GetAlphamaps(0, 0, smallAlphaRes, smallAlphaRes);

        // 计算旋转矩阵（绕Y轴旋转，Unity中Y轴向上，角度转弧度）
        Quaternion rotation = Quaternion.Euler(0, angle, 0);
        // 源地形的中心点（旋转中心）
        Vector3 sourceCenter = source.GetPosition() + Vector3.one / 2 * smallTerrainSize;

        smallTerrainSize += transitionDistance * 0.5f;//额外的过渡范围

        var uv = WSToUV(sourceCenter); 
        var heights = GetHeights(uv, WRToHR(smallTerrainSize / 2), out int xBaseH, out int yBaseH, out int sizeH);
        float[,,] alphas = GetAlphas(uv, WRToAR(smallTerrainSize / 2), out int xBaseA, out int yBaseA, out int sizeA, out int layer);

        for (int y = 0; y < sizeH; y++)
        {
            for (int x = 0; x < sizeH; x++)
            {

                //我们假定地形大小50,50，中心点在25,25，旋转了180度
                //那 0,0 点对于中心的偏移是 -25，-25
                //-25，-25绕，0,0点旋转180度变成了25,25
                //取uv得到1,1

                // 1. 获取主地形上当前点的世界坐标
                Vector3 ws = HSToWS(xBaseH + x, yBaseH + y).ToVector3();
                // 2. 计算该点相对源地形中心的偏移（用于旋转）
                Vector3 offset = ws - sourceCenter;
                // 3. 绕Y轴旋转偏移量
                Vector3 rotatedOffset = rotation * offset;
                // 4. 得到旋转后的世界坐标（用于从源地形取高度）
                Vector3 rotatedWS = sourceCenter + rotatedOffset;

                //没有修改成功说明normalizedDistance不对，现在一直是0
                // 标准化之后(距离边缘)的距离[0,1]
                float normalizedDistance = Mathf.SmoothStep(GetDistanceToTerrainEdge(source, rotatedWS, transitionDistance), GetDistanceToTerrainEdge(source, ws, transitionDistance),0.5f);

                //if (normalizedDistance < 0.99f)
                //{
                //Tool.DrawShape(ShapeType.Rectangle, ws + Vector3.up * WSToHeight(ws), Vector3.one * heightmapRes / data.size.x/3.5f, 1, new Color(1-normalizedDistance, 0, 0, 1));
                //}

                // 从旋转后的坐标获取源地形高度
                //heights[y, x] = Mathf.Lerp(heights[y, x], (source.WSToHeight(rotatedWS)) / terrainHeight, normalizedDistance);
                //WSToHeight 自带边界 clamped 保护
                heights[y, x] = Mathf.SmoothStep(heights[y, x], (source.WSToHeight(rotatedWS)) / terrainHeight, normalizedDistance);
                //heights[y, x] = (source.WSToHeight(rotatedWS)) / terrainHeight;

            }
        }

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

                // 标准化之后(距离边缘)的距离[0,1]
                float normalizedDistance = GetDistanceToTerrainEdge(source, ws, transitionDistance);
                // 从旋转后的世界坐标获取源地形的UV
                Vector2 uvSmall = source.WSToUV(rotatedWS);
                uvSmall = new(Mathf.Clamp01(uvSmall.x), Mathf.Clamp01(uvSmall.y));

                // 纹理权重插值
                for (int i = 0; i < layer; ++i)
                {
                    float smallAlphaValue = SampleSmallAlphaBilinear(smallAlphas, smallAlphaRes, uvSmall, i);
                    alphas[y, x, i] = Mathf.SmoothStep(alphas[y, x, i], smallAlphaValue, normalizedDistance);

                }
            }
        }

        // 写入并同步地形数据
        data.SetHeightsDelayLOD(xBaseH, yBaseH, heights);
        data.SyncHeightmap();
        data.SetAlphamaps(xBaseA, yBaseA, alphas);
        // 刷新地形显示
        if (refresh) Refresh(true);

    }

    #endregion

    #region 其他方法

    public static void Refresh(bool refreshNav) {
        Main.Flush();
        if(refreshNav) nav.UpdateNavMesh(nav.navMeshData);
    }


    /// <summary>
    /// 获取一个矩形的高度值
    /// </summary>
    /// <param name="center">中心点的UV</param>
    /// <param name="radius">半径:像素</param>
    /// <param name="xBase">起始X:像素</param>
    /// <param name="yBase">起始Y:像素</param>
    /// <param name="size">尺寸:像素</param>
    /// <returns>高度数组 [0,1]</returns>
    private static float[,] GetHeights(Vector2 center, float radius, out int xBase, out int yBase, out int size)
    {
        center *= heightmapRes;
        xBase = Mathf.Clamp(Mathf.FloorToInt(center.x - radius), 0, heightmapRes);
        yBase = Mathf.Clamp(Mathf.FloorToInt(center.y - radius), 0, heightmapRes);
        size = Mathf.Clamp(Mathf.FloorToInt(2 * radius), 0, heightmapRes - Mathf.Max(xBase, yBase));
        //Debug.LogError("起点" + xBase + " " + yBase + "大小" + Mathf.FloorToInt(2 * radius)+"最大"+(heightmapRes - Mathf.Max(xBase, yBase)));
        //地形数据
        return data.GetHeights(xBase, yBase, size, size);
    }

    /// <summary>
    /// 获取一个矩形的纹理值
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
        center *= alphamapRes;
        xBase = Mathf.Clamp(Mathf.FloorToInt(center.x - radius), 0, alphamapRes);
        yBase = Mathf.Clamp(Mathf.FloorToInt(center.y - radius), 0, alphamapRes);
        size = Mathf.Clamp(Mathf.FloorToInt(2 * radius), 0, alphamapRes - Mathf.Max(xBase, yBase));
        layer = data.alphamapLayers;
        //地形数据
        return data.GetAlphamaps(xBase, yBase, size, size);
    }


    /// <summary>
    /// 根据标准化的UV坐标，获取地形高度贴图对应位置的高度值
    /// </summary>
    /// <param name="uv">标准化UV坐标[0,1]</param>
    /// <returns>高度值[0,1]</returns>
    private static float GetMapHeightAtUV(Vector2 uv)
    {
        TerrainData data = Main.terrainData;
        int heightmapRes = data.heightmapResolution - 1;// 高度图的像素数（如513 = 512x512网格 + 1）

        // 转换公式：像素索引 = 标准化UV * (分辨率 - 1)，向下取整
        int pixelX = Mathf.FloorToInt(uv.x * heightmapRes);
        int pixelZ = Mathf.FloorToInt(uv.y * heightmapRes);

        // 获取该像素的高度值[0,1]
        float height01 = data.GetHeight(pixelZ, pixelX);

        return height01;
    }

    /// <summary>
    /// 计算高度图中指定点的坡度（角度制）
    /// </summary>
    /// <param name="x">查询点的x坐标</param>
    /// <param name="y">查询点的y坐标</param>
    /// <returns>坡度角度（0-90度）</returns>
    private static float GetSteepness(int x, int y)
    {
        // 边界校验：边缘点无法计算法向量，直接返回0
        if (x <= 0 || x >= heightmapRes - 1 || y <= 0 || y >= heightmapRes - 1)
        {
            return 0;
        }

        // 获取中心点及周边8邻域高度（处理边界时自动使用最近的有效点）
        //float h = data.GetHeight(x, y);
        float h_x0 = data.GetHeight(x - 1, y); // 左
        float h_x1 = data.GetHeight(x + 1, y); // 右
        float h_y0 = data.GetHeight(x, y - 1); // 下
        float h_y1 = data.GetHeight(x, y + 1); // 上

        // 计算x/z方向的梯度（中心差分法）
        float gradientX = (h_x1 - h_x0) / 2;
        float gradientZ = (h_y1 - h_y0) / 2;

        // 计算坡度角（arctan(√(Dh/Dx2 + Dh/Dz2))）
        float slopeRadians = Mathf.Atan(Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ));
        float slopeDegrees = slopeRadians * Mathf.Rad2Deg;

        return Mathf.Clamp(slopeDegrees, 0f, 90f);

    }


    /// <summary>
    /// 计算世界坐标地形的法线向量
    /// </summary>
    /// <param name="x">查询点在高度图中的x索引（整数）</param>
    /// <param name="y">查询点在高度图中的y索引（整数）</param>
    /// <returns>归一化的法线向量（Vector3：x=右，y=上，z=前/下）</returns>
    public static Vector3 GetNormal(Vector3 pos)
    {
        if (!Main) return Vector3.up;
        var hs =Main.WSToHS(pos);
        var x = hs.x;
        var y = hs.y;
        // 边界校验：边缘点无法计算法线，返回默认向上法线
        if (x <= 0 || x >= heightmapRes - 1 || y <= 0 || y >= heightmapRes - 1)
        {
            return Vector3.up; // 边缘点默认法线向上
        }

        // 1. 获取中心点及相邻点高度（和原坡度方法一致的邻域采样）
        //float h = data.GetHeight(x, y);
        float h_x0 = data.GetHeight(x - 1, y); // 左
        float h_x1 = data.GetHeight(x + 1, y); // 右
        float h_y0 = data.GetHeight(x, y - 1); // 下
        float h_y1 = data.GetHeight(x, y + 1); // 上

        // 2. 计算x/z方向的梯度（中心差分法，和坡度方法一致）
        float gradientX = (h_x1 - h_x0) / 2; // x方向高度变化率
        float gradientZ = (h_y1 - h_y0) / 2; // z方向高度变化率

        // 3. 核心：从梯度推导法线向量
        // 原理：法线是梯度的垂直向量，公式为 (-dx, 1, -dz)，再归一化
        Vector3 normal = new Vector3(-gradientX, 1f, -gradientZ);

        // 4. 归一化法线（确保向量长度为1，符合法线标准）
        normal.Normalize();

        // 可选：若需要转换为世界空间法线（需结合地形缩放）
        // normal = Terrain.activeTerrain.transform.TransformDirection(normal);

        return normal;
    }
    private static float GetDistanceToTerrainEdge(Terrain terrain, Vector3 worldPos,float transRange)
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
        float halfSide = data.size.x/2;
        //1.27=90%*√2

        // 转换为地形本地坐标（0~terrainSize）
        float localX = worldPos.x - terrainPos.x;
        float localZ = worldPos.y - terrainPos.z;

        float distX = Mathf.Min(localX, terrainSize - localX); 
        float distZ = Mathf.Min(localZ, terrainSize - localZ);
        float minRectDis = Mathf.Min(distX, distZ)/ transRange;//最小的矩形系数
        //return minRectDis;

        float cirX = distX - halfSide;
        float cirZ = distZ - halfSide;
        Vector2 dir =new (cirX, cirZ);
        float borderDis = halfSide / Mathf.Max(Mathf.Abs(dir.normalized.x), Mathf.Abs(dir.normalized.y));//该方向到边的距离

        //float minCirDis = 0.6345f* terrainSize- Mathf.Sqrt(cirX * cirX + cirZ * cirZ);//距离半径为对角线90%的圆的距离
        //float minCirDis = 0.55f * terrainSize - dir.magnitude;//距离半径为对角线78%的圆的距离
        float minCirDis = 1-(dir.magnitude- 0.8f * halfSide) / (borderDis- 0.8f * halfSide);

        float minDis = Mathf.Min(minRectDis, minCirDis);
        return Mathf.Clamp01(minDis);
    }


    /// <summary>
    /// 双线性插值采样小地形纹理权重
    /// </summary>
    private static float SampleSmallAlphaBilinear(float[,,] smallAlphas, int smallRes, Vector2 uv, int layer)
    {
        // u/v：小地形的归一化UV（0~1），而非像素索引
        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0f;

        // 转换为像素坐标（带小数，保留插值信息）
        float pixelX = uv.x * (smallRes - 1);
        float pixelY = uv.y * (smallRes - 1);

        // 计算四个相邻像素的索引
        int x0 = Mathf.FloorToInt(pixelX);
        int x1 = Mathf.Min(x0 + 1, smallRes - 1);
        int y0 = Mathf.FloorToInt(pixelY);
        int y1 = Mathf.Min(y0 + 1, smallRes - 1);

        // 计算小数部分（插值权重）
        float tx = pixelX - x0;
        float ty = pixelY - y0;

        // 双线性插值：先插值x方向，再插值y方向
        float val0 = Mathf.Lerp(smallAlphas[y0, x0, layer], smallAlphas[y0, x1, layer], tx);
        float val1 = Mathf.Lerp(smallAlphas[y1, x0, layer], smallAlphas[y1, x1, layer], tx);
        return Mathf.Lerp(val0, val1, ty);
    }
    #endregion
}
