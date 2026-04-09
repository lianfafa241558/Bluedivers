using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;
namespace FpsGame.MapUtils
{
    /// <summary>
    /// 生成地形，只从terraindata拿数据，和其他组件不挂钩
    /// </summary>
    public class GenerateNoiseTerrain : MonoBehaviour
    {

        public GameObject StartPoint;


        [Header("地形设置")]
        public Terrain terrain;
        [CustomLabel("是否在Start时自动生成地形")]
        public bool generateOnStart = true;

        [Foldout("基础地形", true)]
        [CustomLabel("基础地形缩放")]
        public float baseScale = 200f;
        [CustomLabel("基础地形高度")]
        public float baseAmplitude = 80f;
        [CustomLabel("细节层缩放")]
        public float detailScale = 25f;
        public float detailAmplitude = 15f;

        [Foldout("高原", true)]
        [CustomLabel("高原半径（占地形比例）")]
        public float plateauRadius = 0.25f;
        [CustomLabel("高原抬升强度")]
        public float plateauIntensity = 0.5f;
        [CustomLabel("边缘衰减幅度")]
        public float edgeDropoff = 0.08f;
        [CustomLabel("高原生成阈值")]
        public float plateauThreshold = 0.65f;

        // 高原形态控制参数
        public float plateauMaskScale = 10;      // 主噪声尺度（控制高原基本形态）

        [Foldout("树", true)]
        List<TreeInstance> trees;
        [CustomLabel("树概率")]
        public float treeProbability = 0.1f;

        [Foldout("其他", true)]
        public bool isLand;

        [SerializeField]
        private Texture2D preHeight, preTexture;//, preBaseHeight;

        private float[,] heightMap;
        private float[,,] textureMap;
        [SerializeField]
        private int width, height, size, speceHeight;

