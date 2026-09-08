using UnityEngine;

/// <summary>
/// 沙漠组件：挂在沙尘暴特效预制体上。预制体勾选"启用周期风暴"，
/// 平静期由控制器隐藏、风暴期启用；无额外差异行为
/// </summary>
public class WeatherEffectDesert : WeatherEffect
{
    /// <summary>沙漠类型（控制器据此匹配预制体）</summary>
    public override WeatherType Type => WeatherType.Desert;
}
