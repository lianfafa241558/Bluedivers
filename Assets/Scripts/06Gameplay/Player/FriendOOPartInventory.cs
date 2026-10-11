using System.Collections.Generic;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 远端盟友的"幽灵背包"（<see cref="IOOPartInventory"/> 的远端实现）：数据不来自玩家存档，
    /// 而是由联机消息**临时注入**、用完即弃。
    ///
    /// <para>▍用途：让"容器类家具的远端重放"成为可能 —— 桥收到 4048（提交了几个）后，
    /// 把它注入到这里，再对家具跑真实的 <c>BeginPress</c>/<c>StepPress</c>/<c>Operate</c>，
    /// 于是 <c>held</c> 与 <c>OnSubmit</c> 的表现（炮弹架要转成炮弹）在远端自然成立，
    /// 不需要再单开一条"结果状态"通道。</para>
    ///
    /// <para>▍挂载：盟友实例（<c>PlayerFriend.prefab</c>）上，由 <c>NetFriendBridge</c> 创建时补挂。</para>
    /// </summary>
    [AddComponentMenu("玩家/盟友幽灵背包(联机)")]
    public class FriendOOPartInventory : MonoBehaviour, IOOPartInventory
    {
        private readonly Dictionary<OOPartEnum, int> items = new();
        private int currentCount;

        public int CurrentCount => currentCount;

        /// <summary>【桥调用】注入本次要"被提交掉"的量（覆盖式：重放前先清）。</summary>
        public void Inject(OOPartEnum type, int count)
        {
            items.Clear();
            currentCount = 0;
            if (count <= 0) return;
            items[type] = count;
            currentCount = count;
        }

        /// <summary>清空（重放收尾 / 单位离场时调用）。</summary>
        public void Clear()
        {
            items.Clear();
            currentCount = 0;
        }

        // 幽灵背包不做容量限制：远端重放的是"已经发生过的提交"，被上限挡住反而会少扣
        public bool IsFull(OOPartEnum type) => false;

        public int GetCount(OOPartEnum type) => items.TryGetValue(type, out var c) ? c : 0;

        public IEnumerable<KeyValuePair<OOPartEnum, int>> GetAll() => items;

        public bool TryAdd(OOPartEnum type, int count)
        {
            if (count <= 0) return false;
            items[type] = GetCount(type) + count;
            currentCount += count;
            return true;
        }

        public int Remove(OOPartEnum type, int count)
        {
            if (count <= 0 || !items.TryGetValue(type, out var have) || have <= 0) return 0;

            int actual = Mathf.Min(count, have);
            if (have == actual) items.Remove(type);
            else items[type] = have - actual;
            currentCount -= actual;
            return actual;
        }
    }
}
