using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;
using FPSGame.GameContract;

namespace FPSGame.Gameplay
{

/// <summary>
/// 场景中的欧帕兹拾取物。
/// </summary>
[AddComponentMenu("交互/欧帕兹拾取物")]
public class OOPart : Furniture_Attached
{

    public override string Desc=> "采集[" + ShowName + "]";


    public override void Operate()
    {
        var owner = this.owner;
        base.Operate();
        var type = Tool.StringToEnum<OOPartEnum>(Id);
        int count = (int)ExtFloatParameter;
        //Debug.LogError("玩家拾取了"+Id+" ,"+type+" "+count+"玩家:"+owner);
        SpeechTypeEnum enumtype=SpeechTypeEnum.CollOOParts;
        if (type == OOPartEnum.Pyroxene)
        {
            var dic = FPSGame.Data.TaskState.CollectProperty;
            if (!dic.TryAdd(type, count)) dic[type] += count;
            Tool.Destroy(gameObject);
        }
        // 采集物先存入玩家携带背包（有上限，满则无法采集）
        else if (owner && owner.TryGetComponent(out PlayerOOPartInventory bag))
        {
            if (bag.IsFull(type))
            {
                enumtype = SpeechTypeEnum.CollOOPartsFail;
            }
            else
            {
                bag.TryAdd(type, count);
                GlobalEventBus.OOPartCollect(owner, type, count);
                Tool.Destroy(gameObject);
            }
        }
        GlobalEventBus.PlayMeetSpeech(owner, enumtype);
        
    }
    
}
}
