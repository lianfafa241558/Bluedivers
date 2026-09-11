using UnityEngine;
using Unity.FPS.Game;
using Meryuhi.Rendering;

/// <summary>
/// 挂在天气特效预制体上的具体天气组件基类：
/// 序列化参数直接在预制体 Inspector 上配置（由 WeatherSystem 控制器实例化并驱动）。
/// 控制器按 Type 匹配预制体，并统一驱动周期风暴计时与跟随玩家；
/// 平时/风暴期两个状态物体的启停由基类统一处理，子类只声明类型和差异行为
/// </summary>
public abstract class WeatherEffect : MonoBehaviour
{
    /// <summary>对应天气类型（由具体子类固定声明，控制器据此匹配预制体）</summary>
    public abstract WeatherType Type { get; }

    [Header("状态物体")]
    [InspectorName("平时状态物体")] [SerializeField] protected GameObject _calmObject;
    [InspectorName("风暴期状态物体")] [SerializeField] protected GameObject _stormObject;

    [Header("风暴周期")]
    [InspectorName("启用周期风暴")] [SerializeField] protected bool _useStormCycle = false;
    [InspectorName("平静时长(秒)")] [SerializeField] protected float _calmDuration = 45f;
    [InspectorName("风暴时长(秒)")] [SerializeField] protected float _stormDuration = 20f;

    [Header("氛围联动")]
    [InspectorName("平时能见度倍率")] [SerializeField] protected float _calmVisibility = 1f;
    [InspectorName("风暴能见度倍率")] [SerializeField] protected float _stormVisibility = 0.45f;
    [InspectorName("平时雾强度增量")] [SerializeField] protected float _calmFogAdd = 0f;
    [InspectorName("风暴雾强度增量")] [SerializeField] protected float _stormFogAdd = 0.7f;
    [InspectorName("平时云层覆盖倍率")] [SerializeField] protected float _calmCloudCoverage = 1f;
    [InspectorName("风暴云层覆盖倍率")] [SerializeField] protected float _stormCloudCoverage = 1.6f;
    [InspectorName("平时云层亮度倍率")] [SerializeField] protected float _calmCloudBrightness = 1f;
    [InspectorName("风暴云层亮度倍率")] [SerializeField] protected float _stormCloudBrightness = 0.7f;
    [InspectorName("平时太阳光强度倍率")] [SerializeField] protected float _calmSunIntensity = 1f;
    [InspectorName("风暴太阳光强度倍率")] [SerializeField] protected float _stormSunIntensity = 0.4f;
    [InspectorName("平时环境光亮度倍率")] [SerializeField] protected float _calmAmbientBrightness = 1f;
    [InspectorName("风暴环境光亮度倍率")] [SerializeField] protected float _stormAmbientBrightness = 0.45f;
    [InspectorName("平时天空盒亮度倍率")] [SerializeField] protected float _calmSkyLerp = 1f;
    [InspectorName("风暴天空盒亮度倍率")] [SerializeField] protected float _stormSkyLerp = 0.3f;
    [InspectorName("平时雾层抬升(米)")]
    [Tooltip("在昼夜高度雾曲线上叠加的高度（米）。0 = 不干预（沿用昼夜曲线），雨/雪一般保持 0")]
    [SerializeField] protected float _calmFogHeightAdd = 0f;
    [InspectorName("风暴雾层抬升(米)")]
    [Tooltip("风暴期在昼夜高度雾曲线上叠加的高度（米）。0 = 不干预。\n" +
             "高度雾是「距离因子 × 高度因子」，天空重建出的世界高度约为「相机 far clip + 相机高度」，" +
             "雾层抬到它之上后天空才会落进雾层、开始随天气变浑（现 far=300 → 建议 500~700，沙尘暴常用）")]
    [SerializeField] protected float _stormFogHeightAdd = 0f;

