using System.Collections.Generic;
using GameContract;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 空投舱:极简版子弹(不是通过武器创建的，正常走的特效过)
    /// </summary>
    public class ProjectilePod : ProjectileBase
    {


        [Header("通用")]
        [CustomLabel("根部变换(精确碰撞检测)")]
        public Transform Root;
        public GameObject fire;

        protected float m_ShootTime;
        protected Vector3 m_LastRootPosition;
        protected Vector3 m_Velocity;
        private bool m_isStop = false;

        protected List<Collider> m_IgnoredColliders;
        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        private float m_LandingHeight;

        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
            OnHit += HitFX;
        }

        private void OnDisable()
        {
            OnShoot = null;
            OnHit = null;
        }

        protected virtual void _OnShoot()
        {
            m_isStop = false;
            m_ShootTime = Time.time;
            m_LastRootPosition = Root.position;
            m_Velocity = Vector3.down*0.1f;//全靠重力加速度
            m_IgnoredColliders = new(GetComponentsInChildren<Collider>());
            if (Physics.Raycast(Root.position, Vector3.down, out var hit, 1000, LayerDefinition.GroundLayers))
            {
                m_LandingHeight = hit.point.y;
            }
            fire.SetActive(true);
        }
        /*
        protected virtual void Update()
        {
            if (m_isStop) return;
            Move();
            TryHit();
            m_LastRootPosition = Root.position;
        }*/

        private void FixedUpdate()
        {
            if (m_isStop) return;
            if (Move())
            {
                TryHit();
            }
            m_LastRootPosition = Root.position;
        }

        protected virtual void TryHit()
        {
            RaycastHit closestHit = new RaycastHit();
            closestHit.distance = Mathf.Infinity;
            bool foundHit = false;

            // Sphere cast
            Vector3 displacementSinceLastFrame = Root.position - m_LastRootPosition;
            RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, 0.3f,
                displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude+0.1f, FpsHelper.GetHittableLayers(m_Velocity.magnitude),
                k_TriggerInteraction);
            foreach (var hit in hits)
            {
                if (IsHitValid(hit) && hit.distance < closestHit.distance)
                {
                    foundHit = true;
                    closestHit = hit;
                }
            }

            if (foundHit)
            {
                // 处理在碰撞箱内部的问题
                if (closestHit.distance <= 0f)
                {
                    closestHit.point = Root.position;
                    closestHit.normal = -Root.forward;
                }
                //Debug.LogError("击中了"+ closestHit.collider, closestHit.collider);
                OnHit?.Invoke(new() {
                    pos = closestHit.point,
                    normal = closestHit.normal,
                    collider = closestHit.collider,
                    data = DamageData,
                    chargeScale = Charge,
                    owner = Owner,
                    sfxRange = SFXRange,
                    weapon = WeaponBase,
                    useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
                });
            }
        }

        protected bool Move()
        {
            transform.position += m_Velocity * Time.fixedDeltaTime;
            m_Velocity += Vector3.down * 20 * Time.fixedDeltaTime;
            
            if (Root.position.y < m_LandingHeight)
            {
                //Debug.LogError("正常停止,高度" + Root.position.y + "目标高度" + m_LandingHeight);
                transform.position =new(transform.position.x, m_LandingHeight - Root.localPosition.y, transform.position.z);
                OnHit?.Invoke(new() {
                    pos = transform.position,
                    normal = Vector3.up,
                    collider = null,
                    data = DamageData,
                    chargeScale = Charge,
                    owner = Owner,
                    sfxRange = SFXRange,
                    weapon = WeaponBase,
                    useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
                });
                Stop();
                return false;
            }
            return true;
        }

        bool IsHitValid(RaycastHit hit)
        {
            var ihd = hit.collider.GetComponent<IgnoreHitDetection>();
            //使用忽略组件忽略点击
            if (ihd)
            {
                //双向忽略
                if (!ihd.Unidirectional) {
                    return false;
                }
                //单向忽略(如果发射点在碰撞箱内部就忽略)
                else if(hit.collider.bounds.Contains(InitialPosition)){
                    return false;
                }
            }

            //忽略没有可损坏组件的触发器的命中
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null)
            {
                return false;
            }

            //忽略具有特定忽略碰撞器（默认情况下为自碰撞器）的碰撞
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
            {
                return false;
            }

            return true;
        }
        
        /// <summary>击中 </summary>
        void HitFX(ProjectileHitData hitdata)
        {
            if (m_isStop) return;
  
            if (!hitdata.collider.IsValid()) {
                Stop();
                return;
            }

            //应该是被单位尸体卡住所以过判定了
            //是直接穿透，击中地面才停止
            
            if (LayerDefinition.GroundLayers.Contains(hitdata.collider.gameObject.layer))
            {
                //Debug.LogError("击中地面,停止高度" + Root.position.y + "目标高度" + m_LandingHeight);
                Stop();
            }

        }

        private void Stop()
        {
            m_isStop = true;
            fire.SetActive(false);
        }

    }
}