using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// 控制只保留一个AudioListener的(这玩意留两个会疯狂跳log)
/// </summary>
public class AudioListenerController : MonoBehaviour
{
    static AudioListenerController now;
    static List<AudioListenerController> all=new();
    //越高越容易保留
    [Range(0,100)]
    public int priority;
    AudioListener listener;
    private void Start()
    {
        listener = GetComponent<AudioListener>();
        all.Add(this);
        //优先级更大就顶掉之前的，否则关闭自己
        if (now == null)
        {
            now = this;
            listener.enabled = true;
        }else if (priority > now.priority)
        {
            now.listener.enabled = false;
            now = this;
            listener.enabled = true;
        }
        else
        {
            listener.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (!listener) return;
        listener.enabled = false;
        //如果自己是现在使用的接收器
        if (this == now)
        {
            now = all.OrderByDescending(item => item==this || !item.gameObject.activeInHierarchy ? -1:item.priority).FirstOrDefault();//如果死了应该就会被移除，所以理论上不存在item==null的情况
            now.listener.enabled = true;
        }
    }
    private void OnDestroy()
    {
        all.Remove(this);
        //如果自己是现在使用的接收器
        if (this==now)
        {
            now = all.OrderByDescending(item=> !item.gameObject.activeInHierarchy?-1:item.priority).FirstOrDefault();//如果死了应该就会被移除，所以理论上不存在item==null的情况
            if(now)now.listener.enabled = true;
        }
    }
}