        //比如分辨率1024/宽512就是2
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
        public void ApplyFractalNoiseToTerrain()
        {
            if (terrain == null)
            {
                Debug.LogWarning("未指定Terrain对象");
                return;
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
            heightMap = terrainData.GetHeights(0, 0, width, height);//原始值(0-1)
            textureMap = terrainData.GetAlphamaps(0, 0, size, size);//原始值(0-1)

            //Debug.LogError("贴图纹理尺寸"+ textureMap.GetLength(2));
            trees = new List<TreeInstance>();
            //textureMap =new float[width,height,4];//原始值(0-1)
            // 生成基础地形
            GenerateBaseTerrain();

            // 添加侵蚀效果
            ApplyErosionEffect();

            //材质
            ApplyTextures();

            // 应用高度图
            terrainData.SetHeights(0, 0, heightMap);
            terrainData.SetAlphamaps(0, 0, textureMap);

            //设置树
            SpawnTrees();

            //terrainData.treeInstances = trees.ToArray();
            terrainData.SetTreeInstances(trees.ToArray(), true);
            //terrain.Flush();


            #region 原版的
            /*
            float[,] heights = terrainData.GetHeights(0, 0, width, height);//原始值(0-1)
            heightmap = terrainData.GetHeights(0, 0, width, height);
            float peakThreshold = 0.5f+0.5f; // 峰顶阈值，超过这个高度开始平缓
            float maxThreshold = 0.65f + 0.5f; // 峰顶阈值，不会超过这个高度

            float valleyThreshold = 0.25f + 0.5f; // 谷底阈值，超过这个高度开始平缓
            float minThreshold = 0.1f + 0.5f; // 谷底阈值，不会超过这个高度


            // 第一步：计算所有点的噪声高度（不考虑有效范围）
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float xCoord = noiseOffset.x + (float)x / width * noiseScale;
                    float yCoord = noiseOffset.y + (float)y / height * noiseScale;

                    heightmap[x, y] = CalculateFractalNoise(xCoord, yCoord);
                    heightmap[x, y] = Mathf.Pow(heightmap[x, y],1.5f)+0.5f;//更抖并且抬升
                    //tempHeights[x, y] *= tempHeights[x, y];//平方让坡度更抖
                }
            }



            // 第二步：根据有效范围调整高度，实现无效点缓慢下降到0
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dis = Vector2.Distance(center * Vector2.one, new Vector2(x, y));
                    bool vaild = dis <= EffectiveRange * center;
                    float heightValue = heightmap[x, y];

                    if (vaild)
                    {
                        if (heightValue > peakThreshold)
                        {
                            // 计算超出阈值部分的比例
                            float excess = (heightValue - peakThreshold) / (maxThreshold - peakThreshold);
                            // 用SmoothStep平滑过渡到高原高度
                            heightValue = Mathf.Lerp(peakThreshold, maxThreshold, Mathf.SmoothStep(0f, 1, excess));
                        }

                        else if(heightValue<valleyThreshold)
                        {
                            // 计算超出阈值部分的比例
                            float excess = (heightValue - minThreshold) / (valleyThreshold - minThreshold);
                            // 用SmoothStep平滑过渡到高原高度
                            heightValue = Mathf.Lerp(minThreshold, valleyThreshold, Mathf.SmoothStep(0, 1, excess));
                        }

                        heights[x, y] = heightValue;
                    }
                    else if(isLand)
                    {
                        // 计算距离超过有效范围的部分比例，范围是[0,1]
                        float excessRatio = Mathf.InverseLerp(EffectiveRange * center, width, dis);
                        // 衰减系数，距离越远越接近0，这里用1 - excessRatio实现线性衰减
                        float attenuation = Mathf.Clamp(1f - excessRatio * 2,0.2f,1);

                        // 高度乘以衰减系数，实现缓慢下降到0.2
                        heights[x, y] = heightValue * attenuation;
                    }
                    else
                    {
                        heights[x, y] = heightValue;

                    }


                }
            }



            //第三步，设置纹理

            // 获取Alpha Map尺寸
            int alphaMapWidth = terrainData.alphamapWidth;
            int alphaMapHeight = terrainData.alphamapHeight;
            int alphaMapLayers = terrainData.alphamapLayers;

            // 获取当前所有Alpha Map数据
            float[,,] alphaMaps = terrainData.GetAlphamaps(0, 0, alphaMapWidth, alphaMapHeight);

            for (int x = 0; x < alphaMapWidth; x++)
            {
                for (int y = 0; y < alphaMapHeight; y++)
                {
                    var b = Mathf.InverseLerp(peakThreshold - 0.02f, peakThreshold+0.05f, heights[x, y]);
                    var c = Mathf.InverseLerp(1- valleyThreshold,1-minThreshold, 1- heights[x, y]);
                    alphaMaps[x, y, 1] = b;
                    alphaMaps[x, y, 2] = c;
                    alphaMaps[x, y, 0] = 1-b-c;
                }
            }

            */



            // 应用修改后的Alpha Map
            //terrainData.SetAlphamaps(0, 0, alphaMaps);


            // 最后一步：计算高度乘数
            /*
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    proInfo.SetPixel(x, y, heightmap[x, y] * Color.white);
                }
            }*/
            #endregion


            preHeight.Apply(false, false);//必须加上
            preTexture.Apply(false, false);//必须加上
            //preBaseHeight.Apply(false, false);//必须加上
                                              //terrainData.SetHeights(0, 0, heightMap);

            var surface = GetComponent<UnityEngine.AI.NavMeshSurface>();
            surface.UpdateNavMesh(surface.navMeshData);
            //surface.BuildNavMesh();

            //Debug.LogError("完成地形设置");

        }


        #region 步骤

        /// <summary>
        /// 基础地形
        /// </summary>
        void GenerateBaseTerrain()
        {
            var now = System.DateTime.Now;
            System.Random TaskRandom = new(now.Month * 100 + now.Day + now.Hour * 100 + (now.Minute / 30 * 30));//每小时刷新
            float offsetX = TaskRandom.Range(0, 9999f);
            float offsetY = TaskRandom.Range(0, 9999f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {

                    // 基础噪声层
                    float nx = offsetX + x / (float)width * baseScale;
                    float ny = offsetY + y / (float)height * baseScale;
                    var nowheight = (Mathf.Pow(Mathf.PerlinNoise(ny, nx), 2) * 0.9f + 0.1f) * baseAmplitude;

                    // 细节噪声层
                    float dx = offsetX + x / (float)width * detailScale;
                    float dy = offsetY + y / (float)height * detailScale;
                    nowheight += (Mathf.Pow(Mathf.PerlinNoise(dy, dx), 2) * 2 - 1) * detailAmplitude;


                    // 标准化高度
                    heightMap[y, x] = nowheight / (1 + baseAmplitude + detailAmplitude);
                    SetPixel(preHeight, y, x, heightMap[y, x], 0);
                }
            }
        }


        /// <summary>
        /// 添加侵蚀效果
        /// </summary>
        void ApplyErosionEffect()
        {

            float plateauOffsetX = Random.Range(0f, 9999f);
            float plateauOffsetY = Random.Range(0f, 9999f);
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float nowheight = heightMap[x, y];
                    if (nowheight > plateauThreshold)
                    {
                        // 计算超出阈值部分的比例
                        float excess = (nowheight - plateauThreshold) / plateauIntensity / 2;
                        // 用SmoothStep平滑过渡到高原高度
                        heightMap[x, y] = nowheight = 0.5f * nowheight + 0.5f * Mathf.Lerp(plateauThreshold, plateauThreshold + plateauIntensity * 2, excess);
                    }


                    // 高原噪声采样
                    float px = plateauOffsetX + x / (float)width * plateauMaskScale;
                    float py = plateauOffsetY + y / (float)height * plateauMaskScale;
                    float plateauMask = Mathf.PerlinNoise(px, py);
                    // 噪声混合策略（强化大面积连续区域）
                    //plateauMask = Mathf.Pow(plateauMask, 2f);

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

            }
        }

