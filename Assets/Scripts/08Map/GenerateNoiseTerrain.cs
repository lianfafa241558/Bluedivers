using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.AI.Navigation;
using UnityEngine;
namespace FpsGame.MapUtils
{
    /// <summary>
    /// 生成地形，只从terraindata拿数据，和其他组件不挂钩
    /// </summary>
    public class GenerateNoiseTerrain : MonoBehaviour
    {

        public GameObject StartPoint;
        [InspectorName("每帧最长阻塞时间")]
        public float maxTimePerFrame = 0.01f;

        [Header("地形设置")]
        public Terrain terrain;
        [InspectorName("是否在Start时自动生成地形")]
        public bool generateOnStart = true;

        [Foldout("基础地形", true)]
        [InspectorName("基础地形缩放")]
        public float baseScale = 200f;
        [InspectorName("基础地形高度")]
        public float baseAmplitude = 80f;
        [InspectorName("细节层缩放")]
        public float detailScale = 25f;
        public float detailAmplitude = 15f;

        [Foldout("高原", true)]
        [InspectorName("高原半径（占地形比例）")]
        public float plateauRadius = 0.25f;
        [InspectorName("高原抬升强度")]
        public float plateauIntensity = 0.5f;
        [InspectorName("边缘衰减幅度")]
        public float edgeDropoff = 0.08f;
        [InspectorName("高原生成阈值")]
        public float plateauThreshold = 0.65f;

        // 高原形态控制参
        public float plateauMaskScale = 10;      // 主噪声尺度（控制高原基本形态）

        [Foldout("树", true)]
        List<TreeInstance> trees;
        [InspectorName("树概率")]
        public float treeProbability = 0.1f;

        [Foldout("其他", true)]
        public bool isLand;

        [SerializeField]
        private Texture2D preHeight, preTexture;//, preBaseHeight;

        private float[,] heightMap;
        private float[,,] textureMap;
        [SerializeField]
        private int width, height, size, speceHeight;

        //比如分辨率1024/512就是2
        private float mapscale => terrain.terrainData.heightmapResolution / terrain.terrainData.size.x;

