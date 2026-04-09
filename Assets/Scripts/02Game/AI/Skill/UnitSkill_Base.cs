using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using Unity.BaseTool;
using UnityEngine;
using Utils;

namespace Unity.FPS.AI
{
    public abstract class UnitSkill_Base : TickBehaviour
    {

        [CustomLabel("冷却")]
        public int Cool;
        [CustomLabel("技能持续")]
        public int Duration;
        [CustomLabel("需要接战时间")]
        public int MeetDetectedTime;
        [CustomLabel("触发范围")]
        public float Range;

        [CustomLabel("标旗")]
        [SerializeField]
        protected new SkillTag tag;


        protected EnemyController m_Controller;
        [SerializeField]
        protected float nowCoolTime;
        [SerializeField]
        protected float nowDurationTime;

        protected bool m_HaveTarget;
        [SerializeField]
        protected float m_LastDetectedTarget;

        bool isDead;

        protected sealed override void Start()
        {
            base.Start();
            m_Controller = GetComponent<EnemyController>();
            m_Controller.OnDetectedTarget += OnDetectedTarget;
            m_Controller.OnLostTarget += OnLostTarget;
            m_Controller.OnDie += OnDeath;
            nowCoolTime = 0;
            isDead = false;
            Init();
        }
        private void OnDestroy()
        {
            m_Controller.OnDetectedTarget -= OnDetectedTarget;
            m_Controller.OnLostTarget -= OnLostTarget;
            m_Controller.OnDie -= OnDeath;

            Uninit();
        }

        protected virtual void Init() { }
        protected virtual void Uninit() { }

        public override bool Tick()
        {
            if (isDead) return true;

            if (CanExecute())
            {
                ResetCool();
                nowDurationTime = Duration;
                SkillStart();
            }
            if (nowDurationTime > 0)
            {
                SkillTick();
                if ((nowDurationTime -= TickTime) <= 0)
                {
                    SkillEnd();
                }
            }
            else
            {
                TickCool();

                if (HaveTag(SkillTag.MeetTargetInRange))
                {
                    if(!HaveTarget(out var actor)//找不到目标
                        && !TargetVaild(actor)//目标不合法
                    ){
                        ResetDetectedTime();
                    }
                }
                
            }


            return true;
        }
        protected abstract void SkillStart();
        protected virtual void SkillEnd(){ }
        protected virtual void SkillTick() { }

        /// <summary>冷却计时</summary>
        protected void TickCool()
        {
            if (nowCoolTime >0) nowCoolTime-=TickTime;
        }

        /// <summary>重置冷却</summary>
        protected void ResetCool()
        {
            nowCoolTime = Cool;
        }
        /// <summary>重置接战时间</summary>
        protected void ResetDetectedTime()
        {
            m_LastDetectedTarget = Time.time;
        }


        /// <summary>发现目标 </summary>
        protected virtual void OnDetectedTarget()
        {
            m_HaveTarget = true;
            ResetDetectedTime();
        }

        /// <summary>丢失目标</summary>
        protected virtual void OnLostTarget()
        {
            m_HaveTarget = false;
        }

        /// <summary>发现目标 </summary>
        protected virtual void OnDeath()
        {
            isDead = true;
        }


        protected bool CanExecute()
        {
            return nowCoolTime <= 0
                && MeetDetectedTime + m_LastDetectedTarget < Time.time
                && HaveTarget(out var actor)
                && TargetVaild(actor);
        }

        protected bool HaveTag(SkillTag t) => tag.HasFlag(t);

        protected bool HaveTarget(out I_Actor actor)
        {
            actor = m_Controller.Target.Actor;
            return actor.IsValid() && m_Controller.IsSeeingTarget;
        }
        protected bool TargetVaild(I_Actor actor)
        {
            return !HaveTag(SkillTag.TargetIsPlayer) || actor.Type.HasFlag(UnitTypeEnum.Player | UnitTypeEnum.Friend);
        }


        [System.Flags]
        protected enum SkillTag
        {
            /// <summary>只对玩家释放</summary>
            [CustomLabel("只对玩家释放")] TargetIsPlayer = 1 << 2,
            /// <summary>离开范围重置接战时间</summary>
            [CustomLabel("离开范围重置接战时间")] MeetTargetInRange = 1 << 4,

            /// <summary>丢失目标时重新冷却</summary>
            //[CustomLabel("丢失目标时重新冷却")]LostTargetReset = 1 << 0,
            /// <summary>发现目标时重新冷却</summary>
            //[CustomLabel("发现目标时重新冷却")] DetectedTargetReset = 1 << 1,

            /// <summary>冷却需要目标</summary>
            //[CustomLabel("冷却需要目标")] CoolMeetTarget = 1 << 3,

        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, Range);
            //Tool.DrawLabel(transform.position+Range*Vector3.forward,"技能范围",Time.deltaTime);
        }
    }
}