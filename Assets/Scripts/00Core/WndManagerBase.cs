using Unity.BaseTool;
using UnityEngine;
//using UnityEngine.Rendering.Universal;
namespace Core
{
    public abstract class WndManagerBase<T> : Singleton<T> where T : WndManagerBase<T>

    {
        [SerializeField]
        private Camera uiCamera;
        [SerializeField]
        private Canvas canvas;
        //[SerializeField]
        //private ScriptableRendererFeature feature;

        public static Camera UiCamera => Instance.uiCamera;
        public static Canvas Canvas => Instance.canvas;
        //public static ScriptableRendererFeature Feature => Instance.feature;


        protected virtual void Start()
        {
            for (int i = 0, l = canvas.transform.childCount; i < l; ++i)
            {
                canvas.transform.GetChild(i).gameObject.SetActive(false);
                //canvas.transform.GetChild(i).GetComponent<Wnd>()?.Init();
            }
        }


        /*
        private void OnApplicationQuit()
        {
            Feature.SetActive(false);
        }*/

    }

    public enum FeatureState
    {
        Close,
        Front,
        Custom,
        Normal,
        TimeStop
    }
}