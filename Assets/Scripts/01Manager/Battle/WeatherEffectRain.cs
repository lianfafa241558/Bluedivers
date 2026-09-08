using UnityEngine;

/// <summary>
/// 雨天组件：挂在雨天特效预制体上。持续降雨常显，预制体不勾选"启用周期风暴"
/// </summary>
public class WeatherEffectRain : WeatherEffect
{
    /// <summary>雨天类型（控制器据此匹配预制体）</summary>
    public override WeatherType Type => WeatherType.Rain;
}
