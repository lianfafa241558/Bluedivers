using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Utils;

[AddComponentMenu("创建场景物体/场景物品")]
public class CreatSupple : MonoBehaviour
{
    [SerializeField]
    float range, probability=50;
    [SerializeField]
    GameObject[] prefabs;

    void Start()
    {
        var pos = transform.position + RandomUtils.RandomVector2().ToVector3() * range;
        var parent = transform.parent;
        var copiedProbability = probability;

        BattleManager.EnqueueInit(() =>
        {
            if (BattleManager.Instance.BattleRandom.Bool(copiedProbability))
            {
                Instantiate(prefabs.RandomTake(BattleManager.Instance.BattleRandom), pos, default, parent);
            }
        });

        Destroy(gameObject);
    }
}
