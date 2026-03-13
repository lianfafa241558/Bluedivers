using System;
using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;

public class ViewTimerController : MonoBehaviour
{
    private ViewTimerSystem _timerSystem;
    void Awake()
    {
        _timerSystem = new();
    }

    // Update is called once per frame
    void Update()
    {
        _timerSystem.Update();
    }


    public LoginTimer CreateTimer(Action cb, float waitTime, int counter = 1, Action endcb = null) => _timerSystem.CreateTimer(cb, waitTime, counter, endcb);
    public LoginTimer CreateTimer(Action<int> cb, float waitTime, int counter = 1, Action endcb = null) => _timerSystem.CreateTimer(cb, waitTime, counter, endcb);
    public LoginTimer CreatePerTimer(Action percb, float waitTime, Action endcb = null) => _timerSystem.CreateTimer(percb, waitTime, endcb);

    public void ClearTimer() =>_timerSystem.Clear();

    public void RemoveTimer(LoginTimer cb) => _timerSystem.RemoveTimer(cb);
}
