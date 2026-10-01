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
            // 发布宿主给下层（2026-10-01 取代 ServiceLocator.Flow 的"调度"部分；见 Core/Timer/TimerHost.cs）。
            // ⚠ 必须发布到**非泛型**静态类：泛型类的静态成员按封闭类型各自独立，放在 GameRootBase<T> 里会各存一份。
            TimerHost.Host = _timerSystem;
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

        /// <summary>
        /// 销毁时统一调用各管理器的 <see cref="I_GlobaManager.UnInit"/>。
        /// 改为 <c>protected virtual</c> 是为了让 <c>GameRoot</c> 能 override 它，
        /// 顺带把 <c>ServiceLocator</c> 的流程服务槽位还回空对象（见 <c>GameRoot.OnDestroy</c>）。
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 与 Awake 的发布成对：宿主组件随本对象销毁 ⇒ 必须归还，否则 TimerHost 会留下"已销毁组件"的引用
            // （MonoBehaviour 引用不走 Unity 的 ==null 重载 ⇒ 上层不会自动回落到"未就绪"）。用 ReferenceEquals 身份判定。
            if (ReferenceEquals(TimerHost.Host, _timerSystem)) TimerHost.Host = null;

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
