using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using FPSGame.Core;
using FPSGame.Attributes;

namespace FPSGame.Utils
{
    public static partial class Tool
{
        /// <summary>
        /// 角度归一化函数，确保角度 ?-180,180)范围 ?
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            angle %= 360;
            if (angle > 180) angle -= 360;
            return angle;
        }

        /// <summary>
        /// 是否为有限值（不是 NaN、不是 ±Inf）。
        /// <para>▍为什么要它：世界坐标/换算结果一旦是 NaN，写进 RectTransform 就让 Canvas **每帧**刷
        /// "<c>Invalid AABB inAABB</c>"（Unity 原生日志、**不带对象名**，极难定位）⇒ UI 侧从"世界→屏幕/
        /// 地图"算出来的坐标与尺寸，落盘前都应过一道。</para>
        /// </summary>
        public static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <inheritdoc cref="IsFinite(float)"/>
        public static bool IsFinite(Vector2 v) => IsFinite(v.x) && IsFinite(v.y);

        /// <inheritdoc cref="IsFinite(float)"/>
        public static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

        /// <summary>
        /// 碰撞体上的随机一 ?
        /// </summary>
        public static Vector3 RandomBoundsPoint(this Collider collider, out Quaternion normal)
        {
            Vector3 pos = collider.ClosestPointOnBounds(collider.transform.position + RandomVctor3() * 10);

            Bounds bounds = collider.bounds;
            Vector3 center = bounds.center;
            Vector3 pointLocal = pos - center;

            // 计算各轴向绝对偏移量
            float xDist = Mathf.Abs(pointLocal.x);
            float yDist = Mathf.Abs(pointLocal.y);
            float zDist = Mathf.Abs(pointLocal.z);
            Vector3 dir;
            // 确定主导 ?
            if (xDist > yDist && xDist > zDist)
                dir = new Vector3(Mathf.Sign(pointLocal.x), 0, 0);
            else if (yDist > zDist)
                dir = new Vector3(0, Mathf.Sign(pointLocal.y), 0);
            else
                dir = new Vector3(0, 0, Mathf.Sign(pointLocal.z));
            normal = Quaternion.FromToRotation(Vector3.up, dir);
            return pos;
        }

        /// <summary>
        /// 碰撞体内的随机一 ?
        /// </summary>
        public static Vector3 RandomPoint(this Collider collider, out Quaternion normal)
        {
            Vector3 pos = collider.ClosestPoint(collider.transform.position + RandomVctor3() * 10);

            Bounds bounds = collider.bounds;
            Vector3 center = bounds.center;
            Vector3 pointLocal = pos - center;

            // 计算各轴向绝对偏移量
            float xDist = Mathf.Abs(pointLocal.x);
            float yDist = Mathf.Abs(pointLocal.y);
            float zDist = Mathf.Abs(pointLocal.z);
            Vector3 dir;
            // 确定主导 ?
            if (xDist > yDist && xDist > zDist)
                dir = new Vector3(Mathf.Sign(pointLocal.x), 0, 0);
            else if (yDist > zDist)
                dir = new Vector3(0, Mathf.Sign(pointLocal.y), 0);
            else
                dir = new Vector3(0, 0, Mathf.Sign(pointLocal.z));
            normal = Quaternion.FromToRotation(Vector3.up, dir);
            return pos;
        }
        public static Vector3 RandomVctor3()
        {
            return Random.rotation * Vector3.forward;
        }

        public static float Distance(this Collider collider, Vector3 pos)
        {
            return Vector3.Distance(collider.ClosestPointOnBounds(pos), pos);
        }
        public static float Distance(this Collider a, Collider b)
        {
            return Vector3.Distance(a.ClosestPointOnBounds(b.transform.position), b.ClosestPointOnBounds(a.transform.position));
        }

        /// <summary>
        /// 求圆内一点沿 direction 方向发射，与圆的交点
        /// </summary>
        /// <param name="center">圆心</param>
        /// <param name="radius">圆半 ?/param>
        /// <param name="point">圆内一 ?/param>
        /// <param name="direction">方向</param>
        /// <returns>射线与圆的交 ?/returns>
        public static Vector2 GetCircleIntersection(Vector2 center, float radius, Vector2 point, Vector2 direction)
        {
            // 1. 方向归一 ?
            Vector2 dir = direction.normalized;

            // 2. 从圆心指向点的向 ?
            Vector2 oc = point - center;

            // 3. 解一元二次方 ?
            float a = Vector2.Dot(dir, dir);
            float b = 2 * Vector2.Dot(oc, dir);
            float c = Vector2.Dot(oc, oc) - radius * radius;

            float discriminant = b * b - 4 * a * c;
            float t = (-b + Mathf.Sqrt(discriminant)) / (2f * a);// 取最近的交点

            // 4. 计算交点
            return point + dir * t;
        }




