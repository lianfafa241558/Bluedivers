using System.Collections.Generic;

using GameContract;
using PEMaths;
using Unity.FPS.Game;

using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 空投舱
    /// 由外部脚本(如 VFXAirdropEffect)创建后调用 <see cref="Launch(GameObject)"/> 开始自由落体，
    /// 下落过程中做球体扫掠碰撞检测：撞到单位时按自身伤害数据结算并继续穿透，撞到地面(或到达落地高度)时停止。
    /// 与 <see cref="ProjectileBase"/> 体系解耦，不依赖任何武器配置(WeaponBaseController)，
    /// 伤害数据、重力、碰撞半径全部由自身序列化字段提供。
    /// - Root：根部变换，作为碰撞检测与落地高度的采样点
    /// - fire：下落尾焰，落地后关闭
    /// - LimitedLife：提供落地后的存续时间(可由外部在 <see cref="OnHit"/> 回调中重置)
    /// </summary>
    [AddComponentMenu("单位/空投舱", 30)]
    public class AirdropPod : MonoBehaviour, IVfxEffect
    {
        private const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        [Header("通用")]
        [InspectorName("根部变换(精确碰撞检测)")]
        [SerializeField]
        private Transform Root;
        [InspectorName("尾焰")]
        [SerializeField]
        private GameObject fire;
        [InspectorName("重力加速度")]
        [SerializeField]
        private float Gravity = 20f;
        [InspectorName("初始下落速度")]
        [SerializeField]
        private float InitialFallSpeed = 0.1f;
        [InspectorName("碰撞检测半径")]
        [SerializeField]
        private float HitRadius = 0.3f;
        [InspectorName("落地高度检测距离")]
        [SerializeField]
        private float LandingDetectDistance = 1000f;


        [Header("伤害数据")]
        [InspectorName("空投舱自带伤害")]
        [SerializeField]
        private SustainedDamageData DamageData;

        /// <summary>伤害来源</summary>
        public GameObject Owner { get; private set; }

        /// <summary>命中回调(外部挂接，用于在落地时创建实际物体)</summary>
        public UnityAction<ProjectileHitData> OnHit;

        private readonly List<Collider> m_IgnoredColliders = new();

        private Vector3 m_InitialPosition;
        private Vector3 m_LastRootPosition;
        private Vector3 m_Velocity;
        private float m_LandingHeight;
        private bool m_IsStop = true;

        private void Awake()
        {
            if (!Root) Root = transform;
        }

        private void OnEnable()
        {
            // 未下发前保持静止，等待外部调用 Launch
            m_IsStop = true;
            if (fire) fire.SetActive(false);
        }

        private void OnDisable()
        {
            OnHit = null;
        }

        private void FixedUpdate()
        {
            if (m_IsStop) return;
            if (Move()) TryHit();
            m_LastRootPosition = Root.position;
        }

        /// <summary>
        /// 部署空投舱并开始自由落体
        /// </summary>
        /// <param name="owner">伤害来源(空投发起者)</param>
        public void Launch(GameObject owner)
        {
            Owner = owner;

            m_InitialPosition = Root.position;
            m_LastRootPosition = m_InitialPosition;
            m_Velocity = Vector3.down * InitialFallSpeed;
            m_IsStop = false;

            // 每次下发重新采样自身碰撞体，避免复用时残留上一次的忽略列表
            m_IgnoredColliders.Clear();
            GetComponentsInChildren(m_IgnoredColliders);

            // 未命中地面时退化为 0 高度，保证始终有落地兜底
            m_LandingHeight = 0f;
            if (Physics.Raycast(Root.position, Vector3.down, out var hit, LandingDetectDistance, LayerDefinition.GroundLayers))
            {
                m_LandingHeight = hit.point.y;
            }

            if (fire) fire.SetActive(true);
        }

        /// <summary>
        /// 部署并开始下落(IVfxEffect 入口，武器相关参数对空投舱无意义)
        /// </summary>
        /// <param name="owner">伤害来源(空投发起者)</param>
        /// <param name="weaponRoot">武器物体，空投舱不使用</param>
        /// <param name="target">命中碰撞体，空投舱不使用</param>
        /// <param name="point">部署点，空投舱由创建方定位，不使用</param>
        public void SetOwner(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point) => Launch(owner);

        /// <summary>推进一帧位移，返回是否还需要做碰撞检测(已落地时为 false)</summary>
        private bool Move()
        {
            transform.position += m_Velocity * Time.fixedDeltaTime;
            m_Velocity += Vector3.down * Gravity * Time.fixedDeltaTime;

            if (Root.position.y > m_LandingHeight) return true;

            // 到达落地高度：贴地后结算一次落地伤害并停止
            var extraHeight = m_LandingHeight - Root.position.y;
            transform.position = new Vector3(transform.position.x, m_LandingHeight + extraHeight, transform.position.z);
            Land();
            return false;
        }

        /// <summary>球体扫掠检测本帧位移内的命中</summary>
        private void TryHit()
        {
            Vector3 displacementSinceLastFrame = Root.position - m_LastRootPosition;
            RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, HitRadius,
                displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude + 0.1f,
                FpsHelper.GetHittableLayers(m_Velocity.magnitude), k_TriggerInteraction);

            RaycastHit closestHit = new RaycastHit { distance = Mathf.Infinity };
            bool foundHit = false;
            foreach (var hit in hits)
            {
                if (IsHitValid(hit) && hit.distance < closestHit.distance)
                {
                    foundHit = true;
                    closestHit = hit;
                }
            }
            if (!foundHit) return;

            // 处理起始点已在碰撞箱内部的情况
            if (closestHit.distance <= 0f)
            {
                closestHit.point = Root.position;
                closestHit.normal = -Root.forward;
            }
            Hit(closestHit);
        }

        /// <summary>撞击结算：外部回调 → 通用伤害链路 → 自身状态处理</summary>
        private void Hit(RaycastHit hit)
        {
            var hitData = BuildHitData(hit.point, hit.normal, hit.collider);
            if (DamageData.IsValid()) FpsHelper.Hit(hitData);
            OnHit?.Invoke(hitData);
            ResolveHit(hitData);
        }

        /// <summary>落地结算：无碰撞体，仅产生范围伤害</summary>
        private void Land()
        {
            var hitData = BuildHitData(transform.position, Vector3.up, null);
            if (DamageData.IsValid()) FpsHelper.Hit(hitData);
            OnHit?.Invoke(hitData);

            Stop();
        }

        private ProjectileHitData BuildHitData(Vector3 pos, Vector3 normal, Collider collider)
            => new ProjectileHitData
            {
                pos = pos,
                normal = normal,
                collider = collider,
                data = DamageData,
                chargeScale = 1,//无蓄力概念
                soure = Owner,
                self = gameObject,
                sfxRange = DamageData.IsValid() ? DamageData.SoundRadius : 0,
                weapon = null,//纯自部署，无武器来源
                useDiffScale = false,
                IgnoreSelf =true
            };

        /// <summary>
        /// 处理命中后的自身状态：忽略组件直接穿透、无碰撞体(落地)或撞到地面时停止，其余一律穿透继续下落
        /// </summary>
        private void ResolveHit(ProjectileHitData hitData)
        {
            if (m_IsStop) return;

            Collider collider = hitData.collider;
            // 单向盾/忽略碰撞：加入忽略列表后继续下落(穿透)
            if (collider && collider.GetComponent<IgnoreHitDetection>())
            {
                m_IgnoredColliders.Add(collider);
                return;
            }
            // 没有碰撞体说明是落地结算
            if (!collider.IsValid())
            {
                Stop();
                return;
            }
            // 撞到地面才停止，撞到单位/残骸直接穿透继续下落
            if (LayerDefinition.GroundLayers.Contains(1 << collider.gameObject.layer))
            {
                Stop();
            }
        }

        private bool IsHitValid(RaycastHit hit)
        {
            // 使用忽略组件忽略命中
            if (hit.collider.TryGetComponent(out IgnoreHitDetection ihd))
            {
                // 双向忽略
                if (!ihd.Unidirectional) return false;
                // 单向忽略(如果发射点在碰撞箱内部就忽略)
                if (hit.collider.bounds.Contains(m_InitialPosition)) return false;
            }

            // 忽略没有可损坏组件的触发器的命中
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null) return false;

            // 忽略自身碰撞体
            return !m_IgnoredColliders.Contains(hit.collider);
        }

        /// <summary>停止下落并关闭尾焰</summary>
        private void Stop()
        {
            m_IsStop = true;
            if (fire) fire.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Root) return;
            if (DamageData.IsValid())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(Root.position, DamageData.GetDamageOuterRadius(1).RawFloat);
            }
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Root.position, HitRadius);
        }
    }
}
