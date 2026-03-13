using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface I_Login
{
    void LogicInit();
    void LogicUnInit();

    void LogicTick();
    bool IsActive();
}

public abstract class LogicBehaviour : MonoBehaviour ,I_Login
{
    protected PEMaths.PEInt TickTime = Constants.LoginFrame;

    protected virtual void Awake()
    {
#if UNITY_EDITOR
        if (GameRoot.Instance.IsLocal) GameRoot.CreateTimer(()=>NetManager.Instance.Add(this), Time.deltaTime);
        else NetManager.Instance.Add(this);
#else
        NetManager.Instance.Add(this);
#endif
        LogicInit();
    }
    protected virtual void OnDestroy()
    {
        NetManager.Instance.Remove(this);
        LogicUnInit();
    }

    public abstract void LogicTick();

    public bool IsActive() => this.IsEnable();

    public abstract void LogicInit();


    public abstract void LogicUnInit();



    //这个写法是显式实现，只能通过接口调用
    //void I_Login.Tick();

}
