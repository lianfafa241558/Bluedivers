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
    /// 虚拟碰撞体的求交数学（纯静态、零分配、不碰物理世界）。
    ///
    /// <para>▍为什么是「胶囊 vs 胶囊」而不是「射线 vs 胶囊」：子弹本身是个带半径的球，
    /// 一帧的位移是一条线段 ⇒ 扫掠体积就是 **半径 = 弹体半径的胶囊**。两个胶囊相交的判据
    /// 退化成"两条轴线段的最短距离 ≤ 两个半径之和"，比给胶囊做膨胀再求交更简洁也更准。</para>
    ///
    /// <para>▍为什么全用裸 float 写、不调 <c>Vector3.Dot</c> / <c>Mathf.Clamp01</c>：
    /// 这里每秒要被调几十万次，而这些小方法都在别的程序集里、Mono 内联不进来，
    /// 单次调用开销能占到整个求交的一半（实测 0.36µs/次，纯浮点运算本该在 0.05µs 量级）。</para>
    ///
    /// <para>⚠ 返回的 <c>t</c> 是**最近点在位移段上的归一化位置**，不是解析入射点。
    /// 用来排序"先打中哪个肢体"够用；要做弹孔精确位置时得换成解析解。</para>
    /// </summary>
    public static class VirtualColliderMath
    {
        private const float Eps = 1e-6f;

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>两条线段的最近点对（Ericson《Real-Time Collision Detection》5.1.9）。</summary>
        /// <param name="s">最近点在线段 P 上的归一化位置 [0,1]</param>
        /// <param name="t">最近点在线段 Q 上的归一化位置 [0,1]</param>
        public static void ClosestPointsSegmentSegment(
            Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1,
            out float s, out float t, out Vector3 cp, out Vector3 cq)
        {
            float d1x = p1.x - p0.x, d1y = p1.y - p0.y, d1z = p1.z - p0.z;
            float d2x = q1.x - q0.x, d2y = q1.y - q0.y, d2z = q1.z - q0.z;
            float rx = p0.x - q0.x, ry = p0.y - q0.y, rz = p0.z - q0.z;

            float a = d1x * d1x + d1y * d1y + d1z * d1z;
            float e = d2x * d2x + d2y * d2y + d2z * d2z;
            float f = d2x * rx + d2y * ry + d2z * rz;

            // 两边都退化成点：直接比两点距离
            if (a <= Eps && e <= Eps)
            {
                s = 0f;
                t = 0f;
                cp = p0;
                cq = q0;
                return;
            }

            if (a <= Eps)
            {
                s = 0f;
                t = Clamp01(f / e);
            }
            else
            {
                float c = d1x * rx + d1y * ry + d1z * rz;
                if (e <= Eps)
                {
                    t = 0f;
                    s = Clamp01(-c / a);
                }
                else
                {
                    float b = d1x * d2x + d1y * d2y + d1z * d2z;
                    float denom = a * e - b * b;

                    // denom ≈ 0 表示两条线段平行：任取 s = 0，靠后面的端点裁剪兜住
                    s = denom > Eps ? Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;

                    if (t < 0f)
                    {
                        t = 0f;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Clamp01((b - c) / a);
                    }
                }
            }

            cp = new Vector3(p0.x + d1x * s, p0.y + d1y * s, p0.z + d1z * s);
            cq = new Vector3(q0.x + d2x * t, q0.y + d2y * t, q0.z + d2z * t);
        }

        /// <summary>扫掠球（线段 <paramref name="p0"/>→<paramref name="p1"/>，半径 <paramref name="ra"/>）
        /// 是否命中胶囊（线段 <paramref name="q0"/>→<paramref name="q1"/>，半径 <paramref name="rb"/>）。</summary>
        /// <param name="t">命中位置在位移段上的归一化比例，用来在多个肢体间取最近</param>
        public static bool SweptSphereCapsule(
            Vector3 p0, Vector3 p1, float ra,
            Vector3 q0, Vector3 q1, float rb,
            out float t, out Vector3 point)
        {
            ClosestPointsSegmentSegment(p0, p1, q0, q1, out float s, out _, out Vector3 cp, out Vector3 cq);

            t = s;
            point = cp;

            float dx = cp.x - cq.x, dy = cp.y - cq.y, dz = cp.z - cq.z;
            float sum = ra + rb;
            return dx * dx + dy * dy + dz * dz <= sum * sum;
        }

        /// <summary>线段是否与球相交（部位级早退：一次求交要筛几十个肢体，先用球把绝大多数扔掉）。</summary>
        public static bool SegmentOverlapsSphere(Vector3 p0, Vector3 p1, Vector3 center, float radius)
        {
            float abx = p1.x - p0.x, aby = p1.y - p0.y, abz = p1.z - p0.z;
            float len2 = abx * abx + aby * aby + abz * abz;

            float t = 0f;
            if (len2 > Eps)
            {
                float dot = (center.x - p0.x) * abx + (center.y - p0.y) * aby + (center.z - p0.z) * abz;
                t = Clamp01(dot / len2);
            }

            float dx = center.x - (p0.x + abx * t);
            float dy = center.y - (p0.y + aby * t);
            float dz = center.z - (p0.z + abz * t);
            return dx * dx + dy * dy + dz * dz <= radius * radius;
        }

        /// <summary>线段到点的最短距离平方（粗筛用）。</summary>
        public static float SqrDistanceSegmentToPoint(Vector3 p0, Vector3 p1, Vector3 point)
        {
            float abx = p1.x - p0.x, aby = p1.y - p0.y, abz = p1.z - p0.z;
            float len2 = abx * abx + aby * aby + abz * abz;

            float t = 0f;
            if (len2 > Eps)
            {
                float dot = (point.x - p0.x) * abx + (point.y - p0.y) * aby + (point.z - p0.z) * abz;
                t = Clamp01(dot / len2);
            }

            float dx = point.x - (p0.x + abx * t);
            float dy = point.y - (p0.y + aby * t);
            float dz = point.z - (p0.z + abz * t);
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
*/
