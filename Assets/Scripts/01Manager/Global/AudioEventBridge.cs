using System;
using FPSGame.Core;
using UnityEngine;
using FPSGame.Audio;
using FPSGame.Gameplay;

namespace FPSGame.Managers
{

/// <summary>
/// AudioSvc 依赖反转桥（2026-09-30 为把 AudioSvc 下沉到 <c>03_Audio</c> 程序集而加）。
///
/// <para>背景：<c>AudioSvc</c> 现在位于 <c>03_Audio</c>（在 <c>01Manager</c> 之下），
/// 不能再直接引用 <see cref="GlobalEventSub"/>／<c>ResSvc</c>／<c>GameRoot</c>。
/// 本类留在 01Manager（上层），负责：</para>
/// <list type="number">
///   <item>把 <c>GlobalEventSub</c> 的两个全局事件转发给 AudioSvc 的公开方法（订阅时序与原先"AudioSvc 在自己的 Awake 里订阅"等价）；</item>
///   <item>注入音频片段加载器（<c>ResSvc.LoadAudio</c>）与定时器请求（<c>GameRoot.CreateTimer</c>）。</item>
/// </list>
/// <para>⚠ 全进程只订阅一次（静态事件在场景重载间保留），比原先"每个 AudioSvc 实例都订一次"更安全。</para>
/// </summary>
public static class AudioEventBridge
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        GlobalEventSub.OnGameStateChange += ForwardGameState;
        GlobalEventSub.OnSettingCange += ForwardSetting;

        AudioSvc.ClipLoader = LoadClip;
        AudioSvc.TimerRequest = (cb, waitTime, counter) => GameRoot.CreateTimer(cb, waitTime, counter);
    }

    private static void ForwardGameState(GameStateEnum oldState, GameStateEnum newState)
    {
        if (AudioSvc.Instance)
        {
            AudioSvc.Instance.InGameStateChange(oldState, newState);
        }
    }

    private static void ForwardSetting(string key, float value)
    {
        if (AudioSvc.Instance)
        {
            AudioSvc.Instance.OnSettingCange(key, value);
        }
    }

    private static AudioClip LoadClip(string path, bool cache)
    {
        return ResSvc.Instance ? ResSvc.Instance.LoadAudio(path, cache) : null;
    }
}
}
