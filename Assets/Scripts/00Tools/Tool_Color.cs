using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using FPSGame.Core;
using FPSGame.Attributes;

namespace FPSGame.Utils
{
    public static partial class Tool
{


        public static float ColorDistance(Color a, Color b) => Vector3.Distance(new(a.r, a.g, a.b), new(b.r, b.g, b.b));

        public static Color ColorLerp(Color a, Color b, float speed) => new(Mathf.Lerp(a.r, b.r, speed), Mathf.Lerp(a.g, b.g, speed), Mathf.Lerp(a.b, b.b, speed));




        private static Color[] colorList = { new(0.58f, 0.02f, 0.03f), new(0.75f, 0.54f, 0.01f), new(0.12f, 0.44f, 0.6f), new(0.58f, 0.28f, 0.64f), Color.grey };



        /// <summary>颜色的灰 ?/summary>
        public static float GetGray(this Color color) => 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;

        /// <summary>颜色的明度只看最 ?/summary>
        public static float GetValue(this Color color) => Mathf.Max(color.r, color.g, color.b);

        /// <summary>颜色的亮 ?最亮和最暗的平均 ? </summary>
        public static float GetBrightness(this Color color) => (Mathf.Max(color.r, color.g, color.b) + Mathf.Min(color.r, color.g, color.b)) / 2;

        /// <summary>颜色的饱和度</summary>
        public static float GetSaturation(this Color color)
        {
            var max = Mathf.Max(color.r, color.g, color.b);
            var min = Mathf.Min(color.r, color.g, color.b);
            float lightness = (max + min) / 2f;

            return (max - min) / (1 - Mathf.Abs(2 * lightness - 1));
        }

        /// <summary>颜色的色 ?</summary>
        public static float GetHue(this Color color)
        {
            var max = Mathf.Max(color.r, color.g, color.b);
            var min = Mathf.Min(color.r, color.g, color.b);
            float hue;
            float delta = max - min;
            if (delta == 0) return 0;

            if (max == color.r)
                hue = 60 * ((color.g - color.b) / delta % 6);
            else if (max == color.g)
                hue = 60 * ((color.b - color.r) / delta + 2);
            else
                hue = 60 * ((color.r - color.g) / delta + 4);
            hue /= 360f;
            return hue;
        }


        public static Color ColorMin(Color a, Color b) => GetValue(a) < GetValue(b) ? a : b;
        public static Color ColorMin(float a, Color b) => a < GetValue(b) ? a * Color.white : b;
        public static Color ColorMin(Color a, float b) => GetValue(a) < b ? a : b * Color.white;


        public static Color ColorMax(Color a, Color b) => GetValue(a) > GetValue(b) ? a : b;
        public static Color ColorMax(float a, Color b) => a > GetValue(b) ? a * Color.white : b;
        public static Color ColorMax(Color a, float b) => GetValue(a) > b ? a : b * Color.white;
        public static Color MultiplyRGB(this Color color, float multiplier)
        {
            return new Color(
                color.r * multiplier,
                color.g * multiplier,
                color.b * multiplier,
                color.a
            );
        }


    }
}
