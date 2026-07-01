using System.Collections.Generic;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 激光子弹
    /// </summary>
    public class ProjectileLaser : ProjectileBase
    {

        /// <summary>射速</summary>
        public float ShootSpeed { get; private set; }

        [Header("通用")]
        [InspectorName("碰撞半径")]
        public float Radius = 0.01f;

        [Header("特效")]
        [InspectorName("创建终点物体")]
        public List<GameObject> EndObject;

        protected float m_ShootTime;

        protected List<Collider> m_IgnoredColliders;
        protected LineRenderer m_line;
        protected List<Transform> m_EndObject;
        protected Vector3 m_lastPos;
        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
        }

        protected virtual void _OnShoot()
        {
            //这里可以有武器，但是update里面得脱落
            ShootSpeed = WeaponBase.AttrFinal(WeaponAttrType.ShootInterval, new(0.1f)).RawFloat;
            m_ShootTime = Time.time;
            //直接设置父级就不用处理移动了
            transform.parent = WeaponBase.WeaponMuzzle;

            //忽略发射者的碰撞
            m_IgnoredColliders = new List<Collider>();
            Collider[] weaponColliders = Owner.GetComponentsInChildren<Collider>();
            if (weaponColliders.IsValid()) m_IgnoredColliders.AddRange(weaponColliders);

            m_line = GetComponent<LineRenderer>();
            m_line.positionCount = 2;

            RaycastHit hit;
            if (!Physics.Raycast(new Ray(transform.position, transform.forward), out hit, 300, FpsHelper.GetHittableLayers(999)))
            {
                hit.point = transform.position + transform.forward * 300;
                hit.normal = -transform.forward;

            }
            m_lastPos = hit.point;
            m_EndObject = new();
            foreach (var item in EndObject)
            {
                //m_EndObject.Add(VFXManager.Creat(item, hit.point, Quaternion.LookRotation(hit.normal, Vector3.forward), null).transform);
                m_EndObject.Add(VFXManager.Creat(item, hit.point, default, null).transform);
            }
        }

        protected virtual void Update()
        {
            Move();
        }


        protected virtual void Move()
        {
            m_line.SetPosition(0, transform.position);
            Vector3 vector = transform.forward;
            Vector3 end;
            if (Physics.Raycast(new Ray(transform.position, vector), out var hit, 300, FpsHelper.GetHittableLayers(999)))
            {
                m_line.SetPosition(1, hit.point);
                end = hit.point;
                TryHit(hit);
                SetEnd(m_lastPos,end, hit.normal, hit.transform.gameObject.layer == LayerDefinition.UnitLayers);
                m_lastPos = end;

            }
            else
            {
                m_line.SetPosition(1, transform.position + vector * 300);
                SetLife();
                
            }
            
        }


        protected virtual void TryHit(RaycastHit hit)
        {
            //这样会导致前面射空了后，新击中目标立即造成伤害，属于无伤大雅的小bug，不管
            //每ShootSpeed 1Hit
            if (Time.time< m_ShootTime + ShootSpeed) return;
            m_ShootTime = Time.time;

            if (IsHitValid(hit))
            {
                // 处理在碰撞箱内部的问题
                if (hit.distance <= 0f)
                {
                    hit.point = transform.position;
                    hit.normal = -transform.forward;
                }
                //Debug.LogError("击中"+ closestHit.collider.name);
                OnHit?.Invoke(new() {
                    pos = hit.point,
                    normal = hit.normal,
                    collider = hit.collider,
                    data = DamageData,
                    chargeScale = Charge,
                    owner = Owner,
                    sfxRange = SFXRange,
                    weapon = WeaponBase,
                    useDiffScale=BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
                });
            }

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

        void SetEnd(Vector3 lastPos, Vector3 pos, Vector3 normal,bool isUnit)
        {

            bool jump = Mathf.Abs(transform.InverseTransformDirection(pos).z - transform.InverseTransformDirection(lastPos).z) > 1 || Vector3.Distance(pos,lastPos)>3;
            pos += normal * 0.03f;
            //Debug.DrawRay(pos,normal,Color.red,1);
            foreach (var item in m_EndObject)
            {
                item.GetComponent<LimitedLife>().ResetLift(1);
                if(item.TryGetComponent<ParticleSystem>(out var ps))
                {
                    if (jump)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        item.position = pos;
                        ps.Play();
                    }
                    else
                    {
                        item.position = pos;
                    }

                    Quaternion targetRot = Quaternion.FromToRotation(Vector3.forward, normal);
                    var main = ps.main;
                    main.startRotationX = -targetRot.eulerAngles.x * Mathf.Deg2Rad;
                    main.startRotationY = -targetRot.eulerAngles.y * Mathf.Deg2Rad;

                    //var shapeModule = ps.shape;
                    //shapeModule.rotation = new Vector3(-targetRot.eulerAngles.x, -targetRot.eulerAngles.y,0);
                }
                else
                {
                    
                    item.forward = normal;
                }
            }
        }
        void SetLife()
        {
            foreach (var item in m_EndObject)
            {
                item.GetComponent<LimitedLife>().ResetLift(1);
            }
        }
    }
}