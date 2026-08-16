using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;
/// <summary>
/// 战备配置界面
/// armamentRoot下的0-3是每个玩家的界面根
/// armamentRoot.GetChild(i, 2)下是全部战备的列表
/// armamentRoot.GetChild(i, 3)下是玩家信息(等级，名称等)
/// armamentRoot.GetChild(i, 4)下是准备按钮
/// armamentRoot.GetChild(i, 5)下是已选择的战备按钮，0-3是战备，4是全队强化(还没弄好)
/// armamentRoot.GetChild(i, 6)下是任务赠送的额外战备
/// armamentRoot.GetChild(i, 7)是选择第几个战备显示的框体
/// armamentRoot.GetChild(i, 8)下是全部强化的列表
/// </summary>
public class ArmamentWnd : Window
{
    public Transform mapName,enemyIcon, enemyName;

    public GameObject prefab,buttonPrefab,boosterButtonPrefab;
    public Transform armamentRoot;

    public Transform tipRoot,frame;

    private bool[] ready;
    private Animator[] animators;
    /// <summary> 当前选择配置的战备位置</summary>
    private int selectAirdropIndex=-1;
    /// <summary> 全队强化面板是否展开</summary>
    private bool boosterExpanded;

    private Transform showSelect;
    private Dictionary<Transform, int> buttons;
    /// <summary> 全队强化 id -> 对应按钮（用于禁止重复选择时置灰）</summary>
    private Dictionary<int, Transform> boosterButtons;

    public void Init()
    {
        BridgeSys.Instance.armament = this;
    }

    protected override void FirstShowWnd()
    {
        int count = Constants.MaxPlayer;
        ready = new bool[count];
        animators = new Animator[count];
        buttons = new();
        boosterButtons = new();
        for (int i=0;i< count; ++i)
        {
            var go = Instantiate(prefab, armamentRoot).transform;
            var a = i;
            if (i == 0)//自己在显示上面固定第一个
            {
                showSelect = go.Find("ShowSelect");
                //装备
                SetCilck(go.GetChild(4), () => {
                    SendPlayerReady(roomManager.SelfIndex, !ready[a]);
                    wndManager.PlaySound(new("Bridge/"+(!ready[a]? "Ready" : "CancelReady")));
                });
                for (int u = 0; u < 4; ++u)
                {
                    var b = u;
                    //战备
                    SetCilck(go.GetChild(5, u), () => {
                        if (selectAirdropIndex == -1)
                        {
                            go.GetComponent<Animator>().SetBool("Airdrop",true);
                            wndManager.PlaySound(new("Bridge/Expand"));
                            InputManager.AddListenerCancel(Cancel);
                        }
                        SetSelectIndex(b, true);
                    });
                }
                //全队强化槽位（index 4）
                SetCilck(go.GetChild(5, 4), () => {
                    ToggleBooster();
                });
            }

            for (int u = 0,y=0; u < 7; ++u)
            {
                if (u<taskManager.nowTask.RequiredAD.Count)
                {
                    var item = ResSvc.airdropDic[taskManager.nowTask.RequiredAD[u]];
                    if (!item.isHide)
                    {
                        //正常的，因为跑了4个玩家
                        //Debug.LogError("??+u+"??+ taskManager.nowTask.RequiredAD[u]);
                        buttons[armamentRoot.GetChild(i, 6, y)] = taskManager.nowTask.RequiredAD[u];
                        SetButton(armamentRoot.GetChild(i, 6, y), tipRoot,false, ShowTip);
                        SetSprite(armamentRoot.GetChild(i, 6, y, 0, 0), item.icon);
                        SetColor(armamentRoot.GetChild(i, 6, y, 0, 0), item.IconColor);
                        SetActive(armamentRoot.GetChild(i, 6, y, 0), true);
                        ++y;
                    }
                    else
                    {
                        SetActive(armamentRoot.GetChild(i, 6, u, 0), false);
                    }

                }
                else{
                    SetActive(armamentRoot.GetChild(i, 6, u,0),false);
                }
            }

            for (int u = 0; u < 5; ++u)
            {
                SetButton(armamentRoot.GetChild(i, 5, u), tipRoot,u==4, ShowTip);
            }

        }

        InitPlayerAirdrop();
        InitBoosterButtons();
    }


    private void SetSelectIndex(int index,bool showFrame)
    {
        selectAirdropIndex = index;
  
        if (showFrame)
        {
            if (index >-1)
            {
                showSelect.position = armamentRoot.GetChild(0, 5, index).position;
                showSelect.SetParent(armamentRoot.GetChild(0, 5, index));
                showSelect.localScale = Vector3.one;
                SetActive(showSelect, true);
            }
            else
            {
                SetActive(showSelect, false);
            }
        }
    }

