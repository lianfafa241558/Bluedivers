using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 表现计时器系统
    /// </summary>
    public class ViewTimerSystem
    {
        private float tick;

        private float lastTime;

        private List<LogicTimer> list;

        public ViewTimerSystem()
        {
            tick = Time.deltaTime;
            lastTime = Time.time;
            list = new List<LogicTimer>();
        }

        public void Update()
        {
            tick = Time.time - lastTime;
            lastTime = Time.time;
            for (int num = list.Count - 1; num >= 0; num--)
            {
                if (list[num].isActive)
                {
                    //Debug.LogError("触发计时器 " + (int)(tick * 1000));
                    list[num].TickTimer((int)(tick*1000));
                }
                else
                {
                    //Debug.LogError("移除计时器");
                    list.Remove(list[num]);
                }
            }
        }

        public LogicTimer CreateTimer(Action<int> cb, float waitTime, int counter, Action endcb = null)
        {
            if (counter == 0)
            {
                return null;
            }

            LogicTimer LogicTimer = new LogicTimer(cb, (int)(waitTime * 1000) , counter, endcb);
            list.Add(LogicTimer);
            return LogicTimer;
        }

        public LogicTimer CreateTimer(Action cb, float waitTime, int counter, Action endcb = null)
        {
            if (counter == 0)
            {
                return null;
            }

            LogicTimer LogicTimer = new LogicTimer(cb, (int)(waitTime * 1000), counter, endcb);
            list.Add(LogicTimer);
            return LogicTimer;
        }

        public LogicTimer CreateTimer(Action percb, float waitTime, Action endcb = null)
        {
            LogicTimer LogicTimer = new LogicTimer(percb, (int)(waitTime * 1000), endcb);
            list.Add(LogicTimer);
            return LogicTimer;
        }

        public void RemoveTimer(LogicTimer lt)
        {
            if (lt == null)
            {
                return;
            }

            for (int num = list.Count - 1; num >= 0; num--)
            {
                if (list[num] == lt)
                {
                    list.RemoveAt(num);
                    break;
                }
            }
        }

        public void Clear()
        {
            list.Clear();
        }
    }
}
