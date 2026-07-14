using System.IO;
using Core;
using UnityEngine;
using UnityEngine.Video;
using static WndTools.WndRootTool;

public class FrontWnd : Window
{
    [SerializeField]
    Transform Button;
    [SerializeField]
    VideoPlayer videoPlayer;

    protected void Start()
    {
        // 组合得到视频的完整路径
        string videoPath = Path.Combine(Application.streamingAssetsPath, "StartCG.mp4");
        videoPlayer.url = videoPath;
        videoPlayer.Play();
    }

    protected override void FirstShowWnd()
    {
        SetCilck(Button,()=>{
            Load();
            Debug.LogError("开始");
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

        ResSvc.Instance.AsyncLoadScene("Utnapishitim", () => {
            GameState = GameStateEnum.Bridge;
            WindowState = WindowStateEnum.Game;
        });
    }


}