        /// <summary>
        /// 夹角度数(0-360)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static float VectorAngle(Vector2 from, Vector2 to)
        {
            float angle;
            Vector3 cross = Vector3.Cross(from, to);
            angle = Vector2.Angle(from, to);
            return cross.z < 0 ? 360 - angle : angle;
        }
        /// <summary>
        /// 点到线的距离
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="target"></param>
        /// <param name="front"></param>
        /// <returns></returns>
        public static float PointToLineDis(Vector3 p1, Vector3 p2, Vector3 target, bool front = true)
        {
            Vector3 p1_2 = p2 - p1;//p1->p2的向 ?
            Vector3 p1_target = target - p1;//p1->target向量
                                            //Debug.LogWarning("夹角"+Vector3.Angle(p1_2, p1_target));
            p1_2.y = 0;
            p1_target.y = 0;
            if (front && Vector3.Angle(p1_2, p1_target) > 90) return 99;
            Vector3 p1f = Vector3.Project(p1_target, p1_2);//计算投影p1->f
            return Vector3.Distance(target, p1f + p1);// 加上p1坐标 然后计算距离
        }


        /// <summary>
        /// 点到点的距离 ?
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static float PointToVectorDis(Vector3 p1, Vector3 p2) => Vector3.Project(p2, p1).magnitude;

        public static Vector3 Clamp(Vector3 value, Vector3 a, Vector3 b) => new(
            Mathf.Clamp(value.x, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x)),
            Mathf.Clamp(value.y, Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y)),
            Mathf.Clamp(value.z, Mathf.Min(a.z, b.z), Mathf.Max(a.z, b.z)));


        /// <summary>
        /// 限制在一个圆角矩 ?近似) ?输入值需要标准化)
        /// </summary>
        public static Vector2 ClampRoundedRectangle(Vector2 value, Vector2 ellipse, Vector2 rectangle)
        {
            //value = GetEllipseIntersection(value, ellipse.x, ellipse.y);
            value = FindEllipseIntersection(value, ellipse);
            value.x = Mathf.Clamp(value.x, -rectangle.x, rectangle.x);
            value.y = Mathf.Clamp(value.y, -rectangle.y, rectangle.y);
            return value;
        }

        /// <summary>
        /// 矢量和椭圆形的交 ?宽度和高度都是半 ?
        /// </summary>
        public static Vector2 GetEllipseIntersection(Vector2 v, float width, float height)
        {
            if (v == Vector2.zero)
                return Vector2.zero;

            float vx = v.x;
            float vy = v.y;

            // 计算 t^2
            float denominator = (vx * vx) / width / width + (vy * vy) / height / height;

            // 如果 denominator  ?0 或小 ?0，说明没有交 ?
            Debug.LogWarning("限制器在椭圆" + (denominator <= 0) + " " + denominator);
            if (denominator <= 0)
            {
                return v; // 返回原始矢量
            }

            float tSquared = 1 / denominator;
            float t = Mathf.Sqrt(tSquared);
            return t * v; // 返回交点
        }


        public static Vector2 FindEllipseIntersection(Vector2 v, Vector2 ellipse)
        {
            if (v == Vector2.zero)
                return Vector2.zero;

            float a = ellipse.x;
            float b = ellipse.y;

            float denominatorX = a * a;
            float denominatorY = b * b;

            float s = (v.x * v.x) / denominatorX + (v.y * v.y) / denominatorY;

            if (s <= 1)
                return v;

            float t = 1f / Mathf.Sqrt(s);

            return t <= 1f ? new Vector2(v.x * t, v.y * t) : v;
        }

        /// <summary>
        ///矢量是否在圆角矩形内
        ///先判断是否在矩形内，然后判断是否在椭圆内
        /// </summary>
        public static bool InRoundedRectangle(Vector2 v, Vector2 ellipse, Vector2 rectangle)
        {
            if (!In2D(v, -rectangle, rectangle))
            {
                return false;
            }
            float denominator = (v.x * v.x) / (ellipse.x * ellipse.x) + (v.y * v.y) / (ellipse.y * ellipse.y);
            // 如果 denominator  ?0 或小 ?0，说明没有交 ?
            //Debug.LogWarning("在椭圆内"+(denominator <= 0)+" "+ denominator);
            return denominator <= 1;
        }


        /// <summary>
        /// 线段到线段的距离
        /// </summary>
        /// <param name="seg1Start"></param>
        /// <param name="seg1End"></param>
        /// <param name="seg2Start"></param>
        /// <param name="seg2End"></param>
        /// <returns></returns>
        public static float LineToLineDis(Vector3 seg1Start, Vector3 seg1End, Vector3 seg2Start, Vector3 seg2End)
        {
            Vector3 closestPoint1 = PointToLinePoint(seg1Start, seg1End, seg2Start);
            Vector3 closestPoint2 = PointToLinePoint(seg1Start, seg1End, seg2End);

            float distance1 = Vector3.Distance(closestPoint1, seg2Start);
            float distance2 = Vector3.Distance(closestPoint2, seg2End);

            float distance = Mathf.Min(distance1, distance2);
            return distance;
        }
        /// <summary>
        /// 点到线的最近点
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Vector3 PointToLinePoint(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            direction.Normalize();

            float t = Mathf.Clamp01(Vector3.Dot(point - start, direction) / length);
            Vector3 closestPoint = start + t * direction * length;

            return closestPoint;
        }


    }
}
