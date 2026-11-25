using System.Collections;
using UnityEngine;
using FPSGame.AI;
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
        private static readonly int BaseMapPid = Shader.PropertyToID("_BaseMap");

        [SerializeField]
        private EnemyTextureVariant_SO data;
        private void Awake()
        {
            Texture tex = data.Get(TaskManager.Instance.EnemyVarietyType);
            var renderers = GetComponentsInChildren<Renderer>(true);
            MpbController mpb = new(renderers);
            //Debug.Log($"{gameObject}敌人类型{TaskManager.Instance.EnemyVarietyType}，取值{tex}",gameObject);
            mpb.Set("_BaseMap", tex).Apply();

            StartCoroutine(StampToSharedSlots(tex, renderers));
        }

        /// <summary>
        /// 变体纹理是**长期外观**：受击闪白/溶解按下标 Flush 时会把渲染器级那块整块顶掉
        /// （现象 = 挨一下打就永久变回默认纹理）。槽位表在 FxControllerBase.Start 才建 ⇒ 等一帧再补写。
        /// </summary>
        private IEnumerator StampToSharedSlots(Texture tex, Renderer[] renderers)
        {
            yield return null;
            var fx = GetComponentInParent<FxControllerBase>();
            if (fx != null)
            {
                for (int i = 0, l = renderers.Length; i < l; ++i)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    for (int k = 0, n = r.sharedMaterials.Length; k < n; ++k)
                    {
                        var blk = fx.GetSharedSlotBlock(r, k);
                        if (blk == null) continue;   // 非生效材质下标，闪白碰不到它，渲染器级那块够用
                        blk.SetTexture(BaseMapPid, tex);
                        fx.MarkSlotDirty(r, k);
                    }
                }
            }
            Destroy(this);
        }
    }
}
