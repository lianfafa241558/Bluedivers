using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Utils;
[AddComponentMenu("创建场景物体/样品")]
public class CreatOOPart : MonoBehaviour
{
    [SerializeField]
    float range, probability=50;

    void Start()
    {
        var pos = transform.position + RandomUtils.RandomVector2().ToVector3() * range;
        var parent = transform.parent;
        var copiedProbability = probability;

        BattleManager.EnqueueInit(() =>
        {
            if (BattleManager.Instance.BattleRandom.Bool(copiedProbability))
            {
                Instantiate(PropertyManager.Instance.CreatOOPart(), pos, default, parent);
            }
        });

        Destroy(gameObject);
    }
}
