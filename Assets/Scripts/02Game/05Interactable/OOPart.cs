using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class OOPart : Furniture_Base
{

    void Start()
    {
        desc = "采集["+ShowName+ "]";   
    }

    public override void Operate()
    {
        base.Operate();
        var type = Tool.StringToEnum<OOPartEnum>(Id);
        GlobalEventManager.OOPartCollect(owner,type,(int)ExtFloatParameter);
        GlobalEventManager.PlayMeetSoeech(owner, SpeechTypeEnum.CollOOParts);
        //var dic = TaskManager.Instance.nowTaskCfg.collectProperty;
        //if (!dic.TryAdd(type,1)) ++dic[type];
        Tool.Destroy(gameObject);
    }
    
}
