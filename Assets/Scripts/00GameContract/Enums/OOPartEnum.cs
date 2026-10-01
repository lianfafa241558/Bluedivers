using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 「物证/采集品」类型（任务提交、统计使用）。
    /// <para>⚠ 成员的声明顺序就是序列化值（Unity 按 int 存），新增成员只能<b>追加在末尾</b>，不要插在中间。</para>
    /// </summary>
    public enum OOPartEnum
    {
        /// <summary>青辉石</summary>
        [InspectorName("青辉石")] Pyroxene,
        /// <summary>电池</summary>
        [InspectorName("电池")] Battery,
        /// <summary>埴轮</summary>
        [InspectorName("埴轮")] Crystal,
        /// <summary>十二面体</summary>
        [InspectorName("十二面体")] Dodecahedron,
        /// <summary>以太</summary>
        [InspectorName("以太")] Ether,
        /// <summary>透镜</summary>
        [InspectorName("透镜")] Glasses,
        /// <summary>圆盘</summary>
        [InspectorName("圆盘")] Pendant,
        /// <summary>手稿</summary>
        [InspectorName("手稿")] Voynich,
    }
}
