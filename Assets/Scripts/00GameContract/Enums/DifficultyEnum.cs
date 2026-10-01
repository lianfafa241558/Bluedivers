using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 难度等级，数值从小到大递增。
    /// <para>⚠ 成员的声明顺序就是序列化值（Unity 按 int 存），新增成员只能<b>追加在末尾</b>，不要插在中间。</para>
    /// </summary>
    public enum DifficultyEnum
    {
        /// <summary>普通</summary>
        [InspectorName("普通")] Normal,
        /// <summary>困难</summary>
        [InspectorName("困难")] Hard,
        /// <summary>非常困难</summary>
        [InspectorName("非常困难")] VeryHard,
        /// <summary>硬核</summary>
        [InspectorName("硬核")] HardCode,
        /// <summary>极限</summary>
        [InspectorName("极限")] Extreme,
        /// <summary>疯狂</summary>
        [InspectorName("疯狂")] Insane,
        /// <summary>煎熬</summary>
        [InspectorName("煎熬")] Torment,
        /// <summary>癫狂</summary>
        [InspectorName("癫狂")] Lunatic,
    }
}
