using UnityEngine;
namespace Utils
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


    }
}
