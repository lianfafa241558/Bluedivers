using System.Collections.Generic;
using FPSGame.GameContract;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 欧帕兹背包的"可读写"抽象。
    ///
    /// <para>▍两个实现：本地玩家 = <see cref="PlayerOOPartInventory"/>；远端盟友 = <see cref="FriendOOPartInventory"/>
    /// （数据由联机消息临时注入）。</para>
    ///
    /// <para>▍为什么需要：容器类家具（<see cref="Furniture_OOPartDepositBase"/>：欧帕兹提交台 / 玩家火炮装填）的
    /// 效果写在 <c>IStepPress.StepPress</c> —— 是"从**操作者背包**里逐个扣出来"。远端的操作者是盟友实例，
    /// 身上原本没有任何背包组件 ⇒ 重放连第一步（<c>BeginPress</c> 读 <c>CurrentCount</c>）都过不去。
    /// 容器家具改为**面向接口**取背包后，远端挂上 <see cref="FriendOOPartInventory"/> 就能跑同一套逻辑。</para>
    /// </summary>
    public interface IOOPartInventory
    {
        /// <summary>当前携带总量。</summary>
        int CurrentCount { get; }

        /// <summary>指定类型是否已满。</summary>
        bool IsFull(OOPartEnum type);

        /// <summary>尝试加入携带，成功返回 true。</summary>
        bool TryAdd(OOPartEnum type, int count);

        /// <summary>取出指定数量，返回实际取出量。</summary>
        int Remove(OOPartEnum type, int count);

        /// <summary>获取某类型的携带量。</summary>
        int GetCount(OOPartEnum type);

        /// <summary>当前携带的全部条目。</summary>
        IEnumerable<KeyValuePair<OOPartEnum, int>> GetAll();
    }
}
