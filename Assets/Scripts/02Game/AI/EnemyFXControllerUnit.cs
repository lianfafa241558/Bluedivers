using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.AI
{
    public class EnemyFXControllerUnit : EnemyControllerFX
    {
        private Vector3 previousPosition;

        //Attack
        //MoveSpeed
        //Alerted
        //Ondamaged
        //Ondeath
        //IsActive - 这个好像基本上用不到，先留着


        private float lastFreeFx = 0;
        private float lastTriggerAttackTime;
        private int lastTriggerAttackName;

        private AudioSource m_moveAudio;
        protected override void Start()
        {
            base.Start();
            if (fxDic.TryGet(OccasionTypeEnum.Movement, out var value))
            {
                if (value.cilp.IsValid())
                {
                    m_moveAudio = AudioManager.CreatSource(gameObject, AudioGroups.Enemy);
                    m_moveAudio.clip = value.cilp;
                }

            }
            lastFreeFx = Time.time + Random.Range(20, 40);//纯表现层不需要同步
        }

        protected override void Update()
        {
            //float moveSpeed = GetActualVelocity();
            float moveSpeed = (m_EnemyController as EnemyController).Velocity.magnitude;
            //show = m_EnemyController.Velocity;
            //if (moveSpeed > 0)
            //{

            //更新动画师速度参数
            SetFloat(Constants.k_AnimMoveSpeedParameter, moveSpeed);
                //根据移动速度改变移动声音的音调
                //m_AudioSource.pitch = Mathf.Lerp(PitchDistortionMovementSpeed.Min, PitchDistortionMovementSpeed.Max,moveSpeed / m_EnemyController.NavMeshAgent.speed);

            //}
            if (m_moveAudio)
            {
                var InMove = moveSpeed > 0.05;
                if (InMove != m_moveAudio.isPlaying)
                {
                    //Debug.LogError("需要并播放:" + InMove + "当前状态" + m_moveAudio.isPlaying);
                    m_moveAudio.SetState(InMove);
                }
            }
            if (Time.time > lastFreeFx)
            {
                lastFreeFx = Time.time + Random.Range(20, 40);//纯表现层不需要同步
                TriggerFX(OccasionTypeEnum.Free, m_EnemyController.Pos, Quaternion.identity, transform);
            }
            base.Update();
        }

        /// <summary>
        /// 获取代理的实际速度（单位：单位/秒）
        /// 停止移动时返回0
        /// </summary>
        /// <returns>代理的实际速度</returns>
        public float GetActualVelocity()
        {
            // 计算实际移动距离
            Vector3 currentPosition = transform.position;
            float distance = Vector3.Distance(previousPosition, currentPosition);

            // 计算速度
            float velocity = distance / Time.deltaTime;

            // 更新记录
            previousPosition = currentPosition;

            return velocity;
        }


        /// <summary>
        /// 攻击时
        /// </summary>
        protected override void OnAttack(WeaponBaseController weapon)
        {
            base.OnAttack(weapon);
            //加了最小屏蔽时间，防止短时间触发多次attack
            int name = (weapon as WeaponEnemyController).AnimName;
            if (Time.time > lastTriggerAttackTime|| lastTriggerAttackName!= name)
            {
                lastTriggerAttackName = name;
                lastTriggerAttackTime = Time.time + 0.5f;
                //攻击没必要
                //TriggerFX(OccasionTypeEnum.Attack, m_EnemyController.AimPoint.position, Quaternion.identity, null);
                SetTrigger(name, true);
            }

        }

        /// <summary>
        /// 发现目标
        /// </summary>
        protected override void OnDetectedTarget()
        {
            base.OnDetectedTarget();
            SetTrigger(Constants.k_AnimAlertedParameter, true);
        }

    }
}