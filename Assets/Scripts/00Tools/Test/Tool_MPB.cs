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
        public static MaterialPropertyBlock mpb;

        public static void SetColor(this MeshRenderer mr, Color color)
        {
            if (!mpb.IsValid()) mpb = new();
            mpb.SetColor("_Color", color);
            mr.SetPropertyBlock(mpb);
            mpb.Clear();
        }



    }
}
