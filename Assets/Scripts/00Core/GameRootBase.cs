using System;
using FPSGame.Core.Interface;
using PEMaths;

using UnityEngine;
//using UnityEngine.Rendering.Universal;

namespace FPSGame.Core
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
