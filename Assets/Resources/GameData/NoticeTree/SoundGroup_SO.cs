using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core;
using Unity.BaseTool;
using UnityEditor;
using UnityEngine;
[CreateAssetMenu(fileName = "NewSoundGroup", menuName = "Audio/Sound Group")]
public class SoundGroup_SO: ScriptableObject
{
    [InspectorName("名称")]
    public string groupName;

    [Header("音频剪辑列表")]
    public List<SoundItem> clips = new();
    [InspectorName("随机音量范围")]
    [MinMaxSlider(0.5f,1.5f)]
    public Vector2 volumeRange = Vector2.one;
    [InspectorName("随机音调范围")]
    [MinMaxSlider(0.8f, 1.2f)]
    public Vector2 pitchRange = Vector2.one;
    [InspectorName("随机延迟范围")]
    public Vector2 delayRange = Vector2.zero;

    [InspectorName("淡入时间（秒）")]
    public float fadeInTime = 0f;
    [InspectorName("淡出时间（秒）")]
    public float fadeOutTime = 0f;
    [CustomLabel("播放范围", "flags",(int)SoundFlag.Space, CompareOperate.Contain)]
    public float range = 20;


    [InspectorName("优先级")]
    public int priority;
    public AudioGroups group;

    [InspectorName("标旗")]
    public SoundFlag flags;

    [System.Flags]
    public enum SoundFlag
    {
        [InspectorName("循环")]
        Loop = 1 << 0,
        [InspectorName("唯一的")]//仅播放一个实例(单独音频源才算)
        Unique = 1 << 1,
        [InspectorName("重要的")]//占据单独的音频源
        Importance = 1 << 2,
        [InspectorName("空间音效")]
        Space = 1 << 3,
    }

}
[System.Serializable]
public class SoundItem
{
    [InspectorName("音频")]
    public AudioClip audioClip;

    [InspectorName("字幕")]
    public string subtitle;
}