    protected override void ShowWnd()
    {
        ready.Clear();
        animators.Clear();
        SetSelectIndex(-1, true);

        WindowState = WindowStateEnum.UI;
        GetComponent<Animator>().Play("Entry");
        SetColor(enemyIcon, taskManager.nowTask.campData.Color);
        SetSprite(enemyIcon, taskManager.nowTask.campData.Sprite);
        SetText(mapName, taskManager.nowTask.mapName);
        SetText(enemyName, "" + taskManager.nowTask.campData.ShowName + "控制");
        for (int i = 0; i < armamentRoot.childCount; ++i)
        {
            var item = armamentRoot.GetChild(i);
            if (i < roomManager.players.Count)
            {
                roomManager.players[i].airdrop=new int[4];
                SetActive(item, true);
                //重置全队强化展开状态与动画
                item.GetComponent<Animator>().SetBool("Booster",false);
                boosterExpanded = false;
                var showModle = resManager.CreatPrefab("Prefabs/StudentModle/" + roomManager.players[i].roleName, false);
                showModle.transform.parent = item.GetChild(1);
                showModle.transform.localPosition = new(0, -2.4f, 980);
                showModle.transform.eulerAngles = new(0, 180, 0);
                showModle.transform.localScale = new(2, 2, 2);
                showModle.SetChildLayer(gameObject.layer, 3);
                var scripts = showModle.GetComponents<MonoBehaviour>();
                foreach (var script in scripts)//关闭注视等组件
                {
                    script.enabled = false;
                }
                animators[i] = showModle.GetComponent<Animator>();

                SetText(item.GetChild(3, 0, 0), animators[i].GetComponent<BaseObject>().ShowName);
                SetText(item.GetChild(3, 0, 1), roomManager.players[i].roleLevel);
                SetSprite(item.GetChild(3, 1, 1), animators[i].GetComponent<BaseObject>().Portrait);
                for(int u = 0; u < 4; ++u)
                {
                    SetSprite(item.GetChild(5, u, 0),wndManager.empty);
                }
                //全队强化槽位：恢复为未选择
                roomManager.players[i].boosterId = 0;
                SetSprite(item.GetChild(5, 4, 0), wndManager.empty);
                SetColor(item.GetChild(5, 4), Color.white);
            }
            else
            {
                SetActive(item, false);
            }

        }

    }
    /// <summary>
    /// 显示可选战备
    /// </summary>
    private void InitPlayerAirdrop()
    {

        var layout = armamentRoot.GetChild(roomManager.Self.index, 2, 0, 0, 0);
        var airdropList = ResSvc.airdropDic.Values.OrderBy(item => item.ID).ToList();
        for (int i = 1; i <= 7; i += 2)
        {
            var root = layout.GetChild(i);
            var type = (AirdropData_SO.AirdropType)(i / 2);
            var list = airdropList.FindAll(item=>item.type== type&& !item.isHide);
            for (int u = 0; u < list.Count; ++u)
            {
                var button=Instantiate(buttonPrefab, root).transform;
                SetSprite(button.GetChild(0),list[u].icon);
                SetColor(button.GetChild(0), list[u].IconColor);
                SetColor(button, list[u].Color);
                buttons.Add(button, list[u].ID);

                SetButton(button,tipRoot,false,ShowTip);
                SetCilck(button,() => SelectAirdrop(button));
            }
        }
    }
    private void UninitAirdrop()
    {
        var layout = armamentRoot.GetChild(0, 2, 0, 0, 0);
        for (int i = 1; i <= 7; i += 2)
        {
            var item = layout.GetChild(i);
            for (int u = item.childCount - 1; u >= 0; --u)
            {
                Tool.Destroy(item.GetChild(u).gameObject);
            }
        }
    }
    /// <summary>
    /// 点击战备按钮
    /// </summary>
    private void SelectAirdrop(Transform button)
    {
        SendPlayerSelectAemament(roomManager.Self.index, ResSvc.airdropDic[buttons[button]].ID, selectAirdropIndex);
    }


    private bool Cancel()
    {
        if (boosterExpanded)
        {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            armamentRoot.GetChild(roomManager.Self.index).GetComponent<Animator>().SetBool("Booster",false);
            boosterExpanded = false;
            return true;
        }
        if (selectAirdropIndex==-1) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        armamentRoot.GetChild(roomManager.Self.index).GetComponent<Animator>().SetBool("Airdrop",false);
        SetSelectIndex(-1, true);
        //selectAirdropIndex = -1;
        //SetSelectIndex(-1,false);
        return true;
    }

    protected override void HideWnd()
    {
        UninitAirdrop();
        UninitBooster();
    }

    //通过动画调用
    private void FinishReady()
    {
        GameState = GameStateEnum.Transition;//会因为切状态自己关??


    }

