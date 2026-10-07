using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Audio;
using FPSGame.Weapon;
namespace FPSGame.AI
{
    /// <summary>
    /// 冲锋
    /// </summary>
    [AddComponentMenu("技能/冲锋", 30)]
    public class UnitSkill_Charge : UnitSkill_Base
    {
        public AudioClip cilp;

        [InspectorName("踩踏挂点")]
        [SerializeField]
        private GameObject TramplePoint;

        protected override void SkillStart()
        {
            m_Controller.Speed.AddModifier(ModifierType.Factor,1);
            m_Controller.GetAttribute(UnitAttrType.AngularSpeed).AddModifier(ModifierType.Factor, -1);
            Vector3 pos = m_Controller.CenterPos;
            _ = AudioSvc.PlaySound(new(cilp, pos, 40, AudioGroups.Enemy, 1));

            // 冲锋期间激活踩踏挂点
            SetTramplePoint(true);
        }
        protected override void SkillEnd()
        {
            m_Controller.Speed.AddModifier(ModifierType.Factor, -1);
            m_Controller.GetAttribute(UnitAttrType.AngularSpeed).AddModifier(ModifierType.Factor, 1);

            SetTramplePoint(false);
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            // 冲锋中阵亡时兜底关闭，避免池化复用时挂点残留
            SetTramplePoint(false);
        }

        /// <summary>启用/停用踩踏挂点；启用时先下发伤害来源(自身)</summary>
        void SetTramplePoint(bool active)
        {
            if (!TramplePoint) return;
            if (active && TramplePoint.TryGetComponent(out IVfxEffect vfx))
            {
                vfx.SetOwner(m_Controller.gameObject, null, null, m_Controller.CenterPos);
            }
            TramplePoint.SetActive(active);
        }
    }
}