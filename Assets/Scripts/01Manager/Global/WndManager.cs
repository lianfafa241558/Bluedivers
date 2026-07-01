using System.Collections.Generic;
using Core;

using UnityEngine;
using UnityEngine.Events;
using Utils;
using static NoticeWnd;
using static WndTools.WndRootTool;

public class WndManager : Singleton<WndManager>
{
    public static WindowStateEnum WindowState
    {
        get => Instance ? Instance.windowState : WindowStateEnum.Game;
        set
        {
            var oldState = Instance.windowState;
            if (oldState != value)
            {
                Instance.windowState = value;
                OnWindowStateChange?.Invoke(oldState, value);
            }
        }
    }
    public static event UnityAction<WindowStateEnum, WindowStateEnum> OnWindowStateChange;

    [InspectorName("界面状态")]
    [SerializeField]
    private WindowStateEnum windowState = WindowStateEnum.UI;

    public OperationWnd operationWnd;
    public TipWnd tipWnd;
    public NoticeWnd noticeWnd;

    //[HideInInspector]
    public SelectMapWnd selectMapWnd;
    //[HideInInspector]
    public SelectRoleWnd selectRoleWnd;

    public VehicleWnd vehicleWnd;

    public Sprite empty;

    public override void Awake()
    {
        base.Awake();

        OnWindowStateChange += OnWindowStateChangeHandler;
        GlobalEventSub.OnSettingCange += OnSettingCange;
    }

    protected void Start()
    {
        // 界面状态/游戏状态已迁移到 WndManager/GameStateManager
    }

    void OnDestroy()
    {
        OnWindowStateChange -= OnWindowStateChangeHandler;
        GlobalEventSub.OnSettingCange -= OnSettingCange;
    }

    private void OnWindowStateChangeHandler(WindowStateEnum oldState, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                break;
            case WindowStateEnum.UI:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

                break;
            case WindowStateEnum.Airdrop:

                break;
        }
    }

    private void OnSettingCange(string key, float value)
    {
        if (key == "显示模式")
        {
            switch ((int)value)
            {
                case 0: Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true); break;
                case 1: Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, false); break;
                case 2: Screen.SetResolution(1920, 1080, false); break;
            }
        }
        if (key == "UI缩放系数")
        {
            for(var i=0;i< transform.childCount;++i)
            {
                transform.GetChild(i).GetComponent<UnityEngine.UI.CanvasScaler>().scaleFactor = value / 100f;
            }
           
        }
    }

    public void CreatTip(TipWndInfo info)
    {
        tipWnd.Creat(info);
    }

    public void CreatNotice(string role, string type, System.Func<bool> func = default,float vaildTime=-1)
    {
        ResSvc.Instance.GetVoice(role, type,out var data,out var sourceName,out var portrait);
        var noticeData = new NoticeData() {
            data = data.Get(),
            sourceName = sourceName,
            portrait = portrait,
            func= func,
            allowWait=true,
            vaildTime = vaildTime
        };
        noticeWnd.Creat(noticeData);
    }

    public void ClearNotice()
    {
        noticeWnd.Clear();
    }
    
    //public void CreatSpeech(NoticeData_SO data, System.Func<bool> func = default)
    //{
    //    subtitleWnd.Creat(data, func);
    //}

    public void PlaySound(AudioPlayInfo info)
    {
        AudioSvc.PlaySound(info);
    }

    public void PlaySoundData(RuntimeSoundData group)
    {
        AudioSvc.PlaySound(group);
    }
}
