using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Utils
{
    public static partial class TerrainUtils
    {
        /// <summary>
        /// 非地形类"已占圆"（世界 XZ 圆心 + 半径）：任务点 / 兴趣点 / 场景点、临时划出的走廊（如铁轨）等。
        /// <para>生产者登记、后来的布点方避让——与 <see cref="AreaCircles"/>（地表覆盖物）同款"薄数据 + 谁生产谁登记"，
        /// 好处是需要避让的玩法层（06_Gameplay）不必反向引用登记方（09_Managers）。</para>
        /// <para>⚠ 与 <see cref="AreaCircles"/> <b>分开存</b>是必须的：那套会被 <c>RockCoverDestructor</c>
        /// 整体重建（<c>ClearAreaCircles</c> ⇒ 再把自己登记的加回去），混在一起会被误删。</para>
        /// </summary>
        private static readonly List<AreaCircle> reservedCircles = new();

        /// <summary>已占圆（只读，可能为空）</summary>
        public static IReadOnlyList<AreaCircle> ReservedCircles => reservedCircles;

        /// <summary>登记一处"已占圆"（半径 &lt;= 0 会被忽略）</summary>
        public static void AddReservedCircle(Vector2 center, float radius)
        {
            if (radius <= 0f) return;
            reservedCircles.Add(new AreaCircle(center, radius));
        }

        /// <summary>清空已占圆（重新生成任务点 / 换地形前由生产者调用一次）</summary>
        public static void ClearReservedCircles() => reservedCircles.Clear();
    }
}
