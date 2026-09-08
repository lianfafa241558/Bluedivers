using UnityEngine;


/// <summary>
/// 天气-氛围全局状态桥（静态）：
/// 天气特效（WeatherEffect 子类，位于 Assembly-CSharp）在状态切换时写入目标值（Target*），
/// 消费方（EnvironmentLightingModule / DrawVolumetricCloud）每帧读取平滑后的当前值（*Multiplier / FogIntensityAdd）。
/// 由 EnvironmentLightingModule.Tick 每帧调用 Smooth 完成向目标的渐变过渡，避免天气切换时参数瞬间跳变。
/// 注意：本文件位于 DayNightSystem.asmdef 程序集内，依赖方向为 Assembly-CSharp → DayNightSystem（自动引用），
/// 因此必须保持全局命名空间（无 namespace），WeatherEffect 侧才能免 using 直接访问。
/// 当前设计假设同一时刻只有一种天气生效
/// </summary>
public static class WeatherAtmosphereController
{
    /// <summary>氛围参数过渡时长（秒）</summary>
    public static float TransitionDuration = 3f;

    // ---- 目标值：天气系统切换状态时直接写入 ----

    /// <summary>目标能见度倍率</summary>
    public static float TargetVisibility = 1f;
    /// <summary>目标额外雾强度（0~1）</summary>
    public static float TargetFogIntensityAdd = 0f;
    /// <summary>目标云层覆盖倍率</summary>
    public static float TargetCloudCoverage = 1f;
    /// <summary>目标云层亮度倍率</summary>
    public static float TargetCloudBrightness = 1f;
    /// <summary>目标太阳光强度倍率</summary>
    public static float TargetSunIntensity = 1f;
    /// <summary>目标环境光亮度倍率</summary>
    public static float TargetAmbientBrightness = 1f;
    /// <summary>目标天空盒亮度倍率：乘到天空盒昼夜插值(_Lerp)上，&lt;1 天空压向夜晚全景图（阴天变暗）</summary>
    public static float TargetSkyLerpMultiplier = 1f;

    // ---- 当前值：向目标值平滑过渡，消费方读取 ----

    /// <summary>当前能见度倍率：乘到全屏雾距离参数上，&lt;1 收近视距</summary>
    public static float VisibilityMultiplier = 1f;
    /// <summary>当前额外雾强度（0~1）：叠加到昼夜曲线算出的雾强度上</summary>
    public static float FogIntensityAdd = 0f;
    /// <summary>当前云层覆盖倍率：乘到体积云贴图 Tiling 上，&gt;1 云更密集</summary>
    public static float CloudCoverageMultiplier = 1f;
    /// <summary>当前云层亮度倍率：乘到体积云曝光上，&lt;1 云更暗</summary>
    public static float CloudBrightnessMultiplier = 1f;
    /// <summary>当前太阳光强度倍率：乘到太阳 Light 强度上，&lt;1 阳光更暗（在 CelestialVisualsModule 中应用）</summary>
    public static float SunIntensityMultiplier = 1f;
    /// <summary>当前环境光亮度倍率：乘到环境光三色上，&lt;1 场景更暗（在 EnvironmentLightingModule 中应用）</summary>
    public static float AmbientBrightnessMultiplier = 1f;
    /// <summary>当前天空盒亮度倍率：乘到天空盒昼夜插值(_Lerp)上（在 EnvironmentLightingModule 中应用）</summary>
    public static float SkyLerpMultiplier = 1f;

    /// <summary>上次执行 Smooth 的帧号（帧保护：一帧内多个消费方调用时只驱动一次，保证渐变速率一致）</summary>
    private static int _lastSmoothFrame = -1;

    /// <summary>
    /// 向目标值平滑过渡一步（帧保护：每帧只执行一次，可由任意消费方驱动，传 Time.deltaTime）
    /// </summary>
    public static void Smooth(float deltaTime)
    {
        if (_lastSmoothFrame == Time.frameCount)
            return;
        _lastSmoothFrame = Time.frameCount;

        float t = TransitionDuration <= 0f ? 1f : Mathf.Clamp01(deltaTime / TransitionDuration);
        VisibilityMultiplier = Mathf.Lerp(VisibilityMultiplier, TargetVisibility, t);
        FogIntensityAdd = Mathf.Lerp(FogIntensityAdd, TargetFogIntensityAdd, t);
        CloudCoverageMultiplier = Mathf.Lerp(CloudCoverageMultiplier, TargetCloudCoverage, t);
        CloudBrightnessMultiplier = Mathf.Lerp(CloudBrightnessMultiplier, TargetCloudBrightness, t);
        SunIntensityMultiplier = Mathf.Lerp(SunIntensityMultiplier, TargetSunIntensity, t);
        AmbientBrightnessMultiplier = Mathf.Lerp(AmbientBrightnessMultiplier, TargetAmbientBrightness, t);
        SkyLerpMultiplier = Mathf.Lerp(SkyLerpMultiplier, TargetSkyLerpMultiplier, t);
    }
}
