using GameContract;
using UnityEngine;

[AddComponentMenu("创建场景物体/建筑类单位")]
public class CreatBuilding : MonoBehaviour
{
    [SerializeField]
    EnemyActorVariant_SO data;

    void Start()
    {
        var pos = transform.position;
        var rotation = transform.rotation;
        var parent = transform.parent;

        BattleManager.EnqueueInit(() =>
        {
            var go=Instantiate(data.Get(TaskManager.Instance.EnemyVarietyType), pos, rotation, parent);
            go.GetComponent<I_AIController>().BirthDuration = 0;
            go.GetComponent<I_Actor>().IsFixed = true;
            
        });

        Destroy(gameObject);
    }
}