        /*
        void Awake()
        {
            if (generateOnStart && terrain != null)
            {
                ApplyFractalNoiseToTerrain();
            }
        }
        */
        /// <summary>
        /// 应用分形噪声到地形
        /// </summary>
        public IEnumerator ApplyFractalNoiseToTerrain()
        {
            if (terrain == null)
            {
                Debug.LogWarning("未指定Terrain对象");
                yield break;
            }

            //Debug.LogError("开始生成地形");
            TerrainData terrainData = terrain.terrainData;
            width = terrainData.heightmapResolution;
            height = terrainData.heightmapResolution;
            size = terrain.terrainData.alphamapResolution;
            speceHeight = (int)terrain.terrainData.size.y;
            //Debug.LogError("有效范围"+ (size-Constants.MapBorder*mapscale) + "中心/半径"+ center);
            //Debug.LogError("贴图尺寸" + terrain.terrainData.heightmapResolution + " 地图大小" + terrain.terrainData.size);

            preHeight = new Texture2D(width, height, TextureFormat.ARGB32, false, false);
            preTexture = new Texture2D(width, height, TextureFormat.ARGB32, false, false);
            //preBaseHeight = new Texture2D(width, height, TextureFormat.ARGB32, false, false);
            heightMap = terrainData.GetHeights(0, 0, width, height);//原始值0-1)
            textureMap = terrainData.GetAlphamaps(0, 0, size, size);//原始值0-1)

            //Debug.LogError("贴图纹理尺寸"+ textureMap.GetLength(2));
            trees = new List<TreeInstance>();
            //textureMap =new float[width,height,4];//原始值0-1)
            // 测量激活阻塞
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
           

            // 生成基础地形
            yield return GenerateBaseTerrain();
            Debug.Log($"生成基础地形时间: {sw.ElapsedMilliseconds} ms");
            sw.Restart();

            // 添加侵蚀效果
            yield return ApplyErosionEffect();
            //Debug.Log($"生成细节地形时间: {sw.ElapsedMilliseconds} ms");
            //sw.Restart();

            //材质
            yield return ApplyTextures();

            Debug.Log($"生成材质时间: {sw.ElapsedMilliseconds} ms");
            sw.Restart();

            // 应用高度图，分块提交，避免单帧卡顿
            int chunkSize = 128; // 每帧提交 128x128
            yield return ApplyHeightsInChunks(chunkSize);
            yield return null;

            // 应用纹理图，分块提交
            yield return ApplyAlphamapsInChunks(chunkSize);
            yield return null;

            // 设置树
            yield return SpawnTrees();
            yield return null;
            terrainData.SetTreeInstances(trees.ToArray(), true);
            yield return null;

            preHeight.Apply(false, false);
            preTexture.Apply(false, false);

            // NavMesh 异步构建并等待完成
            yield return null;
            var surface = GetComponent<NavMeshSurface>();
            if (surface != null)
            {
                var asyncOp = surface.UpdateNavMesh(surface.navMeshData);
                // 等待异步操作完成（最多等 10 秒）
                float timeout = Time.realtimeSinceStartup + 10f;
                while (!asyncOp.isDone && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
                if (!asyncOp.isDone)
                    Debug.LogWarning("NavMesh 异步构建超时，可能仍在后台进行");
            }
            Debug.Log($"完成时间: {sw.ElapsedMilliseconds} ms");
            sw.Restart();

        }


        #region 步骤

        /// <summary>
        /// 基础地形
        /// </summary>
        IEnumerator GenerateBaseTerrain()
        {
            var now = System.DateTime.Now;
            System.Random TaskRandom = new(now.Month * 100 + now.Day + now.Hour * 100 + (now.Minute / 30 * 30));//每小时刷
            float offsetX = TaskRandom.Range(0, 9999f);
            float offsetY = TaskRandom.Range(0, 9999f);

            float startTime = Time.realtimeSinceStartup;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {

                    // 基础噪声
                    float nx = offsetX + x / (float)width * baseScale;
                    float ny = offsetY + y / (float)height * baseScale;
                    var nowheight = (Mathf.Pow(Mathf.PerlinNoise(ny, nx), 2) * 0.9f + 0.1f) * baseAmplitude;

                    // 细节噪声
                    float dx = offsetX + x / (float)width * detailScale;
                    float dy = offsetY + y / (float)height * detailScale;
                    nowheight += (Mathf.Pow(Mathf.PerlinNoise(dy, dx), 2) * 2 - 1) * detailAmplitude;


                    // 标准化高
                    heightMap[y, x] = nowheight / (1 + baseAmplitude + detailAmplitude);
                    SetPixel(preHeight, y, x, heightMap[y, x], 0);
                }

                // 每行结束后检查时间
                if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;  // 让出一帧
                    //Debug.Log($"循环 {y} :{Time.frameCount}");
                    startTime = Time.realtimeSinceStartup;  // 重置计时
                }
            }
            
        }


