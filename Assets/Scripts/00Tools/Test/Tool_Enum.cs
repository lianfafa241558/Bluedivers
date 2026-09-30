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
        /// 字符串转枚举
        /// </summary>
        /// <param name="str">字符 ?/param>
        /// <param name="defaultValue">默认 ?/param>
        /// <param name="ignoreCase">大小写敏感 ?/param>
        /// <returns></returns>
        public static T StringToEnum<T>(string str, T defaultValue = default, bool ignoreCase = true) where T : struct, System.Enum
        {
            // 空 ?空字符串直接返回默认 ?
            if (string.IsNullOrEmpty(str))
            {
                return defaultValue;
            }

            // 尝试转换，失败返回默认 ?
            if (System.Enum.TryParse(str, ignoreCase, out T result))
            {
                return result;
            }
            else
            {
                Debug.LogWarning($"字符串「{str}」无法转换为枚举{typeof(T).Name}，使用默认值：{defaultValue}");
                return defaultValue;
            }
        }

        public static string GetEnumString(this System.Enum value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());
            var attribute = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
            return attribute.Length > 0 ? ((InspectorNameAttribute)attribute[0]).displayName : value.ToString();

        }

        public static T EnumValue<T>(this System.Enum rank, T E, T D, T C, T B, T A, T S)
        {
            switch (System.Convert.ToInt32(rank))
            {
                case 0: return E;
                case 1: return D;
                case 2: return C;
                case 3: return B;
                case 4: return A;
                case 5: return S;
            }
            return C;
        }
        public static int GetEnumIndex<T>(this T enumValue)
        {
            T[] values = (T[])System.Enum.GetValues(typeof(T));
            return System.Array.IndexOf(values, enumValue);
        }

        public static int EnumLenght<T>()
        {
            return System.Enum.GetValues(typeof(T)).Length;
        }

        public static void ForEachFlag<T>(this T e, System.Action<T> action) where T : System.Enum
        {
            for (int i = 0; i <= System.Enum.GetValues(e.GetType()).Length; i++)
            {
                if ((System.Convert.ToInt32(e) & (1 << i)) != 0)
                {
                    action.Invoke(e);
                }
            }
        }

    }
}
