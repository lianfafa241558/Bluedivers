using System.Collections.Generic;
using UnityEngine;
using AirdropState = AirdropController.AirdropState;
using static WndTools.WndRootTool;
using Core;
using Utils;
using Unity.FPS.Game;

public class AirdropWnd : Window
{
    [SerializeField]
    Transform expandRoot,showRoot;
    [SerializeField]
    Transform powText;

    [SerializeField]
    Animator anim;
    [SerializeField]
    Sprite giftSprite,normalSprite;

    List<CanvasGroup> airdropList;
    List<CanvasGroup> showList;
    [SerializeField]
    bool expand;
    AirdropController controller;
    List<DirectionEnum> opterlist;
    float time;


    public void Init()
    {

        gameObject.SetActive(false);
        SetWndState(true);
    }

    protected override void FirstShowWnd()
    {

        AnimSetExpandWnd(0);
        airdropList = new();
        showList = new();
        for (int i = 0; i < expandRoot.childCount; ++i)
        {
            airdropList.Add(expandRoot.GetChild(i).GetComponent<CanvasGroup>());
        }
        for (int i = 0; i < showRoot.childCount; ++i)
        {
            showList.Add(showRoot.GetChild(i).GetComponent<CanvasGroup>());
            
        }
        ResetAirdrop(BattleManager.Instance.ADCont);
    }


    protected override void ShowWnd()
    {
        GlobalEventSub.OnGameStateChange += GameStateChange;
        WndManager.OnWindowStateChange += WindowStateChange;
        BattleEventSub.OnInputAirdrop += OnInput;
        BattleEventSub.OnAuthorizeAirdrop += OnAuthorizeAirdrop;
    }
    protected override void HideWnd()
    {
        GlobalEventSub.OnGameStateChange -= GameStateChange;
        WndManager.OnWindowStateChange -= WindowStateChange;
        BattleEventSub.OnInputAirdrop -= OnInput;
        BattleEventSub.OnAuthorizeAirdrop -= OnAuthorizeAirdrop;
    }
    private void ResetAirdrop(AirdropController cont)
    {
        controller = cont;
        if (cont)
        {
            for (int i = 0; i < controller.useAd.Count; ++i)
            {
                var ad = controller.useAd[i];
                // IsVisible: 有授权或 unAuthorizeVisible 则显示，否则隐藏
                SetActive(airdropList[i].transform, ad.IsVisible);
                SetActive(showList[i].transform, ad.IsVisible);

                SetSprite(airdropList[i].GetChild(0), ad.isGift? giftSprite: normalSprite);
                SetColor(airdropList[i].GetChild(0), ad.cfg.Color);
                SetSprite(airdropList[i].GetChild(0, 0), ad.cfg.icon);
                SetColor(airdropList[i].GetChild(0, 0), ad.cfg.IconColor);
                SetSizeDelta(airdropList[i].GetChild(0, 1),0,0);
                SetActive(airdropList[i].GetChild(0, 2), ad.count > 0);
                SetText(airdropList[i].GetChild(0,2,0), ad.count);
                SetText(airdropList[i].GetChild(1), ad.cfg.showName);
                ResetItemText(i);

                SetAlpha(showList[i], 0);
                SetColor(showList[i].GetChild(0), ad.cfg.Color);
                SetSprite(showList[i].GetChild(0, 0), ad.cfg.icon);
                SetColor(showList[i].GetChild(0, 0), ad.cfg.IconColor);
                SetText(showList[i].GetChild(1), ad.cfg.showName);

            }
            for (int i = controller.useAd.Count; i < airdropList.Count; ++i)
            {
                SetActive(airdropList[i].transform,false);
                showList[i].alpha = 0;
            }
        }
    }


    private void GameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        bool state = false;
        switch (entry)
        {
            case GameStateEnum.Bridge:
                state = false;
                break;
            case GameStateEnum.Ready:
                state = false;
                break;
            case GameStateEnum.Transition:
                state = false;
                break;
            case GameStateEnum.Load:
                state = false;
                break;
            case GameStateEnum.Game:
                state = true;
                break;
            case GameStateEnum.GameEnd:
                state = false;
                break;
        }

