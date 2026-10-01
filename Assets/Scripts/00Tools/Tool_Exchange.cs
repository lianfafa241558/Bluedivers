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
        public static void Exchange(Transform source)
        {
            exchangeArea.Exchange(source);
        }
        public static Transform GetExchangeArea()
        {
            return exchangeArea.transform;
        }

    }
}
