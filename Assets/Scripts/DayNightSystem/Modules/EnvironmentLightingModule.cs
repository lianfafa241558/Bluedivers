using Meryuhi.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/*
Customize the "Ambient Mode - Trilight (Gradient)" according to the time of day:"
- 0% (Left side): Sunrise (Angle 0).
- 25%: Midday (Angle 90).
- 50%: Sunset (Angle 180).
- 75%: Midnight (Angle 270).
- 100% (Right side): Sunrise (Angle 360/0) - Make sure this color matches 0% exactly for a seamless loop.
*/

namespace FPSGame.DayNightSystem
{
    /// <summary>
    /// 环境光照模块，负责根据时间调整环境光照和雾效
    /// </summary>
    [AddComponentMenu("昼夜系统/环境光照模块")]
    public class EnvironmentLightingModule : MonoBehaviour, IDayNightModule
    {
        /// <summary>天空盒昼夜插值属性 ID（昼夜全景天空盒用）</summary>
        private static readonly int SkyLerpId = Shader.PropertyToID("_Lerp");
        /// <summary>天空盒曝光属性 ID（程序化天空盒用，无 _Lerp 时以它表达昼夜与天气压暗）</summary>
        private static readonly int SkyExposureId = Shader.PropertyToID("_Exposure");
        /// <summary>天空盒色调属性 ID（Skybox/Procedural 为 _SkyTint，注意带下划线）</summary>
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        /// <summary>天空盒地面色属性 ID</summary>
        private static readonly int SkyGroundColorId = Shader.PropertyToID("_GroundColor");
        /// <summary>天空盒大气厚度属性 ID（程序化天空盒；沙尘期加厚让天空变浑浊）</summary>
        private static readonly int SkyAtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");

        [InspectorName("环境光模式")][SerializeField]
        private AmbientMode ambientMode = AmbientMode.Trilight;

        [Header("环境照明")]
        [SerializeField] private Gradient skyColor;
        [SerializeField] private Gradient equatorColor;
        [SerializeField] private Gradient groundColor;

