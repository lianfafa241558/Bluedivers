using System.Collections.Generic;
using FPSGame.Furn;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

/// <summary>
/// 凯伊(Kei)欧帕兹提交点。
/// 玩家靠近交互后，将自身携带背包中的欧帕兹全部提交给凯伊，
/// 计入任务采集计数，并保留一份已提交列表供 UI 展示。
/// </summary>
public class Furniture_KeiSubmit : Furniture_Attached
{
    public override string Desc => "提交欧帕兹给凯伊";

    private void Start()
    {
        // 已有采集数据时直接显示历史提交
        if (TaskManager.Instance.nowTask.collectProperty.Count > 0)
        {
            foreach (var kvp in TaskManager.Instance.nowTask.collectProperty)
            {
                GlobalEventSub.KeiSubmit(kvp.Key, kvp.Value);
            }
        }
    }

    public override bool CanOperate(GameObject unit)
    {
        if (!base.CanOperate(unit)) return false;
        // 只有携带了欧帕兹的玩家才能提交
        if (unit && unit.TryGetComponent(out PlayerOOPartInventory bag))
        {
            return bag.CurrentCount > 0;
        }
        return false;
    }

    public override void Operate()
    {
        base.Operate();
        var user = owner;
        if (user == null || !user.TryGetComponent(out PlayerOOPartInventory bag)) return;

        // 把背包里的欧帕兹全部提交给凯伊
        foreach (var kvp in bag.GetAll())
        {
            OOPartEnum type = kvp.Key;
            int count = kvp.Value;
            bag.Remove(type, count);
            // 计入任务采集计数
            BattleManager.Instance.SubmitOOPart(user, type, count);
        }

        GlobalEventSub.PlayMeetSpeech(user, SpeechTypeEnum.Responded);
    }
}
