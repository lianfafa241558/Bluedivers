using System;

using UnityEngine;

namespace Core
{
    public class ViewTimerController : MonoBehaviour
    {
        private ViewTimerSystem _timerSystem;
        void Awake()
        {
            _timerSystem = new();
        }

        // Update is called once per frame
        void Update()
        {
            _timerSystem.Update();
        }


        public LogicTimer CreateTimer(Action cb, float waitTime, int counter = 1, Action endcb = null)
        {
            if (waitTime == 0)
            {
                cb?.Invoke();
                return null;
            }
            else return _timerSystem.CreateTimer(cb, waitTime, counter, endcb);
        }
        public LogicTimer CreateTimer(Action<int> cb, float waitTime, int counter = 1, Action endcb = null)
        {
            //Debug.LogError("创建计时器，每次"+ waitTime+"次数"+ counter);
            if (waitTime == 0)
            {
                cb?.Invoke(0);
                return null;
            }
            else return _timerSystem.CreateTimer(cb, waitTime, counter, endcb);
        }
        public LogicTimer CreatePerTimer(Action percb, float waitTime, Action endcb = null) => _timerSystem.CreateTimer(percb, waitTime, endcb);

        public void ClearTimer() => _timerSystem.Clear();

        public void RemoveTimer(LogicTimer cb) => _timerSystem.RemoveTimer(cb);
    }
}