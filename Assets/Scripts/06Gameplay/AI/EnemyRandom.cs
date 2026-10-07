using System.Collections.Generic;
using FPSGame.Game;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 敌人的**确定性随机流**：按实体 <see cref="Actor.NetId"/> 派生，联机两端拿到同一条
    /// （AI 走位 / 技能落点 / 生产间隔这类"影响位置"的随机必须一致，否则两端会分叉）。
    ///
    /// <para>▍单机（无权威种子）时不追求确定性，但**同样按实体分开**：这样各单位的随机互不干扰
    /// （原来共用全局静态流，会被音效/弹孔/武器散布推进游标）。</para>
    /// </summary>
    public static class EnemyRandom
    {
        static readonly Dictionary<int, System.Random> cache = new Dictionary<int, System.Random>();

        public static System.Random For(Component c) => For(NetIdOf(c));

        public static System.Random For(int netId)
        {
            System.Random r;
            if (cache.TryGetValue(netId, out r)) return r;

            int seed = FPSGame.Data.TaskState.Seed;
            r = seed != 0
                ? new System.Random(SeedUtil.Derive(SeedUtil.Derive(seed, SeedStream.Ai), netId))
                : new System.Random(unchecked(System.Environment.TickCount * 31 + netId));

            cache[netId] = r;
            return r;
        }

        /// <summary>清空缓存（换局时调用：NetId 是新一局重新分配的）。</summary>
        public static void Clear() => cache.Clear();

        static int NetIdOf(Component c)
        {
            if (c == null) return 0;
            var a = c.GetComponentInParent<Actor>();
            return a != null ? a.NetId : 0;
        }
    }
}
