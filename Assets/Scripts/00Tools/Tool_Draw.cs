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


        public static void DrawLabel(Vector3 pos, string text, float time, Color color)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawLabel(pos, color, text, time);
#endif
        }
        public static void DrawLabel(Vector3 pos, string text, float time)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawLabel(pos, Color.white, text, time);
#endif
        }
        public static void DrawWireSphere(Vector3 pos, float size, Color color, float time)
        {
            DrawShape(ShapeType.Circle, pos, Vector3.one * size, time, color);
        }
        public static void DrawShape(ShapeType shape, Vector3 pos, Vector3 size, float time, Color color)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawShape(shape, pos, color, size, time);
#endif
        }
        public static void DrawShape(ShapeType shape, Vector3 pos, Vector3 size, float time)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawShape(shape, pos, Color.white, size, time);
#endif
        }


    }
}
