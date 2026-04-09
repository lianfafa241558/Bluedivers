using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;

public class DeathWnd : WindowRoot
{

    private void Awake()
    {
        //临时的，以后再想办法创建
        gameObject.SetActive(false);

        SetWndState(true);
        SetWndState(false);
    }

    protected override void FirstShowWnd()
    {
        GlobalEventManager.OnPlayerDead+= OnPlayerDead;
        GlobalEventManager.OnPlayerRevive += OnPlayerRevive;
    }
    private void OnDestroy()
    {
        GlobalEventManager.OnPlayerDead -= OnPlayerDead;
        GlobalEventManager.OnPlayerRevive -= OnPlayerRevive;
    }

    protected override void ShowWnd()
    {
     
    }
    protected override void HideWnd()
    {

    }

    public override void Init() { }
    public override void UnInit() { }

    void OnPlayerDead(I_Actor _)
    {
        SetWndState(true);
    }
    void OnPlayerRevive(I_Actor _)
    {
        SetWndState(false);
    }
}
