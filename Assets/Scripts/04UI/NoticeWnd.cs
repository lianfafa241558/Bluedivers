using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WndTools.WndRootTool;

public class NoticeWnd : WindowRoot
{
    [SerializeField]
    private Transform portrait, noiteName, desc;
    [SerializeField]
    private AudioSource audioSource, audioSource2;
    private string nowDesc;
    private int nowCount;
    private float time;
    private float interval;

    private NoticeState noticeState;
    private Queue<(NoticeData_SO, Func<bool>, float)> queue=new();
    private int nowPriority=-1;
    private string nowSourceName;

    public void Creat(NoticeData_SO data, Func<bool> func,float vaildTime=-1)
    {
        if (GameRoot.GameState == Core.GameStateEnum.GameEnd) return;
        if (!data.IsValid()) return;
        //Debug.LogError("新的优先级是"+ data.Priority+"当前优先级"+ nowPriority);
        //加入队列
        if(data.Priority <= nowPriority)
        {
            queue.Enqueue(new(data, func,Time.time+(vaildTime==-1?99: vaildTime)));
        }
        //直接打断
        else
        {
            Creat(data.Desc, data.Clip, data.SourceName, data.Portrait,data.Priority);
        }
    }

    public void Creat(string desc, AudioClip clip,string name,Sprite sprite,int priority)
    {
        nowPriority = priority;
        SetWndState(true);
        PlayAnim("Entry", true);
        nowDesc = desc;
        nowCount = 0;
        time = 0.5f;
        if (clip) interval = Mathf.Min(clip.length / desc.Length, 0.3f);
        else interval = 0.35f - Mathf.Min(desc.Length * 0.01f, 0.2f);
        //Debug.LogError("计算得出的间隔"+ interval);
        noticeState = NoticeState.Load;
        SetText(this.desc, "");
        SetSprite(portrait, sprite);
        SetText(noiteName, name);
        /*
        if (audioSource.isPlaying && nowSourceName != name)
        {
            audioSource2.clip = audioSource.clip;
            audioSource2.time = audioSource.time;
            audioSource2.Play();
        }*/
        audioSource.clip = clip;
        if (clip) audioSource.Play();
        //Debug.LogError("创建"+desc);
        nowSourceName = name;

    }

    public void Clear()
    {
        //Debug.LogError("清除");
        queue.Clear();
        noticeState = NoticeState.Exit;
        SetWndState(false);
        audioSource.Stop();
        //audioSource2.Stop();
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

    protected override void HideWnd()
    {

    }
    protected override void ShowWnd()
    {
        //问题出在，如果是已经显示窗口的时候启动窗口，这里不会被调用
        //PlayAnim("Entry",true);
    }

    private void Update()
    {
        if ((time-=Time.deltaTime) <= 0)
        {
            switch (noticeState)
            {
                case NoticeState.Load:
                    UpdateText();
                    break;
                case NoticeState.End:
                    PlayAnim("Exit",true);
                    noticeState= NoticeState.Exit;
                    time = 0.5f;//持续0.5秒的动画
                    break;
                case NoticeState.Exit:
                    //Debug.LogError("退出");
                    SetWndState(false);
                    break;
            }
        }
    }

    private void UpdateText()
    {
        if (nowCount < nowDesc.Length)
        {
            time += interval;
            ++nowCount;
            SetText(desc, nowDesc.Substring(0, nowCount - 1) + "<alpha=#60>" + nowDesc.Substring(nowCount - 1, 1) + "</a>");
        }else if (audioSource.isPlaying)
        {
            //SetText(desc, nowDesc);
            time = Mathf.Max(audioSource.clip.length - audioSource.time, interval);
            //Debug.LogError("当前播放时间" + audioSource.time + "长度" + audioSource.clip.length + "申请等待"+ time);
        }
        else
        {
            while(queue.Count > 0)
            {
                var item = queue.Dequeue();
                if( Time.time < item.Item3 && (!item.Item2.IsValid() || item.Item2.Invoke()))
                {
                    //继续下一个
                    Creat(item.Item1.Desc,item.Item1.Clip, item.Item1.SourceName, item.Item1.Portrait, item.Item1.Priority);
                    return;
                }
            }
            //正常结束
            {
                //Debug.LogError("播放完成，等待2S");
                //SetText(desc, nowDesc);
                //其他进入了之后再切入end？
                noticeState= NoticeState.End;
                time = 2;
                nowPriority = -1;
            }
        }
    }

    private enum NoticeState
    {
        Load,
        End,
        Exit
    }



}
