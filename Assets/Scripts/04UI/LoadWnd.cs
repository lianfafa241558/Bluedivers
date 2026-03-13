using Core;
using Unity.BaseTool;
using UnityEngine;
using static WndTools.WndRootTool;

public class LoadWnd : WindowRoot
{
    public Sprite[] bgs;
    public Transform BG;
    public Animator anim;

    private float time;
    [SerializeField]
    private bool loadFinal;
    public override void Init()
    {
    }
    public override void UnInit()
    {
    }
    protected override void FirstShowWnd()
    {

    }


    protected override void ShowWnd()
    {
        SetSprite(BG,bgs.RandomTake());
        GlobalEventManager.OnFakeBg(BG);
        time = 2;//起码保持2秒
        loadFinal = false;
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
            AudioManager.StopMusic();
        }
    }
    public void Entry(bool isWhite)
    {
        GameRoot.GameState = GameStateEnum.Load;
        SetWndState(true);
        anim.Play(isWhite? "WhiteEntry": "BlackEntry", 0, 0);
    }

    private void HideBg()
    {
        //GameRoot.WindowState = WindowStateEnum.Game;
        resManager.AsyncContinueLoadScene();
        //GlobalEventManager.OnFakeBg(null);
    }


}
