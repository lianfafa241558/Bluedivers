using Unity.BaseTool;
namespace Core
{
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