        [Header("光照设置")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Gradient sunColor;
        [InspectorName("内置雾颜色（未注入地图雾色时的兜底）")]
        [SerializeField] private Gradient fogColor;

        /// <summary>
        /// 当前生效的雾色渐变：优先用氛围桥上注入的地图雾色（MapData_SO），未注入时用模块兜底渐变
        /// </summary>
        private Gradient ActiveFogGradient =>
            WeatherAtmosphereController.FogColorGradient != null ? WeatherAtmosphereController.FogColorGradient : fogColor;

        [SerializeField] private AnimationCurve fogFactor;
        private bool _isGradientMode;

        [Header("全屏雾效 (Full Screen Fog Volume)")]
        [InspectorName("挂载了 Full Screen Fog 组件的 Volume（如全局 Volume）")]
        [SerializeField] private Volume fogVolume;
        [InspectorName("按时间控制全屏雾强度，X 为当日时间 0~1")]
        [SerializeField] private AnimationCurve fullscreenFogIntensity = new(
            new Keyframe(0f, 0.6f), new Keyframe(0.25f, 0f), new Keyframe(0.5f, 0.6f), new Keyframe(0.75f, 0f), new Keyframe(1f, 0.6f));
        [InspectorName("按时间控制雾密度")]
        [SerializeField] private AnimationCurve fullscreenFogDensity = new(
            new Keyframe(0f, 0.2f), new Keyframe(0.25f, 0.1f), new Keyframe(0.5f, 0.2f), new Keyframe(0.75f, 0.1f), new Keyframe(1f, 0.2f));
        [InspectorName("按时间控制距离雾起点")]
        [SerializeField] private AnimationCurve fogStartLine = new(
            new Keyframe(0f, 10f), new Keyframe(0.25f, 50f), new Keyframe(0.5f, 10f), new Keyframe(0.75f, 50f), new Keyframe(1f, 10f));
        [InspectorName("按时间控制距离雾终点")]
        [SerializeField] private AnimationCurve fogEndLine = new(
            new Keyframe(0f, 100f), new Keyframe(0.25f, 300f), new Keyframe(0.5f, 100f), new Keyframe(0.75f, 300f), new Keyframe(1f, 100f));
        [InspectorName("按时间控制高度雾起点（低于此值开始起雾）")]
        [SerializeField] private AnimationCurve fogStartHeight = new(
            new Keyframe(0f, 10f), new Keyframe(0.25f, 2f), new Keyframe(0.5f, 10f), new Keyframe(0.75f, 2f), new Keyframe(1f, 10f));
        [InspectorName("按时间控制高度雾终点（低于此值全雾）")]
        [SerializeField] private AnimationCurve fogEndHeight = new(
            new Keyframe(0f, -5f), new Keyframe(0.25f, 0f), new Keyframe(0.5f, -5f), new Keyframe(0.75f, 0f), new Keyframe(1f, -5f));
        [InspectorName("低能见度高度抬升(米)")]
        [Tooltip("能见度倍率=1 时抬升 0；倍率越低高度雾整体抬升越多，恶劣天气时雾层上移包裹玩家")]
        [Min(0f)]
        [SerializeField] private float fogHeightRise = 8f;
        [InspectorName("沙尘期大气厚度倍率")]
        [Tooltip("沙尘期把程序化天空盒的 _AtmosphereThickness 乘到这个倍率：天空更浑浊、太阳盘更弱")]
        [Range(1f, 5f)] [SerializeField] private float dustAtmosphereScale = 2.5f;
        [InspectorName("天气氛围过渡时长(秒)")]
        [Tooltip("天气切换时，能见度/雾强度/云层参数从当前值渐变到目标值所需的时间")]
        [Min(0f)]
        [SerializeField] private float atmosphereTransitionDuration = 3f;
        [InspectorName("天空盒昼夜插值曲线")]
        [Tooltip("天空盒的昼夜插值（1=白天 0=夜晚），X 为当日时间 0~1。默认：0/0.5/1 为日出日落过渡。" +
                 "有 _Lerp 的昼夜全景天空盒直接写入 _Lerp；无 _Lerp 的程序化天空盒用它插值 _Exposure")]
        [SerializeField] private AnimationCurve skyboxDayNightLerp = new(
            new Keyframe(0f, 0.5f), new Keyframe(0.06f, 1f), new Keyframe(0.44f, 1f), new Keyframe(0.5f, 0.5f),
            new Keyframe(0.56f, 0f), new Keyframe(0.94f, 0f), new Keyframe(1f, 0.5f));
        [InspectorName("程序化天空盒夜间曝光比例")]
        [Tooltip("天空盒没有 _Lerp 时（如 Skybox/Procedural）改用 _Exposure 表达昼夜：曝光 = 基准曝光 × Lerp(本值, 1, 昼夜插值)。" +
                 "本值即深夜天空的暗度下限（0.15 表示夜晚只有白天 15% 的曝光）")]
        [Range(0f, 1f)] [SerializeField] private float nightSkyExposureScale = 0.15f;
        [InspectorName("程序化天空盒基准曝光")]
        [Tooltip("写进 _Exposure 的基准值（即材质上前手调的 Exposure）。基准只认这里的值，不再从材质读取 —— " +
                 "材质上的 _Exposure 每帧都会被模块覆盖")]
        [Min(0.01f)] [SerializeField] private float skyBaseExposure = 1f;
        [InspectorName("程序化天空盒基准大气厚度")]
        [Tooltip("写进 _AtmosphereThickness 的基准值，沙尘期按「沙尘期大气厚度倍率」放大")]
        [Min(0.01f)] [SerializeField] private float skyBaseAtmosphereThickness = 1f;

        /// <summary>
        /// Full Screen Fog 体积组件的运行时实例引用（Initialize 时从 fogVolume 中获取）
        /// </summary>
        private FullScreenFog _fullscreenFog;

        /// <summary>运行时天空盒材质副本：所有写值都写它，材质资产保持干净</summary>
        private Material _skyboxInstance;
        /// <summary>副本的来源资产：RenderSettings.skybox 被换成别的材质时重建副本</summary>
        private Material _skyboxSourceAsset;

        public void Initialize(DayNightState state)
        {
            RenderSettings.ambientMode = ambientMode;
            //RenderSettings.fog = true;
            //RenderSettings.fogMode = FogMode.Linear;
            //RenderSettings.fogStartDistance = 50f;
            //RenderSettings.fogEndDistance = 300f;
            _isGradientMode = ambientMode == AmbientMode.Trilight;

            // 获取全屏雾组件。未手动指定 Volume 时自动查找场景中的全局 Volume（只认 Default 层的，避免误抓其他层的 Volume）
            if (fogVolume == null)
            {
                int defaultLayer = LayerMask.NameToLayer("Default");
                var volumes = FindObjectsOfType<Volume>(true);
                foreach (var vol in volumes)
                {
                    //Debug.LogError("组件:"+ vol.name+ vol.gameObject.layer, vol);
                    if (vol.isGlobal && vol.gameObject.layer == defaultLayer)
                    {
                        fogVolume = vol;
                        //Debug.LogError("找到了组件",vol);
                        break;
                    }
                }
            }

            // 注意使用 profile（运行时自动实例化副本）而非 sharedProfile，避免污染资产文件
            if (fogVolume != null && fogVolume.profile.TryGet(out _fullscreenFog))
            {
                // 开启所有要被昼夜系统驱动的参数的 override，否则 Volume 混合时会忽略这些值
                _fullscreenFog.mode.overrideState = true;
                _fullscreenFog.color.overrideState = true;
                _fullscreenFog.intensity.overrideState = true;
                _fullscreenFog.density.overrideState = true;
                _fullscreenFog.startLine.overrideState = true;
                _fullscreenFog.endLine.overrideState = true;
                _fullscreenFog.startHeight.overrideState = true;
                _fullscreenFog.endHeight.overrideState = true;
                _fullscreenFog.mode.value = FullScreenFogMode.HeightAndDistance;
            }
            else
            {
                _fullscreenFog = null;
            }
        }

        public void Tick(DayNightState state, float deltaTime)
        {
            float timeFraction = state.NormalizedTime;

            if (sunLight != null)
                sunLight.color = sunColor.Evaluate(timeFraction);

            // 天空盒昼夜插值：白天 1 / 夜晚 0，日出日落过渡。
            // 再乘天气天空盒亮度倍率：暴雨/暴雪乌云蔽日时，白天也压向夜晚暗色
            Material skybox = GetSkyboxForWrite();
            if (skybox != null)
            {
                float skyDayLerp =
                    Mathf.Clamp01(skyboxDayNightLerp.Evaluate(timeFraction) * WeatherAtmosphereController.SkyLerpMultiplier);

                if (skybox.HasProperty(SkyLerpId))
                {
                    // 昼夜全景天空盒：直接写 _Lerp（0=夜晚全景图 1=白天全景图）
                    skybox.SetFloat(SkyLerpId, skyDayLerp);
                }
                else if (skybox.HasProperty(SkyExposureId))
                {
                    // 程序化天空盒（Skybox/Procedural）没有 _Lerp：改用 _Exposure 表达昼夜与天气压暗。
                    // 基准曝光取自本组件的配置（不再从材质读），彻底避免"把上一局写进去的值当成基准"的累积
                    float exposure = skyBaseExposure * Mathf.Lerp(nightSkyExposureScale, 1f, skyDayLerp);
                    skybox.SetFloat(SkyExposureId, exposure);
                }
            }

            if (_isGradientMode)
            {
                // 环境光随天气亮度倍率压暗：暴雨/暴雪乌云挡光，地面应呈冷灰蓝而非满亮度的黄昏暖色
                float ambient = WeatherAtmosphereController.AmbientBrightnessMultiplier;
                RenderSettings.ambientSkyColor = skyColor.Evaluate(timeFraction) * ambient;
                RenderSettings.ambientEquatorColor = equatorColor.Evaluate(timeFraction) * ambient;
                RenderSettings.ambientGroundColor = groundColor.Evaluate(timeFraction) * ambient;
                // 内置雾颜色随环境光一起压暗，避免环境暗了雾反而比场景亮
                RenderSettings.fogColor = ActiveFogGradient.Evaluate(timeFraction) * ambient;
                // 同步到氛围桥：透明物体（体积云）按距离融入同一雾色
                WeatherAtmosphereController.FogColor = RenderSettings.fogColor;
                // 内置雾密度同样受天气能见度联动：能见度低时加浓（参考 0.05 下限防除零）
                RenderSettings.fogDensity = fogFactor.Evaluate(timeFraction) / Mathf.Max(WeatherAtmosphereController.VisibilityMultiplier, 0.05f);
                // 程序化天空盒（Skybox/Procedural）的天空色调属性名是 _SkyTint（带下划线），
                // 写成 "SkyTint" 会静默失败（SetColor 找不到属性直接丢弃）。
                // 曝光已交给 _Exposure 承担昼夜暗度，这里只做天空色调，故随环境光一起被天气轻微压暗
                if (skybox != null)
                {
                    // 沙尘天气（SkyDust>0）：天空色调向沙尘色靠拢（沙尘是散射体，直接给亮色、不乘 ambient），
                    // 同时加厚大气让天空更浑浊、太阳盘更弱
                    float dust = Mathf.Clamp01(WeatherAtmosphereController.SkyDust);
                    Color dustColor = WeatherAtmosphereController.SkyDustColor;
                    if (skybox.HasProperty(SkyTintId))
                        skybox.SetColor(SkyTintId, Color.Lerp(skyColor.Evaluate(timeFraction), dustColor, dust));
                    if (skybox.HasProperty(SkyGroundColorId))
                        skybox.SetColor(SkyGroundColorId, Color.Lerp(equatorColor.Evaluate(timeFraction), dustColor, dust));
                    if (skybox.HasProperty(SkyAtmosphereThicknessId))
                        skybox.SetFloat(SkyAtmosphereThicknessId, GetSkyAtmosphereThickness(dust));
                }
            }

            // 驱动天气氛围参数向目标值平滑过渡（写入静态桥的过渡时长后执行渐变）
            WeatherAtmosphereController.TransitionDuration = atmosphereTransitionDuration;
            WeatherAtmosphereController.Smooth(deltaTime);

            UpdateFullscreenFog(timeFraction);
        }

        /// <summary>
        /// 按当日时间驱动全屏雾 Volume 参数
        /// </summary>
        private void UpdateFullscreenFog(float timeFraction)
        {
            if (_fullscreenFog == null)
                return;

            // 天气氛围合成：能见度倍率收缩距离，雾强度增量直接叠加（保证恶劣天气在昼夜曲线低点也能起雾）
            float visibility = WeatherAtmosphereController.VisibilityMultiplier;

            // 全屏雾颜色同样随环境光压暗：雾的照明来自环境，环境暗雾不能比场景亮
            Color color = ActiveFogGradient.Evaluate(timeFraction) * WeatherAtmosphereController.AmbientBrightnessMultiplier;
            _fullscreenFog.color.value = color;
            _fullscreenFog.intensity.value = Mathf.Clamp01(fullscreenFogIntensity.Evaluate(timeFraction) + WeatherAtmosphereController.FogIntensityAdd);
            _fullscreenFog.density.value = Mathf.Clamp01(fullscreenFogDensity.Evaluate(timeFraction) / Mathf.Max(visibility, 0.05f));
            _fullscreenFog.startLine.value = fogStartLine.Evaluate(timeFraction) * visibility;
            _fullscreenFog.endLine.value = Mathf.Max(fogEndLine.Evaluate(timeFraction) * visibility, fogStartLine.Evaluate(timeFraction) * visibility + 1f);
            // 高度雾整体抬升 = 能见度驱动的抬升（越低能见度雾层越高、包裹玩家）+ 天气配置的雾层抬升（平时/风暴各一个值）
            float heightRise = (1f - Mathf.Clamp01(visibility)) * fogHeightRise
                               + WeatherAtmosphereController.FogHeightAdd;
            _fullscreenFog.startHeight.value = fogStartHeight.Evaluate(timeFraction) + heightRise;
            _fullscreenFog.endHeight.value = fogEndHeight.Evaluate(timeFraction) + heightRise;

            // 同步到氛围桥：透明物体（体积云）按距离融入同一雾色
            WeatherAtmosphereController.FogColor = color;
        }

        /// <summary>沙尘期的大气厚度：基准厚度（组件配置）按沙尘量放大到 dustAtmosphereScale 倍</summary>
        private float GetSkyAtmosphereThickness(float dust)
        {
            return skyBaseAtmosphereThickness * Mathf.Lerp(1f, dustAtmosphereScale, Mathf.Clamp01(dust));
        }

        /// <summary>
        /// 取用于写值的天空盒材质：运行时创建并缓存一个材质副本（副本会接管 RenderSettings.skybox）。
        /// 直接写 RenderSettings.skybox 指向的材质会改到材质**资产**上（编辑器里 Play 会把值留在 .mat），
        /// 下一次进入 Play 时这些残留值又会被当成"基准值"，导致 _Exposure/_AtmosphereThickness 逐局累积衰减。
        /// 编辑器下（未播放）不写，避免弄脏资产
        /// </summary>
        private Material GetSkyboxForWrite()
        {
            Material source = RenderSettings.skybox;
            if (source == null || !Application.isPlaying)
                return null;

            if (_skyboxInstance == null || _skyboxSourceAsset != source)
            {
                ReleaseSkyboxInstance();

                _skyboxSourceAsset = source;
                _skyboxInstance = new Material(source)
                {
                    name = source.name + " (Runtime)",
                    hideFlags = HideFlags.DontSave,
                };
                RenderSettings.skybox = _skyboxInstance;
            }

            return _skyboxInstance;
        }

        /// <summary>还原原始天空盒并销毁运行时副本</summary>
        private void ReleaseSkyboxInstance()
        {
            if (_skyboxInstance == null)
                return;

            if (RenderSettings.skybox == _skyboxInstance && _skyboxSourceAsset != null)
                RenderSettings.skybox = _skyboxSourceAsset;

            if (Application.isPlaying)
                Destroy(_skyboxInstance);
            else
                DestroyImmediate(_skyboxInstance);

            _skyboxInstance = null;
            _skyboxSourceAsset = null;
        }

        public void Dispose()
        {
            ReleaseSkyboxInstance();
        }
    }
}