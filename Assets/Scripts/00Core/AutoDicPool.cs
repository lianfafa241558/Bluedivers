using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>
    /// 自动字典对象池：按 Key 分组管理，每组是一个 AutoObjectPool，Update 自动回收
    /// </summary>
    public class AutoDicPool<K, V>
    {
        protected Func<V, bool> _ItemUpdate;                // 存活判断
        protected Func<K, V> _Add;                         // 工厂方法
        protected Action<V> _Enqueue;                      // 回收回调
        private Dictionary<K, AutoObjectPool<V>> dic;
        private List<K> keysToRemove;
        private float destructionTime;

        public AutoDicPool(Func<V, bool> itemUpdate, Func<K, V> add, Action<V> enqueue, float destructionTime = 0f)
        {
            keysToRemove = new List<K>();
            _ItemUpdate = itemUpdate;
            _Add = add;
            _Enqueue = enqueue;
            this.destructionTime = destructionTime;
            dic = new Dictionary<K, AutoObjectPool<V>>();
        }

        public void Update()
        {
            foreach (KeyValuePair<K, AutoObjectPool<V>> item in dic)
            {
                item.Value.Update();
                if (item.Value.IsVoid() && destructionTime > 0f && Time.time - item.Value.lastGetTime > destructionTime)
                {
                    item.Value.UnInit();
                    keysToRemove.Add(item.Key);
                }
            }
            foreach (K item2 in keysToRemove)
            {
                dic.Remove(item2);
            }
            keysToRemove.Clear();
        }

        protected V Add(K key)
        {
            return _Add(key);
        }

        public V Get(K key)
        {
            if (!dic.TryGetValue(key, out var value))
            {
                dic.Add(key, value = new AutoObjectPool<V>(_ItemUpdate, () => Add(key), _Enqueue, 0, destructionTime / 3f));
            }
            return value.Get();
        }

        public void Release(K key)
        {
            if (dic.TryGetValue(key, out var value))
            {
                value.Release();
            }
        }

        public void Release(K key, V item)
        {
            if (dic.TryGetValue(key, out var value))
            {
                value.Release(item);
            }
        }

        public void Release(K key, Predicate<V> match)
        {
            if (dic.TryGetValue(key, out var value))
            {
                value.Release(match);
            }
        }

        public List<V> Find(K key)
        {
            if (dic.TryGetValue(key, out var value))
            {
                return value.FindAll((V item) => true);
            }
            return null;
        }

        public V Find(K key, Predicate<V> match)
        {
            if (dic.TryGetValue(key, out var value))
            {
                return value.Find(match);
            }
            return default;
        }

        public void Clear()
        {
            foreach (AutoObjectPool<V> value in dic.Values)
            {
                value.UnInit();
            }
            dic.Clear();
            keysToRemove.Clear();
        }
    }
}
