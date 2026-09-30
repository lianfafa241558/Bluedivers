using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using FPSGame.Utils;
using FPSGame.Audio;

namespace FPSGame.AI
{
    [AddComponentMenu("技能/闪现", 30)]
    public class UnitSkill_Blink : UnitSkill_Base
    {
        public AudioClip cilp;
        public GameObject ps;

        protected override void SkillStart()
        {
            Vector3 targetPos = transform.position + RandomUtils.InsideUnitCircle().ToVector3() * 40;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 30, NavMesh.AllAreas))
            {
                _ = AudioSvc.PlaySound(new(cilp, transform.position, 30, AudioGroups.Enemy, 1));
                FPSGame.GameContract.ServiceLocator.Vfx.Creat(ps, transform.position);
                m_Controller.Pos = hit.position;
                FPSGame.GameContract.ServiceLocator.Vfx.Creat(ps, hit.position);
            }
        }
    }
}