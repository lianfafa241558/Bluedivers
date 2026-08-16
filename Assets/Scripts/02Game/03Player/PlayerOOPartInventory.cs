using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

/// <summary>
/// 玩家欧帕兹(OOPart)携带背包组件。
/// 采集到的欧帕兹先存入背包（每种类型上限 <see cref="MaxPerType"/> 个），
/// 必须交由 Kei（交付点）才算完成任务计数。
/// 死亡保留携带量（倒在原地等救援，不掉落）。
/// </summary>
public class PlayerOOPartInventory : MonoBehaviour
{
    /// <summary>每种类型携带上限</summary>
    public const int MaxPerType = 5;

    /// <summary>当前携带总量</summary>
    [SerializeField]
    [InspectorName("当前携带数")]
    private int currentCount;

    /// <summary>按类型记录的携带数量</summary>
    private Dictionary<OOPartEnum, int> items = new();

    /// <summary>携带量变化事件（参数：类型，变更数）</summary>
    public event Action<OOPartEnum, int> OnChanged;

    /// <summary>当前携带总量</summary>
    public int CurrentCount => currentCount;

    /// <summary>指定类型是否已满</summary>
    public bool IsFull(OOPartEnum type) => GetCount(type) >= MaxPerType;

    /// <summary>指定类型剩余可携带量</summary>
    public int Remaining(OOPartEnum type) => MaxPerType - GetCount(type);

    private void OnEnable()
    {
        currentCount = 0;
        items.Clear();
    }

    /// <summary>
    /// 尝试加入携带。
    /// 成功则累加并返回 true；达到该类型上限返回 false（不销毁采集物）。
    /// </summary>
    public bool TryAdd(OOPartEnum type, int count)
    {
        if (count <= 0 || IsFull(type)) return false;

        int actual = Mathf.Min(count, Remaining(type));
        items[type] = items.TryGetValue(type, out var old) ? old + actual : actual;
        currentCount += actual;
        OnChanged?.Invoke(type, actual);
        return true;
    }

    /// <summary>
    /// 取出指定数量的欧帕兹（交付给 Kei 时调用）。
    /// 返回实际取出的数量。
    /// </summary>
    public int Remove(OOPartEnum type, int count)
    {
        if (count <= 0 || !items.TryGetValue(type, out var have) || have <= 0) return 0;

        int actual = Mathf.Min(count, have);
        if (have == actual) items.Remove(type);
        else items[type] = have - actual;
        currentCount -= actual;
        OnChanged?.Invoke(type, -actual);
        return actual;
    }

    /// <summary>获取某类型的携带量</summary>
    public int GetCount(OOPartEnum type) => items.TryGetValue(type, out var c) ? c : 0;

    /// <summary>当前携带的全部条目（类型 + 数量）</summary>
    public IEnumerable<KeyValuePair<OOPartEnum, int>> GetAll() => items;
}
