using System;
using System.Collections.Generic;

using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

public class NoticeWnd : Window
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
    private Queue<(NoticeData, float vaildTime)> queue=new();
    private List<NoticeData> wait = new();
    private List<float> waitTimes = new();
    private int nowPriority=-1;
    private string nowSourceName;

    public struct NoticeData
    {
        public RuntimeSoundData data;
        public string sourceName;
        public Sprite portrait;
        public bool allowWait;
        public Func<bool> func;
        public float vaildTime;
    }

    public void Creat(NoticeData source)
    {
        if (GameState == Core.GameStateEnum.GameEnd) return;
        if (!source.data.IsValid()) return;
        // 检查特定方法是否有正在执行??InvokeRepeating
        if (!IsInvoking(nameof(UpdateWait)))
        {
            InvokeRepeating(nameof(UpdateWait), 0, 0.1f);
        }

        if (source.allowWait && source.data.Delay > 0)
        {
            wait.Add(source);
            waitTimes.Add(source.data.Delay);
        }
        //Debug.LogError("新的优先级是"+ data.Priority+"当前优先??+ nowPriority);
        //加入队列
        else if(source.data.Cfg.priority <= nowPriority)
        {
            queue.Enqueue(new(source,Time.time+(source.vaildTime == -1?99: source.vaildTime)));
        }
        //直接打断
        else
        {
            Creat(source.data.Desc, source.data.Clip , source.sourceName, source.portrait, source.data.Cfg.priority);
        }
    }

    private void Creat(string desc, AudioClip clip,string name,Sprite sprite,int priority)
    {
        nowPriority = priority;
        SetWndState(true);
        PlayAnim("Entry", true);
        nowDesc = desc;
        nowCount = 0;
        time = 0.5f;
        if (clip) interval = Mathf.Min(clip.length / desc.Length, 0.3f);
        else interval = 0.35f - Mathf.Min(desc.Length * 0.01f, 0.2f);
        //Debug.LogError("计算得出的间??+ interval);
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
        //Debug.LogError("清空");
        queue.Clear();
        noticeState = NoticeState.Exit;
        SetWndState(false);
        audioSource.Stop();
        //audioSource2.Stop();
    }
   
    protected override void FirstShowWnd()
    {

    }

    protected override void HideWnd()
    {

    }
    protected override void ShowWnd()
    {
        //问题出在，如果是已经显示窗口的时候启动窗口，这里不会被调??
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
                    //Debug.LogError("退?");
                    SetWndState(false);
                    break;
            }
        }

    }

    private void UpdateWait()
    {
        for (int i = wait.Count - 1; i >= 0; --i)
        {
            if ((waitTimes[i] -= 0.1f) <= 0)
            {
                NoticeData temp = wait[i];
                temp.allowWait = false;
                Creat(temp);
                wait.RemoveAt(i);
                waitTimes.RemoveAt(i);
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
                if( Time.time < item.Item2 && (!item.Item1.func.IsValid() || item.Item1.func.Invoke()))
                {
                    //继续下一个
                    var next = item.Item1;
                    Creat(next.data.Desc, next.data.Clip, next.sourceName, next.portrait, next.data.Cfg.priority);

                    return;
                }
            }
            //正常结束
            {
                //Debug.LogError("播放完成，等2S");
                //SetText(desc, nowDesc);
                //其他进入了之后再切入end??
                noticeState= NoticeState.End;
                time = 2;
                nowPriority = -1;
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        CancelInvoke();
    }


    private enum NoticeState
    {
        Load,
        End,
        Exit
    }



}
