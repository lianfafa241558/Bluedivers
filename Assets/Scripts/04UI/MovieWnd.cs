using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;

public class MovieWnd : Window
{
    private const float MovieAspectRatio = 2.2f;
    private const float MinBlackBarHeight = 100f;

    [SerializeField]
    private Transform text;
    [SerializeField]
    private RectTransform top, under;
    [SerializeField]
    private float lastPro;

    protected override void ShowWnd()
    {
        lastPro = 0;
        SetAlpha(text, 0);
        WindowState = WindowStateEnum.UI;
        AdaptBlackBars();
    }

    private void AdaptBlackBars()
    {
        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        float scaleFactor = scaler != null ? scaler.referenceResolution.y / Tool.ScreenSize2D.y : 1f;

        float canvasWidth = Constants.CanvasWidth;
        float canvasHeight = Constants.CanvasHeight;
        float movieHeight = canvasWidth / MovieAspectRatio;
        float height = Mathf.Max((canvasHeight - movieHeight) / 2f, MinBlackBarHeight);

        top.sizeDelta = new Vector2(top.sizeDelta.x, height);
        under.sizeDelta = new Vector2(under.sizeDelta.x, height);
    }
    
    private void Update()
    {
        if (!resManager.AsyncAllowSkip()) return;
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
   
    protected override void FirstShowWnd()
    {
        
    }
}
