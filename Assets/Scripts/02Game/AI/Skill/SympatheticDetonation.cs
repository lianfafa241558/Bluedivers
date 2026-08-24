using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;
using JetBrains.Annotations;
using Unity.FPS.Game;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

namespace UFPSGame.AI
{
    /// <summary>
    /// 死亡殉爆以及自爆
    /// </summary>
    [AddComponentMenu("技能/殉爆和自爆", 30)]
    public class SympatheticDetonation : MonoBehaviour
    {
        [InspectorName("殉爆延时")]
        [SerializeField]
        float delayTime = 0;
        [InspectorName("自爆时间")]
        [SerializeField]
        float selfExplosionTime = 0;

        [InspectorName("使用武器爆炸")]
        [SerializeField]
        WeaponBaseController weapon;

        [InspectorName("controller的物体")]
        [SerializeField]
        GameObject go;

        [SerializeField]
        I_AIController controller => go.GetComponent<I_AIController>();
        [InspectorName("使用难度增幅")]
        [SerializeField]
        private bool UseDiffScale = true;

        //[Compare("weapon",0, CompareOperate.Equal)]
        [SerializeField]
        protected SustainedDamageData DamageData;

        Coroutine m_Coroutine;
        private void OnEnable()
        {
            if (go != null)
            {
                controller.OnDie += TryExplode;
                if (selfExplosionTime > 0)
                {
                    m_Coroutine = StartCoroutine(nameof(SelfExplode));
                }
            }
        }
        private void OnDisable()
        {
           
            if (go != null)
            {
                controller.OnDie -= TryExplode;
                if(m_Coroutine!=null) StopCoroutine(m_Coroutine);
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
            ApplyEffect();
        }

        void TryExplode()
        {
            StartCoroutine(nameof(DelayExplode));
        }

        /// <summary>对范围内所有目标施加一次效果(范围伤害，不产生直击伤害)</summary>
        void ApplyEffect()
        {
            if(DamageData.ImpactSfx) AudioSvc.PlaySound(new(DamageData.ImpactSfx, transform.position, DamageData.SoundRadius, AudioGroups.Weapon));
            if (DamageData.ImpactVfx) VFXManager.Creat(DamageData.ImpactVfx, transform.position, transform.rotation, null);
            FpsHelper.Hit(new ProjectileHitData {
                pos = controller.CenterPos,
                normal = Vector3.up,
                collider = null,//不产生直击伤害，仅范围伤害
                data = DamageData,
                chargeScale = 1,
                owner = go,
                sfxRange = DamageData.SoundRadius,
                weapon = null,
                useDiffScale = UseDiffScale,
                IgnoreSelf = false,
            });
        }

    }
}