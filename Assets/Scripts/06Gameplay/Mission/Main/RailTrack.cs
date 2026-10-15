using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 一条轨道：折线 + 弧长表 + 设计路基纵坡。
    /// <para>铺轨（MissionInterceptTrain_Rail）与列车行驶（<see cref="TrainUnit"/>）共用同一份数据，
    /// 所以火车必然跑在铺出来的那条线上。</para>
    /// </summary>
    public class RailTrack
    {
        /// <summary>轨道序号（从 0 起，用于日志/发车时刻表）</summary>
        public int Index;

        /// <summary>平滑后的贴地折线（y = 采样时的地面高度，仅供记录；行驶高度看 <see cref="BedHeight"/>）</summary>
        public readonly List<Vector3> Points = new();

        /// <summary>Points 的累计水平弧长（米）</summary>
        public float[] Cumulative;

        /// <summary>总长（米）</summary>
        public float TotalLength;

        /// <summary>设计路基高度（沿弧长按 <see cref="ProfileStep"/> 采样）</summary>
        public float[] BedProfile;

        /// <summary>BedProfile 的采样间距（米）</summary>
        public float ProfileStep = 8f;

        /// <summary>起点 / 终点（世界 XZ）</summary>
        public Vector2 StartPoint, EndPoint;

        /// <summary>重算 <see cref="Cumulative"/> 与 <see cref="TotalLength"/></summary>
        public void BuildCumulative()
        {
            Cumulative = new float[Points.Count];
            for (int i = 1; i < Points.Count; i++)
            {
                Cumulative[i] = Cumulative[i - 1] + new Vector2(
                    Points[i].x - Points[i - 1].x, Points[i].z - Points[i - 1].z).magnitude;
            }
            TotalLength = Points.Count > 0 ? Cumulative[Points.Count - 1] : 0f;
        }

        /// <summary>弧长 s 处的世界 XZ（超界取端点）</summary>
        public Vector2 SampleXZ(float s) => SamplePolyline(Points, Cumulative, s);

        /// <summary>弧长 s 处的设计路基高度（米）</summary>
        public float BedHeight(float s)
        {
            if (BedProfile == null || BedProfile.Length == 0) return 0f;
            float t = Mathf.Clamp(s, 0f, TotalLength) / Mathf.Max(1f, ProfileStep);
            int i0 = Mathf.Clamp(Mathf.FloorToInt(t), 0, BedProfile.Length - 1);
            int i1 = Mathf.Min(i0 + 1, BedProfile.Length - 1);
            return Mathf.Lerp(BedProfile[i0], BedProfile[i1], t - i0);
        }

        /// <summary>弧长 s 处的朝向（含纵坡：x/z 用弦向，y 用路基坡度）</summary>
        public Vector3 SampleForward(float s, float step = 3f)
        {
            float s0 = Mathf.Clamp(s - step, 0f, TotalLength);
            float s1 = Mathf.Clamp(s + step, 0f, TotalLength);
            Vector2 a = SampleXZ(s0);
            Vector2 b = SampleXZ(s1);
            Vector2 flat = b - a;
            if (flat.sqrMagnitude < 0.000001f) return Vector3.forward;
            Vector3 dir = new Vector3(flat.x, 0f, flat.y).normalized;
            float grade = Mathf.Atan2(BedHeight(s1) - BedHeight(s0), flat.magnitude);
            return new Vector3(dir.x * Mathf.Cos(grade), Mathf.Sin(grade), dir.z * Mathf.Cos(grade)).normalized;
        }

        /// <summary>取折线上距起点 <paramref name="distance"/> 米处的 XZ 坐标（超末端则取末点）</summary>
        public static Vector2 SamplePolyline(List<Vector3> points, float[] cumulative, float distance)
        {
            if (points == null || points.Count == 0) return Vector2.zero;
            int last = points.Count - 1;
            if (distance <= 0f) return new Vector2(points[0].x, points[0].z);
            if (cumulative == null) return new Vector2(points[last].x, points[last].z);
            if (distance >= cumulative[last]) return new Vector2(points[last].x, points[last].z);
            int i = 1;
            while (i < last && cumulative[i] < distance) i++;
            float segStart = cumulative[i - 1];
            float segLength = Mathf.Max(0.0001f, cumulative[i] - segStart);
            float t = (distance - segStart) / segLength;
            return new Vector2(
                Mathf.Lerp(points[i - 1].x, points[i].x, t),
                Mathf.Lerp(points[i - 1].z, points[i].z, t));
        }
    }
}
