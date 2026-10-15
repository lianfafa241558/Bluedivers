using System.Collections;
using UnityEngine;

namespace FPSGame.Utils
{
    public static partial class TerrainUtils
    {
        /// <summary>最近一次 <see cref="SmoothCorridorBed"/> 改动的地形像素数（实测/日志用）</summary>
        public static int LastCorridorPixels { get; private set; }

        /// <summary>最近一次 <see cref="SmoothCorridorBed"/> 的最大高差（米）</summary>
        public static float LastCorridorMaxDelta { get; private set; }

        /// <summary>
        /// 沿中心线把地形整成<b>连续路基</b>：目标高度沿弧长线性插值，断面横向按"内宽 → 淡化带"平滑过渡。
        /// <para>⚠ 与 <see cref="ModifyHeightMapRect"/> 的区别：那个是把内框整体压成一个水平面（一块一块），
        /// 斜坡上会形成 纵坡×板长 的台阶；这个是按弧长连续成型。</para>
        /// <para>⚠ 实现口径：<b>每个像素只由"最近的那一段"成型一次</b>（先全段扫描记下最近距离与目标，
        /// 最后再统一写入）。早期版本按段顺序逐个 lerp，弯道处相邻段的断面会重叠覆盖同一像素、被推两次且
        /// 两次目标不同 ⇒ 地形拼出一圈圈 V 形锯齿（2026-10-15 实测踩到）。</para>
        /// <para>代价：一次 GetHeights/SetHeights 覆盖整条走廊的包围盒，外加两个 patch 级 float 数组
        /// （≤ 8MB），所以只在"开局铺轨"这种一次性场合用。</para>
        /// </summary>
        /// <param name="centerline">中心线取样点（x/z = 世界位置，y = 该点的设计路基高度）</param>
        /// <param name="innerHalfWidth">内半宽（米）：这个横向范围内完全按设计高程成型</param>
        /// <param name="blendWidth">淡化带宽度（米）：从内宽到"不动地形"的过渡距离</param>
        /// <param name="deviationThreshold">软门阈值（米）：地面与设计高程差得比它多才动土；≤ 0 = 无条件成型</param>
        /// <param name="refresh">是否刷新地形与 NavMesh（调用方若要批量做，统一传 false，最后自己刷一次）</param>
        public static IEnumerator SmoothCorridorBed(Vector3[] centerline, float innerHalfWidth, float blendWidth,
            float deviationThreshold, bool refresh = true)
        {
            LastCorridorPixels = 0;
            LastCorridorMaxDelta = 0f;
            if (Main == null || centerline == null || centerline.Length < 2) yield break;

            TerrainData terrainData = data;
            if (terrainData == null) yield break;
            Vector3 origin = Main.GetPosition();
            Vector3 size = terrainData.size;
            int res = terrainData.heightmapResolution;
            if (size.x <= 0f || res < 2) yield break;

            float worldPerPixel = size.x / (res - 1);
            float reach = Mathf.Max(innerHalfWidth, 0f) + Mathf.Max(blendWidth, 0f);

            // 1. 中心线包围盒（外扩 reach）→ 高度图 patch
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < centerline.Length; i++)
            {
                minX = Mathf.Min(minX, centerline[i].x);
                maxX = Mathf.Max(maxX, centerline[i].x);
                minZ = Mathf.Min(minZ, centerline[i].z);
                maxZ = Mathf.Max(maxZ, centerline[i].z);
            }
            int xBase = Mathf.Clamp(Mathf.FloorToInt((minX - reach - origin.x) / worldPerPixel), 0, res - 1);
            int yBase = Mathf.Clamp(Mathf.FloorToInt((minZ - reach - origin.z) / worldPerPixel), 0, res - 1);
            int patchW = Mathf.Clamp(Mathf.CeilToInt((maxX + reach - origin.x) / worldPerPixel) - xBase + 1, 1, res - xBase);
            int patchH = Mathf.Clamp(Mathf.CeilToInt((maxZ + reach - origin.z) / worldPerPixel) - yBase + 1, 1, res - yBase);

            float[,] heights = terrainData.GetHeights(xBase, yBase, patchW, patchH);
            yield return null;

            // 2. 扫全段：每个像素只保留"最近段"的横向距离与目标高度（归一化）
            int slots = patchW * patchH;
            float[] bestDistance = new float[slots];
            float[] targetHeight = new float[slots];
            for (int i = 0; i < slots; i++) bestDistance[i] = float.PositiveInfinity;

