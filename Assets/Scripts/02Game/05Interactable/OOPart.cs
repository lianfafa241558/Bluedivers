using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using Utils;

public class OOPart : Furniture_Base
{

    public override string Desc=> "采集[" + ShowName + "]";


    public override void Operate()
    {
        var owner = this.owner;
        base.Operate();
        var type = Tool.StringToEnum<OOPartEnum>(Id);
        int count = (int)ExtFloatParameter;
        Debug.LogError("玩家拾取了"+Id+" ,"+type+" "+count+"玩家:"+owner);
        SpeechTypeEnum enumtype=SpeechTypeEnum.CollOOParts;
        if (type == OOPartEnum.Pyroxene)
        {
            var dic = TaskManager.Instance.nowTask.collectProperty;
            if (!dic.TryAdd(type, count)) dic[type] += count;
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
                GlobalEventSub.OOPartCollect(owner, type, count);
                Tool.Destroy(gameObject);
            }
        }
        GlobalEventSub.PlayMeetSpeech(owner, enumtype);
        
    }
    
}
