using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 雾效 Shader 属性名常量（_FogColor/_FogIntensity/_FogDistance/_FogHigh）
/// </summary>
public static class FogShaderName
{
    public const string FogColor = "_FogColor";
    public const string FogIntensity = "_FogIntensity";
    public const string FogDistance = "_FogDistance";
    public const string FogHigh = "_FogHigh";
}




/// <summary>
/// 雾效 Volume 组件：在场景 Volume 上配置雾颜色/浓度/距离参数，
/// 由 FogPass 读取并写入雾材质；intensity &gt; 0 时激活，AddIntensity 可运行时动态增减雾浓度
/// </summary>
public class FogVolme : VolumeComponent, IPostProcessComponent
{
    public FloatParameter intensity = new FloatParameter(0,true);
    public ColorParameter fogColor = new ColorParameter(Color.white, true);
    public FloatParameter distance = new FloatParameter(0, true);

    public bool IsActive()
    {
        return intensity.value > 0;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
    
    public void AddIntensity(float addValue)
    {
        intensity.Override(intensity.value+addValue);
    }
}
