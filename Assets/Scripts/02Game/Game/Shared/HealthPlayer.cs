using Core;
using PEMaths;
using Unity.BaseTool;
using UnityEngine;
namespace Unity.FPS.Game
{
    public class HealthPlayer : Health 
    {

        [CustomLabel("生命恢复范围")] public float HealthRestoreScale;
        [CustomLabel("生命恢复速度")] public float HealthRestoreSpeed;

        [CustomLabel("护盾恢复速度")] public float ShieldRestoreSpeed;
        [CustomLabel("护盾恢复延迟")] public float ShieldDelay;

        [SerializeField] [CustomLabel("护盾恢复音效")] AudioClip ShieldRestore;
        [SerializeField] [CustomLabel("护盾回满音效")] AudioClip ShieldFull;
        [SerializeField] [CustomLabel("护盾破碎音效")] AudioClip ShieldBreak;
        [SerializeField] [Tooltip("护盾受击音效")] AudioClip[] ShieldDamage;

        AudioSource m_Audio;
     
        protected override void Start() {
            base.Start();
            CurrentShield = MaxShield;
            OnShieldDamaged += _OnDamaged;
            m_Audio=AudioManager.CreatSource(gameObject,AudioGroups.General);
            m_Audio.clip = ShieldRestore;
        }
        private void OnDestroy()
        {
            OnShieldDamaged -= _OnDamaged;
        }

        private void FixedUpdate() {
            if (m_IsDead) return;
            if (ShieldRestoreSpeed > 0 && CurrentShield < MaxShield && Time.time > m_LastHitTime + ShieldDelay) {
                CurrentShield += (PEInt)(Time.fixedDeltaTime * ShieldRestoreSpeed);
                if (!m_Audio.isPlaying) m_Audio.Play();
            }
            if (CurrentShield >= MaxShield&& m_Audio.isPlaying)
            {
                m_Audio.Stop();
                m_Audio.PlayOneShot(ShieldFull);
            }
            if (HealthRestoreSpeed > 0 && GetHpRatio() < HealthRestoreScale) {
                CurrentHealth += (PEInt)(Time.deltaTime * HealthRestoreSpeed);
            }
        }


        void _OnDamaged(bool isBreak)
        {
            if(m_Audio.isPlaying) m_Audio.Stop();
            if (isBreak)
            {
                m_Audio.PlayOneShot(ShieldBreak);
            }
            else
            {
                m_Audio.PlayOneShot(ShieldDamage.RandomTake());
            }

        }

        /// <summary>复活</summary>
        public override void Revive()
        {
            CurrentHealth = MaxHealth/10;
            CurrentShield = 0;
            OnRevive?.Invoke();
            m_IsDead = false;
        }

        //protected override void HandleDeath() {
        //    base.HandleDeath();
        //}

    }
}
