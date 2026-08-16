using System.Collections.Generic;
using Core;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 地雷
    /// </summary>
    [AddComponentMenu("子弹/地雷", 30)]
    public class ProjectileMine : ProjectileBase
    {
        private const float _waitTime = 2;

        [Header("通用")]
        [InspectorName("根部变换")]
        public Transform Root;
        [InspectorName("触发范围")]
        public float TriggerRange=0.7f;

        public static Color RadiusColor = Color.red;

        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        Actor m_Actor;
        Health m_health;
        Collider m_collider;
        LimitedLife m_limitedLife;
        float m_creatTime;
        void OnEnable()
        {
            m_health = GetComponent<Health>();
            m_collider = GetComponent<Collider>();
            m_Actor = GetComponent<Actor>();
            m_limitedLife = GetComponent<LimitedLife>();

            m_limitedLife.OnEnd.AddListener(Recovery);
            m_health.OnDie += Explosion;
            OnShoot += _OnShoot;
            OnHit += HitFX;
            m_creatTime= Time.time;
        }
        
        private void OnDisable()
        {
            m_limitedLife.OnEnd.RemoveListener(Recovery);
            m_health.OnDie -= Explosion;
            OnShoot -= _OnShoot;
            OnHit -= HitFX;
        }
        
        protected virtual void _OnShoot()
        {
            DamageData = WeaponBase.Damages[1];
            m_Actor.ActorState = ActorState.Normal;
        }

        void Update()
        {
            if (Time.time < m_creatTime + _waitTime) return;
            TryHit();
        }


        void TryHit()
        {

            Collider[] hits = Physics.OverlapSphere(Root.position,DamageData.GetDamageOuterRadius(1).RawFloat, FpsHelper.GetHittableLayers(0)- LayerDefinition.GroundLayers,k_TriggerInteraction);
            foreach (var hit in hits)
            {
                if (IsHitValid(hit) && hit.Distance(m_collider) < TriggerRange)
                {
                    //Explosion();
                    //如果直接炸会因为死了再炸一次造成两次伤害
                    m_health.Kill();
                    //Release();
                    break;
                }
            }

        }
        void Recovery()
        {
            m_health.Revive();
            Explosion(null);
        }
        void Explosion(GameObject source)
        {
            //防止回收后再次爆炸
            //if (m_Actor.ActorState == ActorState.Dead) return;
            //m_Actor.ActorState = ActorState.Dead;
            OnHit?.Invoke(new() {
                pos = Root.position,
                normal = Root.forward,
                collider = null,//不产生直击伤害
                data = DamageData,
                chargeScale = Charge,
                owner = Owner,
                sfxRange = SFXRange,
                weapon = WeaponBase,
                useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
            });
            Release();
        }

        bool IsHitValid(Collider collider)
        {
            var ihd = collider.GetComponent<IgnoreHitDetection>();
            //使用忽略组件忽略点击
            if (ihd)
            {
                //双向忽略
                if (!ihd.Unidirectional)
                {
                    return false;
                }
                //单向忽略(如果发射点在碰撞箱内部就忽略)
                else if (collider.bounds.Contains(InitialPosition))
                {
                    return false;
                }
            }

            //忽略没有可损坏组件的触发器的命中
            if (collider.isTrigger &&collider.GetComponent<Damageable>() == null)
            {
                return false;
            }

            return true;
        }



        /// <summary>击中 </summary>
        void HitFX(ProjectileHitData hitdata)
        {
            GetComponent<LimitedLife>().allowRelease = true;
            //会回收而不是直接摧毁
            //Destroy(gameObject);
        }

        protected virtual void OnDrawGizmos()
        {
            Gizmos.color = RadiusColor;
            if (DamageData.IsValid())
            {
                Gizmos.DrawWireSphere(Root.position, DamageData.GetDamageOuterRadius(1).RawFloat);
            }
        }


    }
}