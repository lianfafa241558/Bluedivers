using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCWalk : MonoBehaviour
{ 
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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (agent == null)
        {
            Debug.LogError("WanderController: 需要 NavMeshAgent 组件！");
            return;
        }
        StartWandering();
    }


    void Update()
    {
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
        if (wanderCoroutine != null)
            StopCoroutine(wanderCoroutine);
        wanderCoroutine = StartCoroutine(WanderRoutine());
    }
    public void PauseWandering()
    {
        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = StartCoroutine(WanderRoutine());
        }
        if (agent != null && agent.isActiveAndEnabled)
            agent.ResetPath();
        animator.SetBool("IsMove", false);
    }
    public void StopWandering()
    {
        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = null;
        }
        if (agent != null && agent.isActiveAndEnabled)
            agent.ResetPath();
        animator.SetBool("IsMove", false);
    }

    private IEnumerator WanderRoutine()
    {
        while (true)   
        {
            yield return new WaitForSeconds((0.5f+Random.value*0.5f)*wanderInterval);

            // 在当前自身位置周围随机选一个有效点
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            randomOffset.y = 0; // 保持水平方向偏移，不上下飞

            Vector3 targetPos = transform.position + randomOffset;

            // 检查目标点是否在 NavMesh 上
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
}
