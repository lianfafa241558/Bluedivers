using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;

namespace FPSGame.Gameplay
{

/// <summary>
/// NPC 在 NavMesh 上游荡行走。
/// <para>▍联机：<see cref="RemoteDriven"/> = true（成员端）时**不自己决策**，只应用房主下发的目标点；
/// 房主决策/停下时发 <see cref="BattleEventSub.OnSceneUnitMove"/>，由 09 的场景桥收发。</para>
/// </summary>
[AddComponentMenu("NPC/游荡行走")]
public class NPCWalk : MonoBehaviour
{ 
    /// <summary>成员端置位（由 09 的 <c>SceneUnitMoveSink</c> 按"我是不是房主"设置）：本端不决策，等房主下发。
    /// <para>⚠ 语义是「**有桥在场** 且 我是成员」——桥不在的场景会退回各端本地游荡，不会变成"全体不动"。</para></summary>
    public static bool RemoteDriven;

    [Header("游荡设置")]
    [InspectorName("游荡间隔时间（秒）")]
    public float wanderInterval = 15.0f;

    [InspectorName("游荡半径")]
    public float wanderRadius = 10.0f;
    [InspectorName("允许漫游")]
    public bool allowRoam = true;

    private NavMeshAgent agent;
    private Animator animator;
    private Coroutine wanderCoroutine;

    /// <summary>同步用的键：<c>Actor.Id</c>（两端同一份场景 ⇒ 同 Id）。空 = 不参与同步。</summary>
    private string _unitId;

    /// <summary>网络下发但**当帧应用不了**的目标点（agent 未就绪时暂存，<see cref="Update"/> 里补）。</summary>
    private Vector3 _pendingDestination;
    private bool _hasPendingDestination;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        var actor = GetComponent<Actor>();
        _unitId = actor != null ? actor.Id : null;

        if (agent == null)
        {
            Debug.LogError("WanderController: 需要 NavMeshAgent 组件！");
            return;
        }
        TryApplyPendingDestination();
        StartWandering();
    }


    void Update()
    {
        // 补发"早到 / 当时 agent 不可用"的远端目标点
        TryApplyPendingDestination();

        // 每一帧检测是否在移动，更新动画
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        // 判断是否在移动：速度 > 0.1 表示在移动（阈值可以自己调）
        bool isMoving = agent.velocity.sqrMagnitude > 0.01f;

        // 也可以通过剩余距离判断是否到达
        // bool isMoving = !IsAtDestination();

        // 设置 Animator 参数
        animator.SetBool("IsMove", isMoving);
    }


    public void StartWandering()
    {
        if (!gameObject.activeInHierarchy)
            return;
        if (wanderCoroutine != null)
            StopCoroutine(wanderCoroutine);
        wanderCoroutine = StartCoroutine(WanderRoutine());
    }
    /// <summary>【网络下发】应用房主给的移动目标 / 停下（只由 09 的 <c>SceneUnitMoveSink</c> 调用）。</summary>
    public void ApplyRemoteMove(Vector3 destination, bool stop)
    {
        if (stop)
        {
            _hasPendingDestination = false;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            return;
        }

        _pendingDestination = destination;
        _hasPendingDestination = true;
        TryApplyPendingDestination();
    }

    /// <summary>agent 真正可用时才落点。
    /// <para>⚠ 这里**不能**走 <c>UnitEventSub.PathRequest</c>（敌人那条统一漏斗）：<c>PathRequestManager</c> 由战场的
    /// <c>BattleManager</c> 创建，大厅里没人订阅 ⇒ 请求会被静默丢弃、NPC 一动不动。</para></summary>
    private void TryApplyPendingDestination()
    {
        if (!_hasPendingDestination || agent == null) return;
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        agent.SetDestination(_pendingDestination);
        _hasPendingDestination = false;
    }

    /// <summary>【房主】把"我要去哪 / 停下"发出去（转发的桥内部判 IsHost，成员与单机会自动忽略）。</summary>
    private void PublishMove(Vector3 destination, bool stop)
    {
        if (string.IsNullOrEmpty(_unitId)) return;
        BattleEventSub.SceneUnitMove(_unitId, destination, stop);
    }

    public void PauseWandering()
    {
        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = StartCoroutine(WanderRoutine());
        }
        if (RemoteDriven) return;      // 成员：位置的权威在房主（他暂停时会发 stop），本地别动 agent
        if (agent != null && agent.isActiveAndEnabled)
            agent.ResetPath();
        animator.SetBool("IsMove", false);
        PublishMove(transform.position, true);
    }
    public void StopWandering()
    {
        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = null;
        }
        if (RemoteDriven) return;
        if (agent != null && agent.isActiveAndEnabled)
            agent.ResetPath();
        animator.SetBool("IsMove", false);
        PublishMove(transform.position, true);
    }

    private IEnumerator WanderRoutine()
    {
        while (true)   
        {
            yield return new WaitForSeconds((0.5f+Random.value*0.5f)*wanderInterval);

            // ★ 联机成员：游荡的**决策权在房主**（本端只应用下发），否则两端各摇各的随机、位置永远对不上
            if (RemoteDriven) continue;

            // 在当前自身位置周围随机选一个有效点
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            randomOffset.y = 0; // 保持水平方向偏移，不上下飞

            Vector3 targetPos = transform.position + randomOffset;

            // 检查目标点是否在 NavMesh 上
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                PublishMove(hit.position, false);
            }
        }
    }
}
}
