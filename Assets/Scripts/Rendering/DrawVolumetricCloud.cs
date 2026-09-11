using System;
using UnityEngine;

/// <summary>
/// 体积云层系统（天空云层）。
/// <para>
/// 原理：用跟随相机的半球天穹作为"像素来源"，每个像素把自己的视线方向投影到该层云的高度平面上，
/// 在投影点采样程序化云场，再按视线在该层内穿过的路径长度（Beer-Lambert）算不透明度。
/// </para>
/// <para>
/// 与"水平平板叠 N 层"的旧做法相比：采样点由视线方向决定，云天然铺满天空直到地平线、没有平板边缘；
/// 掠射方向（近地平线）的路径长度成倍增长，云会自动变厚变实，形成层次；不同高度的层有正确视差。
/// </para>
/// <para> 
/// 每层参数用 MaterialPropertyBlock 传入，不写入材质资产；天穹深度压到远平面，因此不依赖 ZClip Off。
/// </para>
/// </summary>
[ExecuteInEditMode]
public class DrawVolumetricCloud : MonoBehaviour
{
    /// <summary>单层云配置</summary>
    [Serializable]
    public class CloudLayer
    {
        [InspectorName("启用")] public bool enabled = true;
        [InspectorName("云层高度(米，世界 Y)")] public float altitude = 550f;
        [InspectorName("云量")] [Range(0f, 1f)] public float coverage = 0.42f;
        [InspectorName("云团尺度(越小云越大)")] [Range(0.002f, 0.5f)] public float scale = 0.005f;
        [InspectorName("密度(不透明度)")] [Range(0.2f, 4f)] public float density = 1.4f;
        [InspectorName("细节占比")] [Range(0f, 0.6f)] public float detail = 0.3f;
        [InspectorName("风速倍率")] [Range(0f, 3f)] public float windScale = 1f;
        [InspectorName("层色调")] public Color tint = Color.white;
    }

    private static readonly int LayerAltitudeId = Shader.PropertyToID("_LayerAltitude");
    private static readonly int LayerCoverageId = Shader.PropertyToID("_LayerCoverage");
    private static readonly int LayerScaleId = Shader.PropertyToID("_LayerScale");
    private static readonly int LayerDensityId = Shader.PropertyToID("_LayerDensity");
    private static readonly int LayerDetailId = Shader.PropertyToID("_LayerDetail");
    private static readonly int LayerTintId = Shader.PropertyToID("_LayerTint");
    private static readonly int LayerWindId = Shader.PropertyToID("_LayerWind");
    private static readonly int CloudLerpId = Shader.PropertyToID("_CloudLerp");
    private static readonly int CloudTintColorId = Shader.PropertyToID("_CloudTintColor");
    private static readonly int CloudExposureId = Shader.PropertyToID("_CloudExposure");
    private static readonly int WeatherCloudCoverageId = Shader.PropertyToID("_WeatherCloudCoverage");
    private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
    private static readonly int CloudSunColorId = Shader.PropertyToID("_CloudSunColor");
    private static readonly int HazeColorId = Shader.PropertyToID("_CloudHazeColor");
    private static readonly int HazeStartId = Shader.PropertyToID("_CloudHazeStart");
    private static readonly int HazeEndId = Shader.PropertyToID("_CloudHazeEnd");
    private static readonly int HazeStrengthId = Shader.PropertyToID("_CloudHazeStrength");

    [Header("云层（按数组顺序绘制，请从高到低排列，先远后近）")]
    [SerializeField] private CloudLayer[] _layers = new CloudLayer[]
    {
        new CloudLayer
        {
            altitude = 900f, coverage = 0.22f, scale = 0.005f, density = 1.0f, detail = 0.10f,
            windScale = 1.25f, tint = new Color(0.97f, 0.98f, 1f, 1f),
        },
        new CloudLayer
        {
            altitude = 550f, coverage = 0.50f, scale = 0.006f, density = 1.4f, detail = 0.12f,
            windScale = 1f, tint = Color.white,
        },
        new CloudLayer
        {
            enabled = false, altitude = 380f, coverage = 0.40f, scale = 0.009f, density = 1.2f,
            detail = 0.10f, windScale = 0.8f, tint = new Color(1f, 0.99f, 0.97f, 1f),
        },
    };

