using System.Collections;
using System.Collections.Generic;
using FPSGame.AI;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;

namespace FPSGame.Gameplay
{

/// <summary>
/// NPC 在 NavMesh 上游荡行走。
/// <para>▍联机：<see cref="RemoteDriven"/> = true（成员端）时**不自己决策**，只应用房主下发的目标点；
/// 房主决策/停下时发 <see cref="BattleEventBus.OnSceneUnitMove"/>，由 09 的场景桥收发。</para>
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

    /// <summary>
    /// 在场 NPC 自记一份（供 09 的 <c>SceneUnitMoveSink</c> 按 Id 查）。
    /// <para>▍为什么不能依赖 <c>ActorsManager.Actors</c>：那张表在 <c>ActorsManager.Awake</c> 里被 <c>Actors = new()</c> **整个换新**，
    /// 而场景里先于它 Awake 的 NPC 登记的是旧表 ⇒ 之后永远查不到（2026-10-09 实测：客机 NPC 全员罚站、指令全卡在 sink 暂存区）。</para>
    /// </summary>
    static readonly List<NPCWalk> s_all = new List<NPCWalk>();

    /// <summary>同步键（<c>Actor.Id</c>；空 = 不参与同步）。</summary>
    public string UnitId => _unitId;

    /// <summary>按同步键找在场的 NPC（只在 NPCWalk 之间找 ⇒ 天然不会撞上同名的玩家角色）。</summary>
    public static NPCWalk FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < s_all.Count; ++i)
        {
            var w = s_all[i];
            if (w == null) continue;
            if (string.Equals(w._unitId, id, System.StringComparison.Ordinal)) return w;
        }
        return null;
    }

    private void OnEnable()
    {
        if (!s_all.Contains(this)) s_all.Add(this);
    }

    private void OnDisable()
    {
        s_all.Remove(this);
    }

    /// <summary>联机落点组件（2026-10-08 接入）：网络目标点的缓存 / 重试全在它里面。
    /// <para>⚠ 本类的落地通道**不能**走 <c>UnitEventBus.PathRequest</c>（<c>PathRequestManager</c> 由战场的
    /// <c>BattleManager</c> 创建，大厅里没人订阅 ⇒ 请求会被静默丢弃、NPC 一动不动）
    /// ⇒ **不注入 applyHandler**，用组件的默认实现（<c>NavMeshAgent.SetDestination</c>，未就绪时自己等）。</para></summary>
    private SyncedNavMover _mover;

    private SyncedNavMover Mover
    {
        get
        {
            if (_mover == null)
            {
                if (!TryGetComponent(out _mover)) _mover = gameObject.AddComponent<SyncedNavMover>();
                // 故意不设 applyHandler：见上面注释（大厅里没有 PathRequestManager）
            }
            return _mover;
        }
    }

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
        // ⚠ "补发早到的远端目标点"由 SyncedNavMover 自己的 Update 负责（2026-10-08），这里不再驱动
        StartWandering();
    }


    void Update()
    {
        // ⚠ "补发远端目标点"已挪进 SyncedNavMover 自己的 Update（2026-10-08）

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
            // 清待落地点 + ResetPath（组件内部已判 "agent 未就绪"）
            Mover.Stop();
            return;
        }

        Mover.ApplyRemoteDestination(destination);   // 缓存 + 立即尝试 + 失败则每帧重试
    }

    /// <summary>【房主】把"我要去哪 / 停下"发出去（转发的桥内部判 IsHost，成员与单机会自动忽略）。</summary>
    private void PublishMove(Vector3 destination, bool stop)
    {
        if (string.IsNullOrEmpty(_unitId)) return;
        BattleEventBus.SceneUnitMove(_unitId, destination, stop);
    }

    public void PauseWandering()
    {
        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = StartCoroutine(WanderRoutine());
        }
        if (RemoteDriven) return;      // 成员：位置的权威在房主（他暂停时会发 stop），本地别动 agent
        Mover.Stop();                  // 顺带清掉待落地点：否则"暂停"会被下一帧的补发顶掉
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
        Mover.Stop();
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
                Mover.SetDestination(hit.position);   // 本端决策也走组件：agent 未就绪时先存着、就绪后再落地
                PublishMove(hit.position, false);
            }
        }
    }
}
}
