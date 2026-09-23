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
    /// 一类地形植被（石块 / 树）的生成参数。
    /// <para>两者都是地形树实例（TerrainData.treeInstances），只是原型索引区间不同，</para>
    /// <para>所以各配一份概率与约束，分别逐格概率生成。</para>
    /// <para>⚠ 这里<b>不再配原型索引区间</b>：区间由 <c>MapData_SO</c> 的
    /// <c>stonePrototypes</c>/<c>treePrototypes</c> 数组长度推导（石块在前、树紧随其后），
    /// 见 <see cref="GenerateNoiseTerrain.ApplyMapPrototypes"/>。</para>
    /// <para>⚠ <c>minSlope</c>/<c>maxSlope</c> 由 <see cref="GenerateNoiseTerrain.GetSteepness"/> 判定，
    /// 而那个函数用的是历史硬编码的近似换算（不是真实米制坡度），所以这两个角度只是"相对刻度"。
    /// 覆盖石（<see cref="RockCoverSpawnData"/>）走的是真实米制坡度的另一条路径，两者不要混着调。</para>
    /// </summary>
    [Serializable]
    public struct VegetationSpawnData
    {
        [InspectorName("生成概率（每格概率，0.001≈每1000㎡一处）")]
        public float probability;
        [InspectorName("最小坡度")]
        public float minSlope;
        [InspectorName("最大坡度")]
        public float maxSlope;
        [InspectorName("最小高度")]
        public float minHeight;
        [InspectorName("最大高度")]
        public float maxHeight;
    }

    /// <summary>
    /// "地形覆盖物"（巨型悬崖 / 巨石）的生成参数。
    /// <para>与 <see cref="VegetationSpawnData"/> 的"逐格概率"不同：这类物体体积巨大，
    /// 逐格概率要么叠在一起要么抽不到，而且没有互相排斥的概念，</para>
    /// <para>所以这里用 <c>count</c>（目标数量）+ 占地圆互斥来做拒绝采样。</para>
    /// <para>预制体与占地半径在 <see cref="GenerateNoiseTerrain"/> 的列表上配，本结构只管"生成规则"。</para>
    /// </summary>
    [Serializable]
    public struct RockCoverSpawnData
    {
        /// <summary>目标数量。0 = 该地形类型不放覆盖物</summary>
        [InspectorName("目标数量")]
        public int count;

        /// <summary>互斥间距系数：两块的最近距离 = (半径1+半径2) × 该系数，1 = 恰好相切</summary>
        [InspectorName("互斥间距系数（半径和的倍数，1=相切）")]
        public float minSpacingScale;

        /// <summary>
        /// 落点允许的最小坡度（度）。
        /// <para>按"占地圆内的地面倾角"判定（占地圆一圈上最高/最低点的高差 ÷ 直径，真实米制坡度），
        /// 不是单个格子的梯度值。</para>
        /// </summary>
        [InspectorName("最小坡度（占地圆倾角，度）")]
        public float minSlope;

        /// <summary>落点允许的最大坡度（度），同样按占地圆内的地面倾角判定</summary>
        [InspectorName("最大坡度（占地圆倾角，度）")]
        public float maxSlope;

        /// <summary>落点允许的最小高度（归一化 0~1）</summary>
        [InspectorName("最小高度")]
        public float minHeight;

        /// <summary>落点允许的最大高度（归一化 0~1）</summary>
        [InspectorName("最大高度")]
        public float maxHeight;
    }

    /// <summary>
    /// 一类"地形覆盖物"（巨型悬崖 / 巨石）的配置。
    /// <para>巨型石块要能走上去、要参与寻路、要有碰撞，地形树做不到（位置只能概率撒、朝向/碰撞受限），</para>
    /// <para>所以用预制体实例化，由 <see cref="GenerateNoiseTerrain"/> 在地形生成时放置，
    /// 并把占地圆作为树与草（细节）的排除区。</para>
    /// </summary>
    [Singleline]
    [Serializable]
    public class RockCoverEntry
    {
        /// <summary>要放置的预制体</summary>
        [InspectorName("预制体")]
        public GameObject prefab;

        /// <summary>被随机选中的权重，越大越容易选中（&lt;1 按 1 处理）</summary>
        [InspectorName("权重（越大越容易被选中）")]
        public int weight = 1;

        /// <summary>占地半径（米）：用于互相排斥、以及擦掉范围内的树与草</summary>
        [InspectorName("占地半径（米）")]
        public float footprintRadius = 10f;

        /// <summary>垂直下沉量（米）：贴着地形高度再往下沉，避免在坡地上一边悬空</summary>
        [InspectorName("下沉量（米）")]
        public float sinkDepth = 1f;
    }

    /// <summary>
    /// 地形预设参数，每种 TerrainType 对应一组完整的地形生成参数
    /// </summary>
    [Serializable]
    public struct TerrainPresetData
    {
        /// <summary>
        /// 基础缩放。控制基础噪声的采样频率，值越大基础地形起伏越密集。
        /// </summary>
        [InspectorName("基础缩放")]
        public float baseScale;

        /// <summary>
        /// 基础振幅。控制基础噪声对地形高度的贡献强度。
        /// </summary>
        [InspectorName("基础振幅")]
        public float baseAmplitude;

        /// <summary>
        /// 倍频数。分形噪声的叠加层数，层数越多细节越丰富。
        /// </summary>
        [InspectorName("倍频数")]
        public int octaves;

        /// <summary>
        /// 间隙度。每个倍频层频率的放大倍数，决定细节的密集程度。
        /// </summary>
        [InspectorName("间隙度")]
        public float lacunarity;

        /// <summary>
        /// 持续度。每个倍频层振幅的衰减比例，决定细节的强弱。
        /// </summary>
        [InspectorName("持续度")]
        public float persistence;

        /// <summary>
        /// 细节缩放。控制细节噪声的采样频率，用于叠加更精细的地形细节。
        /// </summary>
        [InspectorName("细节缩放")]
        public float detailScale;

        /// <summary>
        /// 细节振幅。控制细节噪声对地形高度的贡献强度。
        /// </summary>
        [InspectorName("细节振幅")]
        public float detailAmplitude;

        // ---- 高度重塑 ----

        /// <summary>
        /// 高度幂次曲线。对归一化高度应用幂函数重塑，用于调整地形高低分布的对比度。
        /// </summary>
        [InspectorName("高度幂次曲线")]
        public float heightPower;

        // ---- 后处理参数 ----

        /// <summary>
        /// 高原抬升强度。控制高原区域相对于基础地形的抬升幅度。
        /// </summary>
        [InspectorName("高原抬升强度")]
        public float plateauIntensity;

        /// <summary>
        /// 高原阈值。判定高原区域的高度/遮罩阈值，超过该值视为高原。
        /// </summary>
        [InspectorName("高原阈值")]
        public float plateauThreshold;

        /// <summary>
        /// 高原噪声缩放。控制高原遮罩噪声的采样频率，影响高原边缘的形状。
        /// </summary>
        [InspectorName("高原噪声缩放")]
        public float plateauMaskScale;

        /// <summary>
        /// 边缘衰减。控制地形边缘高度向四周衰减的强度，用于避免边缘突兀。
        /// </summary>
        [InspectorName("边缘衰减")]
        public float edgeDropoff;

        /// <summary>
        /// 侵蚀迭代次数。模拟水力/热力侵蚀的迭代次数，次数越多侵蚀效果越明显。
        /// </summary>
        [InspectorName("侵蚀迭代次数")]
        public int erosionIterations;

        // ---- 植被（石块与树分开配：两者都是地形树实例，靠原型索引区间区分） ----

        /// <summary>
        /// 石块生成配置。包括石块的密度、坡度/高度范围以及原型索引区间等参数。
        /// </summary>
        [InspectorName("石块生成")]
        public VegetationSpawnData rockSpawn;

        /// <summary>
        /// 树生成配置。包括树的密度、坡度/高度范围以及原型索引区间等参数。
        /// </summary>
        [InspectorName("树生成")]
        public VegetationSpawnData treeSpawn;

        // ---- 地形覆盖物（巨型悬崖 / 巨石：用预制体实例化，不是地形树） ----

        /// <summary>
        /// 地形覆盖物生成配置。只放"数量 / 互斥间距 / 坡度 / 高度"，
        /// 具体用哪些预制体、多大占地半径由 <see cref="GenerateNoiseTerrain"/> 上的列表决定。
        /// </summary>
        [InspectorName("地形覆盖物生成")]
        public RockCoverSpawnData rockCover;

        // ---- 细节植被 ----

        /// <summary>
        /// 草密度。控制单位面积内草细节植被的生成数量。
        /// </summary>
        [InspectorName("草密度")]
        public float detailDensity;

        /// <summary>
        /// 草最小坡度。允许生成草细节植被的最小地形坡度。
        /// </summary>
        [InspectorName("草最小坡度")]
        public float detailMinSlope;

        /// <summary>
        /// 草最大坡度。允许生成草细节植被的最大地形坡度。
        /// </summary>
        [InspectorName("草最大坡度")]
        public float detailMaxSlope;

        /// <summary>
        /// 草最小高度。允许生成草细节植被的最小地形高度。
        /// </summary>
        [InspectorName("草最小高度")]
        public float detailMinHeight;

        /// <summary>
        /// 草最大高度。允许生成草细节植被的最大地形高度。
        /// </summary>
        [InspectorName("草最大高度")]
        public float detailMaxHeight;
    }


    /// <summary>
    /// 生成地形。地形高度/材质等来自 TerrainData 与内置预设；树/石/草的原型由调用方（BattleManager）
    /// 从 <c>MapData_SO</c> 传入，本类不直接依赖 SO 类型（跨 asmdef 引用不到）。
    /// </summary>

    public class GenerateNoiseTerrain : MonoBehaviour
    {
        /// <summary>覆盖石实例容器的名字（换局重生成时按名字复用/清理）</summary>
        private const string RockCoverRootName = "RockCovers";

        /// <summary>峰顶判定的高度容差（米）：落点比四周一圈高出这么多才算"山峰顶"（平地噪声抖动不算）</summary>
        private const float RockCoverPeakTolerance = 0.5f;

        /// <summary>峰顶探测的外圈半径倍数：除占地圆外再看 2 倍半径的一圈，能抓到顶面很宽的山包</summary>
        private const float RockCoverPeakProbeScale = 2f;

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
        [InspectorName("覆盖用石块概率（每格概率，<0=用预设值）")]
        [SerializeField] private float _rockProbability = -1f;
        [InspectorName("覆盖用树概率（每格概率，<0=用预设值）")]
        [SerializeField] private float treeProbability = -1f;
        [InspectorName("可被摧毁的索引（0基，石块在前；留空=全部可摧毁）")]
        [SerializeField] private List<int> _destructibleTreePrototypes = new();

        [Foldout("地形覆盖石（巨型悬崖，预制体不是地形树）", true)]
        [InspectorName("覆盖石列表（预制体 / 权重 / 占地半径 / 下沉量）")]
        [SerializeField] private List<RockCoverEntry> _rockCovers = new();
        [InspectorName("覆盖用数量（<0=用预设）")]
        [SerializeField] private int _rockCoverCount = -1;
        [InspectorName("覆盖用互斥间距系数（<0=用预设）")]
        [SerializeField] private float _rockCoverMinSpacingScale = -1f;
        [InspectorName("距地图边缘最小距离（米，避开空气墙）")]
        [SerializeField] private float _rockCoverEdgeMargin = 20f;
        [InspectorName("每个目标的尝试次数上限")]
        [SerializeField] private int _rockCoverAttemptsPerCover = 40;

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

        // ---- 地形覆盖石 ----

        /// <summary>覆盖石实例的容器（换局重生成时整批清掉）</summary>
        private Transform _rockCoverRoot;

        /// <summary>本局地图的石头生成倍率（由 MapData_SO 传入，乘在树密度上）</summary>
        private float _stoneMultiplier = 1f;

        /// <summary>本局地图的树生成倍率（由 MapData_SO 传入，乘在树密度上）</summary>
        private float _treeMultiplier = 1f;

        /// <summary>本局地图的悬崖（地形覆盖石）生成倍率（由 MapData_SO 传入，乘在数量上）</summary>
        private float _rockCoverMultiplier = 1f;


        /// <summary>本局地图的细节生成倍率（由 MapData_SO 传入，乘在数量上）</summary>
        private float _detailsMultiplier = 1f;

        /// <summary>调用方本次是否提供了细节（草）原型数组（null = 没提供，沿用地形资产里的原型与草）</summary>
        private bool _detailProvided;

        /// <summary>本图是否配置了细节（草）原型（由 <see cref="ApplyMapPrototypes"/> 按地图数据设置）；false = 本图不撒草</summary>
        private bool _detailConfigured;
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

                        rockSpawn = new VegetationSpawnData
                        {
                            probability = 0.0012f,
                            minSlope = 0f, maxSlope = 45f, minHeight = 0.05f, maxHeight = 0.95f
                        },
                        treeSpawn = new VegetationSpawnData
                        {
                            probability = 0,
                            minSlope = 0f, maxSlope = 30f, minHeight = 0.1f, maxHeight = 0.7f
                        },
                        rockCover = new RockCoverSpawnData
                        {
                            count = 12, minSpacingScale = 1.2f,
                            minSlope = 0f, maxSlope = 60f, minHeight = 0.05f, maxHeight = 0.95f
                        },
                        detailDensity = 0.02f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 45f,
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

                        rockSpawn = new VegetationSpawnData
                        {
                            probability = 0.0015f,
                            minSlope = 0f, maxSlope = 50f, minHeight = 0.05f, maxHeight = 0.95f
                        },
                        treeSpawn = new VegetationSpawnData
                        {
                            probability = 0.0008f,
                            minSlope = 0f, maxSlope = 40f, minHeight = 0.3f, maxHeight = 0.9f
                        },
                        rockCover = new RockCoverSpawnData
                        {
                            count = 18, minSpacingScale = 1.1f,
                            minSlope = 5f, maxSlope = 60f, minHeight = 0.25f, maxHeight = 0.95f
                        },
                        detailDensity = 0.08f,
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

                        rockSpawn = new VegetationSpawnData
                        {
                            probability = 0.0008f,
                            minSlope = 0f, maxSlope = 55f, minHeight = 0.05f, maxHeight = 0.95f
                        },
                        treeSpawn = new VegetationSpawnData
                        {
                            probability = 0.003f,
                            minSlope = 0f, maxSlope = 50f, minHeight = 0.1f, maxHeight = 0.95f
                        },
                        rockCover = new RockCoverSpawnData
                        {
                            count = 24, minSpacingScale = 1.15f,
                            minSlope = 0f, maxSlope = 55f, minHeight = 0.05f, maxHeight = 0.9f
                        },
                        detailDensity = 0.2f,
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

                        rockSpawn = new VegetationSpawnData
                        {
                            probability = 0.0025f,
                            minSlope = 0f, maxSlope = 55f, minHeight = 0.05f, maxHeight = 0.95f
                        },
                        treeSpawn = new VegetationSpawnData
                        {
                            probability = 0.0015f,
                            minSlope = 0f, maxSlope = 35f, minHeight = 0.1f, maxHeight = 0.85f
                        },
                        rockCover = new RockCoverSpawnData
                        {
                            count = 24, minSpacingScale = 1.2f,
                            minSlope = 0f, maxSlope = 60f, minHeight = 0.05f, maxHeight = 0.9f
                        },
                        detailDensity = 0.1f,
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

                        rockSpawn = new VegetationSpawnData
                        {
                            probability = 0.002f,
                            minSlope = 0f, maxSlope = 50f, minHeight = 0.05f, maxHeight = 0.9f
                        },
                        treeSpawn = new VegetationSpawnData
                        {
                            probability = 0.0005f,
                            minSlope = 0f, maxSlope = 40f, minHeight = 0.05f, maxHeight = 0.8f
                        },
                        rockCover = new RockCoverSpawnData
                        {
                            count = 12, minSpacingScale = 1.2f,
                            minSlope = 0f, maxSlope = 55f, minHeight = 0.05f, maxHeight = 0.9f
                        },
                        detailDensity = 0.1f,
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

                        rockSpawn = new VegetationSpawnData
                        {
                            probability = 0.0012f,
                            minSlope = 0f, maxSlope = 35f, minHeight = 0.05f, maxHeight = 0.9f
                        },
                        treeSpawn = new VegetationSpawnData
                        {
                            probability = 0.0006f,
                            minSlope = 0f, maxSlope = 10f, minHeight = 0.1f, maxHeight = 0.6f
                        },
                        rockCover = new RockCoverSpawnData
                        {
                            count = 12, minSpacingScale = 1.3f,
                            minSlope = 0f, maxSlope = 35f, minHeight = 0.05f, maxHeight = 0.9f
                        },
                        detailDensity = 0.15f,
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

                        rockSpawn = new VegetationSpawnData
                        {
                            probability = 0.0005f,
                            minSlope = 0f, maxSlope = 60f, minHeight = 0f, maxHeight = 0.95f
                        },
                        treeSpawn = new VegetationSpawnData
                        {
                            probability = 0.0003f,
                            minSlope = 0f, maxSlope = 25f, minHeight = 0f, maxHeight = 0.4f
                        },
                        rockCover = new RockCoverSpawnData
                        {
                            count = 30, minSpacingScale = 1.05f,
                            minSlope = 0f, maxSlope = 60f, minHeight = 0f, maxHeight = 0.95f
                        },
                        detailDensity = 0.03f,
                        detailMinSlope = 0f,
                        detailMaxSlope = 40f,
                        detailMinHeight = 0f,
                        detailMaxHeight = 0.85f
                    };

                default:
                    return GetTerrainPreset(TerrainType.Desert);
            }
        }

        /// <summary>
        /// 用地图数据（<c>MapData_SO</c>）提供的原型数组重建地形原型，并推导出石块/树的生成区间。
        /// <para>地形树原型 = 石块原型 + 树原型（石块在前，所以石块索引恒为 0 起的连续段）；</para>
        /// <para>细节原型 = 草原型（不再区分花）。</para>
        /// <para><b>区间不再由预设写死</b>：石块 = <c>[0, stoneCount-1]</c>，树 = <c>[stoneCount, stoneCount+treeCount-1]</c>；
        /// 某一类数量为 0 时区间为空（<c>y &lt; x</c>，SpawnVegetation 直接跳过）。</para>
        /// <para>推导结果同时写入 <see cref="TerrainClearer.TreePrototypeRange"/>/<see cref="TerrainClearer.RockPrototypeRange"/>，
        /// 保证"清理地表物"用的区间与生成时一致（否则会清错原型）。</para>
        /// <para>数组为空 = 不改动地形资产里已有的原型，同时该区间为空 = 本图不生成该类植被。</para>
        /// <para>⚠ 本类在 <c>08_Map</c> asmdef 内，引用不到 <c>MapData_SO</c>（Assembly-CSharp），</para>
        /// <para>所以这里只接 <see cref="GameObject"/> 数组，由调用方（BattleManager）从 SO 上取。</para>
        /// </summary>
        /// <param name="stonePrototypes">石块原型（<c>MapData_SO.stonePrototypes</c>）</param>
        /// <param name="treePrototypes">树原型（<c>MapData_SO.treePrototypes</c>），紧随石块之后</param>
        /// <param name="detailPrototypes">细节（草）原型（<c>MapData_SO.detailPrototypes</c>）</param>
        /// <param name="stoneRange">输出：石块原型索引区间（含头含尾），空为 (0,-1)</param>
        /// <param name="treeRange">输出：树原型索引区间（含头含尾），空为 (n,n-1)</param>
        private void ApplyMapPrototypes(GameObject[] stonePrototypes, GameObject[] treePrototypes,
            GameObject[] detailPrototypes, out Vector2Int stoneRange, out Vector2Int treeRange)
        {
            TerrainData terrainData = terrain.terrainData;
            int stoneCount = stonePrototypes?.Length ?? 0;
            int treeCount = treePrototypes?.Length ?? 0;

            // 区间由数组长度推导（石块在前、树紧随其后）；数量为 0 时 y < x = 空区间
            stoneRange = new Vector2Int(0, stoneCount - 1);
            treeRange = new Vector2Int(stoneCount, stoneCount + treeCount - 1);

            if (stoneCount + treeCount > 0)
            {
                // 沿用地形资产同索引原型的其余配置（bendFactor 等），只替换 prefab；
                // 数量超出的部分用结构体默认值。
                TreePrototype[] existing = terrainData.treePrototypes;
                TreePrototype[] protos = new TreePrototype[stoneCount + treeCount];
                for (int i = 0; i < protos.Length; i++)
                {
                    GameObject prefab = i < stoneCount ? stonePrototypes[i] : treePrototypes[i - stoneCount];
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[原型] 第 {i} 个地形树原型为空，已跳过（该索引不会生成植被）");
                        continue;
                    }
                    protos[i] = i < existing.Length ? existing[i] : new TreePrototype();
                    protos[i].prefab = prefab;
                }

                // ⚠ 换原型数组之前必须先清掉已烘焙的树实例：地形资产里可能已有一批引用旧原型索引的实例，
                // 原型数组一变短，Unity 就会把越界的实例逐条剔除并在控制台刷一堆
                // "Tree removed: invalid prototype N"（编辑器下还会弄脏地形资产）。
                // 本方法之后马上会用 SetTreeInstances 重写整表，所以这里清空不会丢东西。
                if (terrainData.treeInstances.Length > 0)
                    terrainData.SetTreeInstances(Array.Empty<TreeInstance>(), false);

                terrainData.treePrototypes = protos;

                // 清理侧（TerrainClearer）用的是同一套区间，必须同步，否则"清石块"会清到树上
                TerrainClearer.RockPrototypeRange = stoneRange;
                TerrainClearer.TreePrototypeRange = treeRange;
                Debug.Log($"[原型] 地形树原型已按地图数据重建：石块 {stoneCount} 个（区间 {stoneRange}），"
                    + $"树 {treeCount} 个（区间 {treeRange}）");
            }
            else
            {
                Debug.LogWarning("[原型] 地图数据未配置 stonePrototypes/treePrototypes，本图不会生成石块与树");
            }

            // null = 调用方没提供（如编辑器里 [ContextMenu] 手跑、或地图数据没有 mapCfg）→ 完全不动细节；
            // 长度为 0 = 调用方明确表示"本图没有草" → 清空细节层。两者语义不同，不能混。
            bool detailProvided = detailPrototypes != null;
            int detailCount = detailProvided ? detailPrototypes.Length : 0;
            _detailProvided = detailProvided;
            _detailConfigured = detailCount > 0;
            if (_detailConfigured)
            {
                DetailPrototype[] existing = terrainData.detailPrototypes;
                // ⚠ 细节层数变少时，被裁掉的那几层草数据不会随原型数组一起消失（它们仍留在 TerrainData
                // 里），表现为"换成细节更少的地图后，上一张图的草还在原地长着"。必须在改原型数组之前
                // 显式清零——数组一换短，这些层就再也寻址不到了。
                int existingDetailCount = existing?.Length ?? 0;
                if (existingDetailCount > detailCount)
                    ClearDetailLayers(terrainData, detailCount, existingDetailCount - detailCount);

                DetailPrototype[] protos = new DetailPrototype[detailCount];
                // ⚠ 渲染模式（是否 GPU 实例化）必须在这里写死，不能靠继承资产里同索引原型，原因三条：
                //   1. 资产里的原型本身就是混合模式（实测 MainMap：索引 0~3 = Grass + 不实例化、
                //      4~6 = VertexLit + 实例化）→ 只继承会让"哪一层实例化"取决于资产里的顺序；
                //   2. 索引超出资产原型数量的层只会拿到 new DetailPrototype() 的默认值
                //      （usePrototypeMesh=false / useInstancing=false / renderMode=Grass）；
                //   3. 越是原型数量各图不同的项目（本仓 3~8 个不等），继承越不可控。
                // Unity 2022.3 手册「Grass and other details」：勾上 Use GPU Instancing 后 Render Mode 会失效，
                // 走 Instanced mesh 路径——用 prefab 自带的材质与 shader 渲染、用持久化实例常量缓冲，
                // 实例多时 CPU/GPU 更省（代价：禁用 Healthy/Dry Color 噪声，本项目 ToonLit 不读这两个颜色；
                // 每批 ≤1023 实例、不吃 lightprobe/lightmap）。
                // 尺寸/宽高噪声/颜色等"手感参数"仍沿用资产里同索引原型；getter 返回的是新对象副本，
                // 所以改写这些字段不会污染地形资产。
                List<string> materialsWithoutInstancing = new List<string>();
                for (int i = 0; i < detailCount; i++)
                {
                    if (detailPrototypes[i] == null)
                    {
                        Debug.LogWarning($"[原型] 第 {i} 个细节原型为空，已跳过（该索引不会生成草）");
                        continue;
                    }
                    protos[i] = i < existing.Length ? existing[i] : new DetailPrototype();
                    // 用网格原型：接管贴图模式下的 prototype，并清掉万一残留的 billboard 贴图
                    protos[i].prototype = detailPrototypes[i];
                    protos[i].usePrototypeMesh = true;
                    protos[i].prototypeTexture = null;
                    // VertexLit 只是"没勾实例化时"的兜底取值（勾了实例化后 Unity 会把 Render Mode 置灰忽略）
                    protos[i].renderMode = DetailRenderMode.VertexLit;
                    protos[i].useInstancing = true;
                    // 实例化的前提是材质勾了 Enable GPU Instancing，否则 Unity 会静默退回非实例化
                    CollectMaterialsWithoutInstancing(protos[i].prototype, materialsWithoutInstancing);
                }
                terrainData.detailPrototypes = protos;
                Debug.Log($"[原型] 细节（草）原型已按地图数据重建：共 {detailCount} 个（统一 VertexLit + GPU 实例化）");
                LogDetailPrototypes(protos);
                if (materialsWithoutInstancing.Count > 0)
                {
                    Debug.LogWarning("[原型] 下列细节材质未勾选 Enable GPU Instancing，实例化不会生效"
                        + "（Unity 会退回非实例化渲染）：" + string.Join("、", materialsWithoutInstancing));
                }
            }
            else if (detailProvided)
            {
                // 地图明确给了一个空数组 = 本图不长草：把上一张图留下的草全部清掉（原型数组本身不动，
                // 与树的"空 = 不改动资产原型"约定一致），否则换图后旧草会继续显示在原地。
                Debug.LogWarning("[原型] 地图数据未配置 detailPrototypes（空数组），本图不会生成草");
                ClearDetailLayers(terrainData, 0, terrainData.detailPrototypes?.Length ?? 0);
            }
            else
            {
                Debug.Log("[原型] 本次未提供 detailPrototypes（null），沿用地形资产里的细节原型与草数据");
            }

            if (stoneCount + treeCount > 0 || detailCount > 0)
            {
                // 原型变了必须刷新，否则 Terrain 内部仍用旧原型（树实例的 prototypeIndex 会错位）。
                // 树实例随后会被 SetTreeInstances 整表重写，这里不用担心旧实例。
                terrainData.RefreshPrototypes();
            }
        }

        public IEnumerator SetTextures(Texture[] Texture,Vector2[] sizes)
        {
            // 把地图纹理注入为全局纹理：石头材质（ToonLit_Stone.shader）的 _BaseMap/_BlendingMap
            // 在 shader 里改读这两张图，材质资产本身不被改动，也不影响其它使用 ToonLit_Shared.hlsl 的 shader。
            if (Texture.Length > 0 && Texture[0]) Shader.SetGlobalTexture("_TerrainBaseTex", Texture[1]);
            if (Texture.Length > 1 && Texture[1]) Shader.SetGlobalTexture("_TerrainBlendTex", Texture[2]);

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
        /// <param name="stonePrototypes">石块原型（`MapData_SO.stonePrototypes`），与 <paramref name="treePrototypes"/> 拼成地形树原型，石块在前</param>
        /// <param name="treePrototypes">真树原型（`MapData_SO.treePrototypes`），紧跟在石块之后</param>
        /// <param name="detailPrototypes">细节（草）原型（`MapData_SO.detailPrototypes`）</param>
        /// <param name="treeMultiplier">树密度地图级倍率（1=用预设值，0=本图不长树）</param>
        /// <param name="rockCoverMultiplier">悬崖数量地图级倍率（1=用预设值，0=本图不放悬崖）</param>
        public IEnumerator ApplyFractalNoiseToTerrain(TerrainType terrainType,
            GameObject[] stonePrototypes = null, GameObject[] treePrototypes = null,
            GameObject[] detailPrototypes = null,
            float stoneMultiplier = 1f, float treeMultiplier = 1f, float rockCoverMultiplier = 1f,float detailsMultiplier=1f)
        {
            // 地图级倍率（MapData_SO 传进来）：0 = 本图不长树 / 不放悬崖；负数一律按 0 处理
            _stoneMultiplier = Mathf.Max(0f, stoneMultiplier);
            _treeMultiplier = Mathf.Max(0f, treeMultiplier);
            _rockCoverMultiplier = Mathf.Max(0f, rockCoverMultiplier);
            _detailsMultiplier = Mathf.Max(0f, detailsMultiplier);
            if (terrain == null)
            {
                Debug.LogWarning("未指定Terrain对象");
                yield break;
            }
            _terrainType = terrainType;
            TerrainData terrainData = terrain.terrainData;
            width = terrainData.heightmapResolution;
            height = terrainData.heightmapResolution;
            size = terrain.terrainData.alphamapResolution;
            speceHeight = (int)terrain.terrainData.size.y;

            // 获取预设参数
            TerrainPresetData preset = GetTerrainPreset(terrainType);
            // 用 MapData_SO 的原型数组重建地形原型（必须在下面任何生成之前），
            // 石块/树的原型索引区间由数组长度推导（不再写死在预设里）
            ApplyMapPrototypes(stonePrototypes, treePrototypes, detailPrototypes,
                out Vector2Int rockRange, out Vector2Int treeRange);

            Debug.Log($"使用地形预设: {terrainType} | 噪声层数={preset.octaves}"
                + $" | 石块概率={preset.rockSpawn.probability}(原型{rockRange})"
                + $" | 树概率={preset.treeSpawn.probability}(原型{treeRange})"
                + $" | 覆盖石数量={(_overridePreset && _rockCoverCount >= 0 ? _rockCoverCount : preset.rockCover.count)}"
                + $" | 地图倍率：细节×{_detailsMultiplier}  岩石×{_stoneMultiplier} 树×{_treeMultiplier} 悬崖×{_rockCoverMultiplier}"
                + $" | 原型数：石块 {stonePrototypes?.Length ?? 0} 树 {treePrototypes?.Length ?? 0}"
                + $" 细节 {detailPrototypes?.Length ?? 0}");

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

            // 巨型地形覆盖石（悬崖）：必须先于树与草放置——占地圆会作为它们的排除区，
            // 也必须早于下面的 NavMesh 烘焙——石头被设成地面层后会被同一次烘焙算进寻路网格。
            yield return PlaceRockCovers(preset);

            // 设置植被：石块与树都是地形树实例，靠原型索引区间区分（区间由地图数据的原型数量推导），分别逐格概率生成
            yield return SpawnVegetation(preset.rockSpawn, rockRange, "石块", _overridePreset ? _rockProbability : -1f, TerrainUtils.AreaCircles,_stoneMultiplier);
            // 树吃地图级倍率（MapData_SO.TreeSpawnMultiplier），石块不吃
            yield return SpawnVegetation(preset.treeSpawn, treeRange, "树", _overridePreset ? treeProbability : -1f, TerrainUtils.AreaCircles, _treeMultiplier);
            yield return null;
            terrainData.SetTreeInstances(trees.ToArray(), true);
            // 树实例写入后建立"索引 → 世界坐标"表，供 TreeDestructor 做命中判定与销毁（A 方案：
            // 树本身没有碰撞体，命中判定全靠这张表，销毁只改缩放不改索引）
            TreeDestructor.Rebuild(terrain, _destructibleTreePrototypes);
            // 细节（草）擦除器：与树同批缓存地形引用，供爆炸/附加地形擦草使用
            TerrainDetailEraser.Rebuild(terrain);
            yield return null;

            // ⚠ 树碰撞体不会随 SetTreeInstances 更新（实测：旧位置仍挡住、新位置没碰撞体，Flush 也无效），
            // 必须在这里强制重建一次，否则运行时表现为"能穿树 + 撞到看不见的旧树墙"
            TerrainUtils.RebuildTreeColliders(terrain);

            // 设置草（细节）：被覆盖石压住的格子直接跳过（不用事后擦，省一次 SetDetailLayer）
            yield return SpawnDetails(preset, TerrainUtils.AreaCircles,detailsMultiplier);
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

        /// <summary>
        /// 逐格概率生成一类地形植被（石块 / 树共用同一套算法）。
        /// <para>遍历每个格子独立掷骰，命中后才查高度/坡度约束；概率与地图面积无关</para>
        /// <para>（0.001 ≈ 每 1000㎡ 一处），所以小图不会被取整归零。</para>
        /// </summary>
        /// <param name="cfg">该类植被的参数（概率 / 坡度 / 高度）</param>
        /// <param name="prototypeRange">该类使用的原型索引区间（含头含尾），由地图数据的原型数量推导</param>
        /// <param name="label">日志用名字（石块 / 树）</param>
        /// <param name="rateOverride">≥0 时覆盖配置里的概率（"覆盖预设参数"用），&lt;0 表示不覆盖</param>
        IEnumerator SpawnVegetation(VegetationSpawnData cfg, Vector2Int prototypeRange, string label, float rateOverride,
            IReadOnlyList<TerrainUtils.AreaCircle> coverCircles = null, float densityMultiplier = 1f)
        {
            // 与草一致的逐格概率算法：每个格子独立掷骰，命中且满足坡度/高度约束的格子才生成一处。
            // 地图级倍率乘在最终密度上（覆盖值与预设值都乘），0 = 本图不生成
            float rate = (rateOverride >= 0f ? rateOverride : cfg.probability) * Mathf.Max(0f, densityMultiplier);
            int prototypeCount = terrain.terrainData.treePrototypes.Length;

            if (prototypeCount == 0)
            {
                Debug.LogWarning($"[植被] 地形的 Tree Prototypes 为空，无法生成{label}"
                    + "（草用的是 Detail Prototypes，是另一套列表，所以草是正常的）");
                yield break;
            }
            if (rate <= 0f)
            {
                Debug.Log($"[植被] {label}概率为 0，跳过生成");
                yield break;
            }
            // 区间为空（y < x）= 该类没有原型可用（地图数据里这一组是空的），直接跳过
            if (prototypeRange.y < prototypeRange.x)
            {
                Debug.Log($"[植被] {label}没有可用原型（原型区间 {prototypeRange} 为空），跳过生成");
                yield break;
            }

            int protoMin = Mathf.Clamp(prototypeRange.x, 0, prototypeCount - 1);
            int protoMax = Mathf.Clamp(prototypeRange.y, protoMin, prototypeCount - 1);
            if (protoMin != prototypeRange.x || protoMax != prototypeRange.y)
            {
                Debug.LogWarning($"[植被] {label}原型索引范围 {prototypeRange} 超出地形的 Tree Prototypes 数量"
                    + $"（{prototypeCount}），已收窄为 {protoMin}~{protoMax}");
            }

            int before = trees.Count;
            float startTime = Time.realtimeSinceStartup;

            for (int tz = 1; tz < height - 1; tz++)
            {
                for (int tx = 1; tx < width - 1; tx++)
                {
                    // 先掷骰再查约束：99% 以上的格子在这一行就被跳过，比逐格算坡度省得多
                    if (Random.value >= rate) continue;

                    // 被巨型地形覆盖石（悬崖）压住的位置不再生成，避免树/石从石头里长出来
                    if (IsInsideRockCoverNormalized(coverCircles, tx / (float)width, tz / (float)height)) continue;

                    float h = heightMap[tx, tz];
                    // 高度约束
                    if (h < cfg.minHeight || h > cfg.maxHeight) continue;

                    float slope = GetSteepness(tx, tz);
                    // 坡度约束
                    if (slope < cfg.minSlope || slope > cfg.maxSlope) continue;

                    TreeInstance tree = new TreeInstance();
                    tree.position = new Vector3(tx / (float)width, h, tz / (float)height);
                    tree.widthScale = Random.Range(0.7f, 1.5f);
                    tree.heightScale = Random.Range(0.7f, 1.5f);
                    tree.prototypeIndex = Random.Range(protoMin, protoMax + 1);
                    // TreeInstance.rotation 是 X-Z 平面弧度（0~2π），不设的话所有实例朝向完全一致，
                    // 石块尤其明显（像复制粘贴）。rotation 是只读字段语义上要自己构造结构体时写入。
                    tree.rotation = Random.value * Mathf.PI * 2f;

                    trees.Add(tree);
                }

                if (tz % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }

            float area = terrain.terrainData.size.x * terrain.terrainData.size.z;
            Debug.Log($"[植被] {label} 生成 {trees.Count - before} 处（原型 {protoMin}~{protoMax}，每格概率 {rate}，"
                + $"约每 {Mathf.RoundToInt(1f / rate)}㎡ 一处，地图 {area:F0}㎡）");
        }

        /// <summary>
        /// 生成草（细节植被），基于坡度和高度约束，使用 Terrain Detail 系统。
        /// <para>所有 Detail Prototypes 统一按草处理（不再区分花），逐格概率生成。</para>
        /// <para>⚠ 本方法会把本图所有细节层<b>整层重写</b>（不撒草时提交全零）：细节层数据是存在
        /// TerrainData 资产里的，"只写自己那几层"会让上一张图的草残留在新地图上。</para>
        /// </summary>
        IEnumerator SpawnDetails(TerrainPresetData preset, IReadOnlyList<TerrainUtils.AreaCircle> coverCircles = null,float detailsMultiplier=1f)
        {
            int detailRes = terrain.terrainData.detailResolution;
            int protoCount = terrain.terrainData.detailPrototypes.Length;
            if (protoCount == 0) yield break;

            // 本图是否真的要撒草：地图明确没配细节原型（空数组），或密度/倍率为 0 时不撒。
            // 但"不撒"也必须把各层提交成空，否则上一张图的草会留在原地。
            float density = preset.detailDensity * detailsMultiplier;
            bool spawn = preset.detailDensity > 0f && density > 0f
                && (!_detailProvided || _detailConfigured);

            // 为每个原型创建独立细节地图（列表下标与原型索引一一对应）
            var layers = new List<int[,]>(protoCount);
            for (int i = 0; i < protoCount; i++)
                layers.Add(new int[detailRes, detailRes]);

            if (!spawn)
            {
                Debug.Log($"[细节] 本图不生成草（原型已配置={_detailConfigured}，密度={preset.detailDensity}，"
                    + $"地图倍率={detailsMultiplier}），已把 {protoCount} 个细节层重置为空");
            }
            else
            {
                float mapToDetail = width / (float)detailRes;
                float grassScale = preset.detailDensity * 60f;

                float startTime = Time.realtimeSinceStartup;

                for (int dy = 0; dy < detailRes; dy++)
                {
                    for (int dx = 0; dx < detailRes; dx++)
                    {
                        int hx = Mathf.Clamp(Mathf.RoundToInt(dx * mapToDetail), 1, width - 2);
                        int hz = Mathf.Clamp(Mathf.RoundToInt(dy * mapToDetail), 1, height - 2);

                        // 被巨型地形覆盖石压住的格子不生成草（原地形被挖走/覆盖后草会悬空）
                        if (IsInsideRockCoverNormalized(coverCircles, dx / (float)detailRes, dy / (float)detailRes)) continue;

                        float h = heightMap[hx, hz];
                        float slope = GetSteepness(hx, hz);

                        bool inRange = h >= preset.detailMinHeight && h <= preset.detailMaxHeight
                            && slope >= preset.detailMinSlope && slope <= preset.detailMaxSlope;

                        if (!inRange) continue;

                        if (Random.value < density)
                        {
                            int idx = Random.Range(0, protoCount);
                            int val = Mathf.CeilToInt(grassScale * Random.Range(0.3f, 1.0f));
                            layers[idx][dx, dy] = Mathf.Clamp(val, 1, 16);
                        }
                    }

                    if (dy % 8 == 0 && Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                    {
                        yield return null;
                        startTime = Time.realtimeSinceStartup;
                    }
                }
            }

            for (int i = 0; i < protoCount; i++)
            {
                terrain.terrainData.SetDetailLayer(0, 0, i, layers[i]);
                yield return null;
            }
        }

        /// <summary>
        /// 把 <paramref name="data"/> 里指定区间的细节（草）层数据清零。
        /// <para>用途：换图时细节层数变少 → 被裁掉的那几层草数据不会自动消失；或本图不配细节原型 →
        /// 上一张图的草会整片留在原地。两种情况都必须显式清，Unity 不会替我们清。</para>
        /// </summary>
        /// <param name="startLayer">起始层（0 基）</param>
        /// <param name="layerCount">要清的层数</param>
        private static void ClearDetailLayers(TerrainData data, int startLayer, int layerCount)
        {
            if (data == null || layerCount <= 0) return;

            int total = data.detailPrototypes?.Length ?? 0;
            int end = Mathf.Min(startLayer + layerCount, total);
            if (end <= startLayer) return;

            int w = data.detailWidth;
            int h = data.detailHeight;
            if (w <= 0 || h <= 0) return;

            // 各层复用同一块零数组，避免逐层分配整张细节图
            int[,] empty = new int[h, w];
            for (int layer = startLayer; layer < end; layer++)
                data.SetDetailLayer(0, 0, layer, empty);

            Debug.Log($"[细节] 已清空 {end - startLayer} 个不再使用的细节层（层 {startLayer}~{end - 1}），"
                + "避免上一张图的草残留到新地图");
        }

        /// <summary>
        /// 收集细节原型上"没有勾选 Enable GPU Instancing"的材质名（去重）。
        /// <para>细节走 GPU 实例化的前提之一就是这个材质开关，没勾的话 Unity 会静默退回非实例化渲染。</para>
        /// </summary>
        /// <param name="prototype">细节原型（prefab）</param>
        /// <param name="results">输出列表（同时充当去重集合）</param>
        private static void CollectMaterialsWithoutInstancing(GameObject prototype, List<string> results)
        {
            if (prototype == null || results == null) return;
            Renderer[] renderers = prototype.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (material == null || material.enableInstancing) continue;
                if (!results.Contains(material.name)) results.Add(material.name);
            }
        }

        /// <summary>打印每层细节原型的渲染模式，便于进图后对着日志核对"实例化到底有没有生效"</summary>
        /// <param name="protos">提交给地形的细节原型数组</param>
        private static void LogDetailPrototypes(DetailPrototype[] protos)
        {
            if (protos == null) return;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(
                "[原型] 细节层渲染模式（instancing=True 即走 GPU 实例化）：");
            for (int i = 0; i < protos.Length; i++)
            {
                if (protos[i] == null)
                {
                    sb.Append($"\n  [{i}] （空原型）");
                    continue;
                }
                GameObject prototype = protos[i].prototype;
                sb.Append($"\n  [{i}] {(prototype != null ? prototype.name : "null")}"
                    + $" {protos[i].renderMode}/instancing={protos[i].useInstancing}"
                    + $"/网格={protos[i].usePrototypeMesh}");
            }
            Debug.Log(sb.ToString());
        }
        #endregion

        #region 地形覆盖石（巨型悬崖）

        /// <summary>
        /// 按地形类型放置"地形覆盖物"（巨型悬崖 / 巨石）。
        /// <para>与 <see cref="SpawnVegetation"/> 的逐格概率不同：这类物体体积巨大，逐格概率要么叠成一坨、
        /// 要么抽不到，也没有互相排斥的概念，所以用"目标数量 + 占地圆互斥 + 坡度/高度约束"做拒绝采样。</para>
        /// <para>实例化出来的物体<b>会被强制设成地形所在的层</b>（= 地面层），因为 NavMeshSurface 是按
        /// 层的渲染网格烘焙的，只有和地形同层才能被算进寻路网格（AI 才走得上去）。</para>
        /// <para>必须在 <c>SetTreeInstances</c> 之前调用：占地圆是树与草（细节）的排除区。</para>
        /// </summary>
        IEnumerator PlaceRockCovers(TerrainPresetData preset)
        {
            // 管理器负责：清空上一次的登记与纯数据（TerrainUtils.AreaCircles）+ 销毁上一次的实例。
            // 列表为空也照样清空，避免上一局的残留。
            RockCoverDestructor.Rebuild(terrain);
            DestroyRockCovers();

            if (terrain == null || _rockCovers == null || _rockCovers.Count == 0) yield break;

            // 先过滤出可用条目（预制体非空、半径合法），并累计权重
            List<RockCoverEntry> entries = new();
            int totalWeight = 0;
            foreach (RockCoverEntry entry in _rockCovers)
            {
                if (entry == null || entry.prefab == null) continue;
                if (entry.footprintRadius <= 0f)
                {
                    Debug.LogWarning($"[地形覆盖] 条目 {entry.prefab.name} 的占地半径 <= 0，已跳过"
                        + "（半径为 0 时无法计算互斥与树/草排除范围）");
                    continue;
                }
                entries.Add(entry);
                totalWeight += Mathf.Max(1, entry.weight);
            }
            if (entries.Count == 0)
            {
                Debug.LogWarning("[地形覆盖] 列表里没有可用条目（预制体为空或占地半径非法），跳过放置");
                yield break;
            }

            RockCoverSpawnData cfg = preset.rockCover;
            // 地图级倍率（MapData_SO.RockCoverMultiplier）乘在数量上：0 = 本图不放悬崖
            int baseCount = _overridePreset && _rockCoverCount >= 0 ? _rockCoverCount : cfg.count;
            int targetCount = Mathf.RoundToInt(baseCount * _rockCoverMultiplier);
            float spacingScale = _overridePreset && _rockCoverMinSpacingScale >= 0f
                ? _rockCoverMinSpacingScale : cfg.minSpacingScale;
            spacingScale = Mathf.Max(0.01f, spacingScale);
            if (targetCount <= 0)
            {
                Debug.Log("[地形覆盖] 数量为 0，跳过放置");
                yield break;
            }

            TerrainData terrainData = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 mapSize = terrainData.size;
            // 可用落点范围：地图内缩 edgeMargin，避开地图边界与运行时创建空气墙的位置
            float minX = origin.x + _rockCoverEdgeMargin;
            float maxX = origin.x + mapSize.x - _rockCoverEdgeMargin;
            float minZ = origin.z + _rockCoverEdgeMargin;
            float maxZ = origin.z + mapSize.z - _rockCoverEdgeMargin;
            if (maxX <= minX || maxZ <= minZ)
            {
                Debug.LogWarning($"[地形覆盖] 地图尺寸不足（边缘留白 {_rockCoverEdgeMargin}m 过大），跳过放置");
                yield break;
            }

            // 与地形同层：NavMeshSurface 按"地面层"的渲染网格烘焙，不同层就白放了
            int layer = terrain.gameObject.layer;

            int placed = 0;
            int maxAttempts = Mathf.Max(1, _rockCoverAttemptsPerCover) * targetCount;
            float startTime = Time.realtimeSinceStartup;

            for (int attempt = 0; attempt < maxAttempts && placed < targetCount; attempt++)
            {
                RockCoverEntry entry = PickRockCoverEntry(entries, totalWeight);
                float radius = entry.footprintRadius;

                // 落点要留出整块石头的半径，保证石头完整落在地图内
                float posX = Random.Range(minX + radius, maxX - radius);
                float posZ = Random.Range(minZ + radius, maxZ - radius);
                Vector2 posXZ = new(posX, posZ);

                // 高度约束（归一化 0~1，与植被同一套 heightMap）
                int tx = Mathf.Clamp(Mathf.RoundToInt((posX - origin.x) / mapSize.x * (width - 1)), 1, width - 2);
                int tz = Mathf.Clamp(Mathf.RoundToInt((posZ - origin.z) / mapSize.z * (height - 1)), 1, height - 2);
                float h = heightMap[tx, tz];
                if (h < cfg.minHeight || h > cfg.maxHeight) continue;

                // 坡度 / 峰顶约束：按"整块占地圆"采样，而不是单个格子的梯度。
                // 巨石占地 10~30m，单格梯度只看得到脚下一小块，既容易在山尖处误判为平地，
                // 也判断不出这片地到底能不能放稳：
                //   倾角 = 占地圆一圈的最高/最低点高差 ÷ 直径（真实的米制坡度，单位：度）
                //   峰顶 = 中心比周围（占地圆内 + 外一圈）都高，即四周全在下坡 → 巨石会一半悬空
                SampleRockFootprint(posX, posZ, radius, out float tiltDeg, out bool isLocalPeak);
                if (isLocalPeak) continue;
                if (tiltDeg < cfg.minSlope || tiltDeg > cfg.maxSlope) continue;

                // 与其他覆盖石的占地圆互斥
                if (IsOverlappedByRockCover(posXZ, radius, spacingScale)) continue;

                // 落地：不同素材的 pivot 位置完全不一样（有的网格整体在 pivot 之上，直接按 pivot 摆到地表就会浮空），
                // 所以按"网格最低点"对齐：网格最低点 = 地表高度 - sinkDepth
                float surfaceY = SampleWorldHeight(posX, posZ);
                GameObject go = Instantiate(entry.prefab, new Vector3(posX, surfaceY, posZ),
                    Quaternion.Euler(0f, Random.value * 360f, 0f), _rockCoverRoot);
                go.name = $"{entry.prefab.name}_{placed}";
                // 先量出"相对 pivot 的最低点"（含随机 Y 旋转后的结果），再把 pivot 挪到目标高度
                float bottomOffset = GetRendererMinY(go) - go.transform.position.y;
                go.transform.position = new Vector3(posX, surfaceY - entry.sinkDepth - bottomOffset, posZ);
                SetLayerRecursively(go, layer);

                // 登记实例 + 注入纯数据占地圆（TerrainUtils.AreaCircles）：树/草的排除、任务点的避让、
                // 以及后续被挖地形/附加地形时的按范围清除，都只依赖这份登记
                RockCoverDestructor.Register(go, posXZ, radius);
                placed++;

                if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }

            Debug.Log($"[地形覆盖] 放置 {placed}/{targetCount} 处（尝试上限 {maxAttempts}，互斥系数 {spacingScale:F2}，"
                + $"层 {LayerMask.LayerToName(layer)}，占地圆倾角 {cfg.minSlope}~{cfg.maxSlope}°（已排除峰顶），"
                + $"高度 {cfg.minHeight}~{cfg.maxHeight}（归一化，约 {cfg.minHeight * mapSize.y:F0}~{cfg.maxHeight * mapSize.y:F0}m），"
                + $"地图 {mapSize.x:F0}×{mapSize.z:F0}）");
        }

        /// <summary>清掉上一次生成的覆盖石，并保证容器存在（换局/重新生成地形时调用）</summary>
        private void DestroyRockCovers()
        {
            if (_rockCoverRoot == null)
            {
                Transform existing = transform.Find(RockCoverRootName);
                _rockCoverRoot = existing != null ? existing : new GameObject(RockCoverRootName).transform;
                // SetParent(false)：容器只当个挂点，不继承 MapRoot 的缩放/位移
                if (_rockCoverRoot.parent == null) _rockCoverRoot.SetParent(transform, false);
            }

            for (int i = _rockCoverRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = _rockCoverRoot.GetChild(i).gameObject;
#if UNITY_EDITOR
                // 编辑器里通过 [ContextMenu] 生成时 Destroy 不生效，必须用 DestroyImmediate
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child);
                    continue;
                }
#endif
                Destroy(child);
            }
        }

        /// <summary>按权重随机取一个条目（weight &lt; 1 按 1 处理，保证每个条目都可能被选中）</summary>
        private static RockCoverEntry PickRockCoverEntry(List<RockCoverEntry> entries, int totalWeight)
        {
            int roll = Random.Range(0, totalWeight);
            for (int i = 0; i < entries.Count; i++)
            {
                roll -= Mathf.Max(1, entries[i].weight);
                if (roll < 0) return entries[i];
            }
            return entries[entries.Count - 1];
        }

        /// <summary>新落点是否与已放置的覆盖石互相压住（最小距离 = 半径和 × spacingScale，1 = 相切）</summary>
        private bool IsOverlappedByRockCover(Vector2 center, float radius, float spacingScale)
        {
            IReadOnlyList<TerrainUtils.AreaCircle> circles = TerrainUtils.AreaCircles;
            for (int i = 0; i < circles.Count; i++)
            {
                TerrainUtils.AreaCircle other = circles[i];
                float minDistance = (other.Radius + radius) * spacingScale;
                if ((other.Center - center).sqrMagnitude < minDistance * minDistance) return true;
            }
            return false;
        }

        /// <summary>世界 XZ 是否落在某个地表占地圆内</summary>
        private static bool IsInsideRockCoverWorld(IReadOnlyList<TerrainUtils.AreaCircle> circles, float worldX, float worldZ)
        {
            if (circles == null || circles.Count == 0) return false;
            for (int i = 0; i < circles.Count; i++)
            {
                TerrainUtils.AreaCircle circle = circles[i];
                float dx = circle.Center.x - worldX;
                float dz = circle.Center.y - worldZ;
                if (dx * dx + dz * dz < circle.Radius * circle.Radius) return true;
            }
            return false;
        }

        /// <summary>归一化地形坐标（0~1）是否落在某个地表占地圆内（树/草生成时用来排除）</summary>
        private bool IsInsideRockCoverNormalized(IReadOnlyList<TerrainUtils.AreaCircle> circles, float nx, float nz)
        {
            if (circles == null || circles.Count == 0) return false;
            Vector3 origin = terrain.transform.position;
            Vector3 mapSize = terrain.terrainData.size;
            return IsInsideRockCoverWorld(circles, origin.x + nx * mapSize.x, origin.z + nz * mapSize.z);
        }

        /// <summary>
        /// 世界 XZ → 地形表面世界高度。
        /// <para>刻意不用 <c>TerrainUtils.WSToHeight</c>：那个依赖 <c>TerrainUtils.Main</c> 已初始化，
        /// 而本类可能通过 [ContextMenu] 在编辑器里直接跑（此时 Main 为空会空引用）。</para>
        /// </summary>
        private float SampleWorldHeight(float worldX, float worldZ)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 mapSize = terrainData.size;
            float u = Mathf.Clamp01((worldX - origin.x) / mapSize.x);
            float v = Mathf.Clamp01((worldZ - origin.z) / mapSize.z);
            return origin.y + terrainData.GetInterpolatedHeight(u, v);
        }

        /// <summary>
        /// 采样巨石落点"实际压住的那片地"：地面倾角（度）+ 是否落在峰顶。
        /// <para>为什么不用 <see cref="GetSteepness"/>：那是单格梯度，且它的 cellSize 是历史硬编码的
        /// 近似值（不是真实米制坡度）。巨石占地 10~30m，要判断的是"整块地能不能放稳"，必须按占地圆采样。</para>
        /// <para>倾角 = 占地圆一圈上最高点与最低点的高差 ÷ 直径（由米制高差算出，量纲与预设里的角度一致）；
        /// 峰顶 = 中心比占地圆内、外两圈都高（四周全在下坡），这种位置放巨石必然一半悬空。</para>
        /// </summary>
        /// <param name="worldX">落点世界 X</param>
        /// <param name="worldZ">落点世界 Z</param>
        /// <param name="radius">占地半径（米）</param>
        /// <param name="tiltDegrees">输出：占地圆内的地面倾角（度）</param>
        /// <param name="isLocalPeak">输出：落点是否为峰顶/山脊尖端</param>
        private void SampleRockFootprint(float worldX, float worldZ, float radius,
            out float tiltDegrees, out bool isLocalPeak)
        {
            const int directions = 8;
            Vector3 origin = terrain.transform.position;
            Vector3 mapSize = terrain.terrainData.size;

            float centerHeight = SampleWorldHeight(worldX, worldZ);
            float nearMax = float.NegativeInfinity;
            float nearMin = float.PositiveInfinity;
            float farMax = float.NegativeInfinity;

            // 外圈不能探出地形边界：越界会被 SampleWorldHeight 钳到地图边缘（边缘被 edgeDropoff 压低），
            // 那会被误判成"四周都比中心低"。落点本身已按 edgeMargin + radius 内缩，所以这里至少能探 radius。
            float borderDistance = Mathf.Min(
                Mathf.Min(worldX - origin.x, origin.x + mapSize.x - worldX),
                Mathf.Min(worldZ - origin.z, origin.z + mapSize.z - worldZ));
            float farRadius = Mathf.Min(radius * RockCoverPeakProbeScale, Mathf.Max(radius, borderDistance));

            for (int i = 0; i < directions; i++)
            {
                float angle = i * Mathf.PI * 2f / directions;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float near = SampleWorldHeight(worldX + cos * radius, worldZ + sin * radius);
                nearMax = Mathf.Max(nearMax, near);
                nearMin = Mathf.Min(nearMin, near);

                float far = SampleWorldHeight(worldX + cos * farRadius, worldZ + sin * farRadius);
                farMax = Mathf.Max(farMax, far);
            }

            tiltDegrees = Mathf.Atan2(nearMax - nearMin, radius * 2f) * Mathf.Rad2Deg;
            isLocalPeak = centerHeight - Mathf.Max(nearMax, farMax) > RockCoverPeakTolerance;
        }

        /// <summary>
        /// 实例所有渲染器在世界 Y 上的最低点（没有渲染器时返回 pivot 的高度 = 不做修正）。
        /// <para>用于"把网格底面对齐地表"：素材的 pivot 常常不在网格底面（实测 CliffA/C/E 的网格
        /// 整体在 pivot 之上 8~25m，按 pivot 摆就浮空）。</para>
        /// </summary>
        private static float GetRendererMinY(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            float minY = go.transform.position.y;
            bool hasRenderer = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!hasRenderer)
                {
                    minY = renderers[i].bounds.min.y;
                    hasRenderer = true;
                }
                else
                {
                    minY = Mathf.Min(minY, renderers[i].bounds.min.y);
                }
            }
            return minY;
        }

        /// <summary>递归设置层（子物体一起改，LOD/网格子节点才会同层）</summary>
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }

        #endregion
        #region API

        /// <summary>
        /// 计算高度图中指定点的坡度（角度制）。
        /// <para>⚠ 这不是真实米制坡度：下面 <c>cellSize</c> 是历史硬编码的近似换算（把不同地图尺寸都按
        /// 同一比例折算，且忽略 <c>size.y</c>），它只保证"坡度大小关系"可用。</para>
        /// <para>给树/石的逐格概率生成用（那一套预设的角度就是按这个刻度调的）。需要真实坡度时用
        /// <see cref="SampleRockFootprint"/> 那条路径（按米制高差算），不要套用这里的返回值。</para>
        /// </summary>
        /// <param name="x">查询点的x坐标（基于heightmap数组索引）</param>
        /// <param name="y">查询点的y坐标（基于heightmap数组索引）</param>
        /// <returns>坡度角度（0-90度）</returns>
        private float GetSteepness(int x, int y)
        {
            float cellSize = 1 / 16f;//历史近似：真实格距 = size.x/(heightmapResolution-1)，按米制算会整体放大 5~40 倍
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