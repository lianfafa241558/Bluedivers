using System.Collections;
using System.Collections.Generic;
using GameContract;
using Unity.BaseTool;
using UnityEngine;
using Utils;

public class CreatEnemy : MonoBehaviour
{
    [SerializeField]
    float range, probability = 50;
    [SerializeField]
    [InspectorName("类型")]
    UnitTier tier;

    void Start()
    {
        if (BattleManager.Instance.BattleRandom.Bool(probability))
        {
            BattleManager.Instance.CreatUnit(tier,transform.position,range);
        }
        Destroy(gameObject);
    }
}
