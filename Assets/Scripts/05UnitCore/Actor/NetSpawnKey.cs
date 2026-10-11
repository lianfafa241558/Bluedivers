using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>【联机】生成者身份键（<c>INetSpawner.SpawnSourceKey</c> 的唯一算法，两端必须走这里，别各写一份）。
    ///
    /// <para>▍口径：**优先用单位号**（<c>Actor.NetId</c>：敌人在两端同序、派生号两端各算，都已经一致；且不会像位置那样漂）；
    /// 没有号的（刷怪器这类场景静态物）退回 <see cref="ActorsManager.DerivedNetId"/> 的"Id/名字 + 0.1m 量化位置"哈希。
    /// ⚠ 别把可移动单位（召唤师）退到位置哈希：它两端的位置本来就不同。</para>
    /// </summary>
    public static class NetSpawnKey
    {
        public static int Of(GameObject go)
        {
            if (go == null) return 0;

            var actor = go.GetComponent<Actor>();
            if (actor != null && actor.NetId != 0) return actor.NetId;

            string id = actor != null && !string.IsNullOrEmpty(actor.Id) ? actor.Id : go.name;
            return ActorsManager.DerivedNetId(id, go.transform.position);
        }
    }
}
