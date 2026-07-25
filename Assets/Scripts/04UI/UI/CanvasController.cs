using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using static WndTools.WndRootTool;

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
        GlobalEventSub.OnSettingCange += OnSettingCange;
        if (isHUD) WndManager.OnWindowStateChange += OnWindowStateChange;
        if (isHUD && ArchiveSvc.GetSetting("沉浸模式") > 0) SetAlpha(transform, 0);

    }

    void OnDestroy()
    {
        GlobalEventSub.OnSettingCange -= OnSettingCange;
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
                if (ArchiveSvc.GetSetting("沉浸模式") > 0)
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
        if (ArchiveSvc.GetSetting("沉浸模式") > 0)
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
        yield return new WaitForSeconds(4.5f);
        SetAlpha(transform, 0, 1, 500, () => SetActive(transform, true));
        yield return new WaitForSeconds(0.5f);
        inIEnumerator = false;
    }
}
