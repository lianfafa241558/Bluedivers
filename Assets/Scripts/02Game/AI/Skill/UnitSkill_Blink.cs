using System.Collections;
using System.Collections.Generic;
using Core;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using Utils;

namespace Unity.FPS.AI
{
    [AddComponentMenu("技能-闪现", 30)]
    public class UnitSkill_Blink : UnitSkill_Base
    {
        [InspectorName("受击后触发技能的时间")]
        public float DamageSkillTime;

        public AudioClip cilp;
        public GameObject ps;

        protected override void Init()
        {
            m_Controller.OnDamaged += OnDeamage;
            m_Controller.OnAttack += OnAttack;
        }

        protected override void Uninit()
        {
            m_Controller.OnDamaged -= OnDeamage;
            m_Controller.OnAttack -= OnAttack;
        }

        protected override void SkillStart()
        {
            Vector3 targetPos = transform.position + RandomUtils.InsideUnitCircle().ToVector3() * 40;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 30, NavMesh.AllAreas))
            {
                _ = AudioSvc.PlaySound(new(cilp, transform.position, 30, AudioGroups.Enemy, 1));
                VFXManager.Creat(ps, transform.position);
                m_Controller.Pos = hit.position;
                VFXManager.Creat(ps, hit.position);
            }
        }

        void OnDeamage(Collider _)
        {
            //假设8s接战，需要10s,受击改为2s
            //那触发应该是3+10=13s触发
            //8s受击了接战时间改成4-10+2=-4，最后的触发时间就变成了-4+10=6s
            m_LastDetectedTarget = Mathf.Min(m_LastDetectedTarget,Time.time- MeetDetectedTime+ DamageSkillTime);
        }
        void OnAttack(WeaponBaseController _)
        {
            m_LastDetectedTarget = Mathf.Min(m_LastDetectedTarget, Time.time - MeetDetectedTime + DamageSkillTime);
        }

    }
}