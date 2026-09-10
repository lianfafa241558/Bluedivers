using UnityEngine;

/// <summary>
/// 下雪组件：挂在暴雪特效预制体上。预制体勾选"启用周期风暴"，
/// 暴雪期提高全局积雪量、平静期回落（状态物体切换由基类处理）
/// </summary>
public class WeatherEffectSnow : WeatherEffect
{
    [Header("暴雪积雪量")]
    [InspectorName("暴雪积雪量")] [SerializeField] private float _blizzardAmount = 1f;
    [InspectorName("平静积雪量")] [SerializeField] private float _calmAmount = 0.3f;

    /// <summary>下雪类型（控制器据此匹配预制体）</summary>
    public override WeatherType Type => WeatherType.Snow;

    public override void OnInit()
    {
        base.OnInit();
        // 常驻开启积雪渲染（仅本组件开启；基类 OnDisable 销毁时关闭），初始为平静期积雪量
        SnowController.SetEnabled(true);
        SnowController.SetGlobalAmount(_calmAmount);
    }

    public override void OnStormStart()
    {
        base.OnStormStart();
        // 暴雪期提高全局积雪量倍率
        SnowController.SetGlobalAmount(_blizzardAmount);
    }

    public override void OnStormEnd()
    {
        base.OnStormEnd();
        SnowController.SetGlobalAmount(_calmAmount);
    }
}
