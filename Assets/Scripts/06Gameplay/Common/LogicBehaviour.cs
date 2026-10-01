using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Gameplay
{

    // 依赖沿革：2026-09-30 I_Login 从本文件下沉到契约层（当时配合 ServiceLocator.Net 槽）；
    // 2026-10-01 该槽删除，改为 00_Core 的 LogicFrame 原语 ⇒ I_Login 与注册入口都回到最底层，
    // 本类（被玩法层 WeaponBaseController 等 26 个子类继承）的依赖方向最浅。

    public abstract class LogicBehaviour : MonoBehaviour ,I_Login
    {
        protected PEMaths.PEInt TickTime = Constants.LoginFrame;

        protected virtual void Awake()
        {
            // 走 00_Core 的逻辑帧原语：GameRoot/NetManager 都在 01Manager，而本类被玩法层继承 ⇒ 不能直连。
            // 宿主未接管时 Register 是空操作（等价于原空对象的语义）。
    #if UNITY_EDITOR
            // IsLocal 改走「数据自持」（2026-10-01 取代 ServiceLocator.Flow；原判空是死代码）
            if (FPSGame.Data.FlowState.IsLocal) StartCoroutine("Wait");
            else LogicFrame.Register(this);
    #else
            LogicFrame.Register(this);
    #endif
            LogicInit();
        }

    #if UNITY_EDITOR
        IEnumerable Wait()
        {
            yield return null;
            LogicFrame.Register(this);
        }
    #endif

        protected virtual void OnDestroy()
        {
            LogicFrame.Unregister(this);
            LogicUnInit();
        }

        public abstract void LogicTick();

        public bool IsActive() => this.IsEnable();

        public abstract void LogicInit();


        public abstract void LogicUnInit();



        //这个写法是显式实现，只能通过接口调用
        //void I_Login.Tick();

    }
}
