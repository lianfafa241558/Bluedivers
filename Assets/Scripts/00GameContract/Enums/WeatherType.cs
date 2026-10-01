using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 天气类型（被 <c>MapData_SO</c> 等资产序列化）。
    /// <para>⚠ 成员的声明顺序就是序列化值（Unity 按 int 存），新增成员只能<b>追加在末尾</b>，不要插在中间。</para>
    /// </summary>
    public enum WeatherType
    {
        /// <summary>晴天（无天气效果）</summary>
        [InspectorName("晴天")] Sunny,
        /// <summary>雨天</summary>
        [InspectorName("雨天")] Rain,
        /// <summary>沙漠</summary>
        [InspectorName("沙漠")] Desert,
        /// <summary>下雪</summary>
        [InspectorName("下雪")] Snow,
    }
}