        /// <summary>
        /// 设置材质
        /// </summary>
        void ApplyTextures()
        {

            //int size = terrain.terrainData.alphamapResolution;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    /*
                    //高度归一化（0-1）
                    float nowheight = terrain.terrainData.GetHeight(y, x) / terrain.terrainData.size.y;
                    //坡度值（0-1）（坡度本身返回0-90）
                    float steepness = terrain.terrainData.GetSteepness(y / (float)size,
                                        x / (float)size) / 90f;
                    */

                    //生成时没有巢穴，所以系数为1
                    float steepness = GetSteepness(y, x) / 90f;//坡度[0,1]
                    float nowheight = heightMap[y, x];//高度[0,1]

                    // 岩石层（陡坡）(22.5度-67.5度)
                    textureMap[y, x, 4] = Mathf.Clamp01(steepness * 2f - 0.5f);

                    // 沙地层（中等高度)在[0,0.5]高度逐步变为[0,1]
                    textureMap[y, x, 1] = Mathf.Clamp01(nowheight * 2f) * (1 - textureMap[y, x, 4]);

                    // 侵蚀层(低洼区域)在[0,0.5]高度逐步变为[1,0]
                    textureMap[y, x, 2] = Mathf.Clamp01((1 - nowheight) * 2f) * (1 - textureMap[y, x, 4]);

                    textureMap[y, x, 3] = 0;
                    textureMap[y, x, 0] = 0;

                    SetPixel(preTexture, y, x, textureMap[y, x, 0], 0);
                    SetPixel(preTexture, y, x, textureMap[y, x, 1], 1);
                    SetPixel(preTexture, y, x, textureMap[y, x, 2], 2);
                    SetPixel(preTexture, y, x, steepness, 3);
                }
            }

        }

        #endregion

        #region 树
        void SpawnTrees()
        {
            // 10000是因为除2次100
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
            float cellSize = 1 / 16f;//应该是32，但是我的地形后面 +0.5*0.5了
                                     // 获取中心点及周边8邻域高度（处理边界时自动使用最近的有效点）
            float h = heightMap[x, y];
            float h_x0 = heightMap[Mathf.Max(0, x - 1), y];    // 左
            float h_x1 = heightMap[Mathf.Min(width - 1, x + 1), y];  // 右
            float h_y0 = heightMap[x, Mathf.Max(0, y - 1)];    // 下
            float h_y1 = heightMap[x, Mathf.Min(height - 1, y + 1)]; // 上

            // 计算x/z方向的梯度（中心差分法）
            float gradientX = (h_x1 - h_x0) / (2f * cellSize);
            float gradientZ = (h_y1 - h_y0) / (2f * cellSize);

            // 计算坡度角（arctan(√(Dh/Dx2 + Dh/Dz2))）
            float slopeRadians = Mathf.Atan(Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ));
            float slopeDegrees = slopeRadians * Mathf.Rad2Deg;

            return Mathf.Clamp(slopeDegrees, 0f, 90f);
        }


        private void SetPixel(Texture2D texture, int x, int y, float value, int colorMask)
        {
            Color baseColor = Color.black;
            Color color;
            switch (colorMask)
            {
                case 1:
                    color = Color.green;
                    baseColor = texture.GetPixel(width - x, y);
                    break;
                case 2:
                    color = Color.blue;
                    baseColor = texture.GetPixel(width - x, y);
                    break;
                case 3:
                    color = new(0, 0, 0, 1);
                    baseColor = texture.GetPixel(width - x, y);
                    break;
                default:
                    color = Color.red;
                    break;
            }
            texture.SetPixel(width - x, y, baseColor + value * color);
        }


        /*
        /// <summary>
        /// 计算分形噪声值
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
        /// 地形坐标转世界坐标，首先地图坐标转90度才是实际方向，所以要颠倒x和y
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

                var surface = GetComponent<UnityEngine.AI.NavMeshSurface>();
                surface.UpdateNavMesh(surface.navMeshData);
            }
        }
        #endregion

    }
}