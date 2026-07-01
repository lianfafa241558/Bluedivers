using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;

public class DeathUI : Window
{


    protected override void FirstShowWnd()
    {

    }
    public void Init()
    {
        BattleEventSub.OnPlayerDead += OnPlayerDead;
        BattleEventSub.OnPlayerRevive += OnPlayerRevive;
        SetWndState(false);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        BattleEventSub.OnPlayerDead -= OnPlayerDead;
        BattleEventSub.OnPlayerRevive -= OnPlayerRevive;
    }

    protected override void ShowWnd()
    {

    }
    protected override void HideWnd()
    {

    }



    void OnPlayerDead(I_Actor _)
    {
        SetWndState(true);
    }
    void OnPlayerRevive(I_Actor _)
    {
        SetWndState(false);
    }
}
