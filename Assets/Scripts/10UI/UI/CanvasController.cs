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
                SetActive(transform, true);
                break;
            case WindowStateEnum.UI:
                SetActive(transform, false);
                break;
        }
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
            StartCoroutine(_OnGameStart());
        }
    }

    IEnumerator _OnGameStart()
    {
        inIEnumerator = true;
        SetAlpha(transform, 0);
        yield return new WaitForSeconds(3.5f);
        SetAlpha(transform, 0, 1, 500, () => SetActive(transform, true));
        yield return new WaitForSeconds(0.5f);
        inIEnumerator = false;
    }
}
}
