using System.Collections.Generic;
using Core;
using UnityEngine;
using static WndTools.WndRootTool;

/// <summary>
/// 窗口接口，用于GetComponent
/// </summary>
public interface Wnd
{
    public void SetWndState(bool isActive = true);
    /// <summary>初始化</summary>
    public void Init();
    /// <summary>反初始化</summary>
    public void UnInit();
}
/// <summary>
/// UI窗口基类
/// </summary>
public abstract class WindowRoot:MonoBehaviour, Wnd
{
    protected WndManager wndManager;
    protected ResManager resManager;
    protected RoomManager roomManager;
    protected TaskManager taskManager;
    protected PropertyManager propertyManager;
    protected GameRoot root;
    private bool firstInit;

    /// <summary>
    /// 设置窗口显示状态
    /// </summary>
    /// <param name="isActive">是否显示</param>
    public void SetWndState(bool isActive = true)
    {
        if (gameObject.activeSelf != isActive)
        {
            gameObject.SetActive(isActive);
            GlobalEventManager.WndSwitch(gameObject.name, isActive);
            if (isActive)
            {
                if (!firstInit)
                {
                    firstInit = true;
                    wndManager = WndManager.Instance;
                    resManager = ResManager.Instance;
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
    protected GameStateEnum GameState {
        get=> GameRoot.GameState;
        set => GameRoot.GameState = value;
    }
    protected WindowStateEnum WindowState {
        get => GameRoot.WindowState;
        set => GameRoot.WindowState = value;
    }
    protected float TimeScale
    {
        get => GameRoot.TimeScale;
        set => GameRoot.TimeScale = value;
    }
    public bool State => gameObject.activeSelf;

    /// <summary>初始化(初始在WndManager下的才执行)</summary>
    public abstract void Init();
    /// <summary>反初始化(在WndManager下的才执行)</summary>
    public abstract void UnInit();

    /// <summary>窗口第一次打开时</summary>
    protected abstract void FirstShowWnd();
    /// <summary>窗口打开时</summary>
    protected abstract void ShowWnd();
    /// <summary>窗口关闭时</summary>
    protected abstract void HideWnd();
    /// <summary>
    /// 关闭窗口(仅anim中使用)
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
    /*
    /// <summary>初始化跳转</summary>
    private void InitJump()
    {
        foreach (var item in Jump)
        {
            SetCilck(item.button, () => {
                

                //AudioManaqer.PlaySound(new() {cilp = item.clip,cache = true });
            });
        }

    }*/

    public void B_OpenWnd(GameObject item)
    {
        var wnd = item.GetComponent<Wnd>();
        if (wnd != null) wnd.SetWndState(true);
        else SetActive(item, true);
    }
    public void B_CloseWnd(GameObject item)
    {
        var wnd = item.GetComponent<Wnd>();
        if (wnd != null) wnd.SetWndState(false);
        else SetActive(item, false);
    }

    public void B_OpenURL(string url)
    {
        Application.OpenURL(url);
    }

    [System.Serializable]
    protected class JumpInfo
    {
        public Transform button;
        public Transform window;
        public bool state;
        public AudioClip clip;
        public string animName;
        public float delayTime;
    }
}

