using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
namespace FPSGame.AI
{



    public class BuildingFXController : EnemyControllerFX
    {
        [SerializeField]
        private bool UseAttack;

        //Ondamaged
        //Ondeath
        //IsActive

        private float lastTriggerAttackTime;
        private int lastTriggerAttackName;

        /// <summary>
        /// 攻击时
        /// </summary>
        protected override void OnAttack(WeaponBaseController weapon)
        {
            base.OnAttack(weapon);
            if (UseAttack)
            {
                //Debug.Log("发起攻击");
                //加了最小屏蔽时间，防止短时间触发多次attack
                int name = (weapon as WeaponEnemyController).AnimName;
                if (Time.time > lastTriggerAttackTime || lastTriggerAttackName != name)
                {
                    lastTriggerAttackName = name;
                    lastTriggerAttackTime = Time.time + 0.5f;
                    //攻击没必要
                    //TriggerFX(OccasionTypeEnum.Attack, m_EnemyController.AimPoint.position, Quaternion.identity, null);
                    //Debug.LogError("准备触发");
                    SetTrigger(name, true);
                }
            }
        }


#if UNITY_EDITOR
        [ContextMenu("AutoInit")]
        private void AutoInit()
        {
            // 搜索子物体中所有的 SkinnedMeshRenderer
            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderers.Length == 0) return;

            // 取第一个SkinnedMeshRenderer 的第一个材质
            var firstMat = skinnedRenderers[0].sharedMaterial;
            if (firstMat == null) return;

            // 材质下沉到组件：AutoInit 只填单位自身 fxMaterial（共享模板条目材质留空，由组件提供）
            fxMaterial = firstMat;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
    }
}