using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using UnityEngine;

namespace FPSGame.UI
{
using FPSGame.Gameplay;
using FPSGame.Game;
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
using FPSGame.GameData;

/// <summary>
/// HUD 画布的淡入、缩放与沉浸模式。
/// </summary>
[AddComponentMenu("UI/HUD/画布控制")]
public class CanvasController : MonoBehaviour
{
    public bool isHUD;
    public bool isCameraMode;
    [SerializeField]
    private bool inIEnumerator;
    /// <summary>开场淡入序列的句柄。⚠ Unity 在 GameObject 被 SetActive(false) 时会**直接停掉本组件上的协程**，
    /// 标志位不会跟着复位，所以必须留句柄显式收尾（见 <see cref="AbortIntro"/>）。</summary>
    private Coroutine _introRoutine;
    /// <summary>HUD 是被窗口状态（设置/菜单）藏起来的 —— 淡入补间的收尾回调此时不许把它重新激活，
    /// 否则 HUD 会盖在设置界面上（补间挂在 GameRoot 的 viewTimer 上，HUD 停用后它照样在跑）。</summary>
    private bool _hiddenByUi;

    void Awake()
    {
        if (TryGetComponent(out Canvas canvas)&& isCameraMode && canvas.worldCamera == null)
        {
            canvas.worldCamera = UICamera.uiCamera;
        }
        GlobalEventBus.OnSettingCange += OnSettingCange;
        if (isHUD) WndManager.OnWindowStateChange += OnWindowStateChange;
        if (isHUD && ArchivesData_SO.Current.GetSetting("沉浸模式") > 0) SetAlpha(transform, 0);

    }

    void OnDestroy()
    {
        GlobalEventBus.OnSettingCange -= OnSettingCange;
        if (isHUD) WndManager.OnWindowStateChange -= OnWindowStateChange;
    }

    private void OnSettingCange(string key, float value)
    {
        if (key == "UI缩放系数")
        {
            for (var i = 0; i < transform.childCount; ++i)
            {
                GetComponent<UnityEngine.UI.CanvasScaler>().scaleFactor = value / 100f;
            }

        }
        else if (isHUD && key == "沉浸模式"&&value > 0) SetAlpha(transform, 0);
    }


    /// <summary>HUD 画布随窗口状态显隐。
    /// <para>⚠ <c>Airdrop</c> 必须与 <c>Game</c> 一样"可见" —— 它只是**叠加在 HUD 上的战备菜单**，
    /// 不是"离开游戏画面"。漏掉这一支的后果是"整个 HUD 全黑"：战备菜单开着时开设置（状态被记成 UI），
    /// 关掉设置后状态回到 Airdrop ⇒ 没有任何分支把 HUD 打开，要再按一次战备键
    /// （<c>AirdropController.Close</c> 把状态设回 Game）才恢复。2026-10-11 用户实测 + 现场读数：
    /// 状态 `Airdrop` 而 3 个 isHUD 画布全是 `activeSelf=false`。</para>
    /// <para>▍为什么 Airdrop 不淡入：它是"从 UI 回头"的状态，玩家关掉设置窗口就要立刻看见 HUD
    /// （同下面 Game 分支里那句 `oldState != Airdrop` 的意图）。</para></summary>
    private void OnWindowStateChange(WindowStateEnum oldState, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                _hiddenByUi = false;
                if (ArchivesData_SO.Current.GetSetting("沉浸模式") > 0)
                {
                    SetActive(transform, true);
                    SetAlpha(transform, 0);
                }
                else
                {
                    SetActive(transform, true);
                    //Debug.LogError("设置淡入"+"旧状??+ oldState);
                    if (!inIEnumerator && oldState != WindowStateEnum.Airdrop)
                    {
                        SetAlpha(transform, 0, 1, 500);
                    }

                }
                break;
            case WindowStateEnum.Airdrop:   // ★ 同 Game：HUD 必须可见（不淡入，见方法注释）
                _hiddenByUi = false;
                SetActive(transform, true);
                break;
            case WindowStateEnum.UI:
                AbortIntro();      // 必须赶在 SetActive(false) 之前：那一下会把协程杀掉
                _hiddenByUi = true;
                SetActive(transform, false);
                break;
        }
    }

    /// <summary>开场淡入还没走完就被 UI 状态藏了 —— 就地收尾复位。
    /// <para>⚠ 不这么做的话：<c>SetActive(false)</c> 停掉协程，<c>inIEnumerator</c> 永远是 true，
    /// 关掉设置回到 Game 时没有任何分支把 alpha 拉回 1（2026-11-16 用户实测：开局 3.5 秒内开关设置，HUD 全黑）。</para></summary>
    private void AbortIntro()
    {
        if (!inIEnumerator) return;
        if (_introRoutine != null) StopCoroutine(_introRoutine);
        _introRoutine = null;
        inIEnumerator = false;
        SetAlpha(transform, 1);
    }

    /// <summary>淡入补间结束：只在没被窗口状态藏起来时才激活，否则会把 HUD 叠到设置界面上。</summary>
    private void RevealAfterFade()
    {
        if (!_hiddenByUi) SetActive(transform, true);
    }
    
    public void OnGameStart()
    {
        if (ArchivesData_SO.Current.GetSetting("沉浸模式") > 0)
        {
            SetActive(transform, true);
            SetAlpha(transform, 0);
        }
        else
        {
            //Debug.LogError("游戏开始");
            // 重开局时旧的 3.5 秒计时还在跑 ⇒ 必须先停掉，否则两个序列互相抢 alpha
            if (_introRoutine != null) StopCoroutine(_introRoutine);
            _introRoutine = StartCoroutine(_OnGameStart());
        }
    }

    IEnumerator _OnGameStart()
    {
        inIEnumerator = true;
        SetAlpha(transform, 0);
        yield return new WaitForSeconds(3.5f);
        _introRoutine = null;    // 后半段只剩 tween，不再需要句柄
        SetAlpha(transform, 0, 1, 500, RevealAfterFade);
        yield return new WaitForSeconds(0.5f);
        inIEnumerator = false;
    }
}
}
