using Core;

using UnityEngine;
using static WndTools.WndRootTool;

public class LoadWnd : Window
{
    public Sprite[] bgs;
    public Transform BG;
    public Animator anim;

    private float time;
    [SerializeField]
    private bool loadFinal;

    protected override void FirstShowWnd()
    {
        //GlobalEventSub.OnGameStateChange += GameStateChange;
    }


    protected override void ShowWnd()
    {
        SetSprite(BG,bgs.RandomTake());
        //GlobalEventManager.OnFakeBg(BG);
        time = 2;//起码保持2??
        loadFinal = false;
        anim.Play("BlackEntry", 0, 0);
    }
    protected override void HideWnd()
    {

    }

    private void Update()
    {
        if ((time -= Time.deltaTime) < 0 && !loadFinal && resManager.AsyncLoadSceneProgress()>= 100)
        {
            anim.Play("Exit", 0, 0);
            loadFinal = true;
            
            AudioSvc.StopMusic();
        }
    }

    /// <summary>
    /// 动画事件
    /// </summary>
    private void HideBg()
    {
        //WndManager.WindowState = WindowStateEnum.Game;
        resManager.AsyncContinueLoadScene();
        //GlobalEventManager.OnFakeBg(null);
    }
    /*
    public void Entry(bool isWhite)
    {
        
        SetWndState(true);
        anim.Play(isWhite? "WhiteEntry": "BlackEntry", 0, 0);
    }


    private void GameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        if(entry == GameStateEnum.Load) Entry(false);
        else if(exit == GameStateEnum.Load) SetWndState(false);
    }
    */

}
