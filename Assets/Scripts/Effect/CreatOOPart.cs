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
        if (BattleManager.Instance.BattleRandom.Bool(probability))
        {
            Instantiate(PropertyManager.Instance.CreatOOPart(), transform.position + RandomUtils.RandomVector2().ToVector3() * range, default, transform.parent);

        }
        Destroy(gameObject);
    }
}
