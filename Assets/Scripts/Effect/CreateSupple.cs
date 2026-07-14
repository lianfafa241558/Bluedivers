using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Utils;

[AddComponentMenu("创建场景物体/补给品")]
public class CreatSupple : MonoBehaviour
{
    [SerializeField]
    float range, probability=50;
    [SerializeField]
    GameObject prefab;

    void Start()
    {
        if (BattleManager.Instance.BattleRandom.Bool(probability))
        {
            Instantiate(prefab, transform.position + RandomUtils.RandomVector2().ToVector3() * range, default, transform.parent);

        }
        Destroy(gameObject);
    }
}
