using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using FpsGame.Mission;
using GameContract;
using Newtonsoft.Json;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class InterestPoint : BaseObject, I_MissionPoint
{
    float I_MissionPoint.IconSizeScale => 0.5f;

    float I_MissionPoint.AreaRange { get => 0; set{ } }

    public bool HaveTag(MissionTag tag) => this.tag.HasFlag(tag);

    [SerializeField]
    new MissionTag tag;

    /// <summary>已被发现</summary>
    private bool discovered { get; set; }


    private void Update()
    {
        if (!BattleManager.Instance.IsStartBattle) return;

        var dis = ActorsManager.Players.Min(item => Vector2.Distance(item.Pos.ToVector2(), Pos.ToVector2()));
        
        bool entityRange = dis < HalfRange + 10;


        if (entityRange && !discovered)
        {
            TryDiscovered();
            //应该是提示谁谁谁已发现地点
            //CreatNotice("Kotama", "ApproachingTarget", () => !InAirdropRange);
        }

    }

    protected void CreatNotice(string role, string type, System.Func<bool> func = default, float delay = 0, float vaildTime = -1)
    {
        WndManager.Instance.CreatNotice(role, type, func, delay, vaildTime);
    }

    public void TryDiscovered()
    {
        discovered = true;
        GlobalEventManager.MissionEnityShow(this);
    }


}