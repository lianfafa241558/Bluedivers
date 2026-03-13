using Core;
using Unity.BaseTool;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class WndManager : WndManagerBase<WndManager>
{
    public FrontWnd frontWnd;
    public LoadWnd loadWnd;
    public PlayerWnd playerWnd;
    public MissionWnd missionWnd;
    public JetpackWnd jetpackWnd;
    public AirdropWnd airdropWnd;
    public SelectRoleWnd selectRoleWnd;
    public OperationWnd operationWnd;
    public SelectMapWnd selectMapWnd;
    public TipWnd tipWnd;
    public NoticeWnd noticeWnd;
    public BridgeWnd bridgeWnd;
    public CountDownWnd countDownWnd;
    public SubtitleWnd subtitleWnd;
    public HpWnd hpWnd;
    public SettingWnd settingWnd;
    public MovieWnd movieWnd;
    public ArmamentWnd armamentWnd;

    [SerializeField]
    private Transform GameUI,VecticalWnds;
    public Transform WndUI;//只有设置界面用
    public Sprite empty;

    protected override void Start()
    {
        base.Start();
        SetActive(GameUI);
        SetActive(WndUI);
        SetActive(VecticalWnds);
        //foreach (var wnd in Tool.GetComponentsInChildren<Wnd>(transform, 2, false)) { wnd.Init(); }
        foreach (var wnd in TransformUtils.GetComponentsInChildren<WindowRoot>(transform, 2, false)) { wnd.Init(); wnd.gameObject.SetActive(false); }

        GameRoot.OnGameStateChange += OnGameStateChange;
        GameRoot.OnWindowStateChange += OnWindowStateChange;
        GlobalEventManager.OnSettingCange += OnSettingCange;

        if (!GameRoot.Instance.IsLocal)
        {
            frontWnd.SetWndState(true);
        }
        else
        {
            GameRoot.WindowState = WindowStateEnum.Game;
            GameRoot.GameState = GameStateEnum.Game;
        }
    }
    void OnDestroy()
    {
        foreach (var wnd in transform.GetComponentsInChildren<Wnd>()) wnd.UnInit();

        GameRoot.OnGameStateChange -= OnGameStateChange;
        GameRoot.OnWindowStateChange -= OnWindowStateChange;
        GlobalEventManager.OnSettingCange -= OnSettingCange;

    }

    private void OnGameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        if (exit == entry) return;
        switch (exit)
        {
            case GameStateEnum.Bridge:

                break;
            case GameStateEnum.Ready:
                countDownWnd.SetWndState(false);
                bridgeWnd.SetWndState(false);
                playerWnd.SetWndState(false);
                subtitleWnd.SetWndState(false);
                operationWnd.SetWndState(false);
                break;
            case GameStateEnum.Armament:
                armamentWnd.SetWndState(false);
                break;
            case GameStateEnum.Transition:
                movieWnd.SetWndState(false);
                break;
            case GameStateEnum.Load:
                
                break;
            case GameStateEnum.Game:
                playerWnd.SetWndState(false);
                jetpackWnd.SetWndState(false);
                airdropWnd.SetWndState(false);
                operationWnd.SetWndState(false);
                hpWnd.SetWndState(false);
                missionWnd.SetWndState(false);
                break;
            case GameStateEnum.GameEnd:

                break;
        }
        switch (entry)
        {
            case GameStateEnum.Bridge:
                playerWnd.SetWndState(true);
                jetpackWnd.SetWndState(true);
                operationWnd.SetWndState(true);
                bridgeWnd.SetWndState(true);
                subtitleWnd.SetWndState(true);
                break;
            case GameStateEnum.Ready:
                countDownWnd.SetWndState(true);
                CreatNotice("Yuuka2", "Ready");
                break;
            case GameStateEnum.Armament:
                armamentWnd.SetWndState(true);
                break;
            case GameStateEnum.Transition:
                movieWnd.SetWndState(true);
                break;
            case GameStateEnum.Load:

                break;
            case GameStateEnum.Game:
                playerWnd.SetWndState(true);
                jetpackWnd.SetWndState(true);
                airdropWnd.SetWndState(true);
                operationWnd.SetWndState(true);
                subtitleWnd.SetWndState(true);
                hpWnd.SetWndState(true);
                missionWnd.SetWndState(true);
                CreatNotice("Yuuka2", "MissionStart", delay: 3);
                /*
                if (GameRoot.Instance.IsLocal)
                {
                    AudioManager.PlayMusic(AudioManager.MusicGroup.Game, 0.2f);
                }*/
                
                break;
            case GameStateEnum.GameEnd:

                break;
        }
    }

    LoginTimer contAlpha;
    private void OnWindowStateChange(WindowStateEnum oldState, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (oldState != WindowStateEnum.Airdrop)
                {
                    SetActive(GameUI, true);
                    GameUI.GetComponent<CanvasGroup>().alpha = 0;
                    if (contAlpha.IsValid()) contAlpha.Stop();
                    contAlpha = GameRoot.CreateTimer((count) => GameUI.GetComponent<CanvasGroup>().alpha = 0.02f * count, 0.02f, 50);
                }
                break;
            case WindowStateEnum.UI:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                SetActive(GameUI, false);
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
            GetComponent<UnityEngine.UI.CanvasScaler>().scaleFactor = value / 100f;
        }
    }

    public void CreatTip(TipWndInfo info)
    {
        tipWnd.Creat(info);
    }
    public void CreatNotice(NoticeData_SO data, System.Func<bool> func = default,float delay=0)
    {
        if (delay==0) noticeWnd.Creat(data, func);
        else GameRoot.CreateTimer(() => { noticeWnd.Creat(data, func); }, delay);

    }
    public void CreatNotice(string role, string type, System.Func<bool> func = default,float delay=0,float vaildTime=-1)
    {
        if (delay == 0) noticeWnd.Creat(ResManager.Instance.GetVoice(role,type), func);
        else GameRoot.CreateTimer(() => { noticeWnd.Creat(ResManager.Instance.GetVoice(role, type), func, vaildTime); }, delay);
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
        AudioManager.PlaySound(info);
    }
}



