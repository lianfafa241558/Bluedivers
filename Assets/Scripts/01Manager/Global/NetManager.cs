using System.Collections;
using System.Collections.Generic;
using Core.Interface;
using GameContract;
using PEMaths;
using UnityEngine;

public class NetManager : SingletonNet<NetManager>, I_GlobaManager
{
    //按理说这个应该是由(服务器)房主发的?
    private PEInt lastTime;

    private List<I_Login> list;

    public void Init()
    {
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

