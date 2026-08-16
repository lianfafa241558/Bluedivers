using System;
using System.Collections.Generic;
using FPSGame.Furn;
using GameContract;
using PEMaths;

using UnityEngine;
using UnityEngine.Events;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 喷气靴组件
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Jetpack : BagBase
    {

        #region 参数


        [InspectorName("喷气背包特效的粒子系统")]
        public ParticleSystem[] JetpackVfx;

        [InspectorName("抵消重力值的程度")]
        [Range(0f, 1f)]
        public float JetpackDownwardVelocityCancelingFactor = 1f;

        [InspectorName("喷气力度")]
        [Range(0, 10)]
        public float JetpackAcceleration = 7f;

        [InspectorName("喷气维持时间")]
        [Range(0, 10)]
        public float ConsumeDuration = 3f;



        #endregion






        protected bool m_CanUse;//满足使用的条件
        AudioSource AudioSource;


        private void Awake()
        {
            AudioSource = GetComponent<AudioSource>();
        }

        protected override void Update()
        {
            base.Update();//恢复充电总是执行

            //开启条件
            //1.天上
            //2.按住空格(跳跃键)
            //3.不是这一帧刚开始的跳跃
            if (Owner.IsValid() && m_PlayerCharacterController.IsGrounded&& m_CanUse)
            {
                m_CanUse = false;
            }
            else if (Owner.IsValid() &&
                !m_PlayerCharacterController.HasJumpedThisFrame && m_InputHandler.GetJumpInputDown() && !m_CanUse)
            {
                m_CanUse = true;
                OnStateChange?.Invoke(true);

            }

            // jetpack usage
            bool jetpackIsInUse = m_CanUse && Owner.IsValid() && CurrentFillRatio > 0f &&
                                  m_InputHandler.GetJumpInputHeld();
            if (jetpackIsInUse)
            {
                // store the last time of use for refill delay
                m_LastTimeOfUse = Time.time;

                PEInt totalAcceleration = new(JetpackAcceleration);

                //抵消重力
                totalAcceleration += (PEInt)m_PlayerCharacterController.GravityDownForce;

                if (m_PlayerCharacterController.CharacterVelocity.y < 0)
                {
                    // handle making the jetpack compensate for character's downward velocity with bonus acceleration
                    totalAcceleration += ((-m_PlayerCharacterController.CharacterVelocity.y / (PEInt)Time.deltaTime) *
                                          (PEInt)JetpackDownwardVelocityCancelingFactor);
                }
                //将加速度应用于角色的速度
                m_PlayerCharacterController.CharacterVelocity += PEVector3.Up * totalAcceleration * (PEInt)Time.deltaTime;

                // consume fuel
                CurrentFillRatio = CurrentFillRatio - (Time.deltaTime / ConsumeDuration);

                for (int i = 0; i < JetpackVfx.Length; i++)
                {
                    var emissionModulesVfx = JetpackVfx[i].emission;
                    emissionModulesVfx.enabled = true;
                }

                if (!AudioSource.isPlaying)
                    AudioSource.Play();
            }
            else
            {
                for (int i = 0; i < JetpackVfx.Length; i++)
                {
                    var emissionModulesVfx = JetpackVfx[i].emission;
                    emissionModulesVfx.enabled = false;
                }



                if (AudioSource.isPlaying)
                    AudioSource.Stop();
            }
        }


 

    }
}