    [InspectorName("平时天空沙尘量")] [Range(0f, 1f)] [SerializeField] protected float _calmSkyDust = 0f;
    [InspectorName("风暴天空沙尘量")]
    [Tooltip("0~1：天空盒色调向沙尘色靠拢、大气变厚，体积云同时整体化进沙尘。沙尘暴建议 0.8~1；" +
             "默认 0 = 不影响（雨/雪等天气必须保持 0，否则天空会被染成沙色）")]
    [Range(0f, 1f)] [SerializeField] protected float _stormSkyDust = 0f;
    [InspectorName("沙尘色(天空/云)")] [SerializeField] protected Color _skyDustColor = new Color(0.76f, 0.62f, 0.45f, 1f);

    /// <summary>是否周期风暴型（沙漠沙尘暴/下雪暴雪勾选，雨天不勾）</summary>
    public bool UseStormCycle => _useStormCycle;

    /// <summary>平静时长（秒）</summary>
    public float CalmDuration => _calmDuration;

    /// <summary>风暴时长（秒）</summary>
    public float StormDuration => _stormDuration;

    /// <summary>天气应用时回调（实例化后调用一次）：显示平时状态、隐藏风暴期状态，并常驻开启全屏雾（直到死亡/销毁才关）</summary>
    public virtual void OnInit()
    {
        SetCalmState(true);
        ApplyAtmosphere(true);
        // 静态开关跨局持久，进战斗时复位并常驻开启，玩家死亡或本组件销毁（游戏结束）时关闭
        FullScreenFogController.SetEnabled(true);
    }

    /// <summary>风暴开始回调：切换到风暴期状态（隐藏平时物体）</summary>
    public virtual void OnStormStart()
    {
        SetCalmState(false);
        ApplyAtmosphere(false);
    }

    /// <summary>风暴结束回调：恢复平时状态（雾保持常驻，不随风暴开关）</summary>
    public virtual void OnStormEnd()
    {
        SetCalmState(true);
        ApplyAtmosphere(true);
    }

    /// <summary>把当前状态（平时/风暴）的氛围倍率写入全局桥，供昼夜雾与体积云合成读取</summary>
    protected void ApplyAtmosphere(bool calm)
    {
        // 只写目标值，实际数值由 WeatherAtmosphereController.Smooth 每帧渐变逼近，避免瞬间跳变
        WeatherAtmosphereController.TargetVisibility = calm ? _calmVisibility : _stormVisibility;
        WeatherAtmosphereController.TargetFogIntensityAdd = calm ? _calmFogAdd : _stormFogAdd;
        WeatherAtmosphereController.TargetCloudCoverage = calm ? _calmCloudCoverage : _stormCloudCoverage;
        WeatherAtmosphereController.TargetCloudBrightness = calm ? _calmCloudBrightness : _stormCloudBrightness;
        WeatherAtmosphereController.TargetSunIntensity = calm ? _calmSunIntensity : _stormSunIntensity;
        WeatherAtmosphereController.TargetAmbientBrightness = calm ? _calmAmbientBrightness : _stormAmbientBrightness;
        WeatherAtmosphereController.TargetSkyLerpMultiplier = calm ? _calmSkyLerp : _stormSkyLerp;
        WeatherAtmosphereController.TargetSkyDust = calm ? _calmSkyDust : _stormSkyDust;
        WeatherAtmosphereController.TargetSkyDustColor = _skyDustColor;
        WeatherAtmosphereController.TargetFogHeightAdd = calm ? _calmFogHeightAdd : _stormFogHeightAdd;
    }

    /// <summary>切换平时/风暴期状态物体（未配置的忽略）</summary>
    protected void SetCalmState(bool calm)
    {
        if (_calmObject != null) _calmObject.SetActive(calm);
        if (_stormObject != null) _stormObject.SetActive(!calm);
    }

    private void OnDisable()
    {
        // 本组件销毁（游戏结束/场景卸载）时复位渲染开关（静态值跨局持久）：雾关闭、积雪关闭；玩家死亡不干预
        FullScreenFogController.SetEnabled(false);
        SnowController.SetEnabled(false);
    }
}
