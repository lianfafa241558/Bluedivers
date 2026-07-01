using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 逻辑计时器系统
    /// </summary>
    public class LogicTimerSystem
    {
        private int tick;

        private List<LogicTimer> list;

        public LogicTimerSystem(int tickMs)
        {
            tick = tickMs;
            list = new List<LogicTimer>();
        }

        public void Update()
        {
            for (int num = list.Count - 1; num >= 0; num--)
            {
                if (list[num].isActive)
                {
                    list[num].TickTimer(tick);
                }
                else
                {
                    list.Remove(list[num]);
                }
            }
        }

        public LogicTimer CreateTimer(Action<int> cb, int waitTimeMs, int counter, Action endcb = null)
        {
            if (counter == 0)
            {
                return null;
            }

            LogicTimer logicTimer = new LogicTimer(cb, waitTimeMs, counter, endcb);
            list.Add(logicTimer);
            return logicTimer;
        }

        public LogicTimer CreateTimer(Action cb, int waitTimeMs, int counter, Action endcb = null)
        {
            if (counter == 0)
            {
                return null;
            }

            LogicTimer logicTimer = new LogicTimer(cb, waitTimeMs, counter, endcb);
            list.Add(logicTimer);
            return logicTimer;
        }

        public LogicTimer CreateTimer(Action percb, int waitTimeMs, Action endcb = null)
        {
            LogicTimer logicTimer = new LogicTimer(percb, waitTimeMs, endcb);
            list.Add(logicTimer);
            return logicTimer;
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
