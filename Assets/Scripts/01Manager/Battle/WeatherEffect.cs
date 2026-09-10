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
