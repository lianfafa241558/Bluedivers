using UnityEngine;

/// <summary>
/// 沙漠：周期性沙尘暴，平静期隐藏沙尘效果物体，风暴期启用
/// </summary>
public class WeatherDesert : WeatherSystem
{
    /// <summary>沙漠效果物体资源路径</summary>
    protected override string EffectPath => "Prefabs/Weather/Weather_Desert";

    /// <summary>启用周期沙尘暴</summary>
    protected override bool UseStormCycle => true;

    protected override void OnStormStart()
    {
        SetEffectActive(true);
    }

    protected override void OnStormEnd()
    {
        SetEffectActive(false);
    }
}
