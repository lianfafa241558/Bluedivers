using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FPSGame.Core
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
}
