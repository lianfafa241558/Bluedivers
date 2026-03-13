using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class HpWnd : WindowRoot
{
    [SerializeField]
    HpItemBase SoilderPrefab, BossPrefab;

    Dictionary<GameObject, HpItemBase> dic=new();
    AutoObjectPool<GameObject, HpItemBase> pool;

    public override void Init()
    {

    }
    public override void UnInit()
    {

    }

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
        GlobalEventManager.OnEnemyCreate += OnEnemyCreate;
        GlobalEventManager.OnEnemyDead += OnEnemyDead;
        GlobalEventManager.OnUnitHit += OnUnitHit;
    }

    protected override void HideWnd()
    {
        GlobalEventManager.OnEnemyCreate -= OnEnemyCreate;
        GlobalEventManager.OnEnemyDead -= OnEnemyDead;
        GlobalEventManager.OnUnitHit -= OnUnitHit;
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
        var enemy = go.GetComponent<EnemyController>();
        if (enemy.Boss)
        {
            var prefab = Instantiate(BossPrefab, transform.GetChild(0));
            dic.Add(go.gameObject, prefab);
            prefab.Set(go.gameObject);
        }
    }

    private void OnEnemyDead(Actor go)
    {
        var enemy = go.GetComponent<EnemyController>();
        if (enemy.Boss&&dic.TryGetValue(go.gameObject, out var item))
        {
            Tool.Destroy(item);
            dic.Remove(go.gameObject);
        }
    }

    private void OnUnitHit(GameObject victim ,GameObject attacker)
    {
        if (pool.TryFind(victim, out var item))
        {
            item.Refresh();
        }
        else if(victim.GetComponent<Actor>().UseHpBar)
        {
            pool.Get(victim).Set(victim);
        }
        else if (dic.TryGetValue(victim,out var value))
        {
            value.Refresh();
        }
        
    }

}