    [Header("渲染资源")]
    [InspectorName("云材质")]
    [Tooltip("需要引用本 Shader（WalkingFat/VolumetricCloud_URP_Fixed）；脚本每帧用 MaterialPropertyBlock 传参，不会改动材质资产")]
    public Material cloudMaterial;
    [InspectorName("天穹半径(米)")]
    [Tooltip("跟随相机的半球壳半径，只决定像素从哪里来；深度已压到远平面，所以半径不影响遮挡关系")]
    [Min(10f)] [SerializeField] private float _domeRadius = 200f;
    [InspectorName("自定义天穹 Mesh")]
    [Tooltip("留空时自动生成半球天穹；只有需要自定义形状时才填")]
    [SerializeField] private Mesh _domeMeshOverride;
    [InspectorName("半球经向段数")] [Range(8, 96)] [SerializeField] private int _domeSegments = 48;
    [InspectorName("半球纬向段数")] [Range(4, 48)] [SerializeField] private int _domeRings = 24;
    [InspectorName("全局风速(米/秒)")] [Min(0f)] [SerializeField] private float _windSpeed = 8f;
    [InspectorName("风向(XZ)")] [SerializeField] private Vector2 _windDirection = new Vector2(1f, 0.6f);

    [Header("光照与昼夜")]
    [InspectorName("太阳光（云伪光照方向）")] [SerializeField] private Light _sunLight;
    [InspectorName("月亮光（太阳落下时备用）")] [SerializeField] private Light _moonLight;
    [InspectorName("自动匹配天空盒")]
    [Tooltip("每帧读取天空盒材质的色调/曝光给云染色（程序化天空盒读 _SkyTint/_Exposure）")]
    public bool autoSampleSkyColor = true;

    [InspectorName("昼夜插值曲线")]
    [Tooltip("X = 太阳方向的 Y 分量（-1 地平线下 ~ 1 天顶），Y = 云色昼夜插值（1 白亮白天，0 暗夜）")]
    [SerializeField] private AnimationCurve _dayNightBySunHeight = new(
        new Keyframe(-0.10f, 0f), new Keyframe(0.02f, 0.35f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1f));

    [InspectorName("天空色调染色强度")] [Range(0f, 1f)] [SerializeField] private float _skyTintInfluence = 0.6f;
    [InspectorName("主光源色染色强度")] [Range(0f, 1f)] [SerializeField] private float _sunColorInfluence = 0.75f;

    [Header("天气联动")]
    [InspectorName("启用天气联动")] [SerializeField] private bool _useWeatherLink = true;

    [Header("远景雾融合")]
    [InspectorName("启用远景雾融合")]
    [Tooltip("远处云按投影距离溶进地平线雾色，形成远景云雾的层次")]
    [SerializeField] private bool _useHazeLink = true;
    [InspectorName("用地面雾色作为远景雾颜色")]
    [Tooltip("勾选：取环境光照模块写入的雾色（与地面远景同一雾色）；取消：使用下面的自定义颜色")]
    [SerializeField] private bool _useFogColorAsHaze = true;
    [InspectorName("自定义远景雾颜色")] [SerializeField] private Color _hazeColor = new Color(0.72f, 0.78f, 0.86f, 1f);
    [InspectorName("远景雾起始距离(米)")]
    [Tooltip("云从该距离（换算成仰角）开始溶进地平线雾色，越远的地方越靠近地平线。" +
             "云层 550m 高时：3km≈10°、8km≈4°、12km≈2.5°")]
    [Min(0f)] [SerializeField] private float _hazeStartDistance = 3000f;
    [InspectorName("远景雾完成距离(米)")]
    [Tooltip("到该距离时云完全化为雾色并整体淡出（渐隐在到达地平线之前完成，所以不会出现硬边）")]
    [Min(0f)] [SerializeField] private float _hazeEndDistance = 12000f;
    [InspectorName("远景雾强度")] [Range(0f, 1f)] [SerializeField] private float _hazeStrength = 1f;
    [InspectorName("沙尘期远景雾起始(米)")]
    [Tooltip("天空沙尘量=1 时，雾带起止收缩到这两个距离：起止都很近 → 整个天空都被雾化，" +
             "云就会整体化进沙尘里（配合雾色变成沙尘色）")]
    [Min(0f)] [SerializeField] private float _dustHazeStartDistance = 10f;
    [InspectorName("沙尘期远景雾完成(米)")] [Min(0f)] [SerializeField] private float _dustHazeEndDistance = 400f;

