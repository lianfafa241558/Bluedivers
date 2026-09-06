using UnityEngine;

/// <summary>
/// 下雪：持续飘雪 + 周期性暴雪。暴雪期启用暴雪效果物体并提高全局积雪量，平静期回落
/// </summary>
public class WeatherSnow : WeatherSystem
{
    [Header("暴雪积雪量")]
    [InspectorName("暴雪积雪量")] [SerializeField] private float _blizzardAmount = 1f;
    [InspectorName("平静积雪量")] [SerializeField] private float _calmAmount = 0.3f;

    /// <summary>下雪效果物体资源路径（暴雪）</summary>
    protected override string EffectPath => "Prefabs/Weather/Weather_Snow";

    /// <summary>启用周期暴雪</summary>
    protected override bool UseStormCycle => true;

    protected override void OnStormStart()
    {
        SetEffectActive(true);
        // 暴雪期提高全局积雪量倍率
        SnowController.SetGlobalAmount(_blizzardAmount);
    }

    protected override void OnStormEnd()
    {
        SetEffectActive(false);
        SnowController.SetGlobalAmount(_calmAmount);
    }
}
