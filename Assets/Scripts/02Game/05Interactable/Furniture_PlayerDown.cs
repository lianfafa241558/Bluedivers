using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;


/// <summary>
/// 玩家倒地组件
/// </summary>
public class Furniture_PlayerDown : Furniture_Base
{

    HealthPlayer Health { get; set; }

    protected override void Awake()
    {
        base.Awake();
        Health = GetComponent<HealthPlayer>();
        canOperate = false;
        Health.OnDie+= OnDown;
    }

    private void OnDestroy()
    {
        Health.OnDie -= OnDown;
    }

    public override void Operate()
    {
        base.Operate();
        //复活
        Health.Revive();
        canOperate = false;
        //PlaySound(audioOper);
    }

    
    void OnDown(GameObject _)
    {
        canOperate = true;
        PlaySound(audioClose);
    }
}
