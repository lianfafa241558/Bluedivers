using UnityEngine;
using FPSGame.Utils;
using FPSGame.Data;
using FPSGame.Managers;

namespace FPSGame.Effect
{


/// <summary>
/// 实际上这是敌人纹理变体，但是名称错了
/// </summary>
[AddComponentMenu("变体/根据敌人类型的纹理变体", 30)]
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
}
