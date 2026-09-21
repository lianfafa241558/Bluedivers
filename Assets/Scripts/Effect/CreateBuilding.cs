using GameContract;
using UnityEngine;

[AddComponentMenu("创建场景物体/建筑类单位")]
public class CreatBuilding : MonoBehaviour
{
    [SerializeField]
    EnemyActorVariant_SO data;
    [SerializeField]
    MapActorVariant_SO data2;
    void Start()
    {
        var pos = transform.position;
        var rotation = transform.rotation;
        var parent = transform.parent;

        BattleManager.EnqueueInit(() =>
        {
            GameObject tmp = null;
            if (data)
            {
                tmp = data.Get(TaskManager.Instance.EnemyVarietyType);
            }
            else if(data2)
            {
                tmp = data2.Get(TaskManager.Instance.MapId);
            }
            if (tmp == null) return;
            var go=Instantiate(tmp, pos, rotation, parent);
            if (go.TryGetComponent(out I_AIController cont)) cont.BirthDuration = 0;
            if (go.TryGetComponent(out I_Actor actor)) actor.IsFixed = true;
            
        });

        Destroy(gameObject);
    }
}
