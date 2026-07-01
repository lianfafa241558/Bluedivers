using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.AI;

public class PathRequestManager : Singleton<PathRequestManager>
{
    struct Request
    {
        public NavMeshAgent agent;
        public Vector3 destination;
        public int retryCount;
        public const int MaxRetry = 5;
    }

    struct PendingRequest
    {
        public NavMeshAgent agent;
        public Vector3 destination;
        public int retryCount;
        public float startTime;
        public bool stable;
    }

    Queue<Request> queue = new Queue<Request>();
    List<PendingRequest> pendingRequests = new List<PendingRequest>();
    public int maxRequestsPerFrame = 15;
    public float pathTimeout = 1f;

    public void RequestPath(NavMeshAgent agent, Vector3 destination)
    {
        if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        queue.Enqueue(new Request { agent = agent, destination = destination, retryCount = 0 });
    }

    bool TrySetDestination(NavMeshAgent agent, Vector3 destination)
    {
        if (!agent.SetDestination(destination))
            return false;

        // SetDestination 返回 true 不代表路径有效，立刻检查同步结果        
        //如果 pathPending 为false，说明是同步完成的，可以直接读状态
        if (!agent.pathPending)
        {
            var status = agent.pathStatus;
            if (status == NavMeshPathStatus.PathInvalid)
                return false;

            // PathPartial 但只有一个拐点（即只有起点），也是无效路径
            if (status == NavMeshPathStatus.PathPartial && agent.path.corners.Length < 2)
                return false;
        }

        return true;
    }

    void LateUpdate()
    {
        // 1. 分发新请求
        for (int i = 0; i < maxRequestsPerFrame && queue.Count > 0; i++)
        {
            var req = queue.Dequeue();
            if (!req.agent || !req.agent.isActiveAndEnabled || !req.agent.isOnNavMesh)
                continue;

            if (TrySetDestination(req.agent, req.destination))
            {
                pendingRequests.Add(new PendingRequest
                {
                    agent = req.agent,
                    destination = req.destination,
                    retryCount = req.retryCount,
                    startTime = Time.time,
                    stable = false
                });
            }
            else if (req.retryCount < Request.MaxRetry)
            {
                // 先投影终点再重试
                Vector3 corrected = req.destination;
                if (NavMesh.SamplePosition(corrected, out var hit, 5f, NavMesh.AllAreas))
                    corrected = hit.position;

                req.destination = corrected;
                req.retryCount++;
                queue.Enqueue(req);
            }
        }

        // 2. 验证 pending 中的路径
        for (int i = pendingRequests.Count - 1; i >= 0; i--)
        {
            var pending = pendingRequests[i];

            if (!pending.agent || !pending.agent.isActiveAndEnabled || !pending.agent.isOnNavMesh)
            {
                pendingRequests.RemoveAt(i);
                continue;
            }

            if (pending.agent.pathPending)
            {
                if (Time.time - pending.startTime > pathTimeout)
                {
                    if (pending.retryCount < Request.MaxRetry)
                        queue.Enqueue(new Request
                        {
                            agent = pending.agent,
                            destination = pending.destination,
                            retryCount = pending.retryCount + 1
                        });
                    pendingRequests.RemoveAt(i);
                }
                continue;
            }

            // 等一帧让数据稳定
            if (!pending.stable)
            {
                pending.stable = true;
                continue;
            }

            // 稳定后验证
            if (pending.agent.hasPath && pending.agent.remainingDistance > 0.01f)
            {
                pendingRequests.RemoveAt(i);
                continue;
            }

            // 目标很近，remaining =0 正常
            if (pending.agent.hasPath && Vector3.Distance(pending.agent.transform.position, pending.destination) < 1f)
            {
                pendingRequests.RemoveAt(i);
                continue;
            }

            // PathPartial 且有拐点
            if (pending.agent.pathStatus == NavMeshPathStatus.PathPartial && pending.agent.path.corners.Length >= 2)
            {
                pendingRequests.RemoveAt(i);
                continue;
            }

            // 无效路径，重试
            if (pending.retryCount < Request.MaxRetry)
            {
                Vector3 corrected = pending.destination;
                if (NavMesh.SamplePosition(corrected, out var hit, 5f, NavMesh.AllAreas))
                    corrected = hit.position;

                queue.Enqueue(new Request
                {
                    agent = pending.agent,
                    destination = corrected,
                    retryCount = pending.retryCount + 1
                });
                pendingRequests.RemoveAt(i);
            }
            else
            {
                if (NavMesh.SamplePosition(pending.destination, out var hit, 10f, NavMesh.AllAreas))
                    pending.agent.SetDestination(hit.position);
                pendingRequests.RemoveAt(i);
            }
        }
    }

    public void ClearAllPending()
    {
        pendingRequests.Clear();
        queue.Clear();
    }
}
