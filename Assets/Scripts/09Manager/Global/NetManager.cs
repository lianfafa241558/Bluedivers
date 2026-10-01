using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using PEMaths;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Managers
{

/// <summary>
/// 逻辑帧驱动：按 <see cref="I_Login"/> 调度接入逻辑帧的组件。
///
/// <para>▍名字 vs 现状：本类最初是为**联机**准备的（"帧该由服务器 / 房主下发"），
/// 目前只承担**统一的逻辑帧处理**——它是 <see cref="LogicFrame"/> 原语的宿主
/// （<c>LogicFrame.Sink</c>，2026-10-01 起取代 <c>ServiceLocator.Net</c> 槽）。
/// 将来真做联机时只需把"帧来源"从本地时钟换成网络帧，本类结构与接入方（<c>LogicBehaviour</c> 子类）都不受影响。</para>
/// </summary>

public class NetManager : SingletonNet<NetManager>, I_GlobaManager, ILogicFrameSink
{
    //按理说这个应该是服务器或者房主发的
    private PEInt lastTime;

    private List<I_Login> list;

    public void Init()
    {
        //接管逻辑帧宿主：供玩法层（LogicBehaviour 的 26 个子类）注册（见 00Core/LogicFrame.cs）
        LogicFrame.Sink = this;
        lastTime = (PEInt)Time.time;
        list = new();
    }
    public void UnInit()
    {
        //服务下线：把宿主还回去（未接管语义），避免静态字段指向已销毁实例
        if (ReferenceEquals(LogicFrame.Sink, this)) LogicFrame.Sink = null;
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
