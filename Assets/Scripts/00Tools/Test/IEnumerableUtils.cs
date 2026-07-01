using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;
using Random = System.Random;
namespace Utils
{
    public static class IEnumerableUtils
    {

        public static T WeightTake<T>(this List<KVP<int,T>> list,int totleWeight,Random random)
        {
            int randomValue = random.Range(1,totleWeight+1);
            for (int i=0,v=0;i<list.Count;++i)
            {
                v += list[i].Key;
                if (randomValue < v)
                {
                    return list[i].Value;
                }
            }
            return list[0].Value;
        }
        public static T WeightTake<T>(this KVP<T, int>[] list, int totleWeight, Random random)
        {
            int randomValue = random.Range(1, totleWeight + 1);
            for (int i = 0, v = 0; i < list.Length; ++i)
            {
                v += list[i].Value;
                if (randomValue < v)
                {
                    return list[i].Key;
                }
            }
            return list[0].Key;
        }

        public static T WeightTake<T>(this List<SKVP<int, T>> list, int totleWeight, Random random)
        {
            int randomValue = random.Range(1, totleWeight + 1);
            for (int i = 0, v = 0; i < list.Count; ++i)
            {
                v += list[i].Key;
                if (randomValue < v)
                {
                    return list[i].Value;
                }
            }
            return list[0].Value;
        }
        public static T WeightTake<T>(this SKVP<T, int>[] list, int totleWeight, Random random)
        {
            int randomValue = random.Range(1, totleWeight + 1);
            for (int i = 0, v = 0; i < list.Length; ++i)
            {
                v += list[i].Value;
                if (randomValue < v)
                {
                    return list[i].Key;
                }
            }
            return list[0].Key;
        }


        public static T NaturalWeightTake<T>(this List<T> list, bool takeOut = false)
        {
            T re;
            int l = list.Count;
            int value = 0;
            for (int i = 1; i <= l; i++) value += i;

            for (int i = 0; i < l; ++i)
            {
                if ((value -= l - i) < 0)
                {
                    re = list[i];
                    return re;
                }
            }
            re = list[0];
            if (takeOut) list.RemoveAt(0);
            return re;
        }

        public static int Sum(int[] arr)
        {
            int re = 0;
            for (int i = 0; i < arr.Length; ++i) re += arr[i];
            return re;
        }
        public static float Sum(List<float> list)
        {
            float re = 0;
            for (int i = 0; i < list.Count; ++i) re += list[i];
            return re;
        }

        public static int Sum(bool[] arr, bool state = true)
        {
            int count = 0;
            for (int i = 0, l = arr.Length; i < l; ++i) if (arr[i] == state) ++count;
            return count;
        }

        public static T[] GetKeys<T, W>(this Dictionary<T, W> dic)
        {
            T[] keys = new T[dic.Count];
            dic.Keys.CopyTo(keys, 0);
            return keys;
        }
          
        public static List<KVP<K, V>> ToList<K, V>(this DisplayDic<K, V> dis)
        {
            List<KVP<K, V>> list = new();
            foreach (var item in dis)
            {
                list.Add(new(item.Key, item.Value));
            }
            return list;
        }

        public static List<KVP<K, V>> ToList<K, V>(this Dictionary<K, V> dis)
        {
            List<KVP<K, V>> list = new();
            foreach (var item in dis)
            {
                list.Add(new(item.Key, item.Value));
            }
            return list;
        }

        public static bool TryGet<T>(this List<T> list, System.Predicate<T> match, out T output)
        {
            int index = list.FindIndex(match);
            if (index != -1)
            {
                output = list[index];
                return true;
            }
            else
            {
                output = default;
                return false;
            }

        }


        public static bool Contains<K, V>(this List<KVP<K, V>> list, K key)
        {
            return list.FindIndex(item => key.Equals(item.Key)) > -1;
        }

        public static int Index<K, V>(this List<KVP<K, V>> list, K key)
        {
            return list.FindIndex(item => key.Equals(item.Key));
        }
        public static Dictionary<K, V> ToDictionary<K, V>(this DisplayDic<K, V> kvps)
        {
            if (kvps == null) Debug.LogError("错误：List不存在");
            Dictionary<K, V> dic = new();
            kvps.ForEach((key,value) => dic.Add(key, value));
            //kvps.Clear();
            return dic;
        }
        public static Dictionary<K, V> ToDictionary<K, V>(this List<KVP<K, V>> kvps)
        {
            if (kvps == null) Debug.LogError("错误：List不存在");
            Dictionary<K, V> dic = new();
            kvps.ForEach(item => dic.Add(item.Key, item.Value));
            //kvps.Clear();
            return dic;
        } 
        public static Dictionary<K, V> ToDictionary<K, V>(this IEnumerable<V> list, System.Func<V, K> func)
        {
            Dictionary<K, V> dic = new();
            foreach (var item in list)
            {
                dic.Add(func.Invoke(item), item);
            }
            return dic;
        }

        public static List<T> ToVaild<T>(this List<T> list) => list.Where(item => item.IsValid()).ToList();


        public static List<T> Keys<T, K>(this List<KVP<T, K>> list) => list.Select(item => item.Key).ToList();

        public static List<K> Values<T, K>(this List<KVP<T, K>> list) => list.Select(item => item.Value).ToList();

        public static V GetValue<K, V>(this List<KVP<K, V>> list, K key)
        {
            var re = list.Find(item => item.Key.Equals(key));
            if (re.IsValid()) return re.Value;
            return default;
        }
        public static V GetValue<K, V>(this List<SKVP<K, V>> list, K key)
            where K : struct
            where V : struct
        {
            var re = list.Find(item => item.Key.Equals(key));
            if (re.IsValid()) return re.Value;
            return default;
        }

        public static void SetValue<K, V>(this List<KVP<K, V>> list, K key, V value)
        {
            var re = list.Find(item => item.Key.Equals(key));
            if (re.IsValid()) re.Value = value;
            else list.Add(new(key, value));
        }
        public static void SetValue<K, V>(this List<SKVP<K, V>> list, K key, V value)
            where K : struct
            where V : struct
        {
            var re = list.Find(item => item.Key.Equals(key));
            if (re.IsValid()) re.Value = value;
            else list.Add(new(key, value));
        }



        public static string ToString<T>(this IEnumerable<T> list, string interval)
        {
            string re = "";
            foreach (var item in list) re += item + interval;
            return re;
        }

        public static int FindIndex<T>(this T[] arr, System.Predicate<T> match)
        {
            for (int i = 0, l = arr.Length; i < l; ++i)
            {
                if (match(arr[i]))
                {
                    return i;
                }
            }
            return -1;
        }


    }
}