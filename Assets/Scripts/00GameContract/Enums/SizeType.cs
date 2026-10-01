using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 任务/地图尺寸档位（被 <c>MissionMainData_SO.sizeType</c> 等资产序列化）。
    /// <para>⚠ 成员的声明顺序就是序列化值（Unity 按 int 存），新增成员只能<b>追加在末尾</b>，不要插在中间。</para>
    /// </summary>
    public enum SizeType
    {
        /// <summary>小型</summary>
        [InspectorName("小型")] Small,
        /// <summary>中型</summary>
        [InspectorName("中型")] Medium,
        /// <summary>大型</summary>
        [InspectorName("大型")] Large,
        /// <summary>迷你</summary>
        [InspectorName("迷你")] Mini,
    }
}
