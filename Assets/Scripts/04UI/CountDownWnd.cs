using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;


public enum CountDownTypeEnum
{
    Blue,
    Red,

}

/// <summary>
/// 通用倒计时窗口（外部驱动）。
/// 由调用方通过 <see cref="StartDown(Func{int})"/> 传入值提供者，每帧读取返回值驱动显示，
/// 自身不做计时。兼容旧用法：传 ()=>TaskManager.Instance.nowTask.Countdown。
/// 支持配置起始阈值、时间格式、是否显示动画/声音，并可通过事件回调感知倒计时变化与归零。
/// </summary>
public class CountDownWnd : Window
{
    [Serializable]
    private struct CountDownTypeInfo
    {
        public Color bgColor;
        public Color iconColor;
        public Sprite icon;
        public Color titleColor;
    }

    [SerializeField]
    private Transform bg, title, txt, icon,iconFrame;

    [SerializeField]
    private Animator anim;

    [SerializeField]
    private AudioClip warning;

    [InspectorName("首次显示值")]
    [SerializeField]
    private int countDown = 0;

    [Header("通用配置")]
    [Tooltip("低于此值才显示动画与音效(即倒计时启动阈值)")]
    [SerializeField]
    private int activeBelow = 16;

    [Tooltip("文本格式,{0}为秒数")]
    [SerializeField]
    private string timeFormat = "00:{0:D2}";

    [Tooltip("动画状态名")]
    [SerializeField]
    private string animStateName = "Idle";

    [SerializeField]
    private List<KVP<CountDownTypeEnum, CountDownTypeInfo>> infos;

    /// <summary>外部值提供者；为空表示尚未启动</summary>
    private Func<int> _valueProvider;

    /// <summary>窗口内可折叠高度(用于动画展开收起)</summary>
    private int height;

    /// <summary>每次显示数值跳变时触发(参数为当前剩余秒数)</summary>
    public event Action<int> OnValueChanged;

    ///// <summary>倒计时归零时触发</summary>
    //public event Action OnFinished;

    /// <summary>是否正在显示倒计时动画区域</summary>
    public bool IsCounting => _valueProvider != null;

    protected override void FirstShowWnd()
    {
        countDown = activeBelow;
        var rect = transform.RectTransform();
        height = (int)rect.rect.height;
        rect.sizeDelta = new(rect.rect.width, 0);
    }

    protected override void ShowWnd()
    {
        SetActive(anim.transform, false);
        // 兼容旧用法：若未通过 Start(...) 指定提供者,默认跟随当前任务倒计时
        //if (_valueProvider == null)
        //{
        //    _valueProvider = () => taskManager.nowTask.Countdown;
        //}
    }

    protected override void HideWnd()
    {
        Stop();
    }

    /// <summary>
    /// 以外部驱动模式启动，每帧读取 provider 的返回值驱动显示。
    /// 兼容旧用法：传 ()=>TaskManager.Instance.nowTask.Countdown。
    /// </summary>
    public void StartDown(Func<int> provider,CountDownTypeEnum type)
    {
        _valueProvider = provider;
        var info = infos.Find(item => item.Key == type).Value;
        SetColor(bg, info.bgColor);
        SetColor(title, info.titleColor);
        SetColor(iconFrame, info.iconColor);
        SetSprite(icon, info.icon);

        SetWndState(true);
    }

    /// <summary>立即更新一次显示(由调用方驱动时手动刷新)</summary>
    public void SetValue(int value)
    {
        countDown = value;
        ApplyValue(value);
    }

    /// <summary>停止倒计时并隐藏动画区域(不关闭窗口)</summary>
    public void Stop()
    {
        _valueProvider = null;
        SetActive(anim.transform, false);
        var rect = transform.RectTransform();
        rect.sizeDelta = new(rect.rect.width, 0);
    }

    // Update is called once per frame
    private void Update()
    {
        if (_valueProvider == null) return;

        int nowcd = _valueProvider();
        // 只在数值发生变化时刷新,避免逐帧冗余操作
        if (nowcd != countDown)
        {
            if (countDown == 0)
            {
                SetWndState(false);
            }
            else {
                countDown = nowcd;
                ApplyValue(nowcd);
            }

        }
    }

    /// <summary>
    /// 统一根据当前剩余秒数刷新动画显隐、文本与音效。
    /// </summary>
    private void ApplyValue(int nowcd)
    {
        bool show = nowcd < activeBelow;
        if (show != GetActive(anim.transform))
        {
            SetActive(anim.transform, show);
            var rect = transform.RectTransform();
            rect.sizeDelta = new(rect.rect.width, show ? height : 0);
        }

        if (show)
        {
            anim.Play(animStateName, 0, 0);
            SetText(txt, string.Format(timeFormat, nowcd));
            if (warning) AudioSvc.PlaySound(new(warning, AudioGroups.UI));
            OnValueChanged?.Invoke(nowcd);
        }
    }
}
