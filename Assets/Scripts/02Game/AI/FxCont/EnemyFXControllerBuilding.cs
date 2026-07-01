using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
namespace Unity.FPS.AI
{



    public class EnemyFXControllerBuilding : EnemyControllerFX
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
    }
}