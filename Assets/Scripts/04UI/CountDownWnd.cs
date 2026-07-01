using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;
public class CountDownWnd : Window
{
    [SerializeField]
    Transform txt;
    [SerializeField]
    Animator anim;
    [SerializeField]
    AudioClip warning;
    [SerializeField]
    int countDown = 0;

    int height;
  
    protected override void FirstShowWnd()
    {
        countDown = 16;
        var rect = transform.RectTransform();
        height = (int)rect.rect.height;
        rect.sizeDelta = new(rect.rect.width,0);
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
        int nowcd = TaskManager.Instance.nowTask.Countdown;
        if (nowcd < 16!=GetActive(anim.transform))
        {
            SetActive(anim.transform, nowcd < 16);
            var rect = transform.RectTransform();
            rect.sizeDelta = new(rect.rect.width, nowcd < 16?height:0);
        }

        if (nowcd < 16 && countDown != nowcd)
        {
            countDown = nowcd;
            anim.Play("Idle",0,0);
            SetText(txt,string.Format("00:{0:D2}", countDown));
            AudioSvc.PlaySound(new(warning,AudioGroups.UI));
        }

    }
}
