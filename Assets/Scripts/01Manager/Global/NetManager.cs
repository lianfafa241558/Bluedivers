using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using PEMaths;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Managers
{

/// <summary>
/// 逻辑帧驱动，按 I_Login 调度接入逻辑帧的组件。
/// </summary>
[AddComponentMenu("管理/逻辑帧管理")]
public class NetManager : SingletonNet<NetManager>, I_GlobaManager, FPSGame.GameContract.INetService
{
    //按理说这个应该是服务器或者房主发的
    private PEInt lastTime;

    private List<I_Login> list;

    public void Init()
    {
        FPSGame.GameContract.ServiceLocator.Net = this;//注册逻辑帧服务：供玩法层（LogicBehaviour）等下层访问（见 ServiceLocator.cs）
        lastTime = (PEInt)Time.time;
        list = new();
    }
    public void UnInit()
    {
        list = null;
    }

    void Update()
    {
        if((PEInt)Time.time> lastTime + Constants.LoginFrame)
        {
            lastTime += Constants.LoginFrame;
            for (int i = 0; i < list.Count; ++i)
            {
                if(list[i].IsActive()) list[i].LogicTick();
            }
        }
    }

    public void Add(I_Login obj)
    {
        list.Add(obj);
    }
    public void Remove(I_Login obj)
    {
        if(list.IsValid()) list.Remove(obj);
    }
}

}
