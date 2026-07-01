using Core;
using PEMaths;

using UnityEngine;
namespace Unity.FPS.Game
{
    public class HealthPlayer : Health 
    {

        [InspectorName("生命恢复范围")] public float HealthRestoreScale;
        [InspectorName("生命恢复速度")] public float HealthRestoreSpeed;

        [InspectorName("护盾恢复速度")] public float ShieldRestoreSpeed;
        [InspectorName("护盾恢复延迟")] public float ShieldDelay;

        [SerializeField] [InspectorName("护盾恢复音效")] AudioClip ShieldRestore;
        [SerializeField] [InspectorName("护盾回满音效")] AudioClip ShieldFull;
        [SerializeField] [InspectorName("护盾破碎音效")] AudioClip ShieldBreak;
        [SerializeField] [InspectorName("护盾受击音效")] AudioClip[] ShieldDamage; 

        AudioSource m_Audio;

        [Space]
        [DisplayField]
        [InspectorName("剩余护盾值")]
        [SerializeField]
        public int showShield;

        protected override void Start() {
            base.Start();
            CurrentShield = showShield=MaxShield;
            OnShieldDamaged += _OnDamaged;
            m_Audio=AudioSvc.CreatSource(gameObject,AudioGroups.General);
            m_Audio.clip = ShieldRestore;
            InvokeRepeating(nameof(Restore),Constants.LoginFrame.RawFloat, Constants.LoginFrame.RawFloat);
        }
        private void OnDestroy()
        {
            OnShieldDamaged -= _OnDamaged;
        }

        private void Restore() {
            if (m_IsDead) return;

            if (ShieldRestoreSpeed > 0 && CurrentShield < MaxShield && Time.time > m_LastHitTime + ShieldDelay) {
                RestoreShield(Time.fixedDeltaTime * ShieldRestoreSpeed);
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
            showShield = CurrentShield.RawInt;
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
            showShield=CurrentShield.RawInt;

        }

        /// <summary>复活</summary>
        public override void Revive()
        {
            CurrentHealth = Mathf.Max(MaxHealth/10,1);
            CurrentShield = 0;
            OnRevive?.Invoke();
            m_IsDead = false;
        }

        //protected override void HandleDeath() {
        //    base.HandleDeath();
        //}

    }
}
