using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;
public class CountDownWnd : WindowRoot
{
    [SerializeField]
    Transform txt;
    [SerializeField]
    Animator anim;
    [SerializeField]
    AudioClip warning;
    [SerializeField]
    int countDown = 0;

    public override void Init()
    {

    }
    public override void UnInit()
    {

    }
    protected override void FirstShowWnd()
    {
        countDown = 16;
    }

    protected override void ShowWnd()
    {
        SetActive(anim.transform, false);
    }

    protected override void HideWnd()
    {

    }

    // Update is called once per frame
    void Update()
    {
        int nowcd = taskManager.nowTask.Countdown;
        if (nowcd < 16!=GetActive(anim.transform))
        {
            SetActive(anim.transform, nowcd < 16);
        }

        if (nowcd < 16 && countDown != nowcd)
        {
            countDown = nowcd;
            anim.Play("Idle",0,0);
            SetText(txt,string.Format("00:{0:D2}", countDown));
            wndManager.PlaySound(new(warning,AudioGroups.UI));
        }

    }
}
