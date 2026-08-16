using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Attribute;
using Unity.AI.Navigation;
using UnityEngine;
using Random = UnityEngine.Random;
namespace FpsGame.MapUtils
{

    public enum TerrainType
    {
        /// <summary>沙漠</summary>
        [InspectorName("沙漠")]
        Desert,
        /// <summary>高原</summary>
        [InspectorName("高原")]
        Plateau,
        /// <summary>雨林</summary>
        [InspectorName("雨林")]
        Rainforest,
        /// <summary>丘陵</summary>
        [InspectorName("丘陵")]
        Hills,
        /// <summary>盆地</summary>
        [InspectorName("盆地")]
        Basin,
        /// <summary>平原</summary>
        [InspectorName("平原")]
        Plains,
        /// <summary>山地</summary>
        [InspectorName("山地")]
        Mountains,
    }

    /// <summary>
    /// 地形预设参数，每种 TerrainType 对应一组完整的地形生成参数
    /// </summary>
    [Serializable]
    public struct TerrainPresetData
    {
        // ---- 分形噪声参数 ----
        [InspectorName("基础缩放")]
        public float baseScale;
        [InspectorName("基础振幅")]
        public float baseAmplitude;
        [InspectorName("倍频数")]
        public int octaves;
        [InspectorName("间隙度")]
        public float lacunarity;
        [InspectorName("持续度")]
        public float persistence;
        [InspectorName("细节缩放")]
        public float detailScale;
        [InspectorName("细节振幅")]
        public float detailAmplitude;

        // ---- 高度重塑 ----
        [InspectorName("高度幂次曲线")]
        public float heightPower;

        // ---- 后处理参数 ----
        [InspectorName("高原抬升强度")]
        public float plateauIntensity;
        [InspectorName("高原阈值")]
        public float plateauThreshold;
        [InspectorName("高原噪声缩放")]
        public float plateauMaskScale;
        [InspectorName("边缘衰减")]
        public float edgeDropoff;
        [InspectorName("侵蚀迭代次数")]
        public int erosionIterations;

        // ---- 纹理映射（索引对应 TerrainLayer） ----
        [InspectorName("沙地层索引")]
        public int sandLayerIndex;
        [InspectorName("草地层索引")]
        public int grassLayerIndex;
        [InspectorName("岩石层索引")]
        public int rockLayerIndex;
        [InspectorName("雪地层索引")]
        public int snowLayerIndex;

        // ---- 植被 ----
        [InspectorName("树生成概率")]
        public float treeProbability;
        [InspectorName("树原型索引范围")]
        public Vector2Int treePrototypeRange;
        [InspectorName("树最小坡度")]
        public float treeMinSlope;
        [InspectorName("树最大坡度")]
        public float treeMaxSlope;
        [InspectorName("树最小高度")]
        public float treeMinHeight;
        [InspectorName("树最大高度")]
        public float treeMaxHeight;

        // ---- 细节植被 ----
        [InspectorName("草密度")]
        public float detailDensity;
        [InspectorName("花密度")]
        public float detailFlowerDensity;
        [InspectorName("草最小坡度")]
        public float detailMinSlope;
        [InspectorName("草最大坡度")]
        public float detailMaxSlope;
        [InspectorName("草最小高度")]
        public float detailMinHeight;
        [InspectorName("草最大高度")]
        public float detailMaxHeight;
    }


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

        [InspectorName("地形类型")]
        [SerializeField] private TerrainType _terrainType = TerrainType.Desert;

        [Header("手动覆盖（仅调试用，日常请通过地形类型预设控制）")]
        [InspectorName("覆盖预设参数")]
        [SerializeField] private bool _overridePreset = false;

        [InspectorName("调试：写 preHeight/preTexture")]
        [SerializeField] private bool _debugPreTexture = false;

        [Foldout("基础地形", true)]
        [InspectorName("基础地形缩放")]
        public float baseScale = 5;
        [InspectorName("基础地形高度")]
        public float baseAmplitude = 1f;
        [InspectorName("细节层缩放")]
        public float detailScale = 40;
        public float detailAmplitude = 0.05f;

        [Foldout("高原", true)]
        [InspectorName("高原半径（占地形比例）")]
        public float plateauRadius = 0f;
        [InspectorName("高原抬升强度")]
        public float plateauIntensity = 0.03f;
        [InspectorName("边缘衰减幅度")]
        public float edgeDropoff = 0.1f;
        [InspectorName("高原生成阈值")]
        public float plateauThreshold = 0.6f;

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
        private void Start()
        {
            if (generateOnStart && terrain != null)
            {
                StartCoroutine(ApplyFractalNoiseToTerrain(_terrainType));
            }
        }*/

