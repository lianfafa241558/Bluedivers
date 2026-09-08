using FPSGame.Attribute;
using UnityEngine;

namespace FPSGame.DayNightSystem
{
    /// <summary>
    /// 昼夜系统视觉效果模块，控制太阳、月亮、星星和云的显示和渐变效果
    /// </summary>
    [AddComponentMenu("昼夜系统/视觉效果模块")]
    public class CelestialVisualsModule : MonoBehaviour, IDayNightModule
    {
        [Header("对象")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;
        [SerializeField] private MeshRenderer starsRenderer;
        [SerializeField] private MeshRenderer moonRenderer;
        [SerializeField] private MeshRenderer cloudsRenderer;

        [Header("淡出设置")]
        [InspectorName("淡出角度跨度")]
        [Tooltip("天体淡入/淡出完成所需转过的角度（度），越小淡出越快")]
        [Min(0.1f)]
        [SerializeField] private float fadeAngleSpan = 15f;
        [MinMaxSlider(0,360)]
        [SerializeField] private Vector2 sunHiddenRange = new Vector2(206f, 334f);
        [MinMaxSlider(0, 360)]
        [SerializeField] private Vector2 moonHiddenRange = new Vector2(3f, 230f);
        [MinMaxSlider(0, 360)]
        [SerializeField] private Vector2 starsVisibleRange = new Vector2(180f, 355f);
        
        [Header("云设置")]
        [SerializeField] private Gradient cloudColorGradient;
        [SerializeField] private Gradient moonEmissionGradient;


        private float _sunInitialIntensity;
        private float _moonInitialIntensity;
        
        private float _sunScale = 1f;
        private float _moonScale = 1f;
        private float _starsScale = 0f;
        /// <summary>上一帧的角度，用于计算本帧转过的角度增量</summary>
        private float _lastAngle;

        private Material _moonMat;
        private Material _starsMat;
        private Material _cloudsMat;
        

        public void Initialize(DayNightState state)
        {
            if (sunLight) _sunInitialIntensity = sunLight.intensity;
            if (moonLight) _moonInitialIntensity = moonLight.intensity;
            _lastAngle = state.CurrentAngle;

            if (moonRenderer)
            {
                _moonMat = moonRenderer.material;
                _moonMat.EnableKeyword("_EMISSION");
            }

            if (starsRenderer) _starsMat = starsRenderer.material;
            if (cloudsRenderer)
            {
                _cloudsMat = cloudsRenderer.material;
                _cloudsMat.EnableKeyword("_EMISSION");
            }
        }

        public void Tick(DayNightState state, float deltaTime)
        {
            float angle = state.CurrentAngle;
            // 本帧转过的角度增量（DeltaAngle 自动处理 360/0 环绕），除以淡出角度跨度得到本帧淡出步长
            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(_lastAngle, angle));
            _lastAngle = angle;
            float delta = deltaAngle / fadeAngleSpan;

            float sunTarget = IsAngleInRange(angle, sunHiddenRange) ? 0f : 1f;
            float moonTarget = IsAngleInRange(angle, moonHiddenRange) ? 0f : 1f;
            float starsTarget = IsAngleInRange(angle, starsVisibleRange) ? 1f : 0f;

            _sunScale = Mathf.MoveTowards(_sunScale, sunTarget, delta);
            _moonScale = Mathf.MoveTowards(_moonScale, moonTarget, delta);
            _starsScale = Mathf.MoveTowards(_starsScale, starsTarget, delta);

            // 太阳/月亮强度叠乘天气倍率（暴雪/暴雨时天体光变暗，随 WeatherAtmosphereController 平滑过渡）
            float weatherDim = WeatherAtmosphereController.SunIntensityMultiplier;
            WeatherAtmosphereController.Smooth(deltaTime);
            if (sunLight) sunLight.intensity = Mathf.Lerp(0, _sunInitialIntensity, _sunScale) * weatherDim;
            if (moonLight) moonLight.intensity = Mathf.Lerp(0, _moonInitialIntensity, _moonScale) * weatherDim;

            if (_starsMat)
            {
                _starsMat.SetFloat("_Alpha", _starsScale);
            }

            if (_moonMat)
            {
                Color emissionRGB = moonEmissionGradient.Evaluate(state.NormalizedTime);
                _moonMat.SetColor("_EmissionColor", emissionRGB);
            }

            if (_cloudsMat)
            {
                Color cloudColor = cloudColorGradient.Evaluate(state.NormalizedTime);
                _cloudsMat.SetColor("_EmissionColor", cloudColor);
                _cloudsMat.color = cloudColor;
            }
        }

        public void Dispose()
        {
            if (_moonMat != null) Destroy(_moonMat);
            if (_starsMat != null) Destroy(_starsMat);
            if (_cloudsMat != null) Destroy(_cloudsMat);
        }

        /// <summary> Verifies if a circular angle falls within a range X-Y </summary>
        private bool IsAngleInRange(float angle, Vector2 range)
        {
            if (range.x <= range.y) return angle >= range.x && angle <= range.y;
            return angle >= range.x || angle <= range.y;
        }
    }
}