using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 积雪 Volume 组件：在场景 Volume 上配置积雪参数，由 SnowRenderPass 每帧读取。
/// snowAmount 为全局积雪量倍率（与各条目材质自身 _SnowAmount 相乘，保留层级差异）；
/// 其余参数默认不覆盖（overrideState=false），只在 Volume Profile 中勾选后才会写入所有雪材质，
/// 未勾选的参数保留各材质自身的值
/// </summary>
public class SnowVolume : VolumeComponent, IPostProcessComponent
{
    [InspectorName("积雪量")]
    public FloatParameter snowAmount = new FloatParameter(1f, true);

    [InspectorName("雪的颜色")]
    public ColorParameter snowColor = new ColorParameter(new Color(0.92f, 0.95f, 1f), false);

    [InspectorName("积雪阈值")]
    public FloatParameter snowThreshold = new FloatParameter(0.5f, false);

    [InspectorName("边缘柔和度")]
    public FloatParameter snowSoftness = new FloatParameter(0.25f, false);

    [InspectorName("噪声强度")]
    public FloatParameter noiseStrength = new FloatParameter(0.3f, false);

    /// <summary>积雪量为 0 时视为未激活，Pass 直接跳过</summary>
    public bool IsActive()
    {
        return snowAmount.value > 0f;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}
