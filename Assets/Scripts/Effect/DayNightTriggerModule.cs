using System.Collections;
using System.Collections.Generic;
using FPSGame.DayNightSystem;
using UnityEngine;


[AddComponentMenu("昼夜系统/交替事件模块")]
public class DayNightTriggerModule: MonoBehaviour, IDayNightModule
{

    bool isNoon;

    public void Initialize(DayNightState state)
    {
        GlobalEventSub.DaySwitch(isNoon=(state.NormalizedTime >= 0f && state.NormalizedTime < 0.5f));
    }


    public void Tick(DayNightState state, float deltaTime)
    {
       if(state.NormalizedTime >= 0f && state.NormalizedTime < 0.5f)
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
}
