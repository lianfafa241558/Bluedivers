using UnityEngine;

/// <summary>
/// 雨天：持续降雨，效果物体常显，无周期风暴
/// </summary>
public class WeatherRain : WeatherSystem
{
    /// <summary>雨天效果物体资源路径</summary>
    protected override string EffectPath => "Prefabs/Weather/Weather_Rain";
}
