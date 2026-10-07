using System.IO;
using System.Runtime.CompilerServices;
using FPSGame.Core;
using UnityEngine;
using UnityEngine.Video;

namespace FPSGame.UI
{
using FPSGame.Game;
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
using FPSGame.GameData;

/// <summary>
/// 开场 CG 窗口。
/// </summary>
[AddComponentMenu("UI/窗口/开场")]
public class FrontWnd : Window
{
    [SerializeField]
    Transform Button;
    [SerializeField]
    VideoPlayer videoPlayer;
    bool IsLoad = false;
    float startTime = 0;
    protected void Start()
    {
        // 组合得到视频的完整路径
        string videoPath = Path.Combine(Application.streamingAssetsPath, "StartCG.mp4");
        videoPlayer.url = videoPath;
        videoPlayer.Play();
        startTime = Time.time;
    }

    protected override void FirstShowWnd()
    {
        SetCilck(Button,()=>{
            Load();
            //Debug.LogError("开始");
        });

    }

    protected override void ShowWnd()
    {
        WindowState = WindowStateEnum.UI;
        //GlobalEventManager.OnFakeBg(BG);
        PlayAnim("Idle");

    }
    protected override void HideWnd()
    {
        //GlobalEventManager.OnFakeBg(null);
        /*
        GameState = GameStateEnum.Load;
        ResSvc.Instance.AsyncLoadScene("Utnapishitim", () => {
            //Debug.LogError("加载front完成");
            //GameRoot.CreateTimer(()=> GameRoot.GameState = GameStateEnum.Bridge,4);
            GameState = GameStateEnum.Bridge;
            WindowState = WindowStateEnum.Game;
            //AudioManager.PlaySound(new("BG_Shining_L"));
            //GlobalEventManager.OnFakeBg(null);
        },true);*/
    }
    public override void OnDestroy()
    {
        //继承就会导致被移除时再加载一次？？
    }

    private void Update()
    {
        if (Input.anyKeyDown 
            && !Input.GetMouseButtonDown(0)
            && !Input.GetMouseButtonDown(1)
            && !Input.GetMouseButtonDown(2)
        ){
            Load();
        }
    }

    private void Load()
    {
        //PlayAnim("Exit");
        if (IsLoad) return;
        IsLoad = true;
        if (ArchivesData_SO.Current.isNew
#if UNITY_EDITOR
            ||Input.GetKey(KeyCode.U)
#endif
        )
        {
            // 首次进入（新建存档）：先让玩家起名，再进新手教学
            AskPlayerName();
        }
        else
        {
            ResSvc.Instance.AsyncLoadScene("Utnapishitim", () => {
                GameState = GameStateEnum.Bridge;
                WindowState = WindowStateEnum.Game;
            });
        }

    }

    /// <summary>
    /// 首次进入时用可复用的 <see cref="PasswordWnd"/> 收玩家名，写进存档再进教学关。
    /// ⚠ 输入窗没加载（场景缺 PasswordWnd）时**不能卡住流程**，直接按原名进教学。
    /// </summary>
    private void AskPlayerName()
    {
        var wnd = WndHub.Password;
        if (wnd == null)
        {
            Debug.LogWarning("[FrontWnd] 场景里没有 PasswordWnd，跳过起名直接进教学关");
            EnterTeach();
            return;
        }

        wnd.Creat(new PasswordWndInfo
        {
            title = "起个名字",
            desc = "以后就用这个名字联机（随时可在设置里改）",
            placeholder = "输入玩家名",
            presetText = ArchivesData_SO.Current.playerName,
            maxLength = 12,
            onConfirm = name =>
            {
                var archive = ArchivesData_SO.Current;
                archive.playerName = name;
                archive.Save();
                EnterTeach();
            },
            onCancel = EnterTeach,
        });
    }

    private void EnterTeach()
    {
        ResSvc.Instance.AsyncLoadScene("Teach", () => {
            BattleManager.Creat(false);
        });
    }


}
}
