using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>
    /// 为场景提供去重的 EventSystem 单例，避免多份 EventSystem 冲突。
    /// </summary>

    public class EventSystemSingleton : Singleton<EventSystemSingleton>
    {
        public override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                GetComponent<UnityEngine.EventSystems.EventSystem>().enabled = false;
                GetComponent<UnityEngine.EventSystems.StandaloneInputModule>().enabled = false;
            }
            else
            {
                base.Awake();
            }
            enabled = false;
        }
    }
}
