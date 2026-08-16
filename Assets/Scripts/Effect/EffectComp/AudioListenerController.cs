using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// 控制只保留一个AudioListener 这玩意留两个会疯狂跳log)
/// 核心逻辑：始终启用 all 中 priority 最高且处于激活层级(GameObject activeInHierarchy)的监听器。
/// 抢占判断放在 OnEnable，保证相机被禁用后重新启用(如死亡相机->主相机)时能正确抢回监听器。
/// </summary>
public class AudioListenerController : MonoBehaviour
{
    static AudioListenerController now;
    static List<AudioListenerController> all = new();

    //越高越容易保留
    [Range(0, 100)]
    public int priority;
    AudioListener listener;

    private void Awake()
    {
        listener = GetComponent<AudioListener>();
        all.Add(this);
    }

    private void OnEnable()
    {
        // 每次激活都重新评估：只有自己是当前最高优先级活跃监听器时，才启用自己，否则关闭。
        var best = SelectBest();
        if (best == this)
        {
            SetNow(this);
        }
        else if (now == this)
        {
            // 自己不再是最高优先级，让出
            SetNow(best);
        }
        else
        {
            // 自己不是最优，且现在不是自己，直接关闭即可
            if (listener) listener.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (!listener) return;
        // 无论是否被启用，禁用时都关闭自己的监听器
        listener.enabled = false;
        // 如果自己正在使用，重新选下一个
        if (this == now)
        {
            SetNow(SelectBest());
        }
    }

    private void OnDestroy()
    {
        all.Remove(this);
        // 如果自己正在使用，重新选下一个
        if (this == now)
        {
            SetNow(SelectBest());
        }
    }

    /// <summary>
    /// 从 all 中选出 priority 最高且 GameObjet 处于激活层级的监听器；没有则返回 null。
    /// 排除自身与已销毁(inactive)对象。
    /// </summary>
    private static AudioListenerController SelectBest()
    {
        if (all.Count == 0) return null;
        return all
            .Where(item => item && item.gameObject.activeInHierarchy && item.listener)
            .OrderByDescending(item => item.priority)
            .FirstOrDefault();
    }

    /// <summary>
    /// 将 now 切换到 target，关闭旧的、启用新的。target 为 null 时只关闭旧的。
    /// </summary>
    private static void SetNow(AudioListenerController target)
    {
        if (now != null && now.listener) now.listener.enabled = false;
        now = target;
        if (now != null && now.listener)
        {
            now.listener.enabled = true;
            Debug.LogWarning("设置音频监听" + now, now);
        }
    }
}
