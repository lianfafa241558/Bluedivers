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
        private readonly List<KeyValuePair<K, AutoObjectPool<V>>> snapshot = new List<KeyValuePair<K, AutoObjectPool<V>>>();
        private bool isUpdating;
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
            // ⚠ 不能直接 foreach dic：_ItemUpdate（LimitedLife.IsAlive → OnEnd）/ OnHide 都可能回调
            //   VfxPool.Creat ⇒ 本类 Get ⇒ dic.Add 新键，抛 "Collection was modified"（实测 VFXManager.Update 栈）。
            //   改用快照遍历；本帧新增的组下一帧再更新，不影响正确性。
            if (isUpdating) return;
            isUpdating = true;
            snapshot.Clear();
            snapshot.AddRange(dic);

            for (int i = 0; i < snapshot.Count; i++)
            {
                AutoObjectPool<V> value = snapshot[i].Value;
                if (value == null) continue;    // 遍历期间被 Clear 掉了
                value.Update();
                if (destructionTime > 0f && value.IsVoid() && Time.time - value.lastGetTime > destructionTime)
                {
                    keysToRemove.Add(snapshot[i].Key);  // UnInit 延后到遍历结束，避免半销毁的池被重入 Get 取到
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                if (!dic.TryGetValue(keysToRemove[i], out AutoObjectPool<V> value)) continue;
                dic.Remove(keysToRemove[i]);
                value.UnInit();
            }
            keysToRemove.Clear();
            snapshot.Clear();
            isUpdating = false;
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
            // 先摘表再销毁：UnInit 的 Destroy 会触发 OnDestroy 回调，可能重入 Get 给 dic 加键。
            // 用独立列表（不走 snapshot 字段），Clear 可能是 Update 遍历中被回调进来的。
            List<AutoObjectPool<V>> pools = new List<AutoObjectPool<V>>(dic.Values);
            dic.Clear();
            keysToRemove.Clear();
            for (int i = 0; i < pools.Count; i++)
            {
                pools[i]?.UnInit();
            }
        }
    }
}
