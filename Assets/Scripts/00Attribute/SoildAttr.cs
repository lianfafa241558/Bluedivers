
using UnityEngine;

namespace FPSGame.Attribute
{
    public class MinMaxSliderAttribute : PropertyAttribute
    {
        public float minLimit;
        public float maxLimit;

        public MinMaxSliderAttribute(float minLimit, float maxLimit)
        {
            this.minLimit = minLimit;
            this.maxLimit = maxLimit;
        }
    }
}