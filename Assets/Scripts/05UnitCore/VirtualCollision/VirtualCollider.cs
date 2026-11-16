// ============================================================================
// 【已归档 · 2026-11-16】虚拟碰撞体（VirtualCollider）Step 1 原型 —— 整份注释掉，不参与编译
//
// 结论：不采用。Play 实测（299 Actor / 863 部位碰撞体）Physics.SyncColliderTransform
//       ≈ 0.000~0.013 ms/帧，不是瓶颈；而数学求交单条查询比物理 SphereCast 慢 2~30 倍
//       （UnitQuery 粗筛 6.8~22 µs vs 物理 0.73 µs）⇒ 替代后是净亏。
// 恢复：删掉本段与文件末尾的 */ 即可（细节见 .codebuddy/memory/topics/virtual-collider.md）
// ============================================================================
/*
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 单个肢体碰撞体的**虚拟代理**：一段胶囊 + 它跟随的 Transform。
    ///
    /// <para>▍用途（Step 1 原型，尚未接管主流程）：替代肢体 <c>Collider</c> 参与
    /// "子弹打中了哪个部位"的判定，从而让肢体 collider 可以下线、省掉
    /// <c>Physics.SyncColliderTransform</c>。</para>
    ///
    /// <para>▍为什么几何存**局部空间**：代理必须跟随动画骨骼才有精度，而骨骼每帧都在动
    /// ⇒ 只能在 Bake 时定住局部胶囊，世界坐标交给 <see cref="VirtualUnitCollider.UpdateWorld"/>
    /// 每帧重算一次（一次算好、本帧所有子弹共用）。</para>
    ///
    /// <para>⚠ 半径用 <c>lossyScale</c> 最大值近似：非均匀缩放下的胶囊没法用单个半径表达，
    /// 现有的单位片段缩放基本是均匀的，这个误差可接受。</para>
    /// </summary>
    public struct VirtualCollider
    {
        /// <summary>代理跟随的 transform（= 原 Collider 所在节点，通常就是骨骼）</summary>
        public Transform Owner;

        /// <summary>胶囊轴 A 端（<see cref="Owner"/> 局部空间）</summary>
        public Vector3 LocalA;

        /// <summary>胶囊轴 B 端（<see cref="Owner"/> 局部空间）</summary>
        public Vector3 LocalB;

        /// <summary>胶囊半径（<see cref="Owner"/> 局部空间）</summary>
        public float LocalRadius;

        /// <summary>局部半径 → 世界半径的缩放（烘焙时定住）。
        /// <para>⚠ 别在每帧调 <c>Transform.lossyScale</c>：它内部要做矩阵分解，是这条路线上最贵的一次调用。</para></summary>
        public float RadiusScale;

        /// <summary>所属部位在 <c>Actor.Damageables</c> 里的下标；-1 = 认不出</summary>
        public int PartIndex;

        /// <summary>原始 Collider，仅原型期用于和物理结果逐条比对</summary>
        public Collider Source;

        public bool IsValid => Owner != null && LocalRadius > 0f;

        /// <summary>取本帧的世界胶囊（A 端 / B 端 / 世界半径）</summary>
        public void GetWorldPose(out Vector3 a, out Vector3 b, out float radius)
        {
            if (Owner == null)
            {
                a = b = Vector3.zero;
                radius = 0f;
                return;
            }
            GetWorldPose(Owner.localToWorldMatrix, out a, out b, out radius);
        }

        /// <summary>同上，但矩阵由调用方给（同一节点的多个碰撞体只读一次矩阵）。</summary>
        public void GetWorldPose(Matrix4x4 m, out Vector3 a, out Vector3 b, out float radius)
        {
            if (Owner == null)
            {
                a = b = Vector3.zero;
                radius = 0f;
                return;
            }

            a = m.MultiplyPoint3x4(LocalA);
            b = m.MultiplyPoint3x4(LocalB);
            radius = LocalRadius * RadiusScale;
        }

        /// <summary>
        /// 把真实 Collider 烘焙成胶囊代理。盒/网格体按"最长轴当轴向、最大横截面当半径"近似。
        /// </summary>
        /// <returns>false = 这个碰撞体不参与单位命中（触发器 / 未知形状）</returns>
        public static bool TryBake(Collider col, out VirtualCollider vc)
        {
            vc = default;

            // 触发器不是"实心部位"，命中判定里本来就被跳过（ProjectileStandard.IsHitValid）
            if (col == null || col.isTrigger) return false;

            Vector3 center;
            Vector3 axisDir;
            float halfAxis;
            float radius;

            switch (col)
            {
                case CapsuleCollider capsule:
                {
                    center = capsule.center;
                    radius = capsule.radius;
                    axisDir = capsule.direction == 0 ? Vector3.right
                            : capsule.direction == 2 ? Vector3.forward
                            : Vector3.up;
                    // Unity 的 capsule.height 是**含两端半球**的总高，轴向线段长度要减掉两个半径
                    halfAxis = Mathf.Max(0f, capsule.height * 0.5f - radius);
                    break;
                }
                case SphereCollider sphere:
                {
                    center = sphere.center;
                    radius = sphere.radius;
                    axisDir = Vector3.up;
                    halfAxis = 0f;
                    break;
                }
                case BoxCollider box:
                {
                    center = box.center;
                    Vector3 half = box.size * 0.5f;
                    int axis = 0;
                    if (half.y > half[axis]) axis = 1;
                    if (half.z > half[axis]) axis = 2;
                    axisDir = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                    radius = Mathf.Max(0.01f, Mathf.Max(half[(axis + 1) % 3], half[(axis + 2) % 3]));
                    halfAxis = Mathf.Max(0f, half[axis] - radius);
                    break;
                }
                default:
                {
                    // MeshCollider / TerrainCollider 等：退化成本地包围盒的近似胶囊
                    Bounds b = col.bounds;
                    Vector3 ext = col.transform.InverseTransformVector(b.extents);
                    ext = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z));
                    center = col.transform.InverseTransformPoint(b.center);
                    int axis = 0;
                    if (ext.y > ext[axis]) axis = 1;
                    if (ext.z > ext[axis]) axis = 2;
                    axisDir = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                    radius = Mathf.Max(0.01f, Mathf.Max(ext[(axis + 1) % 3], ext[(axis + 2) % 3]));
                    halfAxis = Mathf.Max(0f, ext[axis] - radius);
                    break;
                }
            }

            if (radius <= 0.001f) return false;

            vc.Owner = col.transform;
            vc.LocalA = center - axisDir * halfAxis;
            vc.LocalB = center + axisDir * halfAxis;
            vc.LocalRadius = radius;
            vc.PartIndex = -1;
            vc.Source = col;

            Vector3 scale = col.transform.lossyScale;
            vc.RadiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            if (vc.RadiusScale <= 0.0001f) vc.RadiusScale = 1f;

            return true;
        }
    }
}
*/
