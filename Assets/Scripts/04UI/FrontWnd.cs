using Core;
using UnityEngine;
using static WndTools.WndRootTool;

public class FrontWnd : WindowRoot
{
    public Transform BG;
    public Transform Button;

    public override void Init()
    {

    }
    public override void UnInit()
    {

    }
    protected override void FirstShowWnd()
    {
        SetCilck(Button,()=>{
            Load();
        });
    }

    protected override void ShowWnd()
    {
        GameRoot.WindowState = WindowStateEnum.UI;
        GlobalEventManager.OnFakeBg(BG);
        PlayAnim("Idle");
    }
    protected override void HideWnd()
    {
        GlobalEventManager.OnFakeBg(null);
        wndManager.loadWnd.Entry(false);
        ResManager.Instance.AsyncLoadScene("Utnapishitim", () => {
            //GameRoot.CreateTimer(()=> GameRoot.GameState = GameStateEnum.Bridge,4);
            GameRoot.GameState = GameStateEnum.Bridge;
            GameRoot.WindowState = WindowStateEnum.Game;
            //AudioManager.PlaySound(new("BG_Shining_L"));
            GlobalEventManager.OnFakeBg(null);
        },true);
    }
    private void Update()
    {
        if (Input.anyKeyDown && !Input.GetMouseButtonDown(0)&& !Input.GetMouseButtonDown(1)&&!Input.GetMouseButtonDown(2))
        {
            Load();
        }
    }

    private void Load()
    {
        PlayAnim("Exit");
    }


}
