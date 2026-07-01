using System.Collections;
using System.Collections.Generic;
using GameContract;

using UnityEngine;
using Utils;

/// <summary>
/// 单位创建器，临时单例，随创随用
/// </summary>
public class CreatEnemy : MonoBehaviour
{
    /// <summary>
    /// 每帧最长阻塞时间
    /// </summary>
    private const float maxTimePerFrame = 0.01f;

    private static CreatEnemy instance;
    private Queue<CreateInfo> queue;

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
            queue = new();
            queue.Enqueue(info);
        }
        else
        {
            instance.queue.Enqueue(info);
            Destroy(gameObject);
        }
    }



    private void Update()
    {
        if (GameRoot.GameState != Core.GameStateEnum.Game) return;
        float startTime = Time.realtimeSinceStartup;
        while (queue.Count > 0)
        {
            var info = queue.Dequeue();
            if (BattleManager.Instance.BattleRandom.Bool(info.probability))
            {
                BattleManager.Instance.CreatUnit(info.tier, info.vector, info.range);
            }
            if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
            {
                return;
            }
        }
       
        if (queue.Count==0) {
            instance = null;
            Destroy(gameObject); 
        }
    }

    private struct CreateInfo{
        public UnitTier tier;
        public float range;
        public Vector3 vector;
        public float probability;
    }

}