    private void SetButton(Transform trans,Transform tip, bool isBooster, System.Action<Transform,Transform,bool> action)
    {
        var item = trans.TryGetOrAddComponent<ButtonEnterDetector>();
        item.Enter = (data) => {
            SetActive(tip, true);
            action.Invoke(trans, tip, isBooster);
        };

        item.In = (data) => {
            tip.position = UICamera.uiCamera.ScreenToWorldPoint(Input.mousePosition)+100*Vector3.forward;
            //tip.position =new(Input.mousePosition.x, Input.mousePosition.y,tip.position.z);
        };
        item.Exit = (data) => {
            SetActive(tip, false);
        };

    }
    private void ShowTip(Transform trans, Transform tip,bool isBooster)
    {
        if (buttons.TryGetValue(trans, out var id))
        {
            if (isBooster)
            {
                if (ResSvc.boostDic.TryGetValue(id, out var te))
                {
                    SetSprite(tip.GetChild(0, 0), te.icon);
                    SetColor(tip.GetChild(0, 0), te.color);
                    SetColor(tip.GetChild(0), te.color);
                    SetText(tip.GetChild(1), te.showName);
                    SetText(tip.GetChild(2), "全队强化");
                    SetText(tip.GetChild(3), te.desc);
                    SetText(tip.GetChild(4), "");
                    SetText(tip.GetChild(5), "");
                    SetText(tip.GetChild(6), "");
                    return;
                }
            }
            else
            {
                if (ResSvc.airdropDic.TryGetValue(id, out var data))
                {
                    SetSprite(tip.GetChild(0, 0), data.icon);
                    SetColor(tip.GetChild(0, 0), data.IconColor);
                    SetColor(tip.GetChild(0), data.Color);
                    SetText(tip.GetChild(1), data.showName);
                    SetText(tip.GetChild(2), data.TypeName);
                    SetText(tip.GetChild(3), data.desc);
                    SetText(tip.GetChild(4), data.AttrName);
                    SetText(tip.GetChild(5), data.AttrValue);
                    SetText(tip.GetChild(6), data.opter.OpterTMPString());
                    return;
                }

            }

        }

        SetActive(tip, false);
    }

    /// <summary> 发送玩家选择战备的消息</summary>
    private void SendPlayerSelectAemament(int playerIndex, int id, int index)
    {
        BridgeSys.Instance.SendPlayerSelectArmament(playerIndex, id, index);
    }

    /// <summary>
    /// 点击第5槽位（全队强化）时切换展开/收起面板
    /// </summary>
    private void ToggleBooster()
    {
        var selfGo = armamentRoot.GetChild(roomManager.Self.index);
        if (boosterExpanded)
        {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            selfGo.GetComponent<Animator>().SetBool("Booster", false);
            boosterExpanded = false;
        }
        else
        {
            wndManager.PlaySound(new("Bridge/Expand"));
            selfGo.GetComponent<Animator>().SetBool("Booster", true);
            InputManager.AddListenerCancel(Cancel);
            boosterExpanded = true;
        }
    }

    /// <summary>
    /// 界面打开时预创建全队强化按钮（GetChild(i,8,0,0,0,1) 即 RedList）
    /// 先清空避免重复打开叠加，随后按 ID 填充 buttonPrefab
    /// </summary>
    private void InitBoosterButtons()
    {
        var list = armamentRoot.GetChild(roomManager.Self.index, 8, 0, 0, 0, 1);
        boosterButtons.Clear();
        var boosterList = ResSvc.boostDic.Values.OrderBy(item => item.ID).ToList();
        foreach (var te in boosterList)
        {
            var button = Instantiate(boosterButtonPrefab, list).transform;
            SetSprite(button.GetChild(0), te.icon);
            buttons.Add(button, te.ID);
            boosterButtons.Add(te.ID, button);
            SetButton(button, tipRoot,true, ShowTip);
            SetCilck(button, () => SelectBooster(te.ID));
        }
        RefreshBoosterButtons();
    }

    /// <summary>
    /// 刷新自己面板里全队强化按钮的可用状态。
    /// 同一强化（id≠0）只允许一个玩家选择：已被其他玩家选中的强化按钮置灰禁用；
    /// 自己当前已选的强化按钮保持可用（允许反悔更换）。
    /// </summary>
    private void RefreshBoosterButtons()
    {
        if (boosterButtons == null) return;
        var selfIndex = roomManager.Self.index;
        // 统计其他玩家已选中的强化 id（忽略 0）
        var occupied = new HashSet<int>();
        for (int i = 0; i < roomManager.players.Count; ++i)
        {
            if (i == selfIndex) continue;
            int id = roomManager.players[i].boosterId;
            if (id > 0) occupied.Add(id);
        }
        int myBooster = roomManager.players[selfIndex].boosterId;
        foreach (var kv in boosterButtons)
        {
            // 自己已选的按钮保持可用；被其他玩家占用的才禁用
            bool interactable = kv.Key == myBooster || !occupied.Contains(kv.Key);
            SetButtonInteractable(kv.Value, interactable);
        }
    }
    private void UninitBooster()
    {
        /*
        var layout = armamentRoot.GetChild(roomManager.Self.index, 8, 0, 0, 0,1);
        for (int u = layout.childCount - 1; u >= 0; --u)
        {
            Tool.Destroy(layout.GetChild(u).gameObject);
        }*/
    }


