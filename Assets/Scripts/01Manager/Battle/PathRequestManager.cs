using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 寻路请求管理器。
///
/// 设计要点（相对旧架构的改进）：
/// 1. 去重：同一 NavMeshAgent 同时只保留一个"活跃请求"。新请求若与该 agent 当前活跃请求的目标距离 &lt; 去抖阈值，直接忽略；
///    否则覆盖旧请求（旧请求作废）。避免 Follow 每帧 SetNavDestination 时同 agent 请求堆积。
/// 2. 同步设置：RequestPath 当帧直接调用 NavMeshAgent.SetDestination。SetDestination 本身是 Unity 异步的（不阻塞主帧），
///    且 destination 一旦设置就立即生效，agent 会朝新目标走。40 单位级别完全能承受，无需"每帧限量分发"。
/// 3. 只对"真失败"重试：pathPending=true（路径计算中）期间不超时、不重试。仅当 pathPending=false 后
///    pathStatus==PathInvalid 或 PathPartial(单点) 才视为寻路失败，做 NavMesh.SamplePosition 投影重试 + 兜底。
///    这才是原始设计要解决的"目标不可达导致单位卡住"问题。
/// 4. 不再有"假超时重试风暴"：旧版 pathPending&gt;1s 就重试入队，并发多时造成队列堆积、单位执行旧路径乱走。
/// </summary>
public class PathRequestManager : Singleton<PathRequestManager>
{
    /// <summary>单个 agent 的活跃寻路请求</summary>
    struct ActiveReq
    {
        public NavMeshAgent agent;
        public Vector3 destination;     // 本次请求的目标
        public int retryCount;          // 已重试次数
        public bool verifiedOnce;       // pathPending 结束后是否已验证过一次（避免反复进入失败分支）
        public bool log;
    }

    /// <summary>每个 agent 的活跃请求。同一 agent 只保留最新一条，旧请求被覆盖即作废。</summary>
    readonly Dictionary<NavMeshAgent, ActiveReq> active = new Dictionary<NavMeshAgent, ActiveReq>();

    /// <summary>同 agent 新请求与活跃请求目标距离小于此值时忽略（去抖，避免 Follow 每帧微调目标反复寻路）</summary>
    public float DedupDistance = 0.5f;

    /// <summary>最大重试次数（仅对真失败 PathInvalid/PathPartial单点 生效）</summary>
    public const int MaxRetry = 5;

    /// <summary>失败重试时 NavMesh.SamplePosition 的投影半径</summary>
    public float RetrySampleRadius = 5f;

    /// <summary>达到最大重试仍失败时的兜底投影半径</summary>
    public float FinalSampleRadius = 10f;

    /// <summary>请求寻路。log=true 时打印该单位寻路的关键事件，用于排查"输入目标正确但最终导航点错误"的问题。</summary>
    /// <param name="agent">寻路单位</param>
    /// <param name="destination">目标点（已由调用方确保是期望的最终目标，如玩家位置）</param>
    /// <param name="log">是否打印调试日志</param>
    public void RequestPath(NavMeshAgent agent, Vector3 destination, bool log = false)
    {
        if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            if (log) Debug.LogWarning($"[寻路] 请求被拒：agent 无效或不在 NavMesh 上，目标 {destination}", agent);
            return;
        }

        // 去重：若该 agent 已有活跃请求且新目标与旧目标距离很近，忽略本次请求
        if (active.TryGetValue(agent, out var existing)
            && Vector3.Distance(existing.destination, destination) < DedupDistance)
        {
            return;
        }

        // 同步设置目标。SetDestination 返回 false 表示目标不可达/参数非法。
        bool ok = agent.SetDestination(destination);
        if (log) Debug.Log($"[寻路] SetDestination({destination}) → {(ok ? "成功" : "失败")}, agent.destination={agent.destination}", agent);

