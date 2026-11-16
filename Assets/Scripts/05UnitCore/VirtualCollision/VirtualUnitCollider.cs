// ============================================================================
// 【已归档 · 2026-11-16】虚拟碰撞体（VirtualCollider）Step 1 原型 —— 整份注释掉，不参与编译
//
// 结论：不采用。Play 实测（299 Actor / 863 部位碰撞体）Physics.SyncColliderTransform
//       ≈ 0.000~0.013 ms/帧，不是瓶颈；而数学求交单条查询比物理 SphereCast 慢 2~30 倍
//       （UnitQuery 粗筛 6.8~22 µs vs 物理 0.73 µs）⇒ 替代后是净亏。
// 恢复：删掉本段与文件末尾的 */ 即可（细节见 .codebuddy/memory/topics/virtual-collider.md）
// ============================================================================
/*
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 一个单位的**虚拟碰撞体集合**：把该单位所有部位 Collider 烘焙成胶囊代理，
    /// 并负责"本帧世界坐标"的缓存与"线段扫掠 vs 全部肢体"的求交。
    ///
    /// <para>▍为什么世界坐标要单独缓存：一个单位一帧会被多颗子弹扫到，
    /// 每颗子弹都去读一次 <c>Transform.localToWorldMatrix</c> 会白算 N 倍。
    /// 生产形态应当是「每帧对候选单位 <see cref="UpdateWorld"/> 一次 → 本帧所有子弹共用」。</para>
    ///
    /// <para>⚠ 本类**不产生伤害**，只回答"打中哪个部位（<see cref="VirtualCollider.PartIndex"/>）"。
    /// 伤害结算仍然走 <c>Damageable.InflictDamage</c>。</para>
    /// </summary>
    public class VirtualUnitCollider
    {
        /// <summary>单位主人（拿 <c>Damageables</c> 下标、包围体）</summary>
        public Actor Actor { get; private set; }

        /// <summary>全部肢体代理（顺序 = 烘焙时的 Collider 层级序）</summary>
        public VirtualCollider[] Limbs { get; private set; }

        /// <summary>粗筛用包围球半径：相对 <c>Actor.transform.position</c> 的保守半径（世界尺度）</summary>
        public float BoundingRadius { get; private set; }

        /// <summary>部位表（= <c>Actor.Damageables</c>；编辑态下退化为层级序的 Damageable 数组）</summary>
        public IDamageable[] Parts { get; private set; }

        // 本帧世界胶囊（一次算好、多次复用），与 Limbs 同下标
        private Vector3[] _worldA;
        private Vector3[] _worldB;
        private float[] _worldR;

        // 部位级包围球（世界）：一次求交要筛几十个肢体，先用球扔掉绝大多数，再算胶囊
        private Vector3[] _worldC;
        private float[] _worldM;

        /// <summary>烘焙时被"层级/启用"过滤掉的碰撞体数量（排查"数学命中了物理看不到的东西"用）</summary>
        public static int LastBakeFilteredCount { get; private set; }

        /// <summary>把原 Collider 烘焙成虚拟集合。<paramref name="actor"/> 上没有可命中部位时返回 null。</summary>
        /// <param name="layerMask">哪些层的碰撞体参与命中（**必须与物理侧用的 mask 一致**）。
        /// <para>⚠ 这个参数不是可选的：虚拟集合只要多收了一个物理看不见的碰撞体，
        /// 子弹就会"穿过空气却造成伤害"（实测漏掉它会带来 52% 的幽灵命中）。</para></param>
        public static VirtualUnitCollider Bake(Actor actor, int layerMask = ~0)
        {
            if (actor == null) return null;
            LastBakeFilteredCount = 0;

            Collider[] cols = actor.GetComponentsInChildren<Collider>(true);
            if (cols == null || cols.Length == 0) return null;

            // 编辑态（未进 Play）Actor.Awake 没跑过 ⇒ Damageables 为空，这里按同一采集顺序退化解一次。
            // ⚠ 下标必须与运行时一致：Awake 用的就是 GetComponentsInChildren<Damageable>() 的层级序。
            IDamageable[] parts = actor.Damageables;
            if (parts == null || parts.Length == 0)
            {
                parts = actor.GetComponentsInChildren<Damageable>();
            }
            if (parts == null || parts.Length == 0) return null;

            var limbs = new System.Collections.Generic.List<VirtualCollider>(cols.Length);
            float boundR = 0f;

            for (int i = 0; i < cols.Length; ++i)
            {
                // ① 物理看不见的一律不能收：层不在命中集 / 组件被禁用 / 挂在未激活的物体上
                if (!cols[i].enabled || !cols[i].gameObject.activeInHierarchy)
                {
                    LastBakeFilteredCount++;
                    continue;
                }
                if ((layerMask & (1 << cols[i].gameObject.layer)) == 0)
                {
                    LastBakeFilteredCount++;
                    continue;
                }

                // ② 只烘焙"能映射到部位"的碰撞体：护盾 / CharacterController / 触发器不是肢体
                int part = IndexOfPart(cols[i], parts);
                if (part < 0) continue;

                if (!VirtualCollider.TryBake(cols[i], out VirtualCollider vc)) continue;
                vc.PartIndex = part;
                limbs.Add(vc);

                // 包围球：两端点相对 Actor 原点的距离 + 半径（用当前姿势，保守即可）
                vc.GetWorldPose(out Vector3 wa, out Vector3 wb, out float wr);
                Vector3 origin = actor.transform.position;
                float d = Mathf.Max((wa - origin).magnitude, (wb - origin).magnitude) + wr;
                if (d > boundR) boundR = d;
            }

            if (limbs.Count == 0) return null;

            return new VirtualUnitCollider
            {
                Actor = actor,
                Parts = parts,
                Limbs = limbs.ToArray(),
                BoundingRadius = boundR,
                _worldA = new Vector3[limbs.Count],
                _worldB = new Vector3[limbs.Count],
                _worldR = new float[limbs.Count],
                _worldC = new Vector3[limbs.Count],
                _worldM = new float[limbs.Count],
            };
        }

        /// <summary>Collider → 部位下标；护甲体（<see cref="TransferDamageable"/>）会归到它的主肢体。</summary>
        public static int IndexOfPart(Collider col, IDamageable[] parts)
        {
            if (col == null || parts == null) return -1;

            Damageable d = col.GetComponentInParent<Damageable>();
            if (d == null)
            {
                // 附属肢体（护甲片）自己不扣血，伤害转发给主肢体 ⇒ 部位下标取主肢体
                TransferDamageable td = col.GetComponentInParent<TransferDamageable>();
                if (td != null) d = td.source;
            }
            if (d == null) return -1;

            for (int i = 0; i < parts.Length; ++i)
            {
                if (ReferenceEquals(parts[i], d)) return i;
            }
            return -1;
        }

        /// <summary>刷新全部肢体的世界胶囊（一帧一次）。</summary>
        public void UpdateWorld()
        {
            int n = Limbs.Length;
            Transform last = null;
            Matrix4x4 m = Matrix4x4.identity;

            for (int i = 0; i < n; ++i)
            {
                // 同一个节点上常挂多个碰撞体（大部位）⇒ 矩阵只读一次
                Transform owner = Limbs[i].Owner;
                if (owner != null && !ReferenceEquals(owner, last))
                {
                    m = owner.localToWorldMatrix;
                    last = owner;
                }
                Limbs[i].GetWorldPose(m, out _worldA[i], out _worldB[i], out _worldR[i]);

                Vector3 a = _worldA[i];
                Vector3 b = _worldB[i];
                _worldC[i] = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, (a.z + b.z) * 0.5f);
                _worldM[i] = (a - b).magnitude * 0.5f + _worldR[i];
            }
        }

        /// <summary>
        /// 线段 <paramref name="from"/>→<paramref name="to"/>（扫掠球半径 <paramref name="radius"/>）
        /// 是否命中本单位任一肢体；命中时返回最近的肢体。
        ///
        /// <para>⚠ 调用方负责先调用 <see cref="UpdateWorld"/>：一帧内本单位被多颗子弹扫到时，
        /// 只该刷新一次世界坐标，重复刷新是纯浪费（这也是懒刷新方案能省下来的地方）。</para>
        /// </summary>
        /// <param name="limbIndex">命中的肢体在 <see cref="Limbs"/> 里的下标</param>
        /// <param name="t">命中点在位移段上的归一化位置</param>
        public bool TryHit(Vector3 from, Vector3 to, float radius,
            out int limbIndex, out float t, out Vector3 point)
        {
            limbIndex = -1;
            t = float.MaxValue;
            point = default;

            float best = float.MaxValue;
            int n = Limbs.Length;

            for (int i = 0; i < n; ++i)
            {
                // 一级：部位包围球（39 个肢体里通常只有 0~1 个能过）
                if (!VirtualColliderMath.SegmentOverlapsSphere(from, to, _worldC[i], _worldM[i] + radius)) continue;

                // 二级：胶囊精算
                if (!VirtualColliderMath.SweptSphereCapsule(
                        from, to, radius,
                        _worldA[i], _worldB[i], _worldR[i],
                        out float s, out Vector3 p))
                {
                    continue;
                }

                if (s < best)
                {
                    best = s;
                    limbIndex = i;
                    t = s;
                    point = p;
                }
            }

            return limbIndex >= 0;
        }

        /// <summary>粗筛：线段是否落在本单位包围球内（比逐肢体求交便宜一个量级）。</summary>
        public bool OverlapsSegment(Vector3 from, Vector3 to)
        {
            // 单位可能移动：包围球以 Actor 原点而不是烘焙时的位置为心
            Vector3 center = Actor != null ? Actor.transform.position : Vector3.zero;
            float r = BoundingRadius;
            return VirtualColliderMath.SqrDistanceSegmentToPoint(from, to, center) <= r * r;
        }
    }
}
*/
