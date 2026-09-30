using UnityEngine;
using UnityEngine.AI;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 寻路请求 服务契约（由 <c>PathRequestManager</c> 实现并注册，2026-09-30 为 P5 加入）。
    ///
    /// <para>▍玩法层真实用法只有 1 处（<c>EnemyController</c> 给 NavMeshAgent 发目标点），
    /// 原先写 <c>PathRequestManager.Instance.RequestPath(...)</c> ⇒ AI 反向依赖 01Manager。</para>
    /// </summary>
    public interface IPathService
    {
        /// <summary>给 NavMeshAgent 设置目标点（内部做去重/限流/重试）。</summary>
        void RequestPath(NavMeshAgent agent, Vector3 destination, bool log = false);
    }
}
