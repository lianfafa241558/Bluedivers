using System.Collections.Generic;

namespace FPSGame.Gameplay
{

/// <summary>
/// 状态切换事件的通用派发：把「退出旧状态 / 进入新状态」匹配到一组配置项上并执行命中项。
/// <para>
/// 供 GameStateController（游戏状态）与 WindowStateController（窗口状态）共用 —— 两者原先各写了一份
/// 完全相同的遍历匹配逻辑。它们各自保留自己的序列化配置结构（ChangeItem），
/// 通过实现 <see cref="IItem{TState}"/> 接入本派发，因此 prefab 上已配好的数据不受影响。
/// </para>
/// </summary>
public static class StateEventDispatch
{
    /// <summary>单个状态配置项（由调用方的结构体实现，配合 struct 约束可避免装箱）</summary>
    public interface IItem<TState>
    {
        /// <summary>该行配置针对的状态</summary>
        TState State { get; }
        /// <summary>true = 在"退出该状态"时执行；false = 在"进入该状态"时执行</summary>
        bool IsExit { get; }
        /// <summary>执行该行配置</summary>
        void Invoke();
    }

    /// <summary>
    /// 按 exit/entry 匹配执行：exit 命中 isExit 项、entry 命中非 isExit 项。
    /// </summary>
    public static void Dispatch<TState, TItem>(TItem[] items, TState exit, TState entry)
        where TState : System.Enum
        where TItem : struct, IItem<TState>
    {
        if (items == null) return;

        var comparer = EqualityComparer<TState>.Default;
        for (int i = 0; i < items.Length; ++i)
        {
            TItem item = items[i];
            if (item.IsExit ? comparer.Equals(item.State, exit) : comparer.Equals(item.State, entry))
            {
                item.Invoke();
            }
        }
    }
}
}
