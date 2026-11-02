using System.Linq;
using FPSGame.Core;
using FPSGame.GameContract;
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
        GlobalEventBus.OnMark += Mark;
    }
    private void OnDestroy()
    {
        GlobalEventBus.OnMark -= Mark;
    }


    private void Update()
    {
        if (!FPSGame.Data.BattleState.IsStartBattle) return;

        // 手写"最近玩家距离"：原来 Min(lambda) 每帧每个兴趣点都要建一个捕获闭包 + 折一个枚举器
        // （19 个兴趣点实测 3.1KB/帧，每点 163B）
        var players = ActorsManager.Players;
        if (players.Count == 0) return;
        Vector2 selfPos = Pos.ToVector2();
        float dis = float.MaxValue;
        for (int i = 0, l = players.Count; i < l; ++i)
        {
            if (players[i] == null) continue;
            float d = Vector2.Distance(players[i].Pos.ToVector2(), selfPos);
            if (d < dis) dis = d;
        }
            
        bool entityRange = dis < HalfRange + 10;


        if (entityRange && !discovered)
        {
            TryDiscovered();
        }

    }

    protected void CreatNotice(string role, string type, System.Func<bool> func = default,  float vaildTime = -1)
    {
        FPSGame.Gameplay.GlobalEventBus.Notice(role, type, func, vaildTime);
    }

    public void TryDiscovered()
    {
        // ⚠ 播报只在"首次发现"：Mark（标记点位）与 Update 都会调到这里
        bool first = !discovered;
        discovered = true;
        BattleEventBus.MissionEnityShow(this);
        // 消息窗：发现兴趣点（触发者 = 最近的玩家，与本类 Update 的距离口径同一套）
        if (first) GameMsg.Discover(GameMsg.Of(ActorsManager.NearestPlayer(Pos)), "兴趣点");
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
