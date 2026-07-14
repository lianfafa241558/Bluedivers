using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

    /// <summary>
    /// 随机数工具
    /// </summary>
    public static class RandomUtils
    {
        private static System.Random random;

        public static float Range01 => (float)random.Next(0, 100) / 100f;

        static RandomUtils()
        {
            InitRandom();
        }

        /// <summary>
        /// 初始化随机数
        /// </summary>
        /// <param name="seed">保证每个客户端种子一致，得到的随机数一致</param>
        public static void InitRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public static void InitRandom()
        {
            DateTime now = DateTime.Now;
            random = new System.Random(now.Month * 1000 + now.Day + now.Hour * 10000 + now.Minute * 100);
        }

        /// <summary>
        /// 返回整数 [min,max)
        /// </summary>
        public static int Range(int min, int max)
        {
            return random.Next(min, max);
        }

        /// <summary>
        /// 返回整数 [min,max)
        /// </summary>
        public static int Range(this System.Random rand, int min, int max)
        {
            return rand.Next(min, max);
        }

        /// <summary>
        /// 仅返回2位小数
        /// </summary>
        public static float Range(float min, float max)
        {
            return (float)random.Next((int)(min * 100f), (int)(max * 100f)) / 100f;
        }

        /// <summary>
        /// 仅返回2位小数
        /// </summary>
        public static float Range(this System.Random rand, float min, float max)
        {
            return (float)rand.Next((int)(min * 100f), (int)(max * 100f)) / 100f;
        }

        /// <summary>
        /// 概率(0-100)
        /// </summary>
        public static bool Bool(float value = 50f)
        {
            return Range(0, 100) < 50;
        }

        public static bool Bool(this System.Random rand, float value = 50f)
        {
            return rand.Range(0, 100) < 50;
        }

        /// <summary>
        /// 随机排序
        /// </summary>
        public static List<T> RandomOrdering<T>(this List<T> list)
        {
            return list.OrderBy((T item) => Range(0, 100)).ToList();
        }

        public static List<T> RandomOrdering<T>(this System.Random rand, List<T> list)
        {
            return list.OrderBy((T item) => rand.Range(0, 100)).ToList();
        }

        public static Vector3 RandomVector3XZ()
        {
            return new Vector3(Range(0f, 1f), 0f, Range(0f, 1f)).normalized;
        }

        public static Vector3 RandomVector3XZ(this System.Random rand)
        {
            return new Vector3(rand.Range(0f, 1f), 0f, rand.Range(0f, 1f)).normalized;
        }

        public static Vector2 RandomVector2()
        {
            return new Vector2(Range(0f, 1f), Range(0f, 1f)).normalized;
        }

        public static Vector2 RandomVector2(this System.Random rand)
        {
            return new Vector2(rand.Range(0f, 1f), rand.Range(0f, 1f)).normalized;
        }

        public static T RandomTake<T>(int min = 0, int max = 0) where T : Enum
        {
            return (T)Enum.ToObject(typeof(T), Range(min, Enum.GetValues(typeof(T)).Length + max));
        }

        public static T RandomTake<T>(this System.Random rand, int min = 0, int max = 0) where T : Enum
        {
            return (T)Enum.ToObject(typeof(T), rand.Range(min, Enum.GetValues(typeof(T)).Length + max));
        }

        public static T RandomTake<T>(this IList<T> list)
        {
            return random.RandomTake(list);
        }

        public static T RandomTake<T>(this IList<T> list, System.Random rand)
        {
            return rand.RandomTake(list);
        }

        public static T RandomTake<T>(this IList<T> list, bool takeOut = false)
        {
            return random.RandomTake(list, takeOut);
        }

        public static T RandomTake<T>(this System.Random rand, IList<T> list, bool takeOut = false)
        {
            if (list == null || list.Count == 0)
            {
                return default(T);
            }

            int index = rand.Range(0, list.Count);
            T result = list[index];
            if (takeOut)
            {
                list.RemoveAt(index);
            }

            return result;
        }

        public static Vector2 InsideUnitCircle(this System.Random rand)
        {
            float num = Mathf.Sqrt(rand.Range(0f, 1f));
            float f = rand.Range(0f, (float)Math.PI * 2f);
            return new Vector2(num * Mathf.Cos(f), num * Mathf.Sin(f));
        }

        public static Vector2 InsideUnitCircle()
        {
            return random.InsideUnitCircle();
        }

        public static Vector2 OnUnitCircle(this System.Random rand)
        {
            float f = rand.Range(0f, (float)Math.PI * 2f);
            return new Vector2(Mathf.Cos(f), Mathf.Sin(f));
        }

        public static Vector2 OnUnitCircle()
        {
            return random.OnUnitCircle();
        }

        public static Vector3 OnUnitSphere(this System.Random rand)
        {
            float num = Mathf.Pow(rand.Range(0f, 1f), 0.33f);
            float f = (float)Math.PI * 2f * rand.Range(0f, 1f);
            float f2 = Mathf.Acos(2f * rand.Range(0f, 1f) - 1f);
            float x = num * Mathf.Sin(f2) * Mathf.Cos(f);
            float y = num * Mathf.Sin(f2) * Mathf.Sin(f);
            float z = num * Mathf.Cos(f2);
            return new Vector3(x, y, z);
        }

        public static Vector3 OnUnitSphere()
        {
            return random.OnUnitSphere();
        }
    }
