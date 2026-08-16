using System;
using System.Collections.Generic;
using Core;
using UnityEngine;
using static WndTools.WndRootTool;


/// <summary>
/// UI窗口基类
/// </summary>


public abstract class Window : MonoBehaviour
{
    protected WndManager wndManager;
    protected ResSvc resManager;
    protected RoomManager roomManager;
    protected TaskManager taskManager;
    protected PropertyManager propertyManager;
    protected GameRoot root;

    private bool firstInit;

    /// <summary>
    /// 设置窗口显示状态
    /// </summary>
    /// <param name="isActive">是否显示</param>
    public virtual void SetWndState(bool isActive = true)
    {
        if (gameObject.activeSelf != isActive||(isActive&&!firstInit))
        {
            gameObject.SetActive(isActive);
            GlobalEventSub.WndSwitch(gameObject.name, isActive);
            if (isActive)
            {
                if (!firstInit)
                {
                    firstInit = true;
                    wndManager = WndManager.Instance;
                    resManager = ResSvc.Instance;
                    roomManager = RoomManager.Instance;
                    taskManager = TaskManager.Instance;
                    propertyManager = PropertyManager.Instance;
                    root = GameRoot.Instance;
                    FirstShowWnd();
                }
                ShowWnd();
            }
            else
            {
                HideWnd();
            }
        }
    }

    public virtual void OnDestroy()
    {
        HideWnd();
    }

    protected GameStateEnum GameState {
        get=> GameRoot.GameState;
        set => GameRoot.GameState = value;
    }
    protected WindowStateEnum WindowState {
        get => WndManager.WindowState;
        set => WndManager.WindowState = value;
    }
    protected float TimeScale
    {
        get => GameRoot.TimeScale;
        set => GameRoot.TimeScale = value;
    }
    public bool State => gameObject.activeSelf;


    /// <summary>窗口第一次打开时</summary>
    protected abstract void FirstShowWnd();
    /// <summary>窗口打开??/summary>
    protected abstract void ShowWnd();
    /// <summary>窗口关闭??/summary>
    protected abstract void HideWnd();
    /// <summary>
    /// 关闭窗口,仅anim中使用
    /// </summary>
    protected void CloseWnd() => SetWndState(false);

    /// <summary>播放动画</summary>
    /// <param name="name">状态名</param>
    /// <param name="interrupt">中断当前播放</param>
    /// <param name="layer">层级</param>
    protected bool PlayAnim(string name, bool interrupt = false, int layer = 0)
    {
        var anim = GetComponent<Animator>();
        if (anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1 || interrupt)
        {
            GetComponent<Animator>().Play(name, layer,0);
            return true;
        }
        return false;
    }

    public static void SetButtonEnter(Transform btn, Action<UnityEngine.EventSystems.PointerEventData> action) => btn.TryGetOrAddComponent<ButtonEnterDetector>().Enter = action;

    public static void SetButtonExit(Transform btn, Action<UnityEngine.EventSystems.PointerEventData> action) => btn.TryGetOrAddComponent<ButtonEnterDetector>().Exit = action;

}

