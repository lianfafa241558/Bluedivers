using UnityEngine;

namespace FPSGame.GameContract
{

    /// <summary>
    /// 单位等级（被 <c>CampData_SO.units</c> / <c>CampWaveData.tier</c> 等资产序列化）。
    /// <para>⚠ 取值已全部显式指定，新增必须自己写值，不要依赖自增。</para>
    /// </summary>
    public enum UnitTier
    {
        /// <summary>无</summary>
        [InspectorName("无")] None = 0,
        /// <summary>小型单位</summary>
        [InspectorName("小型单位")] Small = 1,
        /// <summary>中型单位</summary>
        [InspectorName("中型单位")] Medium = 2,
        /// <summary>精英单位</summary>
        [InspectorName("精英单位")] Elite = 3,
        /// <summary>重型单位</summary>
        [InspectorName("重型单位")] Heavy = 4,
        /// <summary>巨型单位</summary>
        [InspectorName("巨型单位")] Giant = 5,
        /// <summary>警戒单位</summary>
        [InspectorName("警戒单位")] Alert = 6,
        /// <summary>特殊单位 1</summary>
        [InspectorName("特殊单位1")] Special1 = 7,
        /// <summary>特殊单位 2</summary>
        [InspectorName("特殊单位2")] Special2 = 8,
        /// <summary>首领</summary>
        [InspectorName("首领")] Boss = 9,
    }
}
