using System.Collections.Generic;

using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class TipWnd : Window
{
    Queue<TipWndInfo> quene=new();

    [SerializeField]
    private TipWndInfo nowInfo;
    [SerializeField]
    private Transform optA, optB,title, desc;
    [SerializeField]
    private Transform costRoot;

    public void Creat(TipWndInfo info)
    {
        quene.Enqueue(info);
        SetWndState();
        LoadTip();
    }

    public void Close()
    {
        if (quene.Count > 0)
        {
            LoadTip();
        }
        else
        {
            nowInfo = default;
            SetWndState(false);
        }
    }


    protected override void FirstShowWnd()
    {

    }

    protected override void ShowWnd()
    {

        InputManager.AddListenerCancel(Listener);
        if (quene.Count==0)
        {
            Close();
            return;
        }
    }
    protected override void HideWnd()
    {

    }
    private void LoadTip()
    {
        AudioSvc.PlaySound(new("UI/UI_Notice"));
        this.nowInfo = quene.Dequeue();
        if (string.IsNullOrEmpty(nowInfo.optA_Text) && string.IsNullOrEmpty(nowInfo.optB_Text)) nowInfo.optB_Text = "确认";

        SetText(title, nowInfo.title);
        SetText(desc, nowInfo.desc);

        ClearButton(optA);
        ClearButton(optB);
        
        if (nowInfo.optA_Click != null) SetCilck(optA, nowInfo.optA_Click);
        if (nowInfo.optB_Click != null) SetCilck(optB, nowInfo.optB_Click);
        SetCilck(optA, Close);
        SetCilck(optB, Close);
        SetText(optA.transform.GetChild(0), nowInfo.optA_Text);
        SetText(optB.transform.GetChild(0), nowInfo.optB_Text);

        int count = nowInfo.costs != null ? nowInfo.costs.Length:0;
        bool allow = true;
        for (int i=0;i<costRoot.childCount;++i)
        {
            if (i < count)
            {
                SetActive(costRoot.GetChild(i), true);
                SetSprite(costRoot.GetChild(i, 0), propertyManager.GetIcon(nowInfo.costs[i].Key));
                int need = nowInfo.costs[i].Value, have = propertyManager.GetCount(nowInfo.costs[i].Key);
                SetText(costRoot.GetChild(i, 1), need + "/" + have);
                //SetColor(costRoot.GetChild(i, 1), Color.red);
                SetColor(costRoot.GetChild(i, 1), need > have?Color.red:Color.white);
                allow &= have >= need;
            }
            else
            {
                SetActive(costRoot.GetChild(i), false);
            }
        }

        SetActive(optA, !string.IsNullOrEmpty(nowInfo.optA_Text)&& allow);
        SetActive(optB, !string.IsNullOrEmpty(nowInfo.optB_Text));

    }

    private bool Listener()
    {
        if (!State) return false;

        ClickButton(optB);
        return true;
    }


#if UNITY_EDITOR
    [ContextMenu("测试")]
    private void _Comfort()
    {
        Creat(new()
        {
            title = "测试标题",
            desc = "测试文本",
            optA_Text = "确定",
            optB_Text = "取消",
        });
    }


#endif


}

[System.Serializable]
public class TipWndInfo
{
    public UnityEngine.Events.UnityAction optA_Click, optB_Click;
    public string optA_Text, optB_Text;
    public string title, desc;
    public KVP<OOPartEnum, int>[] costs;
    public TipWndInfo()
    {

    }
    /*
    public TipWndInfo(TipWndInfo info)
    {
        optA_Click = info.optA_Click;
        optB_Click = info.optB_Click;
        optA_Text = info.optA_Text;
        optB_Text = info.optB_Text;
        title = info.title;
        desc = info.desc;
        costs = info.costs;
    }*/
}