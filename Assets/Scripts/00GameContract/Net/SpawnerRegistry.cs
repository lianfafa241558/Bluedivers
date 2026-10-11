using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>【联机】生成器登记表（按 <see cref="INetSpawner.SpawnSourceKey"/> 惰性比较），供成员端凭 4052 的来源键找回本端那份生成器。
    ///
    /// <para>▍为什么不是字典：键要**现算**（生成者自己的 <c>NetId</c> 可能在复刻时才被覆盖）⇒ 存列表、查的时候逐个比。
    /// 数量级只有几十，够用。⚠ 登记点必须成对（<c>OnEnable</c>/<c>OnDisable</c> 或 <c>Init</c>/<c>Uninit</c>），
    /// 且必须早于"可能用到它"的时机 —— 复刻出来的单位在 <c>Instantiate</c> 内只跑过 Awake/OnEnable，
    /// **Start 还没跑**（所以别把登记放 Start 里）。</para>
    /// </summary>
    public static class SpawnerRegistry
    {
        static readonly List<INetSpawner> all = new List<INetSpawner>();

        public static void Register(INetSpawner spawner)
        {
            if (spawner == null || all.Contains(spawner)) return;
            all.Add(spawner);
        }

        public static void Unregister(INetSpawner spawner)
        {
            if (spawner == null) return;
            all.Remove(spawner);
        }

        /// <summary>按来源键找本端那份生成器（跳过已销毁的；找不到返回 null）。</summary>
        public static INetSpawner Find(int sourceKey)
        {
            for (int i = 0; i < all.Count; ++i)
            {
                var spawner = all[i];
                if (spawner as Component == null) continue;   // 漏了 OnDisable 也不至于指向废对象
                if (spawner.SpawnSourceKey == sourceKey) return spawner;
            }
            return null;
        }

        /// <summary>换局清空（生成器自己会 OnDisable 注销，这里只兜底）。</summary>
        public static void Clear() => all.Clear();
    }
}