        /// <summary>
        /// 根据地形类型获取预设参数
        /// </summary>
        private TerrainPresetData GetTerrainPreset(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Desert:
                    return new TerrainPresetData
                    {
                        baseScale = 5f,
                        baseAmplitude = 1.0f,
                        octaves = 4,
                        lacunarity = 2.0f,
                        persistence = 0.5f,
                        detailScale = 40f,
                        detailAmplitude = 0.05f,
                        heightPower = 0.8f,
                        plateauIntensity = 0f,
                        plateauThreshold = 0.99f,
                        plateauMaskScale = 10f,
                        edgeDropoff = 0.05f,
                        erosionIterations = 2,
                        sandLayerIndex = 1,
                        grassLayerIndex = -1,
                        rockLayerIndex = 4,
                        snowLayerIndex = -1,
                        treeProbability = 0.03f,
                        treePrototypeRange = new Vector2Int(0, 1),
                        treeMinSlope = 0f,
                        treeMaxSlope = 30f,
                        treeMinHeight = 0.1f,
                        treeMaxHeight = 0.7f,
                        detailDensity = 0.02f,
                        detailFlowerDensity = 0f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 15f,
                        detailMinHeight = 0f,
                        detailMaxHeight = 0.3f
                    };

                case TerrainType.Plateau:
                    return new TerrainPresetData
                    {
                        baseScale = 3f,
                        baseAmplitude = 1.5f,
                        octaves = 5,
                        lacunarity = 2.5f,
                        persistence = 0.35f,
                        detailScale = 30f,
                        detailAmplitude = 0.015f,
                        heightPower = 1.2f,
                        plateauIntensity = 0.1f,
                        plateauThreshold = 0.5f,
                        plateauMaskScale = 12f,
                        edgeDropoff = 0.06f,
                        erosionIterations = 1,
                        sandLayerIndex = -1,
                        grassLayerIndex = 0,
                        rockLayerIndex = 4,
                        snowLayerIndex = -1,
                        treeProbability = 0.08f,
                        treePrototypeRange = new Vector2Int(0, 3),
                        treeMinSlope = 0f,
                        treeMaxSlope = 40f,
                        treeMinHeight = 0.3f,
                        treeMaxHeight = 0.9f,
                        detailDensity = 0.08f,
                        detailFlowerDensity = 0.02f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 30f,
                        detailMinHeight = 0.25f,
                        detailMaxHeight = 0.85f
                    };

                case TerrainType.Rainforest:
                    return new TerrainPresetData
                    {
                        baseScale = 8f,
                        baseAmplitude = 1.2f,
                        octaves = 4,
                        lacunarity = 2.5f,
                        persistence = 0.4f,
                        detailScale = 40f,
                        detailAmplitude = 0.04f,
                        heightPower = 1.0f,
                        plateauIntensity = 0f,
                        plateauThreshold = 0.99f,
                        plateauMaskScale = 8f,
                        edgeDropoff = 0.03f,
                        erosionIterations = 3,
                        sandLayerIndex = -1,
                        grassLayerIndex = 0,
                        rockLayerIndex = 4,
                        snowLayerIndex = -1,
                        treeProbability = 0.25f,
                        treePrototypeRange = new Vector2Int(0, 5),
                        treeMinSlope = 0f,
                        treeMaxSlope = 50f,
                        treeMinHeight = 0.1f,
                        treeMaxHeight = 0.95f,
                        detailDensity = 0.2f,
                        detailFlowerDensity = 0.06f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 50f,
                        detailMinHeight = 0f,
                        detailMaxHeight = 0.9f
                    };

                case TerrainType.Hills:
                    return new TerrainPresetData
                    {
                        baseScale = 6f,
                        baseAmplitude = 1.5f,
                        octaves = 4,
                        lacunarity = 2.0f,
                        persistence = 0.5f,
                        detailScale = 35f,
                        detailAmplitude = 0.08f,
                        heightPower = 1.0f,
                        plateauIntensity = 0f,
                        plateauThreshold = 0.99f,
                        plateauMaskScale = 8f,
                        edgeDropoff = 0.06f,
                        erosionIterations = 2,
                        sandLayerIndex = 1,
                        grassLayerIndex = 0,
                        rockLayerIndex = 4,
                        snowLayerIndex = -1,
                        treeProbability = 0.12f,
                        treePrototypeRange = new Vector2Int(0, 3),
                        treeMinSlope = 0f,
                        treeMaxSlope = 35f,
                        treeMinHeight = 0.1f,
                        treeMaxHeight = 0.85f,
                        detailDensity = 0.1f,
                        detailFlowerDensity = 0.04f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 40f,
                        detailMinHeight = 0f,
                        detailMaxHeight = 0.8f
                    };

                case TerrainType.Basin:
                    return new TerrainPresetData
                    {
                        baseScale = 4f,
                        baseAmplitude = 2.5f,
                        octaves = 4,
                        lacunarity = 2.0f,
                        persistence = 0.45f,
                        detailScale = 30f,
                        detailAmplitude = 0.06f,
                        heightPower = 2.0f,
                        plateauIntensity = 0f,
                        plateauThreshold = 0.99f,
                        plateauMaskScale = 6f,
                        edgeDropoff = 0.08f,
                        erosionIterations = 1,
                        sandLayerIndex = 1,
                        grassLayerIndex = 0,
                        rockLayerIndex = 4,
                        snowLayerIndex = -1,
                        treeProbability = 0.10f,
                        treePrototypeRange = new Vector2Int(0, 3),
                        treeMinSlope = 0f,
                        treeMaxSlope = 40f,
                        treeMinHeight = 0.05f,
                        treeMaxHeight = 0.8f,
                        detailDensity = 0.1f,
                        detailFlowerDensity = 0.03f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 40f,
                        detailMinHeight = 0f,
                        detailMaxHeight = 0.85f
                    };

                case TerrainType.Plains:
                    return new TerrainPresetData
                    {
                        baseScale = 12f,
                        baseAmplitude = 0.25f,
                        octaves = 3,
                        lacunarity = 2.0f,
                        persistence = 0.4f,
                        detailScale = 60f,
                        detailAmplitude = 0.02f,
                        heightPower = 0.8f,
                        plateauIntensity = 0f,
                        plateauThreshold = 0.99f,
                        plateauMaskScale = 8f,
                        edgeDropoff = 0.05f,
                        erosionIterations = 2,
                        sandLayerIndex = 1,
                        grassLayerIndex = 0,
                        rockLayerIndex = 4,
                        snowLayerIndex = -1,
                        treeProbability = 0.03f,
                        treePrototypeRange = new Vector2Int(0, 2),
                        treeMinSlope = 0f,
                        treeMaxSlope = 10f,
                        treeMinHeight = 0.1f,
                        treeMaxHeight = 0.6f,
                        detailDensity = 0.15f,
                        detailFlowerDensity = 0.1f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 15f,
                        detailMinHeight = 0f,
                        detailMaxHeight = 0.6f
                    };

                case TerrainType.Mountains:
                    return new TerrainPresetData
                    {
                        baseScale = 1.5f,
                        baseAmplitude = 15.0f,
                        octaves = 7,
                        lacunarity = 2.5f,
                        persistence = 0.4f,
                        detailScale = 20f,
                        detailAmplitude = 0.02f,
                        heightPower = 2.0f,
                        plateauIntensity = 0f,
                        plateauThreshold = 0.99f,
                        plateauMaskScale = 6f,
                        edgeDropoff = 0.08f,
                        erosionIterations = 2,
                        sandLayerIndex = -1,
                        grassLayerIndex = 0,
                        rockLayerIndex = 4,
                        snowLayerIndex = -1,
                        treeProbability = 0.02f,
                        treePrototypeRange = new Vector2Int(0, 2),
                        treeMinSlope = 0f,
                        treeMaxSlope = 25f,
                        treeMinHeight = 0f,
                        treeMaxHeight = 0.4f,
                        detailDensity = 0.03f,
                        detailFlowerDensity = 0f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 20f,
                        detailMinHeight = 0f,
                        detailMaxHeight = 0.35f
                    };

                default:
                    return GetTerrainPreset(TerrainType.Desert);
            }
        }

        public IEnumerator SetTextures(Texture[] Texture,Vector2[] sizes)
        {
            TerrainData terrainData = terrain.terrainData;
            TerrainLayer[] layers = terrainData.terrainLayers;
            for (int i=0;i<Mathf.Min(terrainData.terrainLayers.Length,Texture.Length); ++i)
            {
                TerrainLayer layer = layers[i];
                layer.diffuseTexture = (Texture2D)Texture[i];
                layer.tileSize = sizes[i];
                terrainData.terrainLayers = layers;
            }
            yield return null;
        }


        /// <summary>
        /// 应用分形噪声到地形（使用 Inspector 中设置的地形类型）
        /// </summary>
        public IEnumerator ApplyFractalNoiseToTerrain()
        {
            yield return ApplyFractalNoiseToTerrain(_terrainType);
        }

        /// <summary>
        /// 应用分形噪声到地形
        /// </summary>
        /// <param name="terrainType">地形类型预设</param>
        public IEnumerator ApplyFractalNoiseToTerrain(TerrainType terrainType)
        {
            if (terrain == null)
            {
                Debug.LogWarning("未指定Terrain对象");
                yield break;
            }

            TerrainData terrainData = terrain.terrainData;
            width = terrainData.heightmapResolution;
            height = terrainData.heightmapResolution;
            size = terrain.terrainData.alphamapResolution;
            speceHeight = (int)terrain.terrainData.size.y;

            // 获取预设参数
            TerrainPresetData preset = GetTerrainPreset(terrainType);
            Debug.Log($"使用地形预设: {terrainType} | 噪声层数={preset.octaves} | 树概率={preset.treeProbability}");

            preHeight = new Texture2D(width, height, TextureFormat.ARGB32, false, false);
            preTexture = new Texture2D(width, height, TextureFormat.ARGB32, false, false);
            heightMap = terrainData.GetHeights(0, 0, width, height);
            textureMap = terrainData.GetAlphamaps(0, 0, size, size);

            trees = new List<TreeInstance>();

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            // 生成基础地形（fBm 分形噪声）
            yield return GenerateBaseTerrain(preset);
            Debug.Log($"生成基础地形时间: {sw.ElapsedMilliseconds} ms");
            sw.Restart();

            // 地形后处理（根据类型执行不同策略）
            yield return ApplyTerrainPostProcess(preset, terrainType);
            Debug.Log($"后处理时间: {sw.ElapsedMilliseconds} ms");
            sw.Restart();

            // 材质
            yield return ApplyTextures();
            Debug.Log($"生成材质时间: {sw.ElapsedMilliseconds} ms");
            sw.Restart();

            // 应用高度图，分块提交，避免单帧卡顿
            int chunkSize = 128;
            yield return ApplyHeightsInChunks(chunkSize);
            yield return null;

            yield return ApplyAlphamapsInChunks(chunkSize);
            yield return null;

            // 设置树
            yield return SpawnTrees(preset);
            yield return null;
            terrainData.SetTreeInstances(trees.ToArray(), true);
            yield return null;

            // 设置草（细节）
            yield return SpawnDetails(preset);
            yield return null;

            preHeight.Apply(false, false);
            preTexture.Apply(false, false);

            // NavMesh 构建（首次同步构建填充数据，之后异步增量更新并等待完成）
            yield return null;
            var surface = GetComponent<NavMeshSurface>();
            if (surface != null)
            {
                if (surface.navMeshData == null)
                    surface.BuildNavMesh(); // 首次构建：NavMeshBuilder.BuildNavMeshData 会创建并赋值 navMeshData

                if (surface.navMeshData != null)
                {
                    var asyncOp = surface.UpdateNavMesh(surface.navMeshData);
                    float timeout = Time.realtimeSinceStartup + 10f;
                    while (!asyncOp.isDone && Time.realtimeSinceStartup < timeout)
                    {
                        yield return null;
                    }
                    if (!asyncOp.isDone)
                        Debug.LogWarning("NavMesh 异步构建超时，可能仍在后台进行");
                }
                else
                {
                    Debug.LogWarning("NavMesh 构建失败：navMeshData 为空，跳过 NavMesh 更新");
                }
            }
            Debug.Log($"完成总时间: {sw.ElapsedMilliseconds} ms");
        }


        #region 步骤

        /// <summary>
        /// 分形布朗运动（fBm）噪声采样，替代原来的简单双层 PerlinNoise
        /// </summary>
        private float SampleFbmNoise(float x, float y, int octaves, float lacunarity, float persistence)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return value / maxValue;
        }

        /// <summary>
        /// 生成基础地形（使用 fBm 分形噪声）
        /// </summary>
        IEnumerator GenerateBaseTerrain(TerrainPresetData preset)
        {
            var now = System.DateTime.Now;
            System.Random TaskRandom = new(now.Month * 100 + now.Day + now.Hour * 100 + (now.Minute / 30 * 30));
            float offsetX = TaskRandom.Range(0, 9999f);
            float offsetY = TaskRandom.Range(0, 9999f);

            float effectiveBaseScale = _overridePreset ? baseScale : preset.baseScale;
            float effectiveBaseAmplitude = _overridePreset ? baseAmplitude : preset.baseAmplitude;
            int effectiveOctaves = _overridePreset ? 4 : preset.octaves;
            float effectiveLacunarity = _overridePreset ? 2.0f : preset.lacunarity;
            float effectivePersistence = _overridePreset ? 0.5f : preset.persistence;
            float effectiveDetailScale = _overridePreset ? detailScale : preset.detailScale;
            float effectiveDetailAmplitude = _overridePreset ? detailAmplitude : preset.detailAmplitude;
            float effectiveHeightPower = _overridePreset ? 1.0f : preset.heightPower;

            float startTime = Time.realtimeSinceStartup;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 主噪声层（fBm）：先用 heightPower 重塑噪声分布，再乘以振幅
                    //   heightPower>1 → 低噪声被压缩向下，只有高噪声才能产生高度 → 适合山地
                    //   heightPower<1 → 低噪声被拉高，地面整体偏高 → 适合平原
                    float nx = offsetX + x / (float)width * effectiveBaseScale;
                    float ny = offsetY + y / (float)height * effectiveBaseScale;
                    float baseNoise = SampleFbmNoise(nx, ny, effectiveOctaves, effectiveLacunarity, effectivePersistence);
                    float reshapedNoise = Mathf.Pow(baseNoise, effectiveHeightPower);
                    float nowheight = reshapedNoise * effectiveBaseAmplitude;

                    // 细节噪声层
                    float dx = offsetX + x / (float)width * effectiveDetailScale;
                    float dy = offsetY + y / (float)height * effectiveDetailScale;
                    float detailNoise = SampleFbmNoise(dx, dy, 3, 2.0f, 0.5f);
                    nowheight += (detailNoise * 2f - 1f) * effectiveDetailAmplitude;

                    heightMap[y, x] = nowheight / (1f + effectiveBaseAmplitude + effectiveDetailAmplitude);
                    if (_debugPreTexture) SetPixel(preHeight, y, x, heightMap[y, x], 0);
                }

                if (y % 8 == 0 && y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }
        }


        /// <summary>
        /// 地形后处理——根据地形类型执行不同的处理策略
        /// </summary>
        IEnumerator ApplyTerrainPostProcess(TerrainPresetData preset, TerrainType terrainType)
        {
            float effectivePlateauIntensity = _overridePreset ? plateauIntensity : preset.plateauIntensity;
            float effectivePlateauThreshold = _overridePreset ? plateauThreshold : preset.plateauThreshold;
            float effectivePlateauMaskScale = _overridePreset ? plateauMaskScale : preset.plateauMaskScale;
            float effectiveEdgeDropoff = _overridePreset ? edgeDropoff : preset.edgeDropoff;
            int effectiveErosionIterations = _overridePreset ? 1 : preset.erosionIterations;

            var now = System.DateTime.Now;
            System.Random TaskRandom = new(now.Month * 100 + now.Day + now.Hour * 100 + (now.Minute / 30 * 30));
            float noiseOffsetX = TaskRandom.Range(0, 9999f);
            float noiseOffsetY = TaskRandom.Range(0, 9999f);

            float startTime = Time.realtimeSinceStartup;

            switch (terrainType)
            {
                case TerrainType.Desert:
                    // 沙漠：风蚀平滑 + 沙丘塑形
                    yield return ApplyWindErosion(effectiveErosionIterations, startTime);
                    break;

                case TerrainType.Plateau:
                    // 高原：高度钳制 + 悬崖锐化 + 高原抬升
                    yield return ApplyPlateauProcess(effectivePlateauThreshold, effectivePlateauIntensity,
                        effectivePlateauMaskScale, effectiveEdgeDropoff, noiseOffsetX, noiseOffsetY, startTime);
                    break;

                case TerrainType.Rainforest:
                    // 雨林：水力侵蚀 + 山谷雕刻
                    yield return ApplyHydraulicErosion(effectiveErosionIterations, startTime);
                    break;

                case TerrainType.Hills:
                    // 丘陵：温和侵蚀平滑
                    yield return ApplyGentleSmoothing(effectiveErosionIterations, startTime);
                    break;

                case TerrainType.Basin:
                    // 盆地：径向凹陷 + 水力侵蚀
                    yield return ApplyBasinDepression(effectiveErosionIterations, startTime);
                    break;

                case TerrainType.Plains:
                    // 平原：温和平滑
                    yield return ApplyGentleSmoothing(effectiveErosionIterations, startTime);
                    break;

                case TerrainType.Mountains:
                    // 山地：压低低洼 → 水力侵蚀 → 峰值拉伸 → 基准高度 0.1
                    yield return DepressLowlands(0.4f, 0.7f, startTime);
                    yield return ApplyHydraulicErosion(effectiveErosionIterations, startTime);
                    yield return StretchToMax(0.15f, 1.05f);
                    yield return ApplyBaselineHeight(0.1f);
                    break;
            }
        }

        /// <summary>
        /// 沙漠风蚀处理：定向平滑模拟风蚀效果
        /// </summary>
        IEnumerator ApplyWindErosion(int iterations, float startTime)
        {
            float[,] buffer = new float[height, width];
            for (int iter = 0; iter < iterations; iter++)
            {
                // 风向角度（模拟盛行风）
                float windAngle = 0.3f + iter * 0.5f;
                float windX = Mathf.Cos(windAngle);
                float windZ = Mathf.Sin(windAngle);

                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        // 迎风面采样
                        int sx = Mathf.Clamp(x - (int)(windX * 2), 1, width - 2);
                        int sy = Mathf.Clamp(y - (int)(windZ * 2), 1, height - 2);

                        float current = heightMap[x, y];
                        float windward = heightMap[sx, sy];
                        // 迎风面侵蚀，背风面沉积
                        float diff = current - windward;
                        buffer[x, y] = current - diff * 0.15f;
                    }

                    if (y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                    {
                        yield return null;
                        startTime = Time.realtimeSinceStartup;
                    }
                }

                // 回写并 clamp
                for (int y = 1; y < height - 1; y++)
                    for (int x = 1; x < width - 1; x++)
                        heightMap[x, y] = Mathf.Clamp01(buffer[x, y]);
            }
        }

        /// <summary>
        /// 高原处理：高度钳制 + 悬崖锐化
        /// </summary>
        IEnumerator ApplyPlateauProcess(float threshold, float intensity, float maskScale,
            float dropoff, float offsetX, float offsetY, float startTime)
        {
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    float h = heightMap[x, y];

                    // 高出阈值 → 钳制并平滑过渡到高原面
                    if (h > threshold)
                    {
                        float excess = (h - threshold) / (intensity * 2f + 0.001f);
                        heightMap[x, y] = Mathf.Lerp(h,
                            threshold + intensity * 2f * Mathf.Clamp01(excess), 0.5f);
                    }

                    // 高原噪声蒙版
                    float px = offsetX + x / (float)width * maskScale;
                    float py = offsetY + y / (float)height * maskScale;
                    float plateauMask = Mathf.Pow(Mathf.PerlinNoise(px, py), 2f);

                    float neighborAvg = (heightMap[x + 1, y] + heightMap[x - 1, y] +
                                         heightMap[x, y + 1] + heightMap[x, y - 1]) / 4f;

                    if (plateauMask > threshold && (neighborAvg > threshold || h > threshold))
                    {
                        float plateauBoost = intensity;
                        float edgeAttenuation = 1f - Mathf.Clamp01((h - threshold) / (dropoff + 0.001f));
                        heightMap[x, y] += plateauBoost * edgeAttenuation;

                        // 悬崖陡峭处理
                        float cliffDrop = Mathf.Clamp01((h - neighborAvg) * 8f);
                        heightMap[x, y] += cliffDrop * dropoff * 2f;

                        if (_debugPreTexture)
                            SetPixel(preHeight, x, y, (heightMap[x, y] - threshold) / (intensity + 0.001f), 1);
                    }
                }

                if (y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }
        }

        /// <summary>
        /// 水力侵蚀：模拟雨水冲刷，高处侵蚀低处沉积
        /// </summary>
        IEnumerator ApplyHydraulicErosion(int iterations, float startTime)
        {
            float[,] sediment = new float[height, width];
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int y = 2; y < height - 2; y++)
                {
                    for (int x = 2; x < width - 2; x++)
                    {
                        float h = heightMap[x, y];

                        // 找最低邻居（展开 8 方向，省去循环开销）
                        float minNeighbor = h;
                        int minX = x, minY = y;
                        float nh;
                        nh = heightMap[x - 1, y - 1]; if (nh < minNeighbor) { minNeighbor = nh; minX = x - 1; minY = y - 1; }
                        nh = heightMap[x, y - 1];     if (nh < minNeighbor) { minNeighbor = nh; minX = x; minY = y - 1; }
                        nh = heightMap[x + 1, y - 1]; if (nh < minNeighbor) { minNeighbor = nh; minX = x + 1; minY = y - 1; }
                        nh = heightMap[x - 1, y];     if (nh < minNeighbor) { minNeighbor = nh; minX = x - 1; minY = y; }
                        nh = heightMap[x + 1, y];     if (nh < minNeighbor) { minNeighbor = nh; minX = x + 1; minY = y; }
                        nh = heightMap[x - 1, y + 1]; if (nh < minNeighbor) { minNeighbor = nh; minX = x - 1; minY = y + 1; }
                        nh = heightMap[x, y + 1];     if (nh < minNeighbor) { minNeighbor = nh; minX = x; minY = y + 1; }
                        nh = heightMap[x + 1, y + 1]; if (nh < minNeighbor) { minNeighbor = nh; minX = x + 1; minY = y + 1; }

                        // 高处侵蚀、低处沉积
                        float diff = h - minNeighbor;
                        if (diff > 0)
                        {
                            float erodeAmount = diff * 0.1f;
                            heightMap[x, y] -= erodeAmount;
                            sediment[minY, minX] += erodeAmount;
                        }
                    }

                    if (y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                    {
                        yield return null;
                        startTime = Time.realtimeSinceStartup;
                    }
                }

                // 沉积物沉降
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        heightMap[x, y] = Mathf.Clamp01(heightMap[x, y] + sediment[x, y] * 0.5f);
                        sediment[x, y] *= 0.5f;
                    }
                }
            }
        }

        /// <summary>
        /// 温和平滑：多次邻域平均，适合丘陵类地形
        /// </summary>
        IEnumerator ApplyGentleSmoothing(int iterations, float startTime)
        {
            float[,] buffer = new float[height, width];
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        buffer[x, y] = (heightMap[x, y] * 0.4f +
                                        heightMap[x + 1, y] * 0.15f +
                                        heightMap[x - 1, y] * 0.15f +
                                        heightMap[x, y + 1] * 0.15f +
                                        heightMap[x, y - 1] * 0.15f);
                    }

                    if (y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                    {
                        yield return null;
                        startTime = Time.realtimeSinceStartup;
                    }
                }

                for (int y = 1; y < height - 1; y++)
                    for (int x = 1; x < width - 1; x++)
                        heightMap[x, y] = buffer[x, y];
            }
        }

        /// <summary>
        /// 盆地凹陷：中心到边缘径向抬升
        /// </summary>
        IEnumerator ApplyBasinDepression(int erosionIterations, float startTime)
        {
            float centerX = width * 0.5f;
            float centerY = height * 0.5f;
            float maxDist = Mathf.Sqrt(centerX * centerX + centerY * centerY);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 径向距离（归一化）
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;

                    // 边缘抬升、中心保持低洼
                    float basinFactor = Mathf.Pow(dist, 1.5f) * 0.5f;
                    heightMap[x, y] = Mathf.Clamp01(heightMap[x, y] * 0.9f + basinFactor);
                }

                if (y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }

            // 盆地也可以叠加一次水力侵蚀让过渡更自然
            yield return ApplyHydraulicErosion(erosionIterations, startTime);
        }

        /// <summary>
        /// 渐进式拉伸高度峰值：高于 threshold 的区域按 (h - threshold) 比例递增拉升
        /// 越高的点拉伸越多，低洼区域不受影响
        /// </summary>
        IEnumerator StretchHeightPeaks(float threshold, float boostIntensity)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = heightMap[x, y];
                    if (h > threshold)
                        heightMap[x, y] = Mathf.Clamp01(h * (1f + (h - threshold) * boostIntensity));
                }
                if (y % 8 == 0)
                    yield return null;
            }
        }

        /// <summary>
        /// 压低低洼地形：低于 threshold 的高度向 0 压缩，压缩幅度由 strength 控制（0~1）
        /// </summary>
        IEnumerator DepressLowlands(float threshold, float strength, float startTime)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = heightMap[x, y];
                    if (h < threshold)
                    {
                        // 低于阈值的部分按 strength 比例压向 0
                        float ratio = (threshold - h) / threshold; // 0~1，越接近0越大
                        heightMap[x, y] = Mathf.Lerp(h, 0f, ratio * strength);
                    }
                }
                if (y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                    yield return null;
            }
        }

        /// <summary>
        /// 将高度图线性拉伸，使最高的点达到 targetMax，低于 threshold 的区域不变
        /// 允许高度 >1，后续在提交高度图时统一 Clamp01
        /// </summary>
        IEnumerator StretchToMax(float threshold, float targetMax)
        {
            float currentMax = 0f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    currentMax = Mathf.Max(currentMax, heightMap[x, y]);

            if (currentMax <= threshold) yield break;

            float scale = (targetMax - threshold) / (currentMax - threshold);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = heightMap[x, y];
                    if (h > threshold)
                        heightMap[x, y] = threshold + (h - threshold) * scale;
                    // 不做 Clamp01，让数据可以 >1
                }
                if (y % 8 == 0)
                    yield return null;
            }
        }

        /// <summary>
        /// 基准高度：将 [actualMin, actualMax] 线性映射到 [baseHeight, 1]，保证最低点 = baseHeight
        /// </summary>
        IEnumerator ApplyBaselineHeight(float baseHeight)
        {
            float actualMin = 1f, actualMax = 0f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float h = heightMap[x, y];
                    if (h < actualMin) actualMin = h;
                    if (h > actualMax) actualMax = h;
                }

            float range = actualMax - actualMin;
            if (range <= 0f) yield break;
            float scale = (1f - baseHeight) / range;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    heightMap[x, y] = baseHeight + (heightMap[x, y] - actualMin) * scale;
                if (y % 8 == 0)
                    yield return null;
            }
        }

        /// <summary>
        /// 设置材质
        /// </summary>
        IEnumerator ApplyTextures()
        {
            float startTime = Time.realtimeSinceStartup;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 快速梯度替代 GetSteepness
                    int x0 = Mathf.Max(0, x - 1), x1 = Mathf.Min(width - 1, x + 1);
                    int y0 = Mathf.Max(0, y - 1), y1 = Mathf.Min(height - 1, y + 1);
                    float gx = heightMap[x1, y] - heightMap[x0, y];
                    float gz = heightMap[x, y1] - heightMap[x, y0];
                    float grad = Mathf.Sqrt(gx * gx + gz * gz);
                    float steepness = grad < 0.1f ? grad * 10f : 1f;
                    float nowheight = heightMap[y, x];

                    // 岩石层（陡坡）
                    textureMap[y, x, 4] = Mathf.Clamp01(steepness * 2f - 0.5f);

                    // 沙地层（中等高度）
                    textureMap[y, x, 1] = Mathf.Clamp01(nowheight * 2f) * (1 - textureMap[y, x, 4]);

                    // 侵蚀层（低洼区域）
                    textureMap[y, x, 2] = Mathf.Clamp01((1 - nowheight) * 2f) * (1 - textureMap[y, x, 4]);

                    textureMap[y, x, 3] = 0;
                    textureMap[y, x, 0] = 0;

                    if (_debugPreTexture)
                    {
                        SetPixel(preTexture, y, x, textureMap[y, x, 0], 0);
                        SetPixel(preTexture, y, x, textureMap[y, x, 1], 1);
                        SetPixel(preTexture, y, x, textureMap[y, x, 2], 2);
                        SetPixel(preTexture, y, x, steepness, 3);
                    }
                }

                if (y % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
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
                            chunk[by, bx] = Mathf.Clamp01(heightMap[y + by, x + bx]);
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

        IEnumerator SpawnTrees(TerrainPresetData preset)
        {
            float effectiveTreeProb = _overridePreset ? treeProbability : preset.treeProbability;
            int totalCells = width * height;
            int targetCount = Mathf.FloorToInt(effectiveTreeProb * totalCells / 10000f);

            int maxAttempts = targetCount * 5;
            int attempts = 0;

            for (int i = 0; i < targetCount && attempts < maxAttempts; attempts++)
            {
                int tx = Random.Range(1, width - 1);
                int tz = Random.Range(1, height - 1);

                float h = heightMap[tx, tz];
                float slope = GetSteepness(tx, tz);

                // 坡度约束
                if (slope < preset.treeMinSlope || slope > preset.treeMaxSlope) continue;
                // 高度约束
                if (h < preset.treeMinHeight || h > preset.treeMaxHeight) continue;

                TreeInstance tree = new TreeInstance();
                tree.position = new Vector3(tx / (float)width, h, tz / (float)height);
                tree.widthScale = Random.Range(0.7f, 1.5f);
                tree.heightScale = Random.Range(0.7f, 1.5f);
                tree.prototypeIndex = Random.Range(preset.treePrototypeRange.x,
                    Mathf.Min(preset.treePrototypeRange.y + 1, terrain.terrainData.treePrototypes.Length));

                trees.Add(tree);
                i++;
            }
            yield return null;
        }

        /// <summary>
        /// 生成草（细节植被），基于坡度和高度约束，使用 Terrain Detail 系统
        /// </summary>
        /// <summary>
        /// 生成草和花（细节植被），支持多种原型混合：
        ///   草原型索引：0-2, 9-11
        ///   花原型索引：3-8
        /// </summary>
        IEnumerator SpawnDetails(TerrainPresetData preset)
        {
            int detailRes = terrain.terrainData.detailResolution;
            int protoCount = terrain.terrainData.detailPrototypes.Length;
            if (protoCount == 0) yield break;

            // 索引分组
            int[] validGrass = { 0, 1, 2, 9, 10, 11 };
            int[] validFlowers = { 3, 4, 5, 6, 7, 8 };
            validGrass = Array.FindAll(validGrass, i => i < protoCount);
            validFlowers = Array.FindAll(validFlowers, i => i < protoCount);
            if (validGrass.Length == 0 && validFlowers.Length == 0) yield break;

            // 为每个原型创建独立细节地图
            var layers = new List<(int, int[,])>();
            foreach (int i in validGrass)
                layers.Add((i, new int[detailRes, detailRes]));
            foreach (int i in validFlowers)
                layers.Add((i, new int[detailRes, detailRes]));

            float mapToDetail = width / (float)detailRes;
            float grassScale = preset.detailDensity * 60f;
            float flowerScale = preset.detailFlowerDensity * 60f;
            bool hasGrass = validGrass.Length > 0 && preset.detailDensity > 0f;
            bool hasFlowers = validFlowers.Length > 0 && preset.detailFlowerDensity > 0f;

            float startTime = Time.realtimeSinceStartup;

            for (int dy = 0; dy < detailRes; dy++)
            {
                for (int dx = 0; dx < detailRes; dx++)
                {
                    int hx = Mathf.Clamp(Mathf.RoundToInt(dx * mapToDetail), 1, width - 2);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(dy * mapToDetail), 1, height - 2);

                    float h = heightMap[hx, hz];
                    float slope = GetSteepness(hx, hz);

                    bool inRange = h >= preset.detailMinHeight && h <= preset.detailMaxHeight
                        && slope >= preset.detailMinSlope && slope <= preset.detailMaxSlope;

                    if (!inRange) continue;

                    if (hasGrass && Random.value < preset.detailDensity)
                    {
                        int idx = validGrass[Random.Range(0, validGrass.Length)];
                        int val = Mathf.CeilToInt(grassScale * Random.Range(0.3f, 1.0f));
                        int[,] map = layers.Find(l => l.Item1 == idx).Item2;
                        map[dx, dy] = Mathf.Clamp(val, 1, 16);
                    }

                    if (hasFlowers && Random.value < preset.detailFlowerDensity)
                    {
                        int idx = validFlowers[Random.Range(0, validFlowers.Length)];
                        int val = Mathf.CeilToInt(flowerScale * Random.Range(0.3f, 1.0f));
                        int[,] map = layers.Find(l => l.Item1 == idx).Item2;
                        map[dx, dy] = Mathf.Clamp(val, 1, 16);
                    }
                }

                if (dy % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }

            foreach (var (protoIdx, map) in layers)
            {
                terrain.terrainData.SetDetailLayer(0, 0, protoIdx, map);
                yield return null;
            }
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
            StartCoroutine(ApplyFractalNoiseToTerrain(_terrainType));
        }

        /// <summary>
        /// 使用指定地形类型生成（可在 Inspector 中通过 TerrainType 下拉选择，或代码调用）
        /// </summary>
        public void GenerateTerrainWithType(TerrainType terrainType)
        {
            _terrainType = terrainType;
            StartCoroutine(ApplyFractalNoiseToTerrain(terrainType));
        }

        [ContextMenu("重置地形")]
        public void ResetTerrain()
        {
            Debug.Log("重置地形");
            if (terrain != null)
            {
                TerrainData terrainData = terrain.terrainData;
                int w = terrainData.heightmapResolution;
                int h = terrainData.heightmapResolution;
                terrainData.SetHeights(0, 0, new float[w, h]);

                var surface = GetComponent<NavMeshSurface>();
                if (surface != null)
                {
                    if (surface.navMeshData == null)
                        surface.BuildNavMesh(); // 首次构建填充 navMeshData，避免 UpdateNavMesh 因 data 为 null 抛异常
                    if (surface.navMeshData != null)
                        surface.UpdateNavMesh(surface.navMeshData);
                }
            }
        }
        #endregion

    }
}