        SetWndState(state);
    }
    private void WindowStateChange(WindowStateEnum oldSstate, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                ExpandMenu(false);
                break;
            case WindowStateEnum.UI:
                ExpandMenu(false);
                break;
            case WindowStateEnum.Airdrop:
                ExpandMenu(true);
                break;
        }
    }
    private void ExpandMenu(bool state)
    {
        if (gameObject.activeInHierarchy)
        {
            anim.Play(state ? "Entry": "Exit");
            if (controller&& state)
            {
                for (int i = 0; i < controller.useAd.Count; ++i)
                {
                    var ad = controller.useAd[i];
                    if (!ad.IsVisible) continue; // 不显示的跳过

                    showList[i].alpha = 0;
                    if (!ad.IsCurrentlyAvailable(ActorsManager.Player))
                    {
                        SetAlpha(airdropList[i].GetChild(1),0.35f);
                        airdropList[i].GetComponent<CanvasGroup>().alpha = 0.25f;
                    }
                    else if (ad.State != AirdropState.Ready)
                    {
                        SetAlpha(airdropList[i].GetChild(1),0.35f);
                        airdropList[i].GetComponent<CanvasGroup>().alpha = 1;
                    }
                    else
                    {
                        airdropList[i].GetComponent<CanvasGroup>().alpha = 1;
                        ResetItemText(i);
                    }
                }
            }
        }
        expand = state;
    }

    void OnAuthorizeAirdrop()
    {
        for (int i = 0; i < controller.useAd.Count; ++i)
        {
            var ad = controller.useAd[i];
            SetActive(airdropList[i].transform, ad.IsVisible);
            SetActive(showList[i].transform, ad.IsVisible);
            if (!ad.IsCurrentlyAvailable(ActorsManager.Player))
            {
                airdropList[i].GetComponent<CanvasGroup>().alpha = 0.25f;
                SetAlpha(airdropList[i].GetChild(1), 0.35f);
            }
        }
    }


    private void OnInput(List<DirectionEnum> list)
    {
        opterlist = list;
        for (int i = 0; i < controller.useAd.Count; ++i)
        {
            CheckInput(i);
        }
    }
    void CheckInput(int index)
    {
        var ad = controller.useAd[index];
        if (!ad.IsVisible) return; // 不显示的跳过

        if (!ad.IsCurrentlyAvailable(ActorsManager.Player))
        {
            airdropList[index].GetComponent<CanvasGroup>().alpha = 0.25f;
            ResetItemText(index);
        }
        else if (opterlist == null || opterlist.Count == 0)
        {
            airdropList[index].GetComponent<CanvasGroup>().alpha = 1;
            ResetItemText(index);
        }
        else if (ad.State != AirdropState.Ready)//不会考虑冷却中的
        {

        }
        else if (ad.cfg.opter.Compare(opterlist))
        {
            airdropList[index].GetComponent<CanvasGroup>().alpha = 1;//符合条件的显示
            SetText(airdropList[index].GetChild(2), ad.cfg.opter.OpterColorString(opterlist.Count, new(1, 1, 1, 0.15f), new(1, 1, 1, 1), new(1, 1, 1, 0.55f)));
        }
        else if (airdropList[index].GetComponent<CanvasGroup>().alpha > 0.25f)
        {
            airdropList[index].GetComponent<CanvasGroup>().alpha = 0.25f;//不符合条件的直接虚化
            ResetItemText(index);
        }
    }

    void UpdateShow()
    {
        for (int i = 0; i < controller.useAd.Count; ++i)
        {
            var item = controller.useAd[i];
            if (!item.IsVisible) continue; // 不显示的跳过

            SetSizeDelta(showList[i].GetChild(0, 1), 0, 32 * item.TimeScale);
            SetText(airdropList[i].GetChild(0, 2, 0), item.count);

            if (!item.IsCurrentlyAvailable(ActorsManager.Player))
            {
                // 当前不可用时渐出
                if (showList[i].alpha > 0)
                    showList[i].alpha = Mathf.Lerp(showList[i].alpha, -0.1f, Time.deltaTime * 3);
                continue;
            }

            if (showList[i].alpha < 1 && (
                item.State == AirdropState.Wait
                || item.State == AirdropState.Arrive 
                || item.State == AirdropState.Sustain
                ||(item.State == AirdropState.Cool && Tool.In(item.time, 0, 3))//快冷却完
                )
                //|| (item.State == AirdropState.Cool && Tool.In(item.cool - item.time, 0, 3)))
            ){
                showList[i].alpha = Mathf.Lerp(showList[i].alpha, 1.1f, Time.deltaTime * 3);//渐入

            }
            else if (showList[i].alpha > 0 && (
                item.State == AirdropState.Ready
                || (item.State == AirdropState.Cool && Tool.In(item.cool - item.time, 0, 1)))
            ){
                showList[i].alpha = Mathf.Lerp(showList[i].alpha, -0.1f, Time.deltaTime * 3);//渐出
            }

            if (showList[i].alpha >= 0.1f)
            {
                string re = default;
                switch (item.State)
                {
                    case AirdropState.Ready:
                        re = "就绪";
                        break;
                    case AirdropState.Cool:
                        re = "正在冷却 " + Tool.FloatToTime(item.time);
                        break;
                    case AirdropState.Wait:
                        re = "正在启动";
                        break;
                    case AirdropState.Arrive:
                        re = "即将抵达 " + Tool.FloatToTime(item.time);
                        break;
                    case AirdropState.Sustain:
                        re = "正在进行 " + Tool.FloatToTime(item.time);
                        break;
                    case AirdropState.Unavailable:
                        re = "不可用";
                        break;
                }
                if(!string.IsNullOrEmpty(re))SetText(showList[i].transform.GetChild(2), re);
            }
        }
    }
    void UpdateExpand()
    {
        for (int i = 0; i < controller.useAd.Count; ++i)
        {
            var item = controller.useAd[i];
            if (!item.IsVisible) continue; // 不显示的跳过

            SetSizeDelta(airdropList[i].GetChild(0, 1), 0, 32 * item.TimeScale);

            if (!item.IsCurrentlyAvailable(ActorsManager.Player))
            {
                SetText(airdropList[i].transform.GetChild(2), "不可用");
                if (GetAlpha(airdropList[i].GetChild(1)) > 0.35f)
                {
                    SetAlpha(airdropList[i].GetChild(1), 0.35f);
                }
            }
            else
            {
                string re = default;
                switch (item.State)
                {
                    case AirdropState.Cool:
                        re = "正在冷却 " + Tool.FloatToTime(item.time);
                        break;
                    case AirdropState.Wait:
                        re = "正在启动";
                        break;
                    case AirdropState.Arrive:
                        re = "即将抵达 " + Tool.FloatToTime(item.time);
                        break;
                    case AirdropState.Sustain:
                        re = "正在进行 " + Tool.FloatToTime(item.time);
                        break;
                    case AirdropState.Unavailable:
                        re = "不可用";
                        break;
                }
                if (!string.IsNullOrEmpty(re)) SetText(airdropList[i].transform.GetChild(2), re);

                if (item.State == AirdropState.Ready && GetAlpha(airdropList[i].GetChild(1)) <= 0.35f)
                {
                    SetAlpha(airdropList[i].GetChild(1), 1);
                    //尝试校验
                    CheckInput(i);
                }
            }
        }
    }


    void ResetItemText(int index)
    {
        SetText(airdropList[index].GetChild(2), controller.useAd[index].cfg.opter.OpterTMPString());
    }


    private void Update()
    {
        //if(controller!= BattleManager.Instance.ADCont)ResetAirdrop(BattleManager.Instance.ADCont);
        if (!controller) return;

        if ((time += Time.deltaTime) > 1)
        {
            time -= 1;
            SetText(powText, controller.useAd.FindAll(item => item.State == AirdropState.Ready).Count);
            if (!expand) {
                bool have1 = false, have2 = false;
                for (int i = 0; i < controller.useAd.Count; ++i)
                {
                    var item = controller.useAd[i];
                    if (item.State == AirdropState.Cool)
                    {
                        if (Tool.In(item.time, 1, 3))
                        {
                            have1 = true;
                        }
                        else if (Tool.In(item.time, 0, 1))
                        {
                            have2 = true;
                            break;
                        }

                    }
                }
                if (have2) AudioSvc.PlaySound(new("UI/UI_ElementsA", AudioGroups.General, 0.4f));
                else if (have1) AudioSvc.PlaySound(new("UI/UI_CountDown2", AudioGroups.General, 0.4f));
            }
        }
        
        if (!expand)
        {
            UpdateShow();
        }
        else
        {
            UpdateExpand();
        }
    }
    private void AnimSetExpandWnd(int state)
    {
        SetActive(expandRoot, state > 0);
    }


}
