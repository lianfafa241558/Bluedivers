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
            // ★ 走本单位的确定性流（否则两端闪到不同地方）
            Vector3 targetPos = transform.position + FPSGame.AI.EnemyRandom.For(this).InsideUnitCircle().ToVector3() * 40;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 30, NavMesh.AllAreas))
            {
                _ = AudioSvc.PlaySound(new(cilp, transform.position, 30, AudioGroups.Enemy, 1));
                FPSGame.Core.VfxPool.Creat(ps, transform.position);
                m_Controller.Pos = hit.position;
                FPSGame.Core.VfxPool.Creat(ps, hit.position);
            }
        }
    }
}