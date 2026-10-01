using System.Linq;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.Game
{

/// <summary>
/// 可被任务或 AI 关注的场景兴趣点。
/// </summary>
[AddComponentMenu("单位/兴趣点")]
public class InterestPoint : BaseObject, IMissionPoint
{
    float IMissionPoint.IconSizeScale => 0.5f;

    float IMissionPoint.AreaRange { get => 0; set{ } }

    public bool HaveTag(MissionTag tag) => this.tag.HasFlag(tag);

    [SerializeField]
    new MissionTag tag;

    /// <summary>已被发现</summary>
    private bool discovered { get; set; }

    private void Awake()
    {
        GlobalEventSub.OnMark += Mark;
    }
    private void OnDestroy()
    {
        GlobalEventSub.OnMark -= Mark;
    }


    private void Update()
    {
        if (!FPSGame.Data.BattleState.IsStartBattle) return;

        var dis = ActorsManager.Players.Min(item => Vector2.Distance(item.Pos.ToVector2(), Pos.ToVector2()));
        
        bool entityRange = dis < HalfRange + 10;


        if (entityRange && !discovered)
        {
            TryDiscovered();
            //应该是提示谁谁谁已发现地点
            //CreatNotice("Kotama", "ApproachingTarget", () => !InAirdropRange);
        }

    }

    protected void CreatNotice(string role, string type, System.Func<bool> func = default,  float vaildTime = -1)
    {
        FPSGame.Gameplay.GlobalEventSub.Notice(role, type, func, vaildTime);
    }

    public void TryDiscovered()
    {
        discovered = true;
        BattleEventSub.MissionEnityShow(this);
    }
    private void Mark(GameObject owner, GameObject target, Vector3 point)
    {
        if (!target) return;

        if (!discovered && target && target.transform.IsChildOf(transform))
        {
            TryDiscovered();
        }
    }


}
}
