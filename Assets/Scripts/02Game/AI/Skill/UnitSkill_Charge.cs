using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
namespace Unity.FPS.AI
{
    /// <summary>
    /// 冲锋
    /// </summary>
    [AddComponentMenu("技能/冲锋", 30)]
    public class UnitSkill_Charge : UnitSkill_Base
    {
        public AudioClip cilp;
        protected override void SkillStart()
        {
            m_Controller.Speed.AddModifier(Game.ModifierType.Factor,1);
            Vector3 pos = m_Controller.CenterPos;
            _ = AudioManager.PlaySound(new(cilp, pos, 40, AudioGroups.Enemy, 1));
        }
        protected override void SkillEnd()
        {
            m_Controller.Speed.AddModifier(Game.ModifierType.Factor, -1);
        }
    }
}