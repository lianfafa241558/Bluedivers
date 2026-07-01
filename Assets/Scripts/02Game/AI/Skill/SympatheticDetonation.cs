using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    /// <summary>
    /// 死亡殉爆以及自爆
    /// </summary>
    [AddComponentMenu("技能/殉爆和自爆", 30)]
    public class SympatheticDetonation : MonoBehaviour
    {
        [SerializeField]
        float delayTime = 0;
        [SerializeField]
        float selfExplosionTime = 0;
        [SerializeField]
        AIController controller;
        [SerializeField]
        WeaponBaseController weapon;

        Coroutine m_Coroutine;
        private void OnEnable()
        {
            controller.OnDie += TryExplode;
            if (selfExplosionTime > 0)
            {
                m_Coroutine = StartCoroutine(nameof(SelfExplode));
            }
        }
        private void OnDisable()
        {
            controller.OnDie -= TryExplode;
            if (m_Coroutine != null)
            {
                StopCoroutine(m_Coroutine);
            }
        }
        IEnumerator SelfExplode()
        {
            yield return new WaitForSeconds(selfExplosionTime);
            controller.Kill(false);
        }
        IEnumerator DelayExplode()
        {
            yield return new WaitForSeconds(delayTime);
            weapon.Shoot();
        }

        void TryExplode()
        {
            StartCoroutine(nameof(DelayExplode));
        }
    }
}