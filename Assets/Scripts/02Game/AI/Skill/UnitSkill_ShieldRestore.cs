using System.Collections;
using Core;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    /// <summary>
    /// 护盾恢复（被动技能）：
    /// 护盾肢体被摧毁后延迟恢复；若护盾发射器肢体被摧毁，技能失效，不再恢复护盾。
    /// </summary>
    [AddComponentMenu("技能/护盾恢复", 30)]
    public class UnitSkill_ShieldRestore : MonoBehaviour
    {
        [InspectorName("护盾肢体")]
        [SerializeField]
        private Damageable shield;
        [InspectorName("护盾发射器肢体")]
        [SerializeField]
        private Damageable emitter;
        [InspectorName("恢复延时")]
        [SerializeField]
        private float restoreDelay = 5f;

        /// <summary>护盾发射器已被摧毁，技能失效</summary>
        private bool m_EmitterBroken;
        private Coroutine m_RestoreRoutine;

        private void OnEnable()
        {
            if (shield != null) shield.OnDestroyPart += OnShieldDestroyed;
            if (emitter != null) emitter.OnDestroyPart += OnEmitterDestroyed;
        }

        private void OnDisable()
        {
            if (shield != null) shield.OnDestroyPart -= OnShieldDestroyed;
            if (emitter != null) emitter.OnDestroyPart -= OnEmitterDestroyed;
            if (m_RestoreRoutine != null)
            {
                StopCoroutine(m_RestoreRoutine);
                m_RestoreRoutine = null;
            }
        }

        /// <summary>护盾被摧毁：发射器完好时延迟恢复</summary>
        private void OnShieldDestroyed(Damageable _)
        {
            if (m_EmitterBroken || shield == null) return;

            // 发射器已处于摧毁状态（含初始即摧毁）则技能失效
            if (emitter != null && emitter.remainArmor <= 0)
            {
                m_EmitterBroken = true;
                return;
            }

            if (m_RestoreRoutine != null) StopCoroutine(m_RestoreRoutine);
            m_RestoreRoutine = StartCoroutine(DelayRestore());
        }

        /// <summary>护盾发射器被摧毁：技能失效</summary>
        private void OnEmitterDestroyed(Damageable _)
        {
            m_EmitterBroken = true;
            if (m_RestoreRoutine != null)
            {
                StopCoroutine(m_RestoreRoutine);
                m_RestoreRoutine = null;
            }
        }

        private IEnumerator DelayRestore()
        {
            yield return new WaitForSeconds(restoreDelay);
            m_RestoreRoutine = null;
            if (m_EmitterBroken || shield == null) yield break;
            shield.RestoreArmor();
        }
    }
}
