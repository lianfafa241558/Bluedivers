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
        /// 转成任意进制 ?
        /// </summary>
        public static string ToBase(int value, int toBase) => System.Convert.ToString(value, toBase);

        public static string FloatToTime(float value)
        {
            int Minutes = (int)(value / 60);
            int Seconds = (int)((value + 0.5f) % 60);
            return string.Format("{0:D2}:{1:D2}", Minutes, Seconds);

        }
        /// <summary>
        /// 整数转罗马数 ?
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string IntToRoman(int value)
        {
            int[] nums = new int[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] romans = new string[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            string result = "";
            int start = 0;
            while (value > 0)
            {
                for (int i = start, l = nums.Length; i < l; ++i)
                {
                    if (value >= nums[i])
                    {
                        value -= nums[i];
                        result += romans[i];
                        start = i;
                        break;
                    }
                }
            }

            return result;
        }



        public static string FillZero(int value, int digit)
        {
            string re = value.ToString();
            while (re.Length < digit) re = "0" + re;
            return re;
        }
        /// <summary>
        /// 去掉字符串中 "分组/" 前缀，如 "伤害/直击伤害" → "直击伤害"
        /// </summary>
        public static string TrimStartPrefix(this string value)
        {
            var idx = value.LastIndexOf('/');
            return idx >= 0 ? value.Substring(idx + 1) : value;
        }


        public static string Ksegmentation(float value) => ((int)value).ToString("N0");
        public static int TextLength(string text, int size = 1)
        {
            float re = 0;
            for (int i = 0, l = text.Length; i < l; ++i)
            {
                if (text[i] > 127)
                    re += size;
                else
                    re += size * 0.5f;
            }
            return Mathf.CeilToInt(re);
        }

        private static Dictionary<char, float> textwidth = new() {
            ['!'] = 0.5f,
            ['@'] = 1.75f,
            ['%'] = 1.5f,
            ['^'] = 1.25f,
            ['&'] = 1.5f,
            ['*'] = 0.75f,
            ['('] = 0.5f,
            [')'] = 0.5f,
            ['-'] = 0.75f,
            ['+'] = 1.25f,
            ['_'] = 0.75f,
            ['='] = 1.25f,
            [','] = 0.4f,
            ['.'] = 0.4f,
            ['/'] = 0.75f,
            ['?'] = 0.8f,
            [';'] = 0.4f,
            [':'] = 0.4f,
        };
        public static int TextLength(string text, float letter, float digit, float symbol, float chinese)
        {
            float re = 0;
            for (int i = 0, l = text.Length; i < l; ++i)
            {
                char c = text[i];
                if (char.IsLetter(c))
                {
                    // 字母
                    re += letter;
                }
                else if (char.IsDigit(c))
                {
                    // 数字
                    re += digit;
                }
                else if (char.IsLetterOrDigit(c))
                {
                    // 其他字母数字字符（如中文 ?
                    re += chinese;
                }
                else if (textwidth.TryGetValue(c, out var width))
                {
                    re += digit * width;
                }
                else
                {
                    // 符号和其他字 ?
                    re += symbol;
                }
            }
            return Mathf.FloorToInt(re);
        }

        public static KeyCode GetKeyDownCode()
        {
            if (Input.anyKey)
            {
                foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKey(keyCode))
                    {
                        //Debug.Log(keyCode.ToString());
                        return keyCode;
                    }
                }
            }
            return KeyCode.None;
        }
    }
}
