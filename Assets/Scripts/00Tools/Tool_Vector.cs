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
        /// vector相乘
        /// </summary>
        /// <param name="a">坐标A</param>
        /// <param name="b">坐标B</param>
        /// <returns></returns>
        public static Vector3 Mult(this Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        //vector2本身就是逐一相乘
        //public static Vector2 Mult(this Vector2 a, Vector2 b) => new Vector3(a.x * b.x, a.y * b.y);

        /// <summary>
        /// vector相除
        /// </summary>
        /// <param name="a">坐标A</param>
        /// <param name="b">坐标B</param>
        /// <returns></returns>
        public static Vector3 Div(this Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        public static Vector2 Div(this Vector2 a, Vector2 b) => new Vector3(a.x / b.x, a.y / b.y);



        public static Vector2 Vector3To2(Vector3 vector) => new(vector.x, vector.z);
        public static Vector3 Vector2To3(Vector2 vector) => new(vector.x, 0, vector.y);

        public static Vector2Int ToInt(this Vector2 a) => new Vector2Int(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y));
        public static Vector3Int ToInt(this Vector3 a) => new Vector3Int(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), Mathf.RoundToInt(a.z));

        public static Vector2 ToFloat(this Vector2Int a) => new Vector2(a.x, a.y);
        public static Vector3 ToIntFloat(this Vector3Int a) => new Vector3(a.x, a.y, a.z);

        public static Vector2 ToVector2(this Vector3 vector) => new(vector.x, vector.z);
        public static Vector3 ToVector3(this Vector2 vector) => new(vector.x, 0, vector.y);

    }
}
