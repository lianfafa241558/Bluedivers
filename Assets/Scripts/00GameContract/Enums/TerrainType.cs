using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 地形类型（被 <c>MapData_SO</c> 等资产序列化）。
    /// <para>⚠ 成员的声明顺序就是序列化值（Unity 按 int 存），新增成员只能<b>追加在末尾</b>，不要插在中间。</para>
    /// </summary>
    public enum TerrainType
    {
        /// <summary>沙漠</summary>
        [InspectorName("沙漠")] Desert,
        /// <summary>高原</summary>
        [InspectorName("高原")] Plateau,
        /// <summary>雨林</summary>
        [InspectorName("雨林")] Rainforest,
        /// <summary>丘陵</summary>
        [InspectorName("丘陵")] Hills,
        /// <summary>盆地</summary>
        [InspectorName("盆地")] Basin,
        /// <summary>平原</summary>
        [InspectorName("平原")] Plains,
        /// <summary>山地</summary>
        [InspectorName("山地")] Mountains,
    }
}
