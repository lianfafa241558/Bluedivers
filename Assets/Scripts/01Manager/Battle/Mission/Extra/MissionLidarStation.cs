using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;
/// <summary>
/// 雷达站
/// </summary>
public class MissionLidarStation : MissionBase
{
    KeyScreen keyScreen;

    protected override void Start()
    {
        keyScreen = entity.transform.GetComponentInChildren<KeyScreen>();
        keyScreen.OnComple += OnKeyScreenComple;
        base.Start();
    }

    private void OnKeyScreenComple()
    {
        CompleteMission();
    }
}
