using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 手动对象池
    /// </summary>
    public class ObjectPool<T>
    {
        protected Func<T> _Add;
        protected Action<T> _Pop;
        protected Action<T> _Push;
        protected Stack<T> freeObjects;
        public int Count;
        private List<T> useObjects;

        public ObjectPool(Func<T> add, Action<T> push, int startCount)
        {
            _Add = add;
            _Push = push;
            freeObjects = new Stack<T>();
            for (int i = 0; i < startCount; i++)
            {
                freeObjects.Push(Add());
            }
            useObjects = new List<T>();
        }

        public ObjectPool(Func<T> add, Action<T> pop, Action<T> push, int startCount)
        {
            _Add = add;
            _Push = push;
            _Pop = pop;
            freeObjects = new Stack<T>();
            for (int i = 0; i < startCount; i++)
            {
                freeObjects.Push(Add());
            }
            useObjects = new List<T>();
        }

        protected T Add()
        {
            Count++;
            return _Add();
        }

        public T Get()
        {
            T val = (freeObjects.Count > 0) ? freeObjects.Pop() : Add();
            _Pop?.Invoke(val);
            useObjects.Add(val);
            return val;
        }

        public void Release(T item)
        {
            _Push?.Invoke(item);
            useObjects.Remove(item);
            freeObjects.Push(item);
        }

        public bool Contains(Predicate<T> match)
        {
            return useObjects.Find(match) != null;
        }

        public void Release()
        {
            useObjects.ForEach(delegate (T item)
            {
                freeObjects.Push(item);
            });
            useObjects.Clear();
        }

        public void Release(Predicate<T> match)
        {
            useObjects.FindAll(match).ForEach(delegate (T item)
            {
                Release(item);
            });
        }

        public void UnInit()
        {
            Release();
            for (int i = 0; i < freeObjects.Count; i++)
            {
                Remove();
            }
            freeObjects = null;
            useObjects = null;
        }

        public void Remove()
        {
            T val = freeObjects.Pop();
            if (val is GameObject obj)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else if (val is Component component)
            {
                UnityEngine.Object.Destroy(component.gameObject);
            }
        }
    }

    /// <summary>
    /// 字典对象池
    /// </summary>
    public class DicObjectPool<K, V>
    {
        protected Func<K, V> _Add;
        protected Action<V> _Pop;
        protected Action<V> _Push;
        private Dictionary<K, ObjectPool<V>> dic;

        public DicObjectPool(Func<K, V> add, Action<V> pop, Action<V> push)
        {
            _Add = add;
            _Pop = pop;
            _Push = push;
            dic = new Dictionary<K, ObjectPool<V>>();
        }

        protected V Add(K key)
        {
            return _Add(key);
        }

        public V Get(K key)
        {
            if (!dic.TryGetValue(key, out var value))
            {
                dic.Add(key, value = new ObjectPool<V>(() => Add(key), _Pop, _Push, 1));
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

        public void Remove(K key)
        {
            if (dic.TryGetValue(key, out var value))
            {
                value.UnInit();
            }
        }

        public void Clear()
        {
            foreach (ObjectPool<V> value in dic.Values)
            {
                value.UnInit();
            }
            dic.Clear();
        }
    }

    /// <summary>
    /// 自动对象池的基类
    /// </summary>
    public abstract class AutoObjectPoolBase<T>
    {
        protected Func<T, bool> _ItemUpdate;
        protected Func<T> _Add;//创建时执行
        protected Action<T> _Pop;//释放时执行
        protected Action<T> _Push;//取出时执行
        protected Stack<T> freeObjects;
        public int Count;

        public AutoObjectPoolBase(Func<T, bool> itemUpdate, Func<T> add, Action<T> push, int startCount)
        {
            _ItemUpdate = itemUpdate;
            _Add = add;
            _Push = push;
            freeObjects = new Stack<T>();
            for (int i = 0; i < startCount; i++)
            {
                freeObjects.Push(Add());
            }
        }

        public AutoObjectPoolBase(Func<T, bool> itemUpdate, Func<T> add, Action<T> pop, Action<T> push, int startCount)
        {
            _ItemUpdate = itemUpdate;
            _Pop = pop;
            _Add = add;
            _Push = push;
            freeObjects = new Stack<T>();
            for (int i = 0; i < startCount; i++)
            {
                freeObjects.Push(Add());
            }
        }

        protected T Add()
        {
            Count++;
            return _Add();
        }
    }

    /// <summary>
    /// 无映射的对象池
    /// </summary>
    public class AutoObjectPool<T> : AutoObjectPoolBase<T>
    {
        private List<T> useObjects;
        private float destructionTime;
        public float lastGetTime = float.PositiveInfinity;

        public AutoObjectPool(Func<T, bool> itemUpdate, Func<T> add, Action<T> enqueue, int startCount, float destructionTime = 0f)
            : base(itemUpdate, add, enqueue, startCount)
        {
            useObjects = new List<T>();
            this.destructionTime = destructionTime;
        }

        public AutoObjectPool(Func<T, bool> itemUpdate, Func<T> add, Action<T> pop, Action<T> push, int startCount, float destructionTime = 0f)
            : base(itemUpdate, add, pop, push, startCount)
        {
            useObjects = new List<T>();
            this.destructionTime = destructionTime;
        }

        public T Get()
        {
            lastGetTime = Time.time;
            T val = default;
            while (freeObjects.Count > 0 && IsEmpty(val))
            {
                val = freeObjects.Pop();
            }
            if (IsEmpty(val))
            {
                val = Add();
            }
            else
            {
                _Pop?.Invoke(val);
            }
            useObjects.Add(val);
            return val;
        }

        private bool IsEmpty(T re)
        {
            return re as UnityEngine.Object == null || re == null;
        }

        public void Release(T item)
        {
            _Push(item);
            useObjects.Remove(item);
            if (item != null)
            {
                freeObjects.Push(item);
            }
        }

        public void Update()
        {
            for (int num = useObjects.Count - 1; num >= 0; num--)
            {
                T val = useObjects[num];
                if (!IsValid(val))
                {
                    useObjects.RemoveAt(num);
                }
                else if (!_ItemUpdate(val))
                {
                    Release(val);
                }
            }
            if (useObjects.Count == 0 && destructionTime > 0f && Time.time - lastGetTime > destructionTime && freeObjects.Count > 0)
            {
                Remove();
                lastGetTime = Time.time;
            }
        }

        public bool Contains(Predicate<T> match)
        {
            return useObjects.Find(match) != null;
        }

        public void Release()
        {
            for (int num = useObjects.Count - 1; num >= 0; num--)
            {
                Release(useObjects[num]);
            }
        }

        public void Release(Predicate<T> match)
        {
            useObjects.FindAll(match).ForEach(delegate (T item)
            {
                Release(item);
            });
        }

        public void Foreach(Action<T> action)
        {
            useObjects.ForEach(action);
        }

        public T Find(Predicate<T> match)
        {
            return useObjects.Find(match);
        }

        public List<T> FindAll(Predicate<T> match)
        {
            return useObjects.FindAll(match);
        }

        public void Remove()
        {
            T val = freeObjects.Pop();
            Debug.Log("对象池移除了" + val);
            if (val is GameObject obj)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else if (val is Component component)
            {
                Debug.Log("对象池移除了" + val);
                UnityEngine.Object.Destroy(component.gameObject);
            }
        }

        public void UnInit()
        {
            Release();
            for (int i = 0; i < freeObjects.Count; i++)
            {
                Remove();
            }
            freeObjects = null;
            useObjects = null;
        }

        public bool IsVoid()
        {
            return freeObjects.Count == 0 && useObjects.Count == 0;
        }

        private bool IsValid(UnityEngine.Object obj)
        {
            return obj != null && !obj.Equals(null);
        }

        private bool IsValid(object obj)
        {
            return obj != null && !obj.Equals(null);
        }
    }

    /// <summary>
    /// 自动字典对象池
    /// </summary>
    public class AutoDicPool<K, V>
    {
        protected Func<V, bool> _ItemUpdate;
        protected Func<K, V> _Add;
        protected Action<V> _Enqueue;
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



    /// <summary>一对一的对象池 </summary>
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

    /// <summary>一对多的对象池 </summary>
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
