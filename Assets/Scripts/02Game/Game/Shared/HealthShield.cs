
using Core;
using FPSGame.Attribute;
using PEMaths;

using UnityEngine;
namespace Unity.FPS.Game
{
    public class HealthShield : Health
    {
        [InspectorName("护盾恢复速度")] public float ShieldRestoreSpeed;
        [InspectorName("护盾恢复延迟")] public float ShieldDelay;

        [SerializeField][InspectorName("护盾恢复音效")] AudioClip ShieldRestore;
        [SerializeField][InspectorName("护盾回满音效")] AudioClip ShieldFull;
        [SerializeField][InspectorName("护盾破碎音效")] AudioClip ShieldBreak;
        [SerializeField][InspectorName("护盾受击音效")] AudioClip[] ShieldDamage;

        AudioSource m_Audio;

        [Space]
        [DisplayField]
        [InspectorName("剩余护盾值")]
        [SerializeField]
        public int showShield;

        protected override void Awake()
        {
            base.Awake();
            if (BattleManager.Instance && BattleManager.Instance.HaveBooster(BoosterType.Shield)) MaxShield += 10;
            CurrentShield = showShield = MaxShield;
            OnShieldDamaged += _OnDamaged;
            m_Audio = AudioSvc.CreatSource(gameObject, AudioGroups.General);
            m_Audio.clip = ShieldRestore;
            InvokeRepeating(nameof(Restore), Constants.LoginFrame.RawFloat, Constants.LoginFrame.RawFloat);
        }
        private void OnDestroy()
        {
            OnShieldDamaged -= _OnDamaged;
        }

        private void Restore()
        {
            if (m_IsDead) return;

            if (ShieldRestoreSpeed > 0 && CurrentShield < MaxShield && Time.time > m_LastHitTime + ShieldDelay)
            {
                RestoreShield(Time.fixedDeltaTime * ShieldRestoreSpeed);
                if (!m_Audio.isPlaying) m_Audio.Play();
            }
            if (CurrentShield >= MaxShield && m_Audio.isPlaying)
            {
                m_Audio.Stop();
                m_Audio.PlayOneShot(ShieldFull);
            }
            showShield = CurrentShield.RawInt;
        }


        private void _OnDamaged(bool isBreak)
        {
            if (m_Audio.isPlaying) m_Audio.Stop();
            if (isBreak)
            {
                if (ShieldBreak != null) m_Audio.PlayOneShot(ShieldBreak);
            }
            else
            {
                var clip = ShieldDamage.RandomTake();
                if (clip != null) m_Audio.PlayOneShot(clip);
            }
            showShield = CurrentShield.RawInt;
        }


        //protected override void HandleDeath() {
        //    base.HandleDeath();
        //}

    }
}