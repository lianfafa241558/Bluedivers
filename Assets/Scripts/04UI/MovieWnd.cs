using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class MovieWnd : WindowRoot
{
    [SerializeField]
    private Transform text;
    [SerializeField]
    private RectTransform top,under;
    [SerializeField]
    private float lastPro;

    protected override void ShowWnd()
    {
        lastPro = 0;
        SetAlpha(text, 0);
        WindowState = WindowStateEnum.UI;
        Vector2 size = Tool.ScreenSize2D;
        //Debug.LogWarning("当前高度"+ size.y +"修改比例后的应该的高度"+ (size.x / 2.2f / 2)+"最后高度"+(size.y - (size.x / 2.2f / 2)));
        float height = Mathf.Max((size.y-(size.x / 2.2f))/2,100);
        top.sizeDelta =new(top.sizeDelta.x,height);
        under.sizeDelta = new(top.sizeDelta.x, height);
    }

    private void Update()
    {
        float pro = resManager.AsyncLoadSceneProgress();
        if (lastPro<100 && pro>=100)
        {
            SetAlpha(text,1);
        }
        lastPro = pro;
        if (lastPro >100 && Input.GetKeyDown(KeyCode.Escape))
        {
            resManager.AsyncContinueLoadScene();
        }
    }
    protected override void HideWnd()
    {

        WindowState = WindowStateEnum.Game;
    }
    public override void Init()
    {

    }
    public override void UnInit()
    {

    }
    protected override void FirstShowWnd()
    {
        
    }
}
