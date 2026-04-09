using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameContract;
using PEMaths;
using UnityEngine;
using Utils;

public class GuidedShelling : MonoBehaviour
{

    [Header("基础设置")]
    [Tooltip("没有目标时，搜索敌人的范围 A")]
    [SerializeField]
    private float searchRange = 30f;
    [SerializeField]
    private float searchInterval = 1f; 

    [Tooltip("找到目标后，移动的速度 B")]
    [SerializeField]
    private float moveSpeed = 5f;

    [Header("基础设置")]
    [SerializeField]
    private Transform guideSource;//炮击引导源

    private I_Actor currentTarget;

    private float searchTimer = 0;

    private void Update()
    {

        if (currentTarget == null|| currentTarget.ActorState== Core.ActorState.Dead)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchInterval)
            {
                SearchEnemy();
                searchTimer = 0;
            }
        }
        else
        {
            guideSource.LookAt(transform);
        }
    }

    private void FixedUpdate()
    {
        if (currentTarget != null)
        {
            // 向目标移动
            MoveToTarget();
        }
    }

    /// <summary>
    /// 搜索范围内的敌人（使用你提供的接口）
    /// </summary>
    private void SearchEnemy()
    {
        // 你项目里的搜索接口
        List<I_Actor> units = BattleManager.Instance.FindUnits(
            new PECircle(new(transform.position.ToVector2()), new(searchRange)),
            TargetCfg.Enemy
        );

        // 找到敌人 → 锁定第一个
        if (units != null && units.Count > 0)
        {
            //优先锁大型敌人，建筑的权重降低
            currentTarget=units.OrderByDescending(item=>item.HalfRange*(item.HasFlag(Core.ActorFlag.Nest)?0.2f:1)).FirstOrDefault();
            //currentTarget = units[0];
        }
    }

    private Vector3 _vel = Vector3.zero;
    /// <summary>
    /// 匀速向目标移动
    /// </summary>
    private void MoveToTarget()
    {
        // 目标丢失
        if (!currentTarget.IsValid()) return;

        float distance = Vector3.Distance(transform.position, currentTarget.Pos);

        // 距离足够近 → 直接到位，停止抖动
        if (distance < 0.25f)
        {
            transform.position = currentTarget.Pos;
            // 清空速度，防止惯性继续飘
            _vel = Vector3.zero;
            return;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                currentTarget.Pos,
                ref _vel,
                0.2f,
                moveSpeed
            );
        }

        //transform.position = Vector3.MoveTowards(transform.position, currentTarget.Pos, moveSpeed * Time.deltaTime);
        
    }

}