        // 无论 ok 与否，都登记为活跃请求：失败时由 LateUpdate 的失败分支走投影重试。
        // （ok=false 时 pathPending 通常为 false 且 pathStatus=PathInvalid，会立即进入重试。）
        active[agent] = new ActiveReq
        {
            agent = agent,
            destination = destination,
            retryCount = existing.retryCount, // 保留之前的重试计数（若新旧目标是同一目标链路的延续）
            verifiedOnce = false,
            log = log,
        };
    }

    void LateUpdate()
    {
        // 遍历所有活跃请求。pathPending=true 跳过；pathPending=false 后只查真失败。
        // 注意：遍历字典期间不能修改字典，故把"覆盖"和"移除"动作收集到列表，遍历结束后统一执行。
        List<NavMeshAgent> toRemove = null;
        List<KeyValuePair<NavMeshAgent, ActiveReq>> toUpdate = null;

        foreach (var kv in active)
        {
            var req = kv.Value;
            var agent = req.agent;

            // agent 失效：移除
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                if (req.log) Debug.LogWarning($"[寻路] agent 失效，移除活跃请求（目标 {req.destination}）", agent);
                (toRemove ??= new List<NavMeshAgent>()).Add(agent);
                continue;
            }

            // 路径仍在计算：destination 已设好，agent 朝新目标走。不超时、不重试。
            if (agent.pathPending)
            {
                continue;
            }

            // pathPending 结束：只验证一次（避免每帧反复触发失败分支）
            if (req.verifiedOnce)
            {
                continue;
            }
            req.verifiedOnce = true;
            // 回写 verifiedOnce（收集，遍历结束后统一执行）
            (toUpdate ??= new List<KeyValuePair<NavMeshAgent, ActiveReq>>())
                .Add(new KeyValuePair<NavMeshAgent, ActiveReq>(agent, req));

            var status = agent.pathStatus;
            bool pathFailed = status == NavMeshPathStatus.PathInvalid
                || (status == NavMeshPathStatus.PathPartial && agent.path.corners.Length < 2);

            if (!pathFailed)
            {
                // 路径有效（含 PathPartial 有拐点）：移除活跃请求，不再追踪
                (toRemove ??= new List<NavMeshAgent>()).Add(agent);
                continue;
            }

            // 真失败：投影重试。HandlePathFailed 只做 SetDestination + 日志，返回需要写回的请求（null 表示移除）。
            var result = HandlePathFailed(agent, req, status);
            if (result.HasValue)
            {
                // 覆盖活跃请求（收集）
                (toUpdate ??= new List<KeyValuePair<NavMeshAgent, ActiveReq>>())
                    .Add(new KeyValuePair<NavMeshAgent, ActiveReq>(agent, result.Value));
            }
            else
            {
                // 达上限兜底后不再追踪（收集移除）
                (toRemove ??= new List<NavMeshAgent>()).Add(agent);
            }
        }

        // 遍历结束后统一执行写回与移除
        if (toUpdate != null)
        {
            for (int i = 0; i < toUpdate.Count; i++)
            {
                active[toUpdate[i].Key] = toUpdate[i].Value;
            }
        }
        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                active.Remove(toRemove[i]);
            }
        }
    }

    /// <summary>处理寻路真失败：投影目标后重新 SetDestination。
    /// 返回值：需要写回 active 字典的新 ActiveReq（继续追踪）；null 表示已达上限兜底完成，应从 active 移除。
    /// 注意：本方法不直接修改 active 字典，由调用方在遍历结束后统一执行。</summary>
    ActiveReq? HandlePathFailed(NavMeshAgent agent, ActiveReq req, NavMeshPathStatus status)
    {
        if (req.retryCount < MaxRetry)
        {
            Vector3 corrected = req.destination;
            if (NavMesh.SamplePosition(corrected, out var hit, RetrySampleRadius, NavMesh.AllAreas))
            {
                corrected = hit.position;
            }

            if (req.log)
            {
                Debug.LogWarning($"[寻路] 路径失败({status})，重试 {req.retryCount + 1}/{MaxRetry}。原目标 {req.destination} 投影后 {corrected}（偏差 {Vector3.Distance(corrected, req.destination)}m）", agent);
            }

            bool ok = agent.SetDestination(corrected);
            if (req.log && !ok) Debug.LogWarning($"[寻路] 重试 SetDestination({corrected}) 失败，下一帧继续", agent);

            // 返回需要写回的新请求：用投影后目标继续追踪，重试计数 +1
            return new ActiveReq
            {
                agent = agent,
                destination = corrected,
                retryCount = req.retryCount + 1,
                verifiedOnce = false,
                log = req.log,
            };
        }
        else
        {
            // 已达最大重试：兜底投影
            Vector3 finalHit = req.destination;
            if (NavMesh.SamplePosition(req.destination, out var hit, FinalSampleRadius, NavMesh.AllAreas))
            {
                finalHit = hit.position;
                agent.SetDestination(finalHit);
            }
            if (req.log)
            {
                Debug.LogError($"[寻路] 已达最大重试，{FinalSampleRadius}m 兜底投影。原目标 {req.destination} 最终设置 {finalHit}（偏差 {Vector3.Distance(finalHit, req.destination)}m）", agent);
            }
            // 兜底后不再追踪：返回 null，调用方会从 active 移除
            return null;
        }
    }

    /// <summary>清空所有活跃请求（场景切换/重置时调用）</summary>
    public void ClearAllPending()
    {
        active.Clear();
    }
}
