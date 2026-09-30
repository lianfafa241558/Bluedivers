using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Gameplay
{

// I_Login 已于 2026-09-30 下沉到契约层（00GameContract/Interface_Login.cs）：
// NetManager.Add/Remove(I_Login) 要被 INetService 引用，而实现方 LogicBehaviour 被玩法层
// （WeaponBaseController）继承 ⇒ 接口必须落在"两侧共见"的契约层。

public abstract class LogicBehaviour : MonoBehaviour ,I_Login
{
    protected PEMaths.PEInt TickTime = Constants.LoginFrame;

    protected virtual void Awake()
    {
        // 走服务契约：GameRoot/NetManager 都在 01Manager，而本类被玩法层继承 ⇒ 不能直连
#if UNITY_EDITOR
        if (FPSGame.GameContract.ServiceLocator.Flow != null && FPSGame.GameContract.ServiceLocator.Flow.IsLocal) StartCoroutine("Wait");
        else FPSGame.GameContract.ServiceLocator.Net?.Add(this);
#else
        FPSGame.GameContract.ServiceLocator.Net?.Add(this);
#endif
        LogicInit();
    }

#if UNITY_EDITOR
    IEnumerable Wait()
    {
        yield return null;
        FPSGame.GameContract.ServiceLocator.Net?.Add(this);
    }
#endif

    protected virtual void OnDestroy()
    {
        FPSGame.GameContract.ServiceLocator.Net?.Remove(this);
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
