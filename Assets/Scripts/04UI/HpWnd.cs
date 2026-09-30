using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class HpWnd : Window
{
    /// <summary>敌人距离玩家超过该距离时，受击不再显示血条</summary>
    private const float HpBarMaxDistance = 50f;

    [SerializeField]
    HpItemBase SoilderPrefab, BossPrefab;

    Dictionary<GameObject, HpItemBase> dic=new();
    AutoObjectPool<GameObject, HpItemBase> pool;


    protected override void FirstShowWnd()
    {


        pool = new(
            (item) =>//更新
            {
                if (!item.CanRecycle())
                {
                    item.Tick();
                    return true;
                }
                else
                {
                    item.End();
                    return false;
                }
            },
            () =>//添加
            {
                var item = Instantiate(SoilderPrefab, transform);
                SetActive(item,false);
                return item;
            },
            (item) =>//释放
            {
                SetActive(item,false);
            },
        5);

    }

    protected override void ShowWnd()
    {

        BattleEventSub.OnEnemyCreate += OnEnemyCreate;
        BattleEventSub.OnEnemyDead += OnEnemyDead;
        BattleEventSub.OnUnitHit += OnUnitHit;
    }

    protected override void HideWnd()
    {
        BattleEventSub.OnEnemyCreate -= OnEnemyCreate;
        BattleEventSub.OnEnemyDead -= OnEnemyDead;
        BattleEventSub.OnUnitHit -= OnUnitHit;
    }
    private void Update()
    {
        pool.Update();
        foreach (var item in dic.Values)
        {
            if (!item.CanRecycle())
            {
                item.Tick();
            }
        }
    }



    private void OnEnemyCreate(Actor go)
    {
        //var enemy = go.GetComponent<EnemyController>();
        if (go.HasFlag( ActorFlag.Boss))
        {
            var prefab = Instantiate(BossPrefab, transform.GetChild(0));
            dic.Add(go.gameObject, prefab);
            prefab.Set(go.gameObject);
        }
    }

    private void OnEnemyDead(Actor go)
    {
        //var enemy = go.GetComponent<EnemyController>();
        if (go.HasFlag(ActorFlag.Boss) && dic.TryGetValue(go.gameObject, out var item))
        {
            item.End();
            Tool.Destroy(item.gameObject,1);
            dic.Remove(go.gameObject);
        }
    }

    private void OnUnitHit(GameObject victim ,GameObject attacker)
    {
        victim.TryGetComponent(out Actor actor);

        //距离玩家超过 50m 的敌人受击时不响应血条
        if (IsEnemyTooFar(actor)) return;

        if (pool.TryFind(victim, out var item))
        {
            item.Refresh();
        }
        else if(actor!=null&&actor.UseHpBar)
        {
            pool.Get(victim).Set(victim);
        }
        else if (dic.TryGetValue(victim,out var value))
        {
            value.Refresh();
        }
        
    }

    /// <summary>受击单位是否为距离玩家超过 <see cref="HpBarMaxDistance"/> 的敌人</summary>
    private static bool IsEnemyTooFar(Actor actor)
    {
        if (actor == null || actor.Type != UnitTypeEnum.Enemy) return false;
        var player = ActorsManager.Player;
        if (player == null || !player.IsValidMono()) return false;
        return Vector3.Distance(player.Pos, actor.Pos) > HpBarMaxDistance;
    }

}
