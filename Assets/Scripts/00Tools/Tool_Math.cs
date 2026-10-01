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
        /// 按位取有多少 ?
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int CountOnes(this int number)
        {
            int count = 0;
            while (number > 0)
            {
                number &= (number - 1); // 清除最低位 ?
                ++count;
            }
            return count;
        }

        /// <summary>
        /// 判断一个Enum值是否在(a,b)开区间 ?
        /// </summary>
        public static bool In(this System.Enum value, System.Enum a, System.Enum b)
        {
            int valueInt = System.Convert.ToInt32(value);
            int aInt = System.Convert.ToInt32(a);
            int bInt = System.Convert.ToInt32(b);

            int min = Mathf.Min(aInt, bInt);
            int max = Mathf.Max(aInt, bInt);

            return valueInt > min && valueInt < max;
        }

        /// <summary>
        /// 判断一个值是否在(a,b)开区间 ?
        /// </summary>
        public static bool In(int value, int a, int b)
        {
            return value > Mathf.Min(a, b) && value < Mathf.Max(a, b);
        }
        public static bool In(float value, float a, float b, float allowError = 0)
        {
            return value > Mathf.Min(a, b) - allowError && value < Mathf.Max(a, b) + allowError;
        }
        public static bool In(Vector3 value, Vector3 a, Vector3 b)
        {
            return value.x > Mathf.Min(a.x, b.x) && value.x < Mathf.Max(a.x, b.x) &&
                    value.y > Mathf.Min(a.y, b.y) && value.y < Mathf.Max(a.y, b.y) &&
                    value.z > Mathf.Min(a.z, b.z) && value.z < Mathf.Max(a.z, b.z);
        }
        public static bool In2D(Vector3 value, Vector3 a, Vector3 b)
        {
            return value.x > Mathf.Min(a.x, b.x) && value.x < Mathf.Max(a.x, b.x) &&
                    value.y > Mathf.Min(a.y, b.y) && value.y < Mathf.Max(a.y, b.y);
        }
        public static bool In3D(Vector3 value, Vector3 a, Vector3 b)
        {
            return value.x > Mathf.Min(a.x, b.x) && value.x < Mathf.Max(a.x, b.x) &&
                    value.y > Mathf.Min(a.y, b.y) && value.y < Mathf.Max(a.y, b.y) &&
                    value.z > Mathf.Min(a.z, b.z) && value.z < Mathf.Max(a.z, b.z);
        }


        //public static float LoginDis(Vector3 a, Vector3 b) => Vector2.Distance(new(a.x, a.z), new(b.x, b.z));
        /// <summary>
        /// 返回X位小 ?
        /// </summary>
        public static float Round(float value, int digit = 1) => Mathf.Round(value * Mathf.Pow(10, digit)) * Mathf.Pow(10, -digit);
        public static Vector3 Round(Vector3 value, int digit = 1) => new(Round(value.x, digit), Round(value.y, digit), Round(value.z, digit));

        /// <summary>正余 ?/summary>
        public static int PositiveRemainder(int value, int remainder)
        {
            return (value + remainder) % remainder;
        }



        public static bool Calculate(this CompareOperate operate, float source, float target)
        {
            return operate switch {
                CompareOperate.Equal => Mathf.Approximately(source, target),//这个是相似的方法
                CompareOperate.NotEqual => !Mathf.Approximately(source, target),
                CompareOperate.Less => source < target,
                CompareOperate.LessEqual => source <= target,
                CompareOperate.Greater => source > target,
                CompareOperate.GreaterEqual => source >= target,
                CompareOperate.Contain => (int)source != 0 && ((int)source & (int)target) == (int)target,
                CompareOperate.NotContain => (int)source != 0 && ((int)source & (int)target) == 0,
                _ => throw new System.ArgumentException("找不到操作符" + operate),
            };
        }

        public static float PreventZero(this float value)
    => value <= 0 ? 1 : value;

        public static float Difference(float a, float b) => Mathf.Abs(a - b);

        public static float Lerp(this Vector2 vector, float scale) => Mathf.Lerp(vector.x, vector.y, scale);

        public static int Lerp(this Vector2Int vector, float scale) => Mathf.RoundToInt(Mathf.Lerp(vector.x, vector.y, scale));


        public static float Mapping01(float min, float max, float value) => (value - min) / (max - min);
        public static float Mapping(float min, float max, float value, float scale) => (value - min) / (max - min) * scale;
        public static float Mapping(float min, float max, float scale) => min + (max - min) * scale;
        public static float Mapping(Vector2 map, float scale) => map.x + (map.y - map.x) * scale;


        public static Vector2 Mapping01(Vector2 ldPoint, Vector2 rtPoint, Vector2 value) => (value - ldPoint) / (rtPoint - ldPoint);
        public static Vector2 Mapping(Vector2 ldPoint, Vector2 rtPoint, Vector2 value, Vector2 newMap) => (value - ldPoint) / (rtPoint - ldPoint) * newMap;

    }
}
