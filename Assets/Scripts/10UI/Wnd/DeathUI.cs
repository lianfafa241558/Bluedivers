using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Game;

namespace FPSGame.UI
{
using FPSGame.Core;
using FPSGame.Gameplay;
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;

/// <summary>
/// 死亡与团灭判负界面。
/// </summary>
[AddComponentMenu("UI/窗口/死亡界面")]
public class DeathUI : Window
{


    private float time;
    /// <summary>是否处于团灭判负倒计时中</summary>
    private bool _countingDown;

    protected override void FirstShowWnd()
    {

    }
    public void Init()
    {
        UnitEventSub.OnPlayerDead += OnPlayerDead;
        UnitEventSub.OnPlayerRevive += OnPlayerRevive;
        BattleEventSub.OnWipeFailCountdown += OnWipeFailCountdown;
        BattleEventSub.OnWipeFailCancel += OnWipeFailCancel;
        SetWndState(false);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnitEventSub.OnPlayerDead -= OnPlayerDead;
        UnitEventSub.OnPlayerRevive -= OnPlayerRevive;
        BattleEventSub.OnWipeFailCountdown -= OnWipeFailCountdown;
        BattleEventSub.OnWipeFailCancel -= OnWipeFailCancel;
    }

    protected override void ShowWnd()
    {

    }
    protected override void HideWnd()
    {
        StopCountdown();
    }


    void OnPlayerDead(IActor _)
    {
        SetWndState(true);
    }
    void OnPlayerRevive(IActor _)
    {
        SetWndState(false);
    }



    /// <summary>团灭判负倒计时：显示界面并按剩余秒数刷新文本</summary>
    void OnWipeFailCountdown(float remaining)
    {
        _countingDown = true;
        time = (int)remaining;
        WndManager.Instance.CreatCountDown(() => (int)time,CountDownTypeEnum.Red,11);

        SetWndState(true);
    }
    private void Update()
    {
        if (_countingDown && time > 0) time -= Time.deltaTime;
    }

    /// <summary>倒计时取消：被救起或判负条件不再满足</summary>
    void OnWipeFailCancel()
    {
        StopCountdown();
        // 仍有人处于死亡状态则保留界面，无人死亡则关闭
        if (BattleManager.Instance && !BattleManager.Instance.IsTeamWiped) SetWndState(false);
    }

    /// <summary>结束倒计时显示</summary>
    private void StopCountdown()
    {
        if (!_countingDown) return;
        _countingDown = false;

    }


}
}
