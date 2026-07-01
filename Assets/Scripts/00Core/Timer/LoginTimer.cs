using System;

namespace Core
{
    public class LogicTimer
    { 
        private int timeMax;
        private int timeNow;
        private Action cb;
        private Action endcb;
        private Action percb;
        private Action<int> countcb;
        public bool isActive = true;
        public int counter;
        public int count;

        public LogicTimer(Action cb, int timeMs, int counter, Action endcb = null)
        {
            timeMax = (timeNow = timeMs);
            this.cb = cb;
            this.endcb = endcb;
            count = counter;
        }

        public LogicTimer(Action<int> cb, int timeMs, int counter, Action endcb = null)
        {
            timeMax = (timeNow = timeMs);
            countcb = cb;
            this.endcb = endcb;
            count = counter;
        }

        public LogicTimer(Action percb, int timeMs, Action endcb = null)
        {
            timeMax = (timeNow = timeMs);
            this.percb = percb;
            this.endcb = endcb;
            count = counter;
        }

        public void TickTimer(int tickMs)
        {
            if (timeNow <= 0)
            {
                return;
            }
            timeNow -= tickMs;
            percb?.Invoke();
            if (timeNow <= 0)
            {
                counter++;
                cb?.Invoke();
                countcb?.Invoke(counter);
                if (counter < count)
                {
                    timeNow = timeMax;
                    return;
                }
                endcb?.Invoke();
                isActive = false;
            }
        }

        public void Stop()
        {
            isActive = false;
            countcb = null;
            endcb = null;
            percb = null;
        }
    }
}
