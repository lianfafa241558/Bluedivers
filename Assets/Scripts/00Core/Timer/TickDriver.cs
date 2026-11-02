using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>
    /// **逐帧驱动目标**：实现它即可放弃 MonoBehaviour.Update，改由 <see cref="TickDriver"/> 每帧调一次。
    /// 适合"每帧都必须看一眼"的（Actor 的位置同步）。
    /// </summary>
    public interface ITickDriverTarget
    {
        /// <summary>每渲染帧一次。<paramref name="now"/> 传 <c>Time.time</c>（与各实现原来在 Update 里用的时钟一致）。</summary>
        void TickFrame(float now);
    }

    /// <summary>
    /// **到期驱动目标**：按 <see cref="NextDue"/> 排进最小堆，只有到点了才会被调 —— 适合
    /// "每 N 秒才需要跑一次、却为了判断到没到而每帧空转"的（TickBehaviour 那 1600 个 1s 定时器）。
    /// </summary>
    public interface IScheduledTickTarget
    {
        /// <summary>下一次到期时刻（<c>Time.time</c> 口径的绝对时间）。</summary>
        float NextDue { get; set; }

        /// <summary>到点时调用；实现必须在返回前把 <see cref="NextDue"/> 安排好（否则会在同一帧被反复调度）。</summary>
        void OnScheduledTick(float now);
    }

    /// <summary>
    /// **统一逐帧驱动**：把"一堆 MonoBehaviour.Update 里只做一次时间/状态比较"的组件收进一条 for 循环。
    ///
    /// <para>▍为什么：引擎每次调 Update 都有约 0.9µs 的托管↔原生往返。交战时场景里 Health 1300 + TickBehaviour 1600 +
    /// Actor 1300 个实例，其中绝大多数每次只做一次 float 比较 ⇒ 光"调进去"就烧掉约 3.8ms
    /// （Profiler 里表现为 <c>BehaviorUpdate</c> 的 self 时间，挂不到任何子节点上）。</para>
    ///
    /// <para>▍两条通道：<see cref="Register"/> 平铺（每帧必看，实测 ~450ns/目标）；
    /// <see cref="Schedule"/> 最小堆（只在到期时碰，1s 定时器在 60fps 下每帧只有 1/60 会命中）。</para>
    ///
    /// <para>▍⚠ 语义必须一致：各目标自己在回调首行判 <c>isActiveAndEnabled</c>，
    /// 这样"组件被禁用 / GameObject 被 SetActive(false)（含对象池回收）后不再跑"与引擎行为一致。</para>
    ///
    /// <para>▍⚠ 不做 OnDestroy 注销：这些家族的子类大量覆写 OnDestroy 且**不调 base**，靠不住；
    /// 改为 Pump 里判 <c>UnityEngine.Object == null</c> 自愈（同 <c>Actor.OnDestroy</c> 注册表的思路）。</para>
    /// </summary>
    public static class TickDriver
    {
        // ── 通道 1：每帧平铺 ──
        private static readonly List<ITickDriverTarget> _targets = new(4096);
        private static readonly HashSet<ITickDriverTarget> _targetSet = new();

        // ── 通道 2：到期最小堆 ──
        private static readonly List<IScheduledTickTarget> _heap = new(2048);
        private static readonly HashSet<IScheduledTickTarget> _scheduledSet = new();
        private static readonly List<IScheduledTickTarget> _due = new(64);

        /// <summary>平铺通道的目标数（调试/自检用）。</summary>
        public static int Count => _targets.Count;

        /// <summary>到期堆里的目标数（调试/自检用）。</summary>
        public static int ScheduledCount => _heap.Count;

        public static void Register(ITickDriverTarget target)
        {
            if (target == null) return;
            if (!_targetSet.Add(target)) return;
            _targets.Add(target);
        }

        public static void Unregister(ITickDriverTarget target)
        {
            if (target == null) return;
            if (!_targetSet.Remove(target)) return;
            _targets.Remove(target);
        }

        /// <summary>排进到期堆（同一目标重复 Schedule 只保留一份）。</summary>
        public static void Schedule(IScheduledTickTarget target)
        {
            if (target == null) return;
            if (!_scheduledSet.Add(target)) return;
            Push(target);
        }

        /// <summary>每渲染帧调一次（由 <c>LogicFrameHost.Update</c> 驱动）。</summary>
        public static void Pump(float now)
        {
            // ── 平铺：倒序遍历（目标在回调里销毁/注销自己也不越界；帧内新注册的本帧不跑）──
            for (int i = _targets.Count - 1; i >= 0; --i)
            {
                var target = _targets[i];
                if (target is Object flatObject && flatObject == null)
                {
                    // ⚠ 必须连 _targetSet 一起清，否则同一引用再次 Register 会被 HashSet 挡掉
                    Unregister(target);
                    continue;
                }
                target.TickFrame(now);
            }

            // ── 到期：先把本轮到期的全部弹出，再统一回调 + 回堆 ──
            // ⚠ 必须"先弹完再回调"：回调里若把 NextDue 设回 <=now（追赶逻辑），边弹边推会在同一帧无限循环。
            _due.Clear();
            while (_heap.Count > 0 && _heap[0].NextDue <= now)
            {
                var due = Pop();
                if (due is Object dueObject && dueObject == null)
                {
                    _scheduledSet.Remove(due);
                    continue;
                }
                _due.Add(due);
            }
            for (int i = 0; i < _due.Count; ++i)
            {
                var target = _due[i];
                // ⚠ 弹出到回调之间可能被别的目标的 Tick 销毁（如伤害击杀单位）⇒ 这里要再判一次，
                //   否则对已销毁的 MonoBehaviour 读 isActiveAndEnabled 会抛 MissingReferenceException
                if (target is Object dueObject2 && dueObject2 == null)
                {
                    _scheduledSet.Remove(target);
                    continue;
                }
                target.OnScheduledTick(now);
                Push(target);
            }
            _due.Clear();
        }

        #region 二叉堆（按 NextDue 最小）
        private static void Push(IScheduledTickTarget item)
        {
            _heap.Add(item);
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_heap[parent].NextDue <= _heap[i].NextDue) break;
                var tmp = _heap[parent]; _heap[parent] = _heap[i]; _heap[i] = tmp;
                i = parent;
            }
        }

        private static IScheduledTickTarget Pop()
        {
            var top = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            int i = 0;
            int count = _heap.Count;
            while (true)
            {
                int l = (i << 1) + 1;
                if (l >= count) break;
                int r = l + 1;
                int min = (r < count && _heap[r].NextDue < _heap[l].NextDue) ? r : l;
                if (_heap[i].NextDue <= _heap[min].NextDue) break;
                var tmp = _heap[i]; _heap[i] = _heap[min]; _heap[min] = tmp;
                i = min;
            }
            return top;
        }
        #endregion
    }
}
