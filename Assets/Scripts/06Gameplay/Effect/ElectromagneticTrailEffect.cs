using System.Collections.Generic;
using FPSGame.GameContract;
using FPSGame.Game;
using FPSGame.Utils;
using FPSGame.Weapon;
using PEMaths;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 电磁尾迹：挂在子弹上，每 TickInterval(默认 0.5 秒) 把「开火点 → 子弹当前位置」这条直线扫一遍，
    /// 线上每个单位结算一次武器「尾迹」档的伤害（<c>WeaponBaseController.Damages[DamageIndex]</c>）。
    /// <para>▍为什么挂在子弹上：起点 = <see cref="ProjectileBase.InitialPosition"/>、终点 = 子弹自身位置；
    /// 弹种勾了"命中保留"时子弹会停在落点，这条线自然固定下来，不必额外同步起点/终点。</para>
    /// </summary>
    [AddComponentMenu("持续效果/电磁尾迹(直线持续伤害)")]
    public class ElectromagneticTrailEffect : SustainedEffect
    {
        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        /// <summary>一次胶囊查询的上限（一个单位有多个部位碰撞体，长射程时线上一排人很容易过百）。
        /// <para>顶满 = 多出来的那部分本节拍打不到，只影响"恰好排满线"的极端情况。</para></summary>
        const int k_OverlapCapacity = 128;

        [InspectorName("武器伤害档位")]
        [Tooltip("取武器的第几条伤害配置：尾迹类改装(恐惧/电磁/燃烧尾迹)改的就是 1 号")]
        [SerializeField]
        private int DamageIndex = 1;

        static readonly Collider[] s_OverlapBuffer = new Collider[k_OverlapCapacity];

        /// <summary>单位 → 本节拍候选下标（同一单位只留离直线最近的那个肢体）</summary>
        readonly Dictionary<int, int> _bestPartIndex = new();
        readonly List<Candidate> _candidates = new();

        ProjectileBase _projectile;
        WeaponBaseController _resolvedWeapon;
        LayerMask _hitLayers;
        bool _warnedNoData;

        /// <summary>一个候选肢体：伤害落点 + 到直线的距离（衰减用）</summary>
        struct Candidate
        {
            public IDamageable Damageable;
            public Collider Collider;
            public Vector3 Pos;
            public float Distance;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _projectile = GetComponent<ProjectileBase>();
            // 基类只要求存在一个"存活锚点"：本效果的量程是一条线，锚点就用子弹自身
            if (!DamageAnchor) DamageAnchor = transform;
            _resolvedWeapon = null;      // 池化复用会换武器，上一发的转换结果作废
        }

        /// <summary>Shoot() 在生成的当帧就赋好 WeaponBase，首帧 Update 之前一定能取到配置</summary>
        void Start() => ResolveDamageData();

        protected override void ApplyEffect()
        {
            // 对端那颗"只表现"的弹不结算伤害：开枪者本机已经在算，两端各算一次就是 N 倍
            if (!_projectile || _projectile.IsVisualBullet) return;
            if (!ResolveDamageData()) return;

            Vector3 from = _projectile.InitialPosition;
            Vector3 to = transform.position;
            if ((to - from).sqrMagnitude < 0.0001f) return;
            DamageAlongSegment(from, to);
        }

        /// <summary>
        /// 取/刷新伤害配置。每次跳都对齐一次当前武器（池化复用、运行时改装都会换值），没变则直接返回。
        /// </summary>
        bool ResolveDamageData()
        {
            if (!_projectile) _projectile = GetComponent<ProjectileBase>();
            WeaponBaseController weapon = _projectile ? _projectile.WeaponBase : null;
            if (!weapon || weapon.Damages == null || DamageIndex < 0 || DamageIndex >= weapon.Damages.Count)
            {
                // 前置条件不成立就一条日志 + 不做，不写降级链
                if (!_warnedNoData)
                {
                    _warnedNoData = true;
                    Debug.LogWarning($"{name}：取不到武器伤害配置(档位={DamageIndex})，电磁尾迹不结算伤害", this);
                }
                return false;
            }

            if (_resolvedWeapon == weapon && DamageData.IsValid()) return true;

            _resolvedWeapon = weapon;
            DamageData = SustainedDamageData.FromDamageData(weapon.Damages[DamageIndex], _projectile.Charge);
            // 命中层与子弹同口径（高速弹不能穿盾，见 FpsHelper.GetHittableLayers）
            var bulletData = _projectile.DamageData;
            _hitLayers = FpsHelper.GetHittableLayers(bulletData != null ? bulletData.GetSpeed(1).RawInt : 0);
            return DamageData.IsValid();
        }

        /// <summary>
        /// 对一条线段结算一次伤害：用胶囊包住整条线直接拿碰撞体（比"圆心查询 + 逐个判距离"省，
        /// 也不会因为线长而把半张地图的单位框进来），每个单位只吃一次（取离直线最近的肢体）。
        /// </summary>
        void DamageAlongSegment(Vector3 from, Vector3 to)
        {
            PEInt radius = DamageData.GetDamageOuterRadius(1);
            if (radius <= 0) return;

            int count = Physics.OverlapCapsuleNonAlloc(from, to, radius.RawFloat,
                s_OverlapBuffer, _hitLayers, k_TriggerInteraction);
            if (count == 0) return;

            _candidates.Clear();
            _bestPartIndex.Clear();

            for (int i = 0; i < count; ++i)
            {
                Collider col = s_OverlapBuffer[i];
                if (!col) continue;

                IDamageable damageable = col.GetComponent<IDamageable>();
                if (!damageable.IsValid()) continue;

                // ⚠ 顺序不能反：Source 没连（TransferDamageable 忘配）时 ActorGo 会 NRE
                if (!damageable.Source.IsValid()) continue;

                // 打不到自己：这条线的起点就在枪口，射手永远落在半径内
                GameObject actorGo = damageable.ActorGo;
                if (actorGo == null || actorGo == Origin) continue;

                Vector3 linePoint = ClosestPointOnSegment(col.bounds.center, from, to);
                Vector3 bodyPoint = col.ClosestPointOnBounds(linePoint);
                float distance = (bodyPoint - linePoint).magnitude;
                if (distance > radius.RawFloat) continue;

                var candidate = new Candidate
                {
                    Damageable = damageable,
                    Collider = col,
                    Pos = bodyPoint,
                    Distance = distance,
                };

                int id = actorGo.GetInstanceID();
                if (_bestPartIndex.TryGetValue(id, out int index))
                {
                    if (_candidates[index].Distance <= distance) continue;
                    _candidates[index] = candidate;
                }
                else
                {
                    _bestPartIndex[id] = _candidates.Count;
                    _candidates.Add(candidate);
                }
            }

            GameObject origin = Origin;
            GameObject caused = ResolveCaused(origin);
            bool noSource = DamageData.NoSource || !origin;
            bool localEffect = _resolvedWeapon && _resolvedWeapon.LocalEffectDamage;

            for (int i = 0, l = _candidates.Count; i < l; ++i)
            {
                Candidate candidate = _candidates[i];
                PEInt distance = (PEInt)candidate.Distance;
                PEInt value = DamageData.GetExplosionDamage(1, distance);
                if (value <= 0) continue;

                candidate.Damageable.InflictDamage(new DamagePacket
                {
                    Damage = value,
                    DamageGroups = DamageData.DamageGroupExplosion,
                    AboVulnerabilityGroup = DamageData.AboVulnerabilityGroup,
                    WeaknessBonus = DamageData.GetWeaknessBonus(),
                    AP = DamageData.GetExplosionAP(1),
                    NoSource = noSource,
                    DamageOrigin = origin,
                    DamageCaused = caused,
                    Pos = candidate.Pos,
                    DemolishValue = DamageData.GetDemolishValue(distance),
                    // 非直击包：吃目标爆炸抗性，与 FpsHelper.Hit 的爆炸分支同口径
                    isDirect = false,
                    damageAffected = candidate.Collider,
                    // 支援单位（各端各自生成的炮台）的尾迹：非权威端只对玩家/盟友落地，门在 Damageable
                    LocalEffect = localEffect,
                });
            }
        }

        /// <summary>直接来源（显示/AI 口径）：武器挂在谁身上就是谁，与 <c>ProjectileBase.BuildHitData</c> 同一条规则</summary>
        GameObject ResolveCaused(GameObject origin)
        {
            if (!_resolvedWeapon) return origin;
            Actor actor = _resolvedWeapon.GetComponentInParent<Actor>();
            return actor ? actor.gameObject : origin;
        }

        /// <summary>点到线段最近点</summary>
        static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 0.0001f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
            return a + ab * t;
        }

        /// <summary>基类的球形提示对本效果是误导（量程是一条线），这里画真实的线段</summary>
        protected override void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_projectile) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_projectile.InitialPosition, transform.position);
        }
    }
}
