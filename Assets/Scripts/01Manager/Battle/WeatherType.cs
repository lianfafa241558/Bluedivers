using UnityEngine;


/// <summary>天气类型</summary>
public enum WeatherType
{
    [InspectorName("晴天")]
    /// <summary>晴天（无天气效果）</summary>
    Sunny,
    [InspectorName("雨天")]
    /// <summary>雨天</summary>
    Rain,
    [InspectorName("沙漠")]
    /// <summary>沙漠</summary>
    Desert,
    [InspectorName("下雪")]
    /// <summary>下雪</summary>
    Snow,
}
