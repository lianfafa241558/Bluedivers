using Core;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 一个持续的效果，例如地面的火焰、毒气等。它会在一定时间内持续对进入范围的对象造成影响。
    /// 持续时间由另一个组件控制，例如LimitedLife组件。这个类主要用于处理持续效果的逻辑，例如检测进入范围的对象并应用效果。
    /// </summary>
    [AddComponentMenu("持续效果/基础")]
    public class SustainedEffect : MonoBehaviour, IVfxEffect
    {
        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;
        


        [Header("通用")]
        [InspectorName("伤害中心锚点")]
        [SerializeField]
        protected Transform DamageAnchor;
        [InspectorName("伤害间隔(秒)")]
        [SerializeField]
        private float TickInterval = 0.5f;

        [SerializeField]
        private bool UseDiffScale = false;

        [SerializeField]
        protected SustainedDamageData DamageData;


        /// <summary>伤害来源</summary>
        public GameObject Owner { get; set; }

        //LimitedLife m_limitedLife;
        float m_NextTickTime;

        protected virtual void OnEnable()
        {
            //m_limitedLife = GetComponent<LimitedLife>();
            m_NextTickTime = Time.time;
        }

        void Update()
        {
            TryTick();
        }


        void TryTick()
        {
            // 持续时间由LimitedLife控制，没有则一直持续
            //if (m_limitedLife && !m_limitedLife.IsAlive()) return;
            if (Time.time < m_NextTickTime) return;
            m_NextTickTime = Time.time + TickInterval;
            if (!DamageData.IsValid()) return;

            ApplyEffect();
        }

        /// <summary>对范围内所有目标施加一次效果(范围伤害，不产生直击伤害)</summary>
        void ApplyEffect()
        {
            FpsHelper.Hit(new ProjectileHitData
            {
                pos = DamageAnchor ? DamageAnchor.position : transform.position,
                normal = Vector3.up,
                collider = null,//不产生直击伤害，仅范围伤害
                data = DamageData,
                chargeScale = 1,
                owner = Owner,
                sfxRange = DamageData.SoundRadius,
                weapon = null,
                useDiffScale = UseDiffScale,
                IgnoreSelf = false,
            });
        }

        protected virtual void OnDrawGizmos()
        {
            if (!DamageAnchor) return;
            Gizmos.color = Color.yellow;
            if (DamageData.IsValid())
            {
                Gizmos.DrawWireSphere(DamageAnchor.position, DamageData.GetDamageOuterRadius(1).RawFloat);
            }
            Gizmos.color = Color.red;
            if (DamageData.IsValid())
            {
                Gizmos.DrawWireSphere(DamageAnchor.position, DamageData.GetDamageInnerRadius(1).RawFloat);
            }
        }

        public void SetOwner(GameObject owner, GameObject _, Collider _2, Vector3 _3)
        {
            Owner = owner;
        }
    }
}
