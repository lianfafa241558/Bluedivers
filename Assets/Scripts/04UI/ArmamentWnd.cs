using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using Photon.Pun;

using UnityEngine;
using Utils;
using static WndTools.WndRootTool;
public class ArmamentWnd : Window
{
    public Transform mapName,enemyIcon, enemyName;

    public GameObject prefab,buttonPrefab;
    public Transform armamentRoot;

    public Transform tipRoot,frame;

    private bool[] ready;
    private Animator[] animators;
    /// <summary> 当前选择配置的战备位置</summary>
    private int selectAirdropIndex=-1;

    private Transform showSelect;
    private Dictionary<Transform, int> buttons;

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
                            go.GetComponent<Animator>().Play("Expend", 0, 0);
                            wndManager.PlaySound(new("Bridge/Expand"));
                            InputManager.AddListenerCancel(Cancel);
                        }
                        SetSelectIndex(b, true);
                    });
                }
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
                        SetButton(armamentRoot.GetChild(i, 6, y), tipRoot, ShowTip);
                        SetSprite(armamentRoot.GetChild(i, 6, y, 0, 0), item.icon);
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

            for (int u = 0; u < 4; ++u)
            {
                SetButton(armamentRoot.GetChild(i, 5, u), tipRoot, ShowTip);
            }

        }


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
            }
            else
            {
                SetActive(item, false);
            }

        }
        InitPlayerAirdrop();
    }
    /// <summary>
    /// 显示可选战备
    /// </summary>
    private void InitPlayerAirdrop()
    {
        var layout = armamentRoot.GetChild(0, 2, 0, 0, 0);
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
                SetColor(button, list[u].Color);
                buttons.Add(button, list[u].ID);

                SetButton(button,tipRoot,ShowTip);
                SetCilck(button,() => SelectAirdrop(button));
            }
        }
    }
    private void UnInitPlayerAirdrop()
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
        if (selectAirdropIndex==-1) return false;
        wndManager.PlaySound(new("UI/UI_Button_Back"));
        armamentRoot.GetChild(roomManager.Self.index).GetComponent<Animator>().Play("UnExpend", 0, 0);
        SetSelectIndex(-1, true);
        //selectAirdropIndex = -1;
        //SetSelectIndex(-1,false);
        return true;
    }

    protected override void HideWnd()
    {
        
        //for (int i = 0; i < armamentroot.childcount; ++i)
        //{
        //    if(animators[i]) destroy(animators[i].transform.parent.gameobject);
        //}
        //UnInitPlayerAirdrop();
    }

    //通过动画调用
    private void FinishReady()
    {
        GameState = GameStateEnum.Transition;//会因为切状态自己关??


    }

    private void SetButton(Transform trans,Transform tip, System.Action<Transform,Transform> action)
    {
        var item = trans.TryGetOrAddComponent<ButtonEnterDetector>();
        item.Enter = (data) => {
            SetActive(tip, true);
            action.Invoke(trans, tip);
        };

        item.In = (data) => {
            tip.position = UICamera.uiCamera.ScreenToWorldPoint(Input.mousePosition)+100*Vector3.forward;
            //tip.position =new(Input.mousePosition.x, Input.mousePosition.y,tip.position.z);
        };
        item.Exit = (data) => {
            SetActive(tip, false);
        };

    }
    private void ShowTip(Transform trans, Transform tip)
    {
        if (buttons.TryGetValue(trans, out var id))
        {
            if (ResSvc.airdropDic.TryGetValue(id, out var data))
            {
                SetSprite(tip.GetChild(0, 0), data.icon);
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

        SetActive(tip, false);
    }

    /// <summary> 发送玩家选择战备的消息</summary>
    private void SendPlayerSelectAemament(int playerIndex, int id, int index)
    {
        BridgeSys.Instance.SendPlayerSelectArmament(playerIndex, id, index);
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
        buttons[armamentRoot.GetChild(playerIndex, 5, index)] = id;
        SetSprite(armamentRoot.GetChild(playerIndex, 5, index, 0),ResSvc.airdropDic[id].icon);
        SetColor(armamentRoot.GetChild(playerIndex, 5, index), ResSvc.airdropDic[id].Color);

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
