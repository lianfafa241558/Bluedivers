using System.Collections.Generic;
using FPSGame.Core;
using PEMaths;

namespace FPSGame.GameContract
{
    public struct UnitQueryGridNode : System.IEquatable<UnitQueryGridNode>
    {
        //与此节点相交的单位，key：teamID
        public Dictionary<UnitTypeEnum, List<IActor>> units;

        public PERect rect;
        public int x;
        public int y;

        public UnitQueryGridNode(PERect rect, int x, int y)
        {
            this.rect = rect;
            this.x = x;
            this.y = y;

            units = new();
        }

        public bool IsVaild() => rect.x != 0 && rect.y != 0;

        /// <summary>
        /// 基于x/y坐标判等（IEquatable接口实现，无装箱)
        /// </summary>
        public bool Equals(UnitQueryGridNode other)
        {
            // 两个节点坐标相同，即为同一个节
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            // 先判断类型，再调用强类型Equals
            return obj is UnitQueryGridNode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine((int)x * 1000, (int)y * 1000);
        }

    }
}
