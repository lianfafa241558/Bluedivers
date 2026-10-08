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
