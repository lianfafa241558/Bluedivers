using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using FPSGame.Utils;
using FPSGame.Managers;

namespace FPSGame.Effect
{

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
        Debug.LogError("自注册");
        BattleManager.EnqueueInit(() =>
        {
            if (BattleManager.Instance.BattleRandom.Bool(copiedProbability))
            {
                Debug.LogError("创建");
                Instantiate(prefabs.RandomTake(BattleManager.Instance.BattleRandom), pos, default, parent);
            }
        });

        Destroy(gameObject);
    }
}
}
