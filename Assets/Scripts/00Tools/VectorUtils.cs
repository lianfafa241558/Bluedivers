using UnityEngine;
namespace FPSGame.Utils
{
    public static class VectorUtils
    {
        public static Vector3 GetRandomDirectionXZ()
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            return new Vector3(randomCircle.x, 0f, randomCircle.y);
        }


        public static Vector3 GetRandomPointInCircle(this Vector3 center, float minRadius, float maxRadius)
        {
            float radius = Random.Range(minRadius, maxRadius);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);

            return center + new Vector3(x, 0, z);
        }

        /// <summary>
        /// 同上，但用**调用方给的随机流**（联机时"同一种子 ⇒ 同一落点"的必备重载）。
        /// <para>▍为什么必须有：上面那个走的是 <c>UnityEngine.Random</c>（全局静态），
        /// 会被音效/弹孔/武器散布推进游标 ⇒ 两端同一波会撒到不同位置（波次同步的坑之一）。</para>
        /// </summary>
        public static Vector3 GetRandomPointInCircle(this Vector3 center, System.Random rand, float minRadius, float maxRadius)
        {
            if (rand == null) return center.GetRandomPointInCircle(minRadius, maxRadius);

            float radius = rand.Range(minRadius, maxRadius);
            float angle = rand.Range(0f, 360f) * Mathf.Deg2Rad;

            return center + new Vector3(radius * Mathf.Cos(angle), 0, radius * Mathf.Sin(angle));
        }


    }
}
