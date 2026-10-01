using FPSGame.Core;
using FPSGame.Attributes;
using PEMaths;

using UnityEngine;
using FPSGame.Audio;
using FPSGame.Utils;

namespace FPSGame.Game
{
    /// <summary>
    /// 护盾自动恢复组件：挂在带 <see cref="Health"/> 的物体上，负责护盾随时间的恢复与配套音效
    /// （延迟后回盾、回满提示音、受击音、破碎音）。
    /// <para>
    /// 该逻辑原先在 HealthPlayer 与 HealthShield 里各写了一份（HealthShield 已删除），现统一到这里：
    /// 任何单位只要挂上本组件就能获得护盾恢复能力。
    /// HealthPlayer 会在运行时自动挂载本组件，并通过 <see cref="Configure"/> 注入它 Inspector 上的原有配置，
    /// 因此玩家 prefab 上的字段与数值不受影响。
    /// </para>
    /// </summary>
    [AddComponentMenu("生命/护盾恢复")]
    public class ShieldBehaviour : MonoBehaviour
    {
        [InspectorName("护盾恢复速度")]
        [SerializeField] private float shieldRestoreSpeed;
        [InspectorName("护盾恢复延迟")]
        [SerializeField] private float shieldDelay;

        [SerializeField] [InspectorName("护盾恢复音效")] private AudioClip shieldRestore;
        [SerializeField] [InspectorName("护盾回满音效")] private AudioClip shieldFull;
        [SerializeField] [InspectorName("护盾破碎音效")] private AudioClip shieldBreak;
        [SerializeField] [InspectorName("护盾受击音效")] private AudioClip[] shieldDamage;

        [Space]
        [DisplayField]
        [InspectorName("剩余护盾值")]
        [SerializeField] private int showShield;

        private Health m_Health;
        private AudioSource m_Audio;

        /// <summary>
        /// 由外部（如 HealthPlayer）注入配置，使配置仍保存在原有组件上、不改变 prefab 序列化数据。
        /// </summary>
        public void Configure(float restoreSpeed, float delay, AudioClip restore, AudioClip full, AudioClip breakClip, AudioClip[] damageClips)
        {
            shieldRestoreSpeed = restoreSpeed;
            shieldDelay = delay;
            shieldRestore = restore;
            shieldFull = full;
            shieldBreak = breakClip;
            shieldDamage = damageClips;
            ApplyAudioClip();
        }

        private void Awake()
        {
            m_Health = GetComponent<Health>();
            if (m_Health == null)
            {
                Debug.LogError("ShieldBehaviour 需要与 Health 挂在同一物体上", this);
                enabled = false;
                return;
            }

            showShield = m_Health.CurrentShield.RawInt;
            m_Health.OnShieldDamaged += OnShieldDamaged;
            m_Audio = AudioSvc.CreatSource(gameObject, AudioGroups.General);
            ApplyAudioClip();
            InvokeRepeating(nameof(Restore), Constants.LoginFrame.RawFloat, Constants.LoginFrame.RawFloat);
        }

        private void OnDestroy()
        {
            if (m_Health != null) m_Health.OnShieldDamaged -= OnShieldDamaged;
        }

        private void ApplyAudioClip()
        {
            if (m_Audio != null) m_Audio.clip = shieldRestore;
        }

        /// <summary>按登录帧节拍恢复护盾：延迟后回盾、回满播提示音</summary>
        private void Restore()
        {
            if (m_Health == null || m_Health.IsDead) return;

            if (shieldRestoreSpeed > 0 && m_Health.CurrentShield < m_Health.MaxShield
                && Time.time > m_Health.LastHitTime + shieldDelay)
            {
                m_Health.RestoreShield(Time.fixedDeltaTime * shieldRestoreSpeed);
                if (!m_Audio.isPlaying) m_Audio.Play();
            }
            if (m_Health.CurrentShield >= m_Health.MaxShield && m_Audio.isPlaying)
            {
                m_Audio.Stop();
                m_Audio.PlayOneShot(shieldFull);
            }
            showShield = m_Health.CurrentShield.RawInt;
        }

        /// <summary>护盾受击/破碎音效</summary>
        private void OnShieldDamaged(bool isBreak)
        {
            if (m_Audio == null) return;

            if (m_Audio.isPlaying) m_Audio.Stop();
            if (isBreak)
            {
                if (shieldBreak != null) m_Audio.PlayOneShot(shieldBreak);
            }
            else
            {
                AudioClip clip = shieldDamage != null && shieldDamage.Length > 0 ? shieldDamage.RandomTake() : null;
                if (clip != null) m_Audio.PlayOneShot(clip);
            }
            if (m_Health != null) showShield = m_Health.CurrentShield.RawInt;
        }
    }
}
