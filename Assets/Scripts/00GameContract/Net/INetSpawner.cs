using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>【联机】"运行时生成器"（巢穴 / 召唤师 / 场景刷怪器）的统一契约：房主唯一生成并上报 4052，成员凭来源键找到
    /// **本端那份生成器**、由它按自己的 prefab 表复刻。
    ///
    /// <para>▍为什么复刻必须由生成器自己实现：生成器分布在 06（巢穴 / 召唤师）与 10_Effect（刷怪器），而收发只有一份代码
    /// （09 的 <c>NetHostSpawnBridge</c>）；桥看不见它们的 prefab 表，只有生成器自己知道"槽位 → 哪把 prefab"。</para>
    ///
    /// <para>▍两条约定（破了就是两端对不上）：① <see cref="SpawnSourceKey"/> 两端必须算出同一个值；
    /// ② <see cref="ReplaySpawn"/> 里**覆盖 <c>NetId</c> 必须早于任何用到号的动作**（<c>SetNavDestination</c> / 注册表查询）。</para>
    /// </summary>
    public interface INetSpawner
    {
        /// <summary>跨端稳定的"生成者身份键"：房主上报、成员据此在本端找到同一个生成者。</summary>
        int SpawnSourceKey { get; }

        /// <summary>【成员】用**本端那份** prefab 复刻一个单位（由 4052 的桥调用，期间 <c>ActorsManager.Replicating</c> 为 true）。
        /// <para>参数：<paramref name="slot"/> = 生成者自己表里的下标（巢穴 = <c>creatData</c> 序 / 召唤师 = <c>summoned</c> 序 /
        /// 刷怪器 = <c>(int)tier</c>）；<paramref name="prefabId"/> = 单位的 <c>Actor.Id</c>（只有"按类型抽表"的刷怪器需要，
        /// 其余可忽略）；<paramref name="target"/> 全 0 = 无首帧目标点。</para></summary>
        /// <returns>复刻出来的单位；失败返回 null。</returns>
        GameObject ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target, string prefabId);

        /// <summary>上一次 <see cref="ReplaySpawn"/> 返回 null 是否只是"**已挂起、等前置对象**"（例：火车炮塔等车厢建出来），
        /// 而不是真失败 —— 桥接层据此区分日志级别（挂起不告警，真失败才告警）。</summary>
        bool HasPending { get; }
    }
}
