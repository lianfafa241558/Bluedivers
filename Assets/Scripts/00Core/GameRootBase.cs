using System;
using Core.Interface;
using PEMaths;

using UnityEngine;
//using UnityEngine.Rendering.Universal;

namespace Core
{
    public class GameRootBase<T> : Singleton<T> where T : GameRootBase<T>
    {
       

        public const int MaxPlayerCount = 4;
        public static int PlayerIndex;

        
        private ViewTimerController _timerSystem;

        public override void Awake()
        {
            base.Awake();

            if (Instance != this) return;
            Screen.fullScreen = false;
            
            _timerSystem = gameObject.AddComponent<ViewTimerController>();
            DontDestroyOnLoad(this);

            //ArchivesData_SO.playArchive = ShowArchive;

            var managers = GetComponents<I_GlobaManager>();
            foreach (var item in managers) item.Init();
            for (int i = 0; i < transform.childCount; ++i)
            {
                var childManagers = transform.GetChild(i).GetComponents<I_GlobaManager>();
                foreach (var item in childManagers) item.Init();
            }

        }

        private void OnDestroy()
        {
            var managers = GetComponents<I_GlobaManager>();
            foreach (var item in managers) item.UnInit();
            for (int i = 0; i < transform.childCount; ++i)
            {
                var childManagers = transform.GetChild(i).GetComponents<I_GlobaManager>();
                foreach (var item in childManagers) item.UnInit();
            }
        }


        public static void ExitGame()//定义一个退出游戏的方法
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;//如果是在unity编译器中
#else
        Application.Quit();//否则在打包文件中
#endif
        }

        /// <summary>创建计时器</summary>
        /// <param name="cb">每次回调函数</param>
        /// <param name="endcb">结束回调函数</param>
        /// <param name="waitTime">每次计时时间(单位：秒)</param>
        public static LogicTimer CreateTimer(Action cb, float waitTime, int counter = 1, Action endcb = null) => Instance._timerSystem.CreateTimer(cb, waitTime, counter, endcb);
        public static LogicTimer CreateTimer(Action<int> cb, float waitTime, int counter = 1, Action endcb = null) => Instance._timerSystem.CreateTimer(cb, waitTime, counter, endcb);
        public static LogicTimer CreatePerTimer(Action percb, float waitTime,Action endcb = null) => Instance._timerSystem.CreatePerTimer(percb, waitTime, endcb);

        public static void ClearTimer() => Instance._timerSystem.ClearTimer();

        public static void RemoveTimer(LogicTimer cb) => Instance._timerSystem.RemoveTimer(cb);

    }

}


public abstract class BaseMono :MonoBehaviour
{
    public virtual Vector3 Pos
    {
        get => transform.position;
        set
        {   
            transform.position = value;
        }
    }
    public virtual PEVector2 LogicPos
    {
        get => (PEVector2)transform.position;
        set
        {
            transform.position = value.RawVector2;
        }
    }
    public virtual PEVector3 Logic3Pos
    {
        get => (PEVector3)transform.position;
        set
        {
            transform.position = value.RawVector3;
        }
    }

    public Vector3 Angles => transform.eulerAngles;

    public virtual Vector3 Forward => transform.forward;
    public virtual Vector3 CenterPos =>this==null?default: transform.position + Vector3.up * 2;



    public void LookAt(BaseMono mono)
    {
        if (!mono) return;
        transform.LookAt(mono.transform);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
    }
    public void LookAt(Vector3 vector)
    {
        transform.LookAt(vector);
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
    }
}