            float startTime = Time.realtimeSinceStartup;
            for (int i = 0; i < centerline.Length - 1; i++)
            {
                Vector3 p3 = centerline[i];
                Vector3 q3 = centerline[i + 1];
                Vector2 p = new(p3.x, p3.z);
                Vector2 ab = new(q3.x - p3.x, q3.z - p3.z);
                float segmentLengthSq = ab.sqrMagnitude;
                if (segmentLengthSq < 0.0001f) continue;
                float hFrom = Mathf.Clamp01((p3.y - origin.y) / size.y);
                float hTo = Mathf.Clamp01((q3.y - origin.y) / size.y);

                float pxMin = Mathf.Min(p3.x, q3.x) - reach;
                float pxMax = Mathf.Max(p3.x, q3.x) + reach;
                float pzMin = Mathf.Min(p3.z, q3.z) - reach;
                float pzMax = Mathf.Max(p3.z, q3.z) + reach;
                int px0 = Mathf.Clamp(Mathf.FloorToInt((pxMin - origin.x) / worldPerPixel), xBase, xBase + patchW - 1);
                int px1 = Mathf.Clamp(Mathf.CeilToInt((pxMax - origin.x) / worldPerPixel), xBase, xBase + patchW - 1);
                int pz0 = Mathf.Clamp(Mathf.FloorToInt((pzMin - origin.z) / worldPerPixel), yBase, yBase + patchH - 1);
                int pz1 = Mathf.Clamp(Mathf.CeilToInt((pzMax - origin.z) / worldPerPixel), yBase, yBase + patchH - 1);

                for (int pz = pz0; pz <= pz1; pz++)
                {
                    float worldZ = origin.z + pz * worldPerPixel;
                    int row = pz - yBase;
                    for (int px = px0; px <= px1; px++)
                    {
                        float worldX = origin.x + px * worldPerPixel;
                        // 点到线段的最近点：用"投影参数"而不是"段的局部坐标系" ⇒ 弯道处不会出现断面重叠
                        Vector2 rel = new(worldX - p.x, worldZ - p.y);
                        float t = Mathf.Clamp01(Vector2.Dot(rel, ab) / segmentLengthSq);
                        float distance = (rel - ab * t).magnitude;
                        if (distance > reach) continue;

                        int slot = row * patchW + px - xBase;
                        if (distance >= bestDistance[slot]) continue;
                        bestDistance[slot] = distance;
                        targetHeight[slot] = Mathf.Lerp(hFrom, hTo, t);   // t 是弧长比例 ⇒ 目标沿弧长连续
                    }
                }

                if (Time.realtimeSinceStartup - startTime >= maxTimePerFrame)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }

            // 3. 写入：横向权重只看"到折线的距离"，软门用**原始高度**判断要不要动土 ⇒ 每个像素只成型一次
            int touched = 0;
            float maxDelta = 0f;
            for (int row = 0; row < patchH; row++)
            {
                for (int column = 0; column < patchW; column++)
                {
                    int slot = row * patchW + column;
                    float distance = bestDistance[slot];
                    if (float.IsPositiveInfinity(distance)) continue;

                    float weight = distance <= innerHalfWidth
                        ? 1f
                        : 1f - Mathf.SmoothStep(0f, 1f, (distance - innerHalfWidth) / Mathf.Max(0.01f, reach - innerHalfWidth));
                    if (weight <= 0.0001f) continue;

                    float current = heights[row, column];
                    float target = targetHeight[slot];
                    if (deviationThreshold > 0f)
                    {
                        float deviation = Mathf.Abs(current - target) * size.y;
                        weight *= Mathf.InverseLerp(deviationThreshold * 0.5f, deviationThreshold, deviation);
                        if (weight <= 0.0001f) continue;
                    }

                    float delta = Mathf.Abs(target - current) * size.y;
                    if (delta > maxDelta) maxDelta = delta;
                    heights[row, column] = Mathf.Lerp(current, target, weight);
                    touched++;
                }

                if ((row & 63) == 63)
                {
                    yield return null;
                    startTime = Time.realtimeSinceStartup;
                }
            }

            // 4. 一次写回 + 同步
            terrainData.SetHeightsDelayLOD(xBase, yBase, heights);
            terrainData.SyncHeightmap();
            LastCorridorPixels = touched;
            LastCorridorMaxDelta = maxDelta;
            if (refresh) AsyncRefresh(true);
        }
    }
}