    /// <summary> 点击某个强化按钮，选中后收起面板并广播 </summary>
    private void SelectBooster(int id)
    {
        SendPlayerSelectTeamEnhance(roomManager.Self.index, id);
        wndManager.PlaySound(new("Bridge/" + (id == 0 ? "CancelReady" : "Ready")));
        armamentRoot.GetChild(roomManager.Self.index).GetComponent<Animator>().SetBool("Booster", false);
        boosterExpanded = false;
    }

    /// <summary> 发送玩家选择全队强化的消息</summary>
    private void SendPlayerSelectTeamEnhance(int playerIndex, int id)
    {
        BridgeSys.Instance.SendPlayerSelectTeamEnhance(playerIndex, id);
    }

    /// <summary> 收到玩家选择全队强化的回调</summary>
    public void ReceivePlayerSelectTeamEnhance(int playerIndex, int id)
    {
        if (playerIndex >= roomManager.players.Count)
        {
            Debug.LogError("收到玩家选择全队强化回调错误，玩家"+ playerIndex);
            return;
        }
        roomManager.players[playerIndex].boosterId = id;
        var child = armamentRoot.GetChild(playerIndex, 5, 4);
        if (ResSvc.boostDic.TryGetValue(id, out var data))
        {
            SetSprite(child.GetChild(0), data.icon);
            SetColor(child.GetChild(0), data.color);
            SetColor(child, Color.white);
        }
        else
        {
            SetSprite(child.GetChild(0), wndManager.empty);
            SetColor(child, Color.white);
        }
        // 绑定提示显示
        buttons[child] = id;
        // 刷新自己面板强化按钮的可用状态（禁止重复选择）
        RefreshBoosterButtons();
    }

 
    /// <summary> 收到玩家选择战备的回调</summary>
    public void ReceivePlayerSelectAemament(int playerIndex, int id, int index)
    {
        if(playerIndex>= roomManager.players.Count)
        {
            Debug.LogError("收到玩家选择战备的回调错误，玩家"+ playerIndex);
            return;
        }
        roomManager.players[playerIndex].airdrop[index] = id;
        var child = armamentRoot.GetChild(playerIndex, 5, index);
        buttons[child] = id;
        SetSprite(child.GetChild(0),ResSvc.airdropDic[id].icon);
        SetColor(child.GetChild(0), ResSvc.airdropDic[id].IconColor);
        SetColor(child, ResSvc.airdropDic[id].Color);

        //SetButtonInteractable(button,false);//这个战备就不能重复选择
        //selectAirdropIndex = -1;
        if (playerIndex == roomManager.Self.index)
        {
            bool have = false;
            for (int i = 0; i < 4; ++i)
            {
                if (roomManager.players[playerIndex].airdrop[i] == 0)
                {
                    SetSelectIndex(i, true);
                    have = true;
                    break;
                }
            }
            if (!have)
            {
                Cancel();
            }
        }

    }


    /// <summary> 发送玩家准备的消息</summary>
    public void SendPlayerReady(int playerIndex, bool state)
    {
        BridgeSys.Instance.SendPlayerReady(playerIndex, state);
    }

    /// <summary> 收到玩家准备的回调</summary>
    public void ReceivePlayerReady(int playerIndex, bool state)
    {
        ready[playerIndex] = state;
        //SetAlpha(go.GetChild(4, 0), ready[a] ? 0.2f : 0.01f);
        SetText(armamentRoot.GetChild(playerIndex, 4, 1), state ? "就绪" : "尚未就绪");
        //Debug.LogError("让"+ armamentRoot.GetChild(playerIndex)+"播放"+(state ? "Ready" : "UnReady"), armamentRoot.GetChild(playerIndex));
        //armamentRoot.GetChild(playerIndex).GetComponent<Animator>().Play(state ? "Ready" : "UnReady", 1, 0);
        //Debug.LogError("让" + animators[playerIndex].gameObject + "Bool IsReady", animators[playerIndex].gameObject);
        animators[playerIndex].SetBool("IsReady",state);
        if (IEnumerableUtils.Sum(ready) == roomManager.players.Count)
        {
            GetComponent<Animator>().Play("Exit", 0, 0);
            //Debug.LogError("让" + gameObject + "播放 Exit", gameObject);
        }

    }


}
