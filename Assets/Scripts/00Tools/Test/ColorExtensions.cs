using UnityEngine;

public static class ColorExtensions
{

    /// <summary>
    /// 饱和度调整
    /// </summary>
    public static Color SaturateMultiplied(this Color color, float saturationMultiplier)
    {
        // 使用Color.RGBToHSV和Color.HSVToRGB
        Color.RGBToHSV(color, out float h, out float s, out float v);
        s = Mathf.Clamp01(s * saturationMultiplier);
        return Color.HSVToRGB(h, s, v);
    }

    /// <summary>
    /// RGB调整
    /// </summary>
    public static Color RGBMultiplied(this Color color, float multiplier)
    {
        return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
    }

    /// <summary>
    /// 透明度调整
    /// </summary>
    public static Color AlphaMultiplied(this Color color, float multiplier)
    {
        return new Color(color.r, color.g, color.b, color.a * multiplier);
    }

    /// <summary>
    /// RGB相乘
    /// </summary>
    public static Color RGBMultiplied(this Color color, Color multiplier)
    {
        return new Color(color.r * multiplier.r, color.g * multiplier.g, color.b * multiplier.b, color.a);
    }





}