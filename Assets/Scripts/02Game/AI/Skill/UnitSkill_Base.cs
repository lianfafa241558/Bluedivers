using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;
using Unity.Collections;
using UnityEngine;
using Utils;

namespace Unity.FPS.AI
{
    public abstract class UnitSkill_Base : TickBehaviour
    {

        [InspectorName("冷却")]
        public int Cool;
        [InspectorName("技能持续")]
        public int Duration;
        [InspectorName("需要接战时间")]
        public int MeetDetectedTime;
        [InspectorName("触发范围")]
        public float Range;

        [InspectorName("标旗")]
        [SerializeField]
        protected new SkillTag tag;

        [DisplayField]
        [InspectorName("控制器")]
        [SerializeField]
        protected EnemyController m_Controller;
        [DisplayField]
        [InspectorName("当前冷却时间")]
        [SerializeField]
        protected float nowCoolTime;
        [DisplayField]
        [InspectorName("当前持续时间")]
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
            if (m_Controller == null) return;
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

                if (m_HaveTarget&&HaveTag(SkillTag.MeetTargetInRange))
                {
                    if(!HaveTarget(out var actor)//找不到目标
                        || !TargetVaild(actor)//目标不符合
                        || Vector3.Distance(transform.position, m_Controller.Target.Pos) > Range//超出技能范围
                    ){
                        /*
                        Debug.LogError(gameObject + "重置脱战时间,原因:"
                            +"存在目标?"+ m_Controller.Target.Actor
                            + "目标可见 ?"+m_Controller.IsSeeingTarget
                            + "目标无效?"+ (!TargetVaild(actor))
                            +" 超出范围?"+(Vector3.Distance(transform.position, m_Controller.Target.Pos) > Range), gameObject);
                        */ResetDetectedTime();
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

        /// <summary>
        /// 允许释放
        /// </summary>
        /// <returns></returns>
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
            return actor.IsValid()
                && (!HaveTag(SkillTag.TargetIsPlayer)
                || actor.Type.HasFlag(UnitTypeEnum.Player)
                || actor.Type.HasFlag(UnitTypeEnum.Friend));
        }


        [System.Flags]
        protected enum SkillTag
        {
            /// <summary>只对玩家释放</summary>
            [InspectorName("只对玩家释放")] TargetIsPlayer = 1 << 2,
            /// <summary>离开范围重置接战时间</summary>
            [InspectorName("离开范围重置接战时间")] MeetTargetInRange = 1 << 4,

            /// <summary>丢失目标时重新冷却</summary>
            //[InspectorName("丢失目标时重新冷却")]LostTargetReset = 1 << 0,
            /// <summary>发现目标时重新冷却</summary>
            //[InspectorName("发现目标时重新冷却")] DetectedTargetReset = 1 << 1,

            /// <summary>冷却需要目标</summary>
            //[InspectorName("冷却需要目标")] CoolMeetTarget = 1 << 3,

        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, Range);
            //Tool.DrawLabel(transform.position+Range*Vector3.forward,"技能范围",Time.deltaTime);
        }
    }
}