    /// <summary>自动生成的半球天穹（非序列化，只用于绘制）</summary>
    private Mesh _domeMesh;
    /// <summary>自建天穹的生成参数：用于判断是否需要重建</summary>
    private int _builtSegments = -1;
    private int _builtRings = -1;
    /// <summary>每层一个 MaterialPropertyBlock（同一帧内不可复用同一实例）</summary>
    private MaterialPropertyBlock[] _layerBlocks;
    /// <summary>主相机缓存：丢失（切场景/被禁用）时自动刷新</summary>
    private Camera _mainCamera;

    // ---- 每帧刷新一次的全局环境参数（避免逐层重复计算）----
    private Vector3 _sunDirection = Vector3.up;
    private Color _sunTint = Color.white;
    private float _cloudLerp = 1f;
    private float _weatherCoverage = 1f;
    private float _cloudExposure = 1f;
    private Color _skyTint = Color.white;
    private bool _hasSkyTint;

    private void Update()
    {
        if (cloudMaterial == null)
            return;

        // Camera.main 缓存，避免每帧调用查找
        if (_mainCamera == null) _mainCamera = Camera.main;
        Camera camera = _mainCamera;
        if (camera == null)
            return;

        EnsureDomeMesh();
        if (_domeMesh == null)
            return;

        // 天气氛围平滑（帧保护：一帧内多个消费方调用时只驱动一次）
        WeatherAtmosphereController.Smooth(Time.deltaTime);
        RefreshEnvironment();

        int layerCount = _layers != null ? _layers.Length : 0;
        if (_layerBlocks == null || _layerBlocks.Length != layerCount)
            _layerBlocks = new MaterialPropertyBlock[layerCount];

        Vector3 cameraPosition = camera.transform.position;
        // 天穹跟随相机：球心即相机，于是顶点的归一化方向就是该像素的视线方向
        Matrix4x4 domeMatrix = Matrix4x4.TRS(cameraPosition, Quaternion.identity, Vector3.one * _domeRadius);

        for (int i = 0; i < layerCount; i++)
        {
            CloudLayer layer = _layers[i];
            if (layer == null || !layer.enabled)
                continue;

            MaterialPropertyBlock block = _layerBlocks[i];
            if (block == null)
            {
                block = new MaterialPropertyBlock();
                _layerBlocks[i] = block;
            }

            FillEnvironment(block);
            FillLayer(block, layer);
            Graphics.DrawMesh(_domeMesh, domeMatrix, cloudMaterial, gameObject.layer, camera, 0, block, false, false, false);
        }
    }

    /// <summary>每帧刷新一次全局环境参数（昼夜、天气、天空盒、主光源方向与颜色）</summary>
    private void RefreshEnvironment()
    {
        Light activeLight = PickMainLight();
        _sunDirection = activeLight != null ? -activeLight.transform.forward : Vector3.up;
        _sunTint = GetSunTint(activeLight);

        // 昼夜插值取"太阳"（不是当前主光源）的高度角：月亮升起不代表白天
        Vector3 dayRefDir = _sunLight != null ? -_sunLight.transform.forward : _sunDirection;
        _cloudLerp = Mathf.Clamp01(_dayNightBySunHeight.Evaluate(dayRefDir.y));

        _weatherCoverage = _useWeatherLink ? WeatherAtmosphereController.CloudCoverageMultiplier : 1f;
        float brightness = _useWeatherLink ? WeatherAtmosphereController.CloudBrightnessMultiplier : 1f;

        // 天空盒匹配：程序化天空盒读 _SkyTint/_Exposure，旧昼夜全景天空盒读 _TintColor
        float exposure = 1f;
        _hasSkyTint = false;
        if (autoSampleSkyColor && RenderSettings.skybox != null)
        {
            Material skyMat = RenderSettings.skybox;
            Color skyTint = skyMat.HasProperty("_SkyTint") ? skyMat.GetColor("_SkyTint")
                : skyMat.HasProperty("_TintColor") ? skyMat.GetColor("_TintColor")
                : Color.white;
            // 天空色调只用于染色：先按最大通道归一化，避免 _SkyTint 自身偏暗把云整体压黑
            _skyTint = Color.Lerp(Color.white, NormalizeColor(skyTint), _skyTintInfluence);
            _hasSkyTint = true;
            if (skyMat.HasProperty("_Exposure"))
                exposure = Mathf.Max(skyMat.GetFloat("_Exposure"), 0f);
        }
        // 天空的昼夜与天气压暗由 EnvironmentLightingModule 写进天空盒 _Exposure，这里读它即已一并带进来
        _cloudExposure = exposure * brightness;
    }

    /// <summary>填充与具体云层无关的参数</summary>
    private void FillEnvironment(MaterialPropertyBlock block)
    {
        block.SetFloat(CloudLerpId, _cloudLerp);
        block.SetFloat(CloudExposureId, _cloudExposure);
        block.SetFloat(WeatherCloudCoverageId, _weatherCoverage);
        block.SetVector(SunDirectionId, _sunDirection);
        block.SetColor(CloudSunColorId, _sunTint);
        if (_hasSkyTint)
            block.SetColor(CloudTintColorId, _skyTint);

        // 远景雾融合：雾色与地面远景保持一致（同一来源），云才不会在远处"浮"在雾前面。
        // 沙尘天气（SkyDust>0）：雾色向沙尘色靠拢，且雾带收缩到贴近相机——全天空都被雾化，
        // 云整体化进沙尘里（不需要改 Shader，靠起止距离换算出的仰角实现）
        if (_useHazeLink)
        {
            float dust = Mathf.Clamp01(WeatherAtmosphereController.SkyDust);
            Color hazeColor = _useFogColorAsHaze ? WeatherAtmosphereController.FogColor : _hazeColor;
            if (dust > 0f)
                hazeColor = Color.Lerp(hazeColor, WeatherAtmosphereController.SkyDustColor, dust);

            float hazeStart = Mathf.Lerp(_hazeStartDistance, _dustHazeStartDistance, dust);
            float hazeEnd = Mathf.Lerp(_hazeEndDistance, _dustHazeEndDistance, dust);

            block.SetColor(HazeColorId, hazeColor);
            block.SetFloat(HazeStartId, Mathf.Max(hazeStart, 0f));
            block.SetFloat(HazeEndId, Mathf.Max(hazeEnd, hazeStart + 1f));
            block.SetFloat(HazeStrengthId, _hazeStrength);
        }
    }

    /// <summary>填充单层云参数（含该层的风位移）</summary>
    private void FillLayer(MaterialPropertyBlock block, CloudLayer layer)
    {
        Vector2 windDir = _windDirection.sqrMagnitude > 0.0001f ? _windDirection.normalized : Vector2.right;
        Vector2 windOffset = windDir * (_windSpeed * Mathf.Max(layer.windScale, 0f)) * Time.time;

        block.SetFloat(LayerAltitudeId, layer.altitude);
        block.SetFloat(LayerCoverageId, layer.coverage);
        block.SetFloat(LayerScaleId, Mathf.Max(layer.scale, 0.0001f));
        block.SetFloat(LayerDensityId, layer.density);
        block.SetFloat(LayerDetailId, layer.detail);
        block.SetColor(LayerTintId, layer.tint);
        block.SetVector(LayerWindId, new Vector4(windOffset.x, windOffset.y, 0f, 0f));
    }

    /// <summary>取或生成天穹 Mesh（半球，半径 1）：段数不变时复用，变了才重建</summary>
    private void EnsureDomeMesh()
    {
        if (_domeMeshOverride != null)
        {
            _domeMesh = _domeMeshOverride;
            return;
        }

        int segments = Mathf.Clamp(_domeSegments, 8, 96);
        int rings = Mathf.Clamp(_domeRings, 4, 48);
        if (_domeMesh != null && _builtSegments == segments && _builtRings == rings)
            return;

        ReleaseDomeMesh();
        _domeMesh = CreateDomeMesh(segments, rings);
        _builtSegments = segments;
        _builtRings = rings;
    }

    /// <summary>释放自建天穹（用户自定义的 Mesh 不动）</summary>
    private void ReleaseDomeMesh()
    {
        if (_domeMesh != null && _domeMesh != _domeMeshOverride)
        {
            if (Application.isPlaying)
                Destroy(_domeMesh);
            else
                DestroyImmediate(_domeMesh);
        }
        _domeMesh = null;
    }

    /// <summary>生成单位半球（半径 1，只保留 y ≥ 0）</summary>
    private static Mesh CreateDomeMesh(int segments, int rings)
    {
        Mesh mesh = new Mesh { name = "CloudDome", hideFlags = HideFlags.DontSave };

        int vertexCount = (rings + 1) * (segments + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[rings * segments * 6];

        int vi = 0;
        for (int r = 0; r <= rings; r++)
        {
            // phi 从 0（天顶）到 π/2（地平线）
            float phi = Mathf.PI * 0.5f * r / rings;
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);
            for (int s = 0; s <= segments; s++)
            {
                float theta = Mathf.PI * 2f * s / segments;
                vertices[vi++] = new Vector3(sinPhi * Mathf.Cos(theta), cosPhi, sinPhi * Mathf.Sin(theta));
            }
        }

        int ti = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s;
                int b = a + segments + 1;
                triangles[ti++] = a;
                triangles[ti++] = b;
                triangles[ti++] = a + 1;
                triangles[ti++] = a + 1;
                triangles[ti++] = b;
                triangles[ti++] = b + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return mesh;
    }

    /// <summary>
    /// 取当前主光源：优先太阳，太阳落下（强度归零）时用月亮；两者都亮时取强度更高的一盏。
    /// 与程序化天空盒的太阳盘取光规则一致，保证云的光照方向与天空太阳位置对齐。
    /// </summary>
    private Light PickMainLight()
    {
        bool sunOn = _sunLight != null && _sunLight.intensity > 0.01f;
        bool moonOn = _moonLight != null && _moonLight.intensity > 0.01f;
        if (sunOn && moonOn)
            return _sunLight.intensity >= _moonLight.intensity ? _sunLight : _moonLight;
        if (sunOn) return _sunLight;
        if (moonOn) return _moonLight;
        return null;
    }

    /// <summary>
    /// 主光源颜色（保留色相、亮度归一到 1）用于给云染色：夜晚月光亮度低，
    /// 若直接相乘会把云压成全黑，故只取其色相。
    /// </summary>
    private Color GetSunTint(Light light)
    {
        if (light == null || _sunColorInfluence <= 0f)
            return Color.white;
        return Color.Lerp(Color.white, NormalizeColor(light.color), _sunColorInfluence);
    }

    /// <summary>按最大通道归一化颜色：保留色相，把亮度拉到 1（全黑时返回白色）</summary>
    private static Color NormalizeColor(Color c)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max < 0.001f)
            return Color.white;
        return new Color(c.r / max, c.g / max, c.b / max, 1f);
    }

    private void OnValidate()
    {
        // 段数被改时旧天穹需要重建（EnsureDomeMesh 会按参数判断）
        if (_domeMeshOverride == null && _domeMesh != null &&
            (_builtSegments != Mathf.Clamp(_domeSegments, 8, 96) || _builtRings != Mathf.Clamp(_domeRings, 4, 48)))
            ReleaseDomeMesh();

        if (_layers == null || _layers.Length == 0)
            Debug.LogWarning("[体积云] 至少需要配置一层云", this);
    }

    private void OnDestroy()
    {
        ReleaseDomeMesh();
    }
}
