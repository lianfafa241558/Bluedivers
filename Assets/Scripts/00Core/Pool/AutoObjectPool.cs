using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Core
{
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
}
