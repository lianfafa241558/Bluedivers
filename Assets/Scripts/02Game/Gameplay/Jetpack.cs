using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 喷气靴组件
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Jetpack : MonoBehaviour
    {
        [Foldout("关联组件")]
        [CustomLabel("音频源")]
        public AudioSource AudioSource;

        [CustomLabel("喷气背包特效的粒子系统")]
        public ParticleSystem[] JetpackVfx;

        [Foldout("参数")]
        [CustomLabel("初始解锁")]
        public bool IsJetpackUnlockedAtStart = false;

        [CustomLabel("喷气力度")]
        public float JetpackAcceleration = 7f;

        [Range(0f, 1f)]
        [CustomLabel("抵消重力值的程度")]
        public float JetpackDownwardVelocityCancelingFactor = 1f;

        [Foldout("持续时间")]
        [CustomLabel("喷气维持时间")]
        public float ConsumeDuration = 3f;

        [CustomLabel("在地面补充所需的时间")]
        public float RefillDurationGrounded = 15f;

        [CustomLabel("在空中补充所需的时间")]
        public float RefillDurationInTheAir = 15f;

        [CustomLabel("开始补充前的延迟时间")]
        public float RefillDelay = 1f;

        [Foldout("音频")]
        [CustomLabel("使用喷气背包时播放的声音")]
        public AudioClip JetpackSfx;

        bool m_CanUseJetpack;
        PlayerController m_PlayerCharacterController;
        PlayerInputHandler m_InputHandler;
        float m_LastTimeOfUse;

        /// <summary>剩余燃料(0-1)</summary>
        public float CurrentFillRatio { get; private set; }

        /// <summary>是否启用</summary>
        public bool IsJetpackUnlocked { get; private set; }


        /// <summary> 喷气状态变化时 </summary>
        public UnityAction<bool> OnJetpackChange;


        public bool IsPlayergrounded() => m_PlayerCharacterController.IsGrounded;

        public UnityAction<bool> OnUnlockJetpack;

        void Start()
        {
            IsJetpackUnlocked = IsJetpackUnlockedAtStart;

            m_PlayerCharacterController = GetComponent<PlayerController>();
            m_InputHandler = GetComponent<PlayerInputHandler>();
  
            CurrentFillRatio = 1f;

            AudioSource.clip = JetpackSfx;
            AudioSource.loop = true;
        }

        void Update()
        {
            //开启条件
            //1.天上
            //2.按住空格
            //3.不是这一帧刚开始的跳跃
            //4.解锁喷气
            if (IsPlayergrounded())
            {
                m_CanUseJetpack = false;
            }
            else if (IsJetpackUnlocked&&
                !m_PlayerCharacterController.HasJumpedThisFrame && m_InputHandler.GetJumpInputDown())
            {
                m_CanUseJetpack = true;
                OnJetpackChange?.Invoke(true);
            }

            // jetpack usage
            bool jetpackIsInUse = m_CanUseJetpack && IsJetpackUnlocked && CurrentFillRatio > 0f &&
                                  m_InputHandler.GetJumpInputHeld();
            if (jetpackIsInUse)
            {
                // store the last time of use for refill delay
                m_LastTimeOfUse = Time.time;

                float totalAcceleration = JetpackAcceleration;

                //抵消重力
                totalAcceleration += m_PlayerCharacterController.GravityDownForce;

                if (m_PlayerCharacterController.CharacterVelocity.y < 0f)
                {
                    // handle making the jetpack compensate for character's downward velocity with bonus acceleration
                    totalAcceleration += ((-m_PlayerCharacterController.CharacterVelocity.y / Time.deltaTime) *
                                          JetpackDownwardVelocityCancelingFactor);
                }
                //将加速度应用于角色的速度
                m_PlayerCharacterController.CharacterVelocity += Vector3.up * totalAcceleration * Time.deltaTime;

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
                // 随着时间的推移，重新填充仪表
                if (IsJetpackUnlocked && Time.time - m_LastTimeOfUse >= RefillDelay)
                {
                    float refillRate = 1 / (m_PlayerCharacterController.IsGrounded
                        ? RefillDurationGrounded
                        : RefillDurationInTheAir);
                    CurrentFillRatio = CurrentFillRatio + Time.deltaTime * refillRate;
                }

                for (int i = 0; i < JetpackVfx.Length; i++)
                {
                    var emissionModulesVfx = JetpackVfx[i].emission;
                    emissionModulesVfx.enabled = false;
                }

                // keeps the ratio between 0 and 1
                CurrentFillRatio = Mathf.Clamp01(CurrentFillRatio);

                if (AudioSource.isPlaying)
                    AudioSource.Stop();
            }
        }

        public bool TryUnlock()
        {
            if (IsJetpackUnlocked)
                return false;

            OnUnlockJetpack.Invoke(true);
            IsJetpackUnlocked = true;
            m_LastTimeOfUse = Time.time;
            return true;
        }
    }
}