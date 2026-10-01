using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>一对一的对象池：每个 Key 对应一个对象，Update 自动回收</summary>
    public class AutoObjectPool<K, V> : AutoObjectPoolBase<V>
    {
        private Dictionary<K, V> useObjects;

        public AutoObjectPool(Func<V, bool> itemUpdate, Func<V> add, Action<V> release, int startCount) : base(itemUpdate, add, release, startCount)
        {
            useObjects = new();
        }


        /// <summary> 取出一个对象</summary>
        public V Get(K key)
        {

            V item = freeObjects.Count > 0 ? freeObjects.Pop() : Add();
            useObjects.Add(key, item);
            return item;
        }
        /// <summary> 释放一个对象</summary>
        public void Release(K key)
        {
            if (useObjects.TryGetValue(key, out var item))
            {
                _Push?.Invoke(item);
                freeObjects.Push(item);
                useObjects.Remove(key);
            }
            else
            {
                Debug.LogError("自动池中没有项目" + key);
            }
        }

        public void Update()
        {
            var keys=useObjects.Keys.ToList();
            for(int i= keys.Count - 1; i >= 0; --i)
            {
                var key = keys[i];
                if (!_ItemUpdate.Invoke(useObjects[key]))
                {
                    Release(key);
                }
            }
        }
         
        public V Find(K key)
        {
            if (useObjects.TryGetValue(key, out var item))
            {
                return item;
            }
            else
            {
                Debug.LogError("自动池中没有项目" + key);
                return default;
            }
        }
        public bool TryFind(K key,out V value)
        {
            if (useObjects.TryGetValue(key, out value))
            {
                return true;
            }
            else
            {
                value= default;
                //Debug.LogError("自动池中没有项目" + key);
                return default;
            }
        }
    }

    /// <summary>一对多的对象池：每个 Key 对应一组对象（G 为 List 容器），Update 自动回收</summary>
    public class AutoObjectPool<K, V, G> : AutoObjectPoolBase<V>
        where G : List<V>, new()
    {

        private Dictionary<K, G> useObjects;

        public AutoObjectPool(Func<V, bool> itemUpdate, Func<V> add, Action<V> enqueue, int startCount) : base(itemUpdate, add, enqueue, startCount)
        {
            useObjects = new();
        }


        /// <summary> 取出一个对象</summary>
        public V Get(K key)
        {
            V item = freeObjects.Count > 0 ? freeObjects.Pop() : Add();
            //已经有组
            if (useObjects.TryGetValue(key, out var re))
            {
                re.Add(item);
            }
            //还没有组
            else
            {
                useObjects.Add(key, new G() { item });
            }
            return item;
        }

        /// <summary> 释放一组对象</summary>
        public void Release(K key)
        {
            if (useObjects.TryGetValue(key, out var group))
            {
                for (int i = group.Count - 1; i >= 0; --i)
                {
                    base._Pop.Invoke(group[i]);
                    freeObjects.Push(group[i]);
                    group.Remove(group[i]);
                }
                useObjects.Remove(key);
            }

        }
        /// <summary> 释放一个对象</summary>
        public void Release(K key, V item)
        {
            if (useObjects.TryGetValue(key, out var group))
            {
                _TryRelease(key, item, group);
            }

        }

        /// <summary> 释放一个对象</summary>
        public void Release(K key, Predicate<V> match)
        {
            if (useObjects.TryGetValue(key, out var group))
            {
                var item = group.Find(match);
                if (item!=null) _TryRelease(key, item, group);
            }

        }
        private void _TryRelease(K key, V item, G group)
        {
            base._Pop.Invoke(item);
            freeObjects.Push(item);
            group.Remove(item);
            //如果空了就释放
            if (group.Count == 0)
            {
                useObjects.Remove(key);
            }
        }



        public void Update()
        {
            // 将键转换为数组
            K[] keys = new K[useObjects.Count];
            useObjects.Keys.CopyTo(keys, 0);

            // 使用 for 循环遍历字典
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                K key = keys[i];
                UpdateItem(key, useObjects[key]);
            }
        }
        private void UpdateItem(K key, G group)
        {
            for (int i = group.Count - 1; i >= 0; --i)
            {
                if (!_ItemUpdate.Invoke(group[i]))
                {
                    _TryRelease(key, group[i], group);
                }
            }

        }

        public G Find(K key)
        {
            if (useObjects.TryGetValue(key, out var group))
            {
                return group;
            }
            else
            {
                //LogUtil.Error("自动池中没有项目" + key);
                return default;
            }
        }
        public V Find(K key, Predicate<V> match)
        {
            if (useObjects.TryGetValue(key, out var group))
            {
                return group.Find(match);
            }
            else
            {
                //LogUtil.Error("自动池中没有项目" + key);
                return default;
            }
        }
    }

}
