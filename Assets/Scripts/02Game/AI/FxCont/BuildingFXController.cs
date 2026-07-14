using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
namespace Unity.FPS.AI
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
                Debug.LogError("发起攻击");
                //加了最小屏蔽时间，防止短时间触发多次attack
                int name = (weapon as WeaponEnemyController).AnimName;
                if (Time.time > lastTriggerAttackTime || lastTriggerAttackName != name)
                {
                    lastTriggerAttackName = name;
                    lastTriggerAttackTime = Time.time + 0.5f;
                    //攻击没必要
                    //TriggerFX(OccasionTypeEnum.Attack, m_EnemyController.AimPoint.position, Quaternion.identity, null);
                    Debug.LogError("准备触发");
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

            // rendererSet 中每一项的 material 设为该材质
            for (int i = 0; i < rendererSet.Count; i++)
            {
                rendererSet[i].material = firstMat;
            }
        }
#endif
    }
}