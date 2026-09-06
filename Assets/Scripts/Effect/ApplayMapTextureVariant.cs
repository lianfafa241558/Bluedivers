using UnityEngine;
using Utils;


public class ApplayMapTextureVariant: MonoBehaviour
{
    [SerializeField]
    private EnemyTextureVariant_SO data;

    private void Awake()
    {
        MpbController mpb = new(transform);
        mpb.Set("_BaseMap", data.Get(TaskManager.Instance.EnemyVarietyType)).Apply();

        Destroy(this);
    }


}