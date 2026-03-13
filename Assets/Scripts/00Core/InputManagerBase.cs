using System;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;

namespace Core
{
    public abstract class InputManagerBase<T, W> : Singleton<InputManagerBase<T, W>>
        where T : System.Enum
        where W : System.Enum
    {
        [System.Serializable]
        public class InputItem
        {
            public W window;
            public T key;
            public KeyCode positiveMainValue;//主键
            public KeyCode positiveSpareValue;//备用键
            public KeyCode negativeMainValue;//反向主键
            public KeyCode negativeSpareValue;//反向备用键
            public float lastTime;
            public event Action OnDown; 

            public bool Get()
            {
                return Input.GetKey(positiveMainValue) || (positiveSpareValue != default && Input.GetKey(positiveSpareValue));
            }
            public bool GetNegative()
            {
                return (negativeMainValue != default && Input.GetKey(negativeMainValue)) || (positiveSpareValue != default && Input.GetKey(negativeSpareValue));
            }

            public bool GetUp()
            {
                return Input.GetKeyUp(positiveMainValue) || (positiveSpareValue != default && Input.GetKeyUp(positiveSpareValue));
            }
            public bool GetDown()
            {
                return Input.GetKeyDown(positiveMainValue) || (positiveSpareValue != default && Input.GetKeyDown(positiveSpareValue));
            }

            public float GetAxis()
            {
                return GetNegative() ? -1 : (Get() ? 1 : 0);
            }

            public void Invoke() => OnDown?.Invoke();
        }

        private static Dictionary<W, Dictionary<T, InputItem>> inputDic = new();


        [SerializeField]
        protected List<InputItem> inputList = new();

        public abstract W NowWindowState { get; }

        //private static W defaultState;
        private static readonly W defaultState=default;//(全部)

        private List<System.Func<bool>> NowCancelList = new();
        private Stack<System.Func<bool>> WaitCancelStack = new();//临时的，最后走的NowCancelList过
        private bool useCancel;//这一帧是否有人使用了
        public static void ListenerCancel(System.Func<bool> action)
        {
            Instance.WaitCancelStack.Push(action);
        }
        public static bool CancelEmpty()
        {
            return Instance.NowCancelList.Count == 0 && !Instance.useCancel;//这一帧没人使用返回
        }
        private static List<(W, T, Action)> waitBind=new();
        public static void Bind(W state,T key,Action action) {
            if (inputDic.TryGetValue(state,out var dic))
            {
                if (dic.TryGetValue(key, out var output2))
                {
                    output2.OnDown += action;
                }
            }
            else
            {
                waitBind.Add(new(state,key,action));
            }
        }
        public static void UnBind(W state, T key, Action action)
        {
            if (inputDic[state].TryGetValue(key, out var output))
            {
                output.OnDown -= action;
            }
        }

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            //defaultState = (W)(object)0;
            foreach (W item in System.Enum.GetValues(typeof(W)))
            {
                inputDic.Add(item, new());
            }

            foreach (var item in inputList)
            {
                inputDic[item.window].Add(item.key, item);
            }

            foreach (var item in waitBind)
            {
                Bind(item.Item1, item.Item2,item.Item3);
            }
            waitBind.Clear();
        }
        //需要在这一帧结束之后才记录点击，否则单击也会触发双击
        private void LateUpdate()
        {
            // 检测是否有任何键被按下
            if (Input.anyKeyDown)
            {
                foreach (var item in inputDic[defaultState].Values)
                {
                    if (Vaild(item) && item.GetDown())
                    {
                        item.lastTime = Time.time;
                        item.Invoke();
                        return;
                    }
                }
                foreach (var item in inputDic[NowWindowState].Values)
                {
                    if (Vaild(item) && item.GetDown())
                    {
                        item.lastTime = Time.time;
                        item.Invoke();
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            useCancel = false;
            //把上一帧待添加的加入
            while (WaitCancelStack.Count > 0)
            {
                var func = WaitCancelStack.Pop();
                NowCancelList.RemoveAll(item=>item==func);
                NowCancelList.Add(func);
            }
            if (NowCancelList.Count > 0 && (Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape)))
            {
                //直到第一个返回true截止
                while (NowCancelList.Count > 0)
                {
                    var func = NowCancelList[NowCancelList.Count - 1];
                    NowCancelList.RemoveAt(NowCancelList.Count - 1);
                    if (func.Invoke())
                    {
                        useCancel = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 获得某个键的状态
        /// </summary>
        /// <param name="key"></param>
        /// <returns>按下:1  按着:2 抬起:3 无:0</returns>
        public static int GetState(T key)
        {
            if (GetUp(key)) return 3;
            else if (GetDown(key)) return 1;
            else if (Get(key)) return 2;
            else return 0;
        }

        //CompareTo返回0表示相等
        public static bool Get(T key)
        {

            if(Find(key, out var output) && Vaild(output))
            {
                return output.Get();
            }
            return false;
        }
        public static bool GetUp(T key)
        {
            if (Find(key, out var output) && Vaild(output))
            {
                return output.GetUp();
            }
            return false;
        }
        public static bool GetDown(T key)
        {
            if (Find(key, out var output) && Vaild(output))
            {
                return output.GetDown();
            }
            return false;
        }
        
        public static bool GetDouble(T key)
        {
            if (Find(key, out var output) && Vaild(output) &&( Time.time - output.lastTime < 0.8f) && GetDown(key))
            {
                //Debug.LogWarning("双击"+key);
                //Instance. lastInput.value = Time.time;
                return true;
            }
            return false;
        }

        public static bool GetLong(T key)
        {
            return Find(key, out var output) && Time.time - output.lastTime > 1.5f && Get(key);
        }



        public static float GetAxis(T key)
        {
            if (Find(key, out var output) && Vaild(output))
            {
                return output.GetAxis();
            }
            return 0;
        }
        private static bool Find(T key,out InputItem output)
        {
            if (!inputDic[defaultState].TryGetValue(key, out output)) inputDic[Instance.NowWindowState].TryGetValue(key, out output);
            return output != null;
        }

        private static bool Vaild(InputItem output)
        {
            return IsEqual(output.window,defaultState) || IsEqual(output.window, Instance.NowWindowState);
        }

        private static bool IsEqual(W a, W b) => a.GetHashCode() == b.GetHashCode();
        
    }

}