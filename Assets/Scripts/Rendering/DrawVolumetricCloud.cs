using UnityEngine;

[ExecuteInEditMode]
public class DrawVolumetricCloud : MonoBehaviour
{
    [Header("云层配置")]
    public int horizontalStackSize = 20;
    public float cloudHeight = 5f;

    [Header("渲染资源")]
    public Mesh quadMesh;
    public Material cloudMaterial;
    [InspectorName("太阳光（云伪光照方向）")] [SerializeField] private Light _sunLight;
    [InspectorName("月亮光（太阳落下时备用）")] [SerializeField] private Light _moonLight;

    [Header("天空盒颜色匹配")]
    public bool autoSampleSkyColor = true;

    [Header("性能设置")]
    public bool useGpuInstancing = true;
    public bool castShadows = false;
    public int layer = 0;
    [InspectorName("视锥裁剪余量(度)")] [Tooltip("层中心超出视锥角多少度内仍绘制，防止大尺寸云层边缘被误裁")] [Min(0f)]
    [SerializeField] private float _cullMarginDegrees = 20f;

    [Header("天气联动")]
    [InspectorName("启用天气联动")] [SerializeField] private bool _useWeatherLink = true;

    /// <summary>矩阵缓存：仅在层数变化时重建，避免每帧 GC</summary>
    private Matrix4x4[] _matrices;
    /// <summary>主相机缓存：丢失（切场景/被禁用）时自动刷新</summary>
    private Camera _mainCamera;

    void Update()
    {
        // Camera.main 缓存，避免每帧调用查找
        if (_mainCamera == null) _mainCamera = Camera.main;
        var targetCamera = _mainCamera;

        cloudMaterial.SetFloat("_midYValue", transform.position.y);
        cloudMaterial.SetFloat("_cloudHeight", cloudHeight);

        if (autoSampleSkyColor && RenderSettings.skybox != null)
        {
            Material skyMat = RenderSettings.skybox;
            Texture dayTex = skyMat.GetTexture("_DayTex");
            Texture nightTex = skyMat.GetTexture("_NightTex");
            if (dayTex != null && nightTex != null)
            {
                cloudMaterial.SetTexture("_CloudDayTex", dayTex);
                cloudMaterial.SetTexture("_CloudNightTex", nightTex);
                cloudMaterial.SetFloat("_CloudLerp", skyMat.GetFloat("_Lerp"));
                cloudMaterial.SetColor("_CloudTintColor", skyMat.GetColor("_TintColor"));
                cloudMaterial.SetFloat("_CloudExposure", skyMat.GetFloat("_Exposure"));
            }
        }

        // 伪光照方向：优先太阳，太阳落下（强度归零）时用月亮；方向为"指向光源"
        Light activeLight = null;
        if (_sunLight != null && _sunLight.intensity > 0.01f) activeLight = _sunLight;
        else if (_moonLight != null && _moonLight.intensity > 0.01f) activeLight = _moonLight;
        Vector3 sunDir = activeLight != null ? -activeLight.transform.forward : Vector3.up;
        cloudMaterial.SetVector("_SunDirection", sunDir);

        // 天气联动：雨天/暴雪时云层更密、更暗（倍率由 WeatherEffect 通过 WeatherAtmosphereController 写入）
        // 顺便驱动一次平滑（帧保护，重复调用自动跳过），保证昼夜系统未运行时云的渐变也正常
        WeatherAtmosphereController.Smooth(Time.deltaTime);
        if (_useWeatherLink)
        {
            cloudMaterial.SetFloat("_CloudCoverage", WeatherAtmosphereController.CloudCoverageMultiplier);
            cloudMaterial.SetFloat("_CloudExposure", cloudMaterial.GetFloat("_CloudExposure") * WeatherAtmosphereController.CloudBrightnessMultiplier);
        }

        float layerSpacing = cloudHeight / Mathf.Max(horizontalStackSize - 1, 1);
        Vector3 startPosition = transform.position - Vector3.up * (cloudHeight / 2f);

        // 矩阵缓存：仅层数变化时重建，消除每帧 new[] 的 GC
        if (_matrices == null || _matrices.Length != horizontalStackSize)
            _matrices = new Matrix4x4[horizontalStackSize];

        // 视锥锥形裁剪：跳过相机看不到的层（半视锥角 + 余量），显著减少全屏半透明 overdraw
        bool canCull = targetCamera != null;
        float cullCos = -1f;
        Vector3 camPos = Vector3.zero;
        Vector3 camFwd = Vector3.forward;
        if (canCull)
        {
            float halfAngle = targetCamera.fieldOfView * 0.5f + _cullMarginDegrees;
            cullCos = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            camPos = targetCamera.transform.position;
            camFwd = targetCamera.transform.forward;
        }

        int visibleCount = 0;
        for (int i = 0; i < horizontalStackSize; i++)
        {
            Vector3 pos = startPosition + Vector3.up * (layerSpacing * i);
            if (canCull)
            {
                Vector3 toLayer = pos - camPos;
                // 层中心在相机背后、或偏离视线锥角外：该层不可见，直接跳过不生成绘制
                if (Vector3.Dot(toLayer, camFwd) < 0f ||
                    Vector3.Dot(toLayer / Mathf.Max(toLayer.magnitude, 0.001f), camFwd) < cullCos)
                    continue;
            }
            var matrix = Matrix4x4.TRS(pos, transform.rotation, transform.localScale);
            if (useGpuInstancing)
                _matrices[visibleCount++] = matrix;
            else
                Graphics.DrawMesh(quadMesh, matrix, cloudMaterial, layer, targetCamera, 0, null, castShadows, false, false);
        }

        if (useGpuInstancing && visibleCount > 0)
        {
            var shadowCasting = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            Graphics.DrawMeshInstanced(quadMesh, 0, cloudMaterial, _matrices, visibleCount, null, shadowCasting, false, layer, targetCamera);
        }
    }
}
