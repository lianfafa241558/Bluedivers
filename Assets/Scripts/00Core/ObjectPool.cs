using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 手动对象池，需外部自行管理回收时机
    /// </summary>
    /// <typeparam name="T">池中对象类型</typeparam>
    public class ObjectPool<T>
    {
        protected Func<T> _Add;                            // 工厂方法，创建新对象
        protected Action<T> _Pop;                          // 取出回调：在 Get() 中调用，用于激活对象
        protected Action<T> _Push;                         // 回收回调：在 Release() 中调用，用于休眠对象
        protected Stack<T> freeObjects;                    // 空闲对象栈
        public int Count;                                  // 创建过的对象总数
        private List<T> useObjects;                        // 正在使用的对象列表

        /// <summary>构造手动对象池（无取出回调版本）</summary>
        /// <param name="add">工厂方法，创建新对象</param>
        /// <param name="push">回收回调：对象归还池时调用（如 SetActive(false)）。注意：初始预创建的对象不会调用此回调，工厂应自行设置初始状态</param>
        /// <param name="startCount">初始预创建数量</param>
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

        /// <summary>构造手动对象池（完整版本）</summary>
        /// <param name="add">工厂方法，创建新对象</param>
        /// <param name="pop">取出回调：Get() 时调用（如 SetActive(true)）</param>
        /// <param name="push">回收回调：Release() 时调用（如 SetActive(false)）</param>
        /// <param name="startCount">初始预创建数量</param>
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

        /// <summary>从池中取出一个对象（优先复用空闲对象，无空闲时新建）</summary>
        public T Get()
        {
            T val = (freeObjects.Count > 0) ? freeObjects.Pop() : Add();
            _Pop?.Invoke(val);
            useObjects.Add(val);
            return val;
        }

        /// <summary>归还单个对象到池中</summary>
        public void Release(T item)
        {
            _Push?.Invoke(item);
            useObjects.Remove(item);
            freeObjects.Push(item);
        }

        /// <summary>检查使用列表中是否存在匹配项</summary>
        public bool Contains(Predicate<T> match)
        {
            return useObjects.Find(match) != null;
        }

        /// <summary>归还所有使用中的对象（不触发 _Push 回调）</summary>
        public void Release()
        {
            useObjects.ForEach(delegate (T item)
            {
                freeObjects.Push(item);
            });
            useObjects.Clear();
        }

        /// <summary>按条件归还匹配的对象</summary>
        public void Release(Predicate<T> match)
        {
            useObjects.FindAll(match).ForEach(delegate (T item)
            {
                Release(item);
            });
        }

        /// <summary>卸载对象池，归还所有对象并销毁</summary>
        public void UnInit()
        {
            Release();
            while (freeObjects.Count > 0)
            {
                Remove();
            }
            freeObjects = null;
            useObjects = null;
        }

        /// <summary>从空闲栈中移除并销毁一个对象</summary>
        public void Remove()
        {
            T val = freeObjects.Pop();
            if (val is GameObject obj)
            {
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
            else if (val is Component component)
            {
                if (component != null && component.gameObject != null)
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                }
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
    /// 自动对象池基类，通过 _ItemUpdate 谓词自动回收"已完成任务"的对象
    /// </summary>
    public abstract class AutoObjectPoolBase<T>
    {
        protected Func<T, bool> _ItemUpdate;               // 存活判断：返回 false 时自动回收对象
        protected Func<T> _Add;                            // 工厂方法，创建新对象
        protected Action<T> _Pop;                          // 取出回调：Get() 时调用（如 SetActive(true)）
        protected Action<T> _Push;                         // 回收回调：Release() 时调用（如 SetActive(false)）
        protected Stack<T> freeObjects;
        public int Count;

        /// <summary>构造自动对象池（无取出回调版本）</summary>
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

        /// <summary>构造自动对象池（完整版本）</summary>
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
    /// 自动对象池：Update() 中根据 _ItemUpdate 谓词自动回收对象
    /// 用法：_ItemUpdate 返回 true 表示对象仍在"使用中"（如 AudioSource.isPlaying），
    /// 返回 false 时自动调用 Release 回收
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

        /// <summary>检测对象引用是否已丢失（== null 或已 Destroy）</summary>
        private bool IsEmpty(T re)
        {
            return re as UnityEngine.Object == null || re == null;
        }

        /// <summary>归还单个对象到池中</summary>
        public void Release(T item)
        {
            _Push(item);
            useObjects.Remove(item);
            if (item != null)
            {
                freeObjects.Push(item);
            }
        }

        /// <summary>每帧检查：_ItemUpdate 返回 false 的对象自动回收</summary>
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

        /// <summary>检查使用列表中是否存在匹配项</summary>
        public bool Contains(Predicate<T> match)
        {
            return useObjects.Find(match) != null;
        }

        /// <summary>归还所有使用中的对象</summary>
        public void Release()
        {
            for (int num = useObjects.Count - 1; num >= 0; num--)
            {
                Release(useObjects[num]);
            }
        }

        /// <summary>按条件归还匹配的对象</summary>
        public void Release(Predicate<T> match)
        {
            useObjects.FindAll(match).ForEach(delegate (T item)
            {
                Release(item);
            });
        }

        /// <summary>遍历所有使用中的对象</summary>
        public void Foreach(Action<T> action)
        {
            useObjects.ForEach(action);
        }

        /// <summary>查找第一个匹配项</summary>
        public T Find(Predicate<T> match)
        {
            return useObjects.Find(match);
        }

        /// <summary>查找所有匹配项</summary>
        public List<T> FindAll(Predicate<T> match)
        {
            return useObjects.FindAll(match);
        }

        /// <summary>从空闲栈中移除并销毁一个对象</summary>
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

        /// <summary>卸载对象池，归还所有对象并销毁</summary>
        public void UnInit()
        {
            Release();
            while (freeObjects.Count > 0)
            {
                Remove();
            }
            freeObjects = null;
            useObjects = null;
        }

        /// <summary>对象池是否完全为空（无空闲、无使用中）</summary>
        public bool IsVoid()
        {
            return freeObjects.Count == 0 && useObjects.Count == 0;
        }

        /// <summary>检测 Unity Object 是否有效（未被 Destroy）</summary>
        private bool IsValid(UnityEngine.Object obj)
        {
            return obj != null && !obj.Equals(null);
        }

        /// <summary>检测普通对象是否有效</summary>
        private bool IsValid(object obj)
        {
            return obj != null && !obj.Equals(null);
        }
    }

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
