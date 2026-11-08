using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>
    /// 外部访问，公共静态成员（单例）?    /// </summary>
    public class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T instance;

        public static T Instance => instance;

        public virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.Log(base.gameObject.name + "已有相同物体" + instance.gameObject.name);
                // ⚠ 只 Destroy 不够：Destroy 是**帧末**执行，中间这一帧里本物体上的其它组件照样跑
                //   Awake/OnEnable/Update（会重复订阅事件、重复写自己的单例引用）。
                //   先 SetActive(false) 把它们一起按住，再排队销毁。
                gameObject.SetActive(false);
                Object.Destroy(base.gameObject);
                return;
            }

            instance = (T)this;
        }

        public static bool isInit()
        {
            if ((bool)instance)
            {
                return true;
            }

            return false;
        }

        protected virtual void onDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
