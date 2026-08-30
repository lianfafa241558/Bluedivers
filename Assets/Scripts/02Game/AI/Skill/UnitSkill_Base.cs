using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;
using Unity.Collections;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.AI
{
    public abstract class UnitSkill_Base : TickBehaviour
    {

        [InspectorName("冷却")]
        public int Cool;
        [InspectorName("技能持续")]
        public int Duration;

        /// <summary>
        /// 释放类型：被动=冷却结束即释放；主动=需要目标且接战时间达标；受击=受击/攻击后提前接战时间触发
        /// </summary>
        [InspectorName("释放类型")]
        public SkillReleaseType releaseType = SkillReleaseType.Active;

        /// <summary>
        /// 需要接战时间（仅主动/受击类型使用）
        /// </summary>
        [InspectorName("需要接战时间")]
        [Compare("releaseType", (int)SkillReleaseType.Passive, CompareOperate.NotEqual)]
        public int MeetDetectedTime;

        /// <summary>
        /// 触发范围（仅主动/受击类型使用）
        /// </summary>
        [InspectorName("触发范围")]
        [Compare("releaseType", (int)SkillReleaseType.Passive, CompareOperate.NotEqual)]
        public float Range;

        /// <summary>
        /// 受击后触发技能的时间（仅受击类型生效）：受击/攻击后接战时间提前到此值触发
        /// </summary>
        [InspectorName("受击后触发技能的时间")]
        [Compare("releaseType", (int)SkillReleaseType.Damaged, CompareOperate.Equal)]
        public float DamageSkillTime;

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
            //受击类型：受击/攻击后提前接战时间，加速技能释放
            if (releaseType == SkillReleaseType.Damaged)
            {
                m_Controller.OnDamaged += OnDamagedSkill;
                m_Controller.OnAttack += OnAttackSkill;
            }
            Init();
        }
        private void OnDestroy()
        {
            if (m_Controller == null) return;
            m_Controller.OnDetectedTarget -= OnDetectedTarget;
            m_Controller.OnLostTarget -= OnLostTarget;
            m_Controller.OnDie -= OnDeath;
            if (releaseType == SkillReleaseType.Damaged)
            {
                m_Controller.OnDamaged -= OnDamagedSkill;
                m_Controller.OnAttack -= OnAttackSkill;
            }

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

                //被动技能无需目标/接战时间，冷却结束即可释放
                if (releaseType == SkillReleaseType.Passive) return true;

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
        /// 受击后提前接战时间，加速技能释放（受击类型专用）
        /// </summary>
        void OnDamagedSkill(Collider _)
        {
            m_LastDetectedTarget = Mathf.Min(m_LastDetectedTarget, Time.time - MeetDetectedTime + DamageSkillTime);
        }

        /// <summary>
        /// 攻击后提前接战时间，加速技能释放（受击类型专用）
        /// </summary>
        void OnAttackSkill(WeaponBaseController _)
        {
            m_LastDetectedTarget = Mathf.Min(m_LastDetectedTarget, Time.time - MeetDetectedTime + DamageSkillTime);
        }

        /// <summary>
        /// 允许释放
        /// </summary>
        /// <returns></returns>
        protected bool CanExecute()
        {
            if (nowCoolTime > 0) return false;

            //被动：冷却结束即释放，无需目标/接战时间
            if (releaseType == SkillReleaseType.Passive) return true;

            //主动/受击：需要目标且接战时间达标（受击由技能内订阅事件提前 m_LastDetectedTarget 触发）
            return MeetDetectedTime + m_LastDetectedTarget < Time.time
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


        /// <summary>
        /// 技能释放类型
        /// </summary>
        public enum SkillReleaseType
        {
            /// <summary>被动：冷却结束就释放，无需目标</summary>
            [InspectorName("被动")] Passive,
            /// <summary>主动：需要目标且接战时间达标</summary>
            [InspectorName("主动")] Active,
            /// <summary>受击：受击/攻击后提前接战时间触发</summary>
            [InspectorName("受击")] Damaged,
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