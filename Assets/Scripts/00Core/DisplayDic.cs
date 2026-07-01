using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class DisplayDic<Key, Value> : IEnumerable<KVP<Key, Value>>, IEnumerable
    {
        [SerializeField]
        private bool ShowSetting = false;

        [SerializeField]
        private bool NoLog = false;

        [SerializeField]
        private bool NoDefaultVale = false;

        [SerializeField]
        private Value DefaultValue;

        [SerializeField]
        private List<KVP<Key, Value>> arr;

        [SerializeField]
        private Dictionary<Key, Value> dic;

        private Func<Key, Value> DefaultSet;

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
                    Debug.LogError("错误：没找到Key:" + val?.ToString() + "初始设置:" + (DefaultSet != null));
                }

                value = ((DefaultSet != null) ? DefaultSet(key) : DefaultValue);
                arr.Add(new KVP<Key, Value>(key, value));
                dic[key] = value;
                return value;
            }
            set
            {
                TryInit();
                if (!dic.TryGetValue(key, out var _))
                {
                    arr.Add(new KVP<Key, Value>(key, value));
                    dic.Add(key, value);
                    return;
                }

                dic[key] = value;
                int index = arr.FindIndex((KVP<Key, Value> item) => item.Key.Equals(key));
                arr[index].Value = value;
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
        }

        public void Log()
        {
            for (int i = 0; i < arr.Count; i++)
            {
                Key key = arr[i].Key;
                string obj = key?.ToString();
                Value value = arr[i].Value;
                Debug.LogError("Key:" + obj + " Value:" + value);
            }
        }

        /// <summary>
        /// 非覆盖的合并/同步
        /// </summary>
        public bool Synchronize(DisplayDic<Key, Value> source)
        {
            bool re = false;
            source.ForEach(delegate (Key key, Value item)
            {
                if (Add(key, item))
                {
                    re = true;
                }
            });
            return re;
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
