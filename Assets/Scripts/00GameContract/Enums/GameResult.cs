using UnityEngine;

namespace FPSGame.GameContract
{

    public enum GameResult
    {
        /// <summary>未知</summary>
        [InspectorName("未知")] Unknow,
        /// <summary>胜利</summary>
        [InspectorName("胜利")] Victory,
        /// <summary>失败</summary>
        [InspectorName("失败")] Failure,
        /// <summary>中断</summary>
        [InspectorName("中断")] Interrupt,
    }
}