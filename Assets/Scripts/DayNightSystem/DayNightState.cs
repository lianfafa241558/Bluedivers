using System;
using FPSGame.Attribute;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPSGame.DayNightSystem
{
    [Serializable]
    [Singleline]

    public class DayNightState
    {
        //[Tooltip("0-360")]
        [InspectorName("当前角度")]
        [Range(0,360)]
        public float CurrentAngle;

        [InspectorName("一天时间")]
        public float CycleDurationSecond = 1800f;

        /// <summary>标准化时间</summary>
        public float NormalizedTime => CurrentAngle / 360f;

        /// <summary>是否是白天</summary>
        public bool IsDaytime => CurrentAngle >= 0f && CurrentAngle < 180f;
    }
}