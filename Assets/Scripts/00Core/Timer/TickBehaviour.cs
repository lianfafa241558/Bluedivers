using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core{
    public interface I_TickClass
    {
        public bool Tick();
    }

    public abstract class TickBehaviour : MonoBehaviour, I_TickClass
    {
        protected List<I_TickClass> ticks = new();
        protected float TickTime = 1;
        private float lastTickTime;
        /// <summary>
        /// 已经经历的Tick次数
        /// </summary>
        protected int TickCount;

        protected virtual void Start()
        {
            ticks.Add(this);
            lastTickTime = Time.time;
        }

        protected virtual void Update()
        {
            if (Time.time > TickTime + lastTickTime)
            {
                //lastTickTime = Time.time;
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
        }

        public abstract bool Tick();

        //这个写法是显式实现，只能通过接口调用
        //void I_TickClass.Tick();



    }
}