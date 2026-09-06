using System.Collections;
using System.Collections.Generic;
using GameContract;

using UnityEngine;
using Utils;

[AddComponentMenu("创建场景物体/单位")]
/// <summary>
/// 单位创建器，单例模式，随创随用。
/// 单例对象持续存活，不随某次创建结束而销毁，避免创建器生命周期竞态导致 CreateInfo 被吞。
/// </summary>
public class CreatEnemy : MonoBehaviour
{
    /// <summary>
    /// 每帧最长阻塞时间
    /// </summary>
    private const float maxTimePerFrame = 0.01f;

    private static CreatEnemy instance;

    /// <summary>
    /// 待处理创建队列。static 保证所有创建器写入同一个队列，
    /// 不依赖 instance 指向的对象的实例字段，避免单例被替换时队列丢失。
    /// </summary>
    private static Queue<CreateInfo> queue;

    [SerializeField]
    float range;
    [Range(0,100)]
    [SerializeField]
    float probability = 50;
    [SerializeField]
    [InspectorName("类别")]
    UnitTier tier;

    private void Awake()
    {
        var info = new CreateInfo() {
            tier = tier,
            range = range,
            probability = probability,
            vector = transform.position,
        };

        if (!instance)
        {
            instance = this;
            // 静态队列懒初始化，避免 instance 存在但 queue 未建导致空引用
            queue = new();
        }

        // 无论是否首个，都入队到静态队列；非单例对象自身销毁即可
        queue.Enqueue(info);
        if (instance != this)
        {
            Destroy(gameObject);
        }
    }



    private void Update()
    {
        if (GameRoot.GameState != Core.GameStateEnum.Game) return;

        if (queue == null || queue.Count == 0)
        {
            return;
        }

        float startTime = Time.realtimeSinceStartup;
        while (queue.Count > 0)
        {
            var info = queue.Dequeue();
            if (BattleManager.Instance.BattleRandom.Bool(info.probability))
            {
                //Debug.LogError("创建类型为  "+ info.tier+"单位"+"  在" + info.vector+"  范围" + info.range);
                BattleManager.Instance.CreatUnit(info.tier, info.vector, info.range);
            }
            if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
            {
                return;
            }
        }
    }

    private struct CreateInfo{
        public UnitTier tier;
        public float range;
        public Vector3 vector;
        public float probability;
    }

}