        /// <summary>
        /// 添加侵蚀效果
        /// </summary>
        IEnumerator ApplyErosionEffect()
        {

            var now = System.DateTime.Now;
            System.Random TaskRandom = new(now.Month * 100 + now.Day + now.Hour * 100 + (now.Minute / 30 * 30));//每小时刷
            float plateauOffsetX = TaskRandom.Range(0, 9999f);
            float plateauOffsetY = TaskRandom.Range(0, 9999f);

            float startTime = Time.realtimeSinceStartup;


            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float nowheight = heightMap[x, y];
                    if (nowheight > plateauThreshold)
                    {
                        // 计算超出阈值部分的比例
                        float excess = (nowheight - plateauThreshold) / plateauIntensity / 2;
                        // 用SmoothStep平滑过渡到高原高
                        heightMap[x, y] = nowheight = 0.5f * nowheight + 0.5f * Mathf.Lerp(plateauThreshold, plateauThreshold + plateauIntensity * 2, excess);
                    }


                    // 高原噪声采样
                    float px = plateauOffsetX + x / (float)width * plateauMaskScale;
                    float py = plateauOffsetY + y / (float)height * plateauMaskScale;
                    float plateauMask = Mathf.PerlinNoise(px, py);
                    // 噪声混合策略（强化大面积连续区域
                    plateauMask = Mathf.Pow(plateauMask, 2f);

                    //邻居的平均高度(卷积)
                    float neighborAvg = (heightMap[x + 1, y] + heightMap[x - 1, y] +
                                       heightMap[x, y + 1] + heightMap[x, y - 1]) / 4f;

                    // 高原生成条件
                    if (plateauMask > plateauThreshold && (neighborAvg > plateauThreshold || nowheight > plateauThreshold))
                    {
                        // 高原高度计算
                        float plateauBoost = plateauIntensity;
                        //抬升系数:离高原阈值越高，这个系数越低(原本的限制在0-1之间)edgeDropoff越低越快归零
                        float edgeAttenuation = 1 - Mathf.Clamp01((nowheight - plateauThreshold) / edgeDropoff);

                        // 应用高原抬升
                        heightMap[x, y] += plateauBoost * edgeAttenuation;
                        //heightmap[x, y] =(1- 0.5f) * heightmap[x, y] + 0.5f * Mathf.SmoothStep(plateauThreshold, plateauThreshold+ plateauIntensity, heightmap[x, y]);

                        // 边缘陡峭处理
                        //计算悬崖陡峭程度(平滑1陡峭)
                        float cliffDrop = Mathf.Clamp01((nowheight - neighborAvg) * 5f);
                        //越陡峭的点，高度增高越多
                        heightMap[x, y] += cliffDrop * edgeDropoff;


                        SetPixel(preHeight, x, y, (heightMap[x, y] - plateauThreshold) / plateauIntensity, 1);
                    }

                }
                if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;  // 让出一帧
                    startTime = Time.realtimeSinceStartup;  // 重置计时
                }
            }
        }
            
        /// <summary>
        /// 设置材质
        /// </summary>
        IEnumerator ApplyTextures()
        {
            float startTime = Time.realtimeSinceStartup;

            //int size = terrain.terrainData.alphamapResolution;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    /*
                    //高度归一化（0-1）
                    float nowheight = terrain.terrainData.GetHeight(y, x) / terrain.terrainData.size.y;
                    //坡度值（0-1）（坡度本身返回0-90度）
                    float steepness = terrain.terrainData.GetSteepness(y / (float)size,
                                        x / (float)size) / 90f;
                    */

                    //生成时没有巢穴，所以系数为1
                    float steepness = GetSteepness(y, x) / 90f;//坡度[0,1]
                    float nowheight = heightMap[y, x];//高度[0,1]

                    // 岩石层（陡坡）22.5度-67.5度）
                    textureMap[y, x, 4] = Mathf.Clamp01(steepness * 2f - 0.5f);

                    // 沙地层（中等高度)在[0,0.5]高度逐步变为[0,1]
                    textureMap[y, x, 1] = Mathf.Clamp01(nowheight * 2f) * (1 - textureMap[y, x, 4]);

                    // 侵蚀层（低洼区域）在[0,0.5]高度逐步变为[1,0]
                    textureMap[y, x, 2] = Mathf.Clamp01((1 - nowheight) * 2f) * (1 - textureMap[y, x, 4]);

                    textureMap[y, x, 3] = 0;
                    textureMap[y, x, 0] = 0;

                    SetPixel(preTexture, y, x, textureMap[y, x, 0], 0);
                    SetPixel(preTexture, y, x, textureMap[y, x, 1], 1);
                    SetPixel(preTexture, y, x, textureMap[y, x, 2], 2);
                    SetPixel(preTexture, y, x, steepness, 3);

                }
                if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;  // 让出一帧
                    //Debug.Log($"循环 {y} :{Time.frameCount}");
                    startTime = Time.realtimeSinceStartup;  // 重置计时器
                }
            }
            
        }

        #endregion

        #region 分块提交

        /// <summary>
        /// 分块提交高度图，每帧只提交一次chunk，避免SetHeights 卡帧
        /// </summary>
        IEnumerator ApplyHeightsInChunks(int chunkSize)
        {
            for (int y = 0; y < height; y += chunkSize)
            {
                for (int x = 0; x < width; x += chunkSize)
                {
                    int blockW = Mathf.Min(chunkSize, width - x);
                    int blockH = Mathf.Min(chunkSize, height - y);
                    float[,] chunk = new float[blockH, blockW];
                    for (int by = 0; by < blockH; by++)
                        for (int bx = 0; bx < blockW; bx++)
                            chunk[by, bx] = heightMap[y + by, x + bx];
                    // SetHeights(xBase, yBase, heights) xBase对应x列，yBase对应y行
                    terrain.terrainData.SetHeights(x, y, chunk);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 分块提交纹理图，避免 SetAlphamaps 卡帧
        /// </summary>
        IEnumerator ApplyAlphamapsInChunks(int chunkSize)
        {
            int layers = textureMap.GetLength(2);
            for (int y = 0; y < size; y += chunkSize)
            {
                for (int x = 0; x < size; x += chunkSize)
                {
                    int blockW = Mathf.Min(chunkSize, size - x);
                    int blockH = Mathf.Min(chunkSize, size - y);
                    float[,,] chunk = new float[blockH, blockW, layers];
                    for (int by = 0; by < blockH; by++)
                        for (int bx = 0; bx < blockW; bx++)
                            for (int l = 0; l < layers; l++)
                                chunk[by, bx, l] = textureMap[y + by, x + bx, l];
                    // SetAlphamaps(xBase, yBase, alphamaps) xBase对应x列，yBase对应y行
                    terrain.terrainData.SetAlphamaps(x, y, chunk);
                    yield return null;
                }
            }
        }

        #endregion

        #region 树 

        IEnumerator SpawnTrees()
        {
            // 10000是因为除200
            int plateaus = Mathf.FloorToInt(treeProbability * width * height / 10000f);
            //int range = (int)((width - Constants.MapBorder) * 0.5f);

            //Debug.LogError("数量"+ plateaus);
            for (int i = 0; i < plateaus; i++)
            {
                Vector3Int pos = new Vector3Int(
                    Random.Range(0, width),
                    0,
                    Random.Range(0, height)
                );
                //pos.y = terrain.SampleHeight(pos);

                TreeInstance tree = new TreeInstance();
                tree.position = new Vector3(pos.x / (float)width, heightMap[pos.x, pos.z], pos.z / (float)height); // 转换为归一化坐标
                tree.widthScale = Random.Range(0.5f, 2);
                tree.heightScale = Random.Range(0.5f, 2);
                //tree.prototypeIndex = 0; // 使用第一个树木原型
                tree.prototypeIndex = Random.Range(0, 5);

                trees.Add(tree);
            }
            /*
            foreach(var item in nests)
            {
                var pos = new Vector3(item.Item1.x/(float)width,0, item.Item1.y / (float)height);
                trees.RemoveAll(tree =>Vector3.Distance(pos, new(tree.position.x,0,tree.position.z))<item.Item2/ (float)width);
            }
            */
            //Debug.LogError("最终数量" + trees.Count);
            yield return null;
        }
        #endregion
        #region API

        /// <summary>
        /// 计算高度图中指定点的坡度（角度制）
        /// </summary>
        /// <param name="heightmap">二维高度数组（值范围建议0-1）</param>
        /// <param name="x">查询点的x坐标（基于heightmap数组索引）</param>
        /// <param name="y">查询点的y坐标（基于heightmap数组索引）</param>
        /// <param name="cellSize">单个网格的世界空间尺寸（用于正确计算水平距离）</param>
        /// <returns>坡度角度（0-90度）</returns>
        private float GetSteepness(int x, int y)
        {
            float cellSize = 1 / 16f;//应该是2，但是我的地形后面+0.5*0.5
            // 获取中心点及周边8邻域高度（处理边界时自动使用最近的有效点）
            float h = heightMap[x, y];
            float h_x0 = heightMap[Mathf.Max(0, x - 1), y];    
            float h_x1 = heightMap[Mathf.Min(width - 1, x + 1), y];  
            float h_y0 = heightMap[x, Mathf.Max(0, y - 1)];    
            float h_y1 = heightMap[x, Mathf.Min(height - 1, y + 1)];
            // 计算x/z方向的梯度（中心差分法）
            float gradientX = (h_x1 - h_x0) / (2f * cellSize);
            float gradientZ = (h_y1 - h_y0) / (2f * cellSize);

            // 计算坡度角（arctan(sqrt(Dh/Dx2 + Dh/Dz2))）
            float slopeRadians = Mathf.Atan(Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ));
            float slopeDegrees = slopeRadians * Mathf.Rad2Deg;

            return Mathf.Clamp(slopeDegrees, 0f, 90f);
        }


        private void SetPixel(Texture2D texture, int x, int y, float value, int colorMask)
        {
            Color baseColor = Color.black;
            Color color = Color.red;

            switch (colorMask)
            {
                case 1:
                    color = Color.green;
                    // 从原始黑色开始累加（避免 GPU 回读）
                    break;
                case 2:
                    color = Color.blue;
                    break;
                case 3:
                    color = new Color(0, 0, 0, 1);
                    break;
                default:
                    color = Color.red;
                    break;
            }
            texture.SetPixel(width - x, y, baseColor + value * color);
        }


        /*
        /// <summary>
        /// 计算分形噪声
        /// </summary>

        private float CalculateFractalNoise(float x, float y)
        {
            float noiseValue = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxNoiseValue = 0f; // 用于归一化
            for (int i = 0; i < octaves; i++)
            {
                float perlinValue = Mathf.PerlinNoise(x * frequency, y * frequency) * 2f - 1f; // [-1,1]
                noiseValue += perlinValue * amplitude;
                maxNoiseValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // 归一化到[0,1]
            noiseValue = (noiseValue / maxNoiseValue + 1f) * 0.5f;
            return noiseValue;
        }*/

        // 计算当前位置的高度梯度（x,z方向）
        Vector2 CalculateHeightGradient(Vector3 pos)
        {
            int x = Mathf.Clamp((int)pos.x, 1, width - 2);
            int z = Mathf.Clamp((int)pos.z, 1, height - 2);

            // 中心差分法计算梯度
            float dhdx = (heightMap[x + 1, z] - heightMap[x - 1, z]) * 0.5f;
            float dhdz = (heightMap[x, z + 1] - heightMap[x, z - 1]) * 0.5f;

            return new Vector2(dhdx, dhdz);
        }
        /// <summary>
        /// 地形坐标转世界坐标，首先地图坐标90度才是实际方向，所以要颠倒x和y
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        private Vector3 TerrainPosToWorldPos(Vector3 vector)
        {
            Vector3 re = new Vector3(vector.z, 0, vector.x) / mapscale;
            re.y = vector.y * speceHeight;
            return re;
        }
        #endregion

        #region 按钮

        [ContextMenu("生成分形噪声地形")]
        public void GenerateTerrain()
        {
            ApplyFractalNoiseToTerrain();
        }

        [ContextMenu("重置地形")]
        public void ResetTerrain()
        {
            Debug.LogError("重置地形");
            if (terrain != null)
            {
                TerrainData terrainData = terrain.terrainData;
                int width = terrainData.heightmapResolution;
                int height = terrainData.heightmapResolution;
                terrainData.SetHeights(0, 0, new float[width, height]);

                var surface = GetComponent<NavMeshSurface>();
                surface.UpdateNavMesh(surface.navMeshData);
            }
        }
        #endregion

    }
}