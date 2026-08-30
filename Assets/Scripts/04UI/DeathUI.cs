using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;
using static WndTools.WndRootTool;

public class DeathUI : Window
{

    [SerializeField]
    [InspectorName("倒计时根节点")]
    private Transform _root;

    [SerializeField]
    [InspectorName("倒计时文本")]
    private Transform _text;


    private int time;
    /// <summary>是否处于团灭判负倒计时中</summary>
    private bool _countingDown;

    protected override void FirstShowWnd()
    {

    }
    public void Init()
    {
        BattleEventSub.OnPlayerDead += OnPlayerDead;
        BattleEventSub.OnPlayerRevive += OnPlayerRevive;
        BattleEventSub.OnWipeFailCountdown += OnWipeFailCountdown;
        BattleEventSub.OnWipeFailCancel += OnWipeFailCancel;
        SetWndState(false);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        BattleEventSub.OnPlayerDead -= OnPlayerDead;
        BattleEventSub.OnPlayerRevive -= OnPlayerRevive;
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


    void OnPlayerDead(I_Actor _)
    {
        SetWndState(true);
    }
    void OnPlayerRevive(I_Actor _)
    {
        SetWndState(false);
    }



    /// <summary>团灭判负倒计时：显示界面并按剩余秒数刷新文本</summary>
    void OnWipeFailCountdown(float remaining)
    {
        _countingDown = true;
        time = (int)remaining;
        WndManager.Instance.CreatCountDown(() => time,CountDownTypeEnum.Red);
        SetActive(_root, true);
        SetWndState(true);
        SetCountDownText(remaining);
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
        SetActive(_root, false);
    }

    /// <summary>将剩余秒数写入所有倒计时文本</summary>
    private void SetCountDownText(float remaining)
    {
        var content = Mathf.CeilToInt(remaining).ToString();
        SetText(_text, content);
    }
}
