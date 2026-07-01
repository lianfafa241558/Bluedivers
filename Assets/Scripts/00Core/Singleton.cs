using UnityEngine;

namespace Core
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
                Object.Destroy(base.gameObject);
            }
            else
            {
                instance = (T)this;
            }
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
