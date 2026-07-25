using System.Collections;
using System.Collections.Generic;
using FPSGame.DayNightSystem;
using UnityEngine;


[AddComponentMenu("昼夜系统/交替事件模块")]
public class DayNightTriggerModule: MonoBehaviour, IDayNightModule
{
    [InspectorName("白天偏移")]
    [Range(-0.5f, 0.5f)]
    [SerializeField]
    private float dayOffect;

    [InspectorName("夜晚偏移")]
    [Range(-0.5f,0.5f)]
    [SerializeField]
    private float nightOffect;

    bool isNoon;

    public void Initialize(DayNightState state)
    {
        GlobalEventSub.DaySwitch(isNoon = IsNoon(state));
    }


    public void Tick(DayNightState state, float deltaTime)
    {
       if(IsNoon(state))
       {
           if (!isNoon)
           {
               isNoon = true;
                //Debug.LogError("现在变为白天");
               GlobalEventSub.DaySwitch(isNoon);
           }
       }
       else
       {
           if (isNoon)
           {
               isNoon = false;
                //Debug.LogError("现在变为晚上");
                GlobalEventSub.DaySwitch(isNoon);
           }
       }
    }

    public void Dispose() { }


    private bool IsNoon(DayNightState state)
    {
        float dayStart = Mathf.Repeat(dayOffect, 1f);
        float nightStart = Mathf.Repeat(0.5f + nightOffect, 1f);
        float t = state.NormalizedTime;

        if (dayStart < nightStart)
        {
            // 白天区间不跨零点：dayStart <= t < nightStart
            return t >= dayStart && t < nightStart;
        }
        else
        {
            // 白天区间跨零点：t >= dayStart 或 t < nightStart
            return t >= dayStart || t < nightStart;
        }
    }
}
