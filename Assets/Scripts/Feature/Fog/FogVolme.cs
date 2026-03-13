using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FogShaderName
{
    public const string FogColor = "_FogColor";
    public const string FogIntensity = "_FogIntensity";
    public const string FogDistance = "_FogDistance";
    public const string FogHigh = "_FogHigh";
}




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
