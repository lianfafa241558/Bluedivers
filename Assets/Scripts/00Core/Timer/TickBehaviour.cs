using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.Core{
    public interface I_TickClass
    {
        public bool Tick();
    }

    public abstract class TickBehaviour : MonoBehaviour, I_TickClass, IScheduledTickTarget
    {
        protected List<I_TickClass> ticks = new();
        protected float TickTime = 1;
        private float lastTickTime;
        private float _nextDue;
        /// <summary>
        /// 已经经历的Tick次数
        /// </summary>
        protected int TickCount;

        protected virtual void Start()
        {
            ticks.Add(this);
            lastTickTime = Time.time;
            // ⚠ 到期时刻先填 lastTickTime（= 下帧立刻被评估一次），不能填 lastTickTime + TickTime：
            //   子类常在 base.Start() **之后**才改 TickTime（如 AirdropController 设 0.02s），
            //   提前算好的到期时刻会沿用默认的 1s，首拍就晚一拍。
            _nextDue = lastTickTime;
            TickDriver.Schedule(this);   // 交给统一驱动（原先靠自身 MonoBehaviour.Update 每帧空转着问到点没有）
        }

        float IScheduledTickTarget.NextDue
        {
            get => _nextDue;
            set => _nextDue = value;
        }

        /// <summary>到期回调（见 <see cref="TickDriver"/> 的到期堆通道），替代原来的 <c>Update</c>。
        /// <para>▍为什么不再用 Update：交战时 1600 个实例每帧只做一次 float 比较，光引擎调度就 ~1.4ms；
        /// 改成到期堆后 60fps 下每帧只碰 1/60 的实例。</para></summary>
        void IScheduledTickTarget.OnScheduledTick(float now)
        {
            // 池中/被禁用：与"引擎不再调 Update"一致 —— 推迟一个周期，不做追赶
            // （⚠ 与原实现略有差异：原实现禁用期间 lastTickTime 不走，重新启用会连跑若干帧把欠的 Tick 补回来）
            if (!isActiveAndEnabled)
            {
                _nextDue = now + TickTime;
                return;
            }

            if (now > TickTime + lastTickTime)
            {
                lastTickTime += TickTime;
                ++TickCount;
                for (int i = ticks.Count - 1; i >= 0; --i)
                {
                    if (!ticks[i].Tick())
                    {
                        ticks.RemoveAt(i);
                    }
                }
            }
            // 长卡顿后 lastTickTime 仍落后 ⇒ 下一帧会被再次弹出，按 1 帧 1 次把欠的 Tick 补回来（与原实现一致）
            _nextDue = lastTickTime + TickTime;
        }

        public abstract bool Tick();

        //这个写法是显式实现，只能通过接口调用
        //void I_TickClass.Tick();



    }
}