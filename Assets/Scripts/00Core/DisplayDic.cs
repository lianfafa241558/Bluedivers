using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Attributes;
using UnityEngine;

namespace FPSGame.Core
{
    [Serializable]
    public class DisplayDic<Key, Value> : IEnumerable<KVP<Key, Value>>, IEnumerable
    {
        [SerializeField]
        private bool ShowSetting = false;
        [Compare("ShowSetting", 1, CompareOperate.Equal)]
        [SerializeField]
        private bool NoLog = false;
        [Compare("ShowSetting", 1, CompareOperate.Equal)]
        [SerializeField]
        private bool NoDefaultVale = false;
        [Compare("ShowSetting", 1, CompareOperate.Equal)]
        [SerializeField]
        private Value DefaultValue;

        [SerializeField]
        private List<KVP<Key, Value>> arr;

        [SerializeField]
        private Dictionary<Key, Value> dic;

        private Func<Key, Value> DefaultSet;

        /// <summary>
        /// 用 <see cref="DefaultValue"/> 兜底时临时塞进字典的"占位键"。
        ///
        /// <para>▍为什么需要：占位条目是深拷贝出来的空模板（如 <c>ArchSettingData.value.value == ""</c>），
        /// 它一旦入表，之后的 <see cref="Synchronize"/> 会因为"键已存在"而拒绝用真实默认值补齐，
        /// 读取方就永远拿不到有效数据（曾让缺项的存档每次启动都崩）。
        /// 记在这里的键只作占位 ⇒ 同步时允许被真实默认值覆盖。</para>
        /// </summary>
        [NonSerialized]
        private HashSet<Key> _placeholderKeys;

        [HideInInspector]
        [SerializeField]
        private bool meetReset;

        public Value this[Key key]
        {
            get
            {
                TryInit();
                if (dic.TryGetValue(key, out var value))
                {
                    return value;
                }

                if (NoDefaultVale)
                {
                    return default(Value);
                }

                if (!NoLog)
                {
                    Key val = key;
                    Debug.LogWarning("错误：没找到Key:" + val?.ToString() + "初始设置:" + (DefaultSet != null));
                }

                // 没有工厂时只能拿 DefaultValue 兜底 ⇒ 这条是"占位条目"，同步时允许被真实默认值覆盖
                bool isPlaceholder = DefaultSet == null;
                value = ((DefaultSet != null) ? DefaultSet(key) : DefaultValue);
                // 引用类型需深拷贝，避免所有条目共享同一个 DefaultValue 实例
                if (value != null && !typeof(Value).IsValueType)
                {
                    value = JsonUtility.FromJson<Value>(JsonUtility.ToJson(value));
                }
                arr.Add(new KVP<Key, Value>(key, value));
                dic[key] = value;
                if (isPlaceholder)
                {
                    MarkPlaceholder(key);
                }
                return value;
            }
            set
            {
                TryInit();
                if (!dic.TryGetValue(key, out var _))
                {
                    arr.Add(new KVP<Key, Value>(key, value));
                    dic.Add(key, value);
                    UnmarkPlaceholder(key);
                    return;
                }

                dic[key] = value;
                int index = arr.FindIndex((KVP<Key, Value> item) => item.Key.Equals(key));
                arr[index].Value = value;
                UnmarkPlaceholder(key);
            }
        }

        public Value[] Values
        {
            get
            {
                TryInit();
                return dic.Values.ToArray();
            }
        }

        public Key[] Keys
        {
            get
            {
                TryInit();
                return dic.Keys.ToArray();
            }
        }

        public int Count => arr.Count;

        public int DicCount => dic.Count;

        public DisplayDic()
        {
        }

        public DisplayDic(bool noLog)
        {
            NoLog = noLog;
        }

        public DisplayDic(bool noLog, bool noDefaultVale)
        {
            NoLog = noLog;
            NoDefaultVale = noDefaultVale;
        }

        public DisplayDic(bool noLog, Func<Key, Value> defaultSet)
            : this(noLog)
        {
            DefaultSet = defaultSet;
        }

        public DisplayDic(bool noLog, List<KVP<Key, Value>> arr)
            : this(noLog)
        {
            this.arr = arr;
        }

        public IEnumerator<KVP<Key, Value>> GetEnumerator()
        {
            return arr.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGet(Key key, out Value value)
        {
            TryInit();
            if (dic.TryGetValue(key, out value))
            {
                return true;
            }

            value = default(Value);
            return false;
        }

        public Value TryGet(Key key, Value defaultValue)
        {
            TryInit();
            if (dic.TryGetValue(key, out var value))
            {
                return value;
            }

            this[key] = defaultValue;
            return defaultValue;
        }

        public bool Add(Key key, Value value)
        {
            TryInit();
            if (!dic.TryGetValue(key, out var _))
            {
                arr.Add(new KVP<Key, Value>(key, value));
                dic[key] = value;
                return true;
            }

            return false;
        }

        public bool Remove(Key key)
        {
            TryInit();
            if (dic.Remove(key))
            {
                arr.RemoveAll((KVP<Key, Value> item) => key.Equals(item.Key));
                return true;
            }

            return false;
        }

        public void ForEach(Action<Key, Value> action)
        {
            TryInit();
            foreach (KeyValuePair<Key, Value> item in dic)
            {
                action(item.Key, item.Value);
            }
        }

        public void Clear()
        {
            if (dic == null)
            {
                dic = new Dictionary<Key, Value>();
            }
            else
            {
                dic.Clear();
            }

            arr.Clear();
            _placeholderKeys?.Clear();
        }

        public void Log()
        {
            for (int i = 0; i < arr.Count; i++)
            {
                Key key = arr[i].Key;
                string obj = key?.ToString();
                Value value = arr[i].Value;
                Debug.LogWarning ("Key:" + obj + " Value:" + value);
            }
        }

        /// <summary>
        /// 非覆盖的合并/同步，新增项保持 source 中的顺序。
        /// <para>例外：本表里由 <see cref="_placeholderKeys"/> 标记的"兜底占位条目"会被 source 的真实值覆盖
        /// （它们本来就只是空模板，不覆盖等于永久缺项）。</para>
        /// </summary>
        public bool Synchronize(DisplayDic<Key, Value> source)
        {
            TryInit();
            source.TryInit();
            bool re = false;
            foreach (var kvp in source.arr)
            {
                if (_placeholderKeys != null && _placeholderKeys.Contains(kvp.Key))
                {
                    // 走索引器赋值：内部会清掉占位标记，arr 顺序也保持不动
                    this[kvp.Key] = kvp.Value;
                    re = true;
                    continue;
                }

                if (Add(kvp.Key, kvp.Value))
                {
                    re = true;
                }
            }
            return re;
        }

        private void MarkPlaceholder(Key key)
        {
            (_placeholderKeys ??= new HashSet<Key>()).Add(key);
        }

        private void UnmarkPlaceholder(Key key)
        {
            _placeholderKeys?.Remove(key);
        }

        public KVP<Key, Value> TryGetIndex(int index)
        {
            if (index >= 0 && index < arr.Count)
            {
                return arr[index];
            }

            Debug.LogError("获取index没有" + index);
            return null;
        }

        private void TryInit()
        {
            if (dic != null && !meetReset)
            {
                return;
            }

            dic = new Dictionary<Key, Value>();
            meetReset = false;
            if (arr != null)
            {
                arr.ForEach(delegate (KVP<Key, Value> item)
                {
                    dic.Add(item.Key, item.Value);
                });
            }
            else
            {
                arr = new(); 
            }
        }
    }
}
