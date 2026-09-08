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
        [InspectorName("环境光模式")][SerializeField]
        private AmbientMode ambientMode = AmbientMode.Trilight;

        [Header("环境照明")]
        [SerializeField] private Gradient skyColor;
        [SerializeField] private Gradient equatorColor;
        [SerializeField] private Gradient groundColor;

        [Header("光照设置")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Gradient sunColor;
        [InspectorName("内置雾颜色，同时也会作为全屏雾的颜色")]
        [SerializeField] private Gradient fogColor;

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
        [InspectorName("天气氛围过渡时长(秒)")]
        [Tooltip("天气切换时，能见度/雾强度/云层参数从当前值渐变到目标值所需的时间")]
        [Min(0f)]
        [SerializeField] private float atmosphereTransitionDuration = 3f;
        [InspectorName("天空盒昼夜插值曲线")]
        [Tooltip("驱动天空盒材质的 Day-Night Lerp（1=白天全景图 0=夜晚全景图），X 为当日时间 0~1。默认：0/0.5/1 为日出日落过渡")]
        [SerializeField] private AnimationCurve skyboxDayNightLerp = new(
            new Keyframe(0f, 0.5f), new Keyframe(0.06f, 1f), new Keyframe(0.44f, 1f), new Keyframe(0.5f, 0.5f),
            new Keyframe(0.56f, 0f), new Keyframe(0.94f, 0f), new Keyframe(1f, 0.5f));

        /// <summary>
        /// Full Screen Fog 体积组件的运行时实例引用（Initialize 时从 fogVolume 中获取）
        /// </summary>
        private FullScreenFog _fullscreenFog;

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
                    Debug.LogError("组件:"+ vol.name+ vol.gameObject.layer, vol);
                    if (vol.isGlobal && vol.gameObject.layer == defaultLayer)
                    {
                        fogVolume = vol;
                        Debug.LogError("找到了组件",vol);
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

            // 驱动天空盒昼夜插值（Environment/DayNightPanoramic 的 _Lerp）：白天 1 / 夜晚 0，日出日落过渡。
            // 再乘天气天空盒亮度倍率：暴雨/暴雪乌云蔽日时，白天全景图也压向夜晚暗色
            var skybox = RenderSettings.skybox;
            if (skybox != null && skybox.HasProperty("_Lerp"))
                skybox.SetFloat("_Lerp", skyboxDayNightLerp.Evaluate(timeFraction) * WeatherAtmosphereController.SkyLerpMultiplier);

            if (_isGradientMode)
            {
                // 环境光随天气亮度倍率压暗：暴雨/暴雪乌云挡光，地面应呈冷灰蓝而非满亮度的黄昏暖色
                float ambient = WeatherAtmosphereController.AmbientBrightnessMultiplier;
                RenderSettings.ambientSkyColor = skyColor.Evaluate(timeFraction) * ambient;
                RenderSettings.ambientEquatorColor = equatorColor.Evaluate(timeFraction) * ambient;
                RenderSettings.ambientGroundColor = groundColor.Evaluate(timeFraction) * ambient;
                // 内置雾颜色随环境光一起压暗，避免环境暗了雾反而比场景亮
                RenderSettings.fogColor = fogColor.Evaluate(timeFraction) * ambient;
                // 内置雾密度同样受天气能见度联动：能见度低时加浓（参考 0.05 下限防除零）
                RenderSettings.fogDensity = fogFactor.Evaluate(timeFraction) / Mathf.Max(WeatherAtmosphereController.VisibilityMultiplier, 0.05f);
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
            Color color = fogColor.Evaluate(timeFraction) * WeatherAtmosphereController.AmbientBrightnessMultiplier;
            _fullscreenFog.color.value = color;
            _fullscreenFog.intensity.value = Mathf.Clamp01(fullscreenFogIntensity.Evaluate(timeFraction) + WeatherAtmosphereController.FogIntensityAdd);
            _fullscreenFog.density.value = Mathf.Clamp01(fullscreenFogDensity.Evaluate(timeFraction) / Mathf.Max(visibility, 0.05f));
            _fullscreenFog.startLine.value = fogStartLine.Evaluate(timeFraction) * visibility;
            _fullscreenFog.endLine.value = Mathf.Max(fogEndLine.Evaluate(timeFraction) * visibility, fogStartLine.Evaluate(timeFraction) * visibility + 1f);
            // 高度雾随能见度整体抬升：能见度越低雾层越高，包裹玩家（终点同步抬升保持雾层厚度不变）
            float heightRise = (1f - Mathf.Clamp01(visibility)) * fogHeightRise;
            _fullscreenFog.startHeight.value = fogStartHeight.Evaluate(timeFraction) + heightRise;
            _fullscreenFog.endHeight.value = fogEndHeight.Evaluate(timeFraction) + heightRise;
        }

        public void Dispose() { }
    }
}