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
        [SerializeField] private Gradient fogColor;

        [SerializeField] private AnimationCurve fogFactor;
        private bool _isGradientMode;

        public void Initialize(DayNightState state)
        {
            RenderSettings.ambientMode = ambientMode;
            //RenderSettings.fog = true;
            //RenderSettings.fogMode = FogMode.Linear;
            //RenderSettings.fogStartDistance = 50f;
            //RenderSettings.fogEndDistance = 300f;
            _isGradientMode = ambientMode == AmbientMode.Trilight;
        }

        public void Tick(DayNightState state, float deltaTime)
        {
            float timeFraction = state.NormalizedTime;

            if (sunLight != null) 
                sunLight.color = sunColor.Evaluate(timeFraction);

            if (_isGradientMode)
            {
                RenderSettings.ambientSkyColor = skyColor.Evaluate(timeFraction);
                RenderSettings.ambientEquatorColor = equatorColor.Evaluate(timeFraction);
                RenderSettings.ambientGroundColor = groundColor.Evaluate(timeFraction);
                RenderSettings.fogColor = fogColor.Evaluate(timeFraction);
                RenderSettings.fogDensity = fogFactor.Evaluate(timeFraction);
            }
        }

        public void Dispose() { }
    }
}