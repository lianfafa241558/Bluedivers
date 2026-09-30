using System.Collections.Generic;
using FPSGame.Core;

using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Audio
{

/// <summary>
/// 全局音频播放与音乐管理。
/// </summary>
[AddComponentMenu("管理/音频服务")]
public class AudioSvc : AudioManagerBase<AudioSvc>
{
    static Dictionary<MusicGroup, List<AudioClip>> musicDic;
    [SerializeField] 
    List<KVP<MusicGroup, List<AudioClip>>> musicList;
    private static bool musicLock = false;


    public override void Awake()
    {
        base.Awake();
        musicDic = musicList.ToDictionary();
        musicList.Clear();
        //⚠ 本类已下沉到 03_Audio 程序集（位于 01Manager 之下），不能引用 GlobalEventSub。
        //  游戏状态变化 / 设置变化 改由上层 AudioEventBridge 转发到 InGameStateChange / OnSettingCange（订阅时机与原先等价）。
    }

    private float musicVolume;
    /// <summary>由上层 AudioEventBridge 转发（原先是订阅 GlobalEventSub.OnSettingCange）</summary>
    public void OnSettingCange(string key, float value)
    {
        float sound;
        switch (key)
        {
            case "主音量":
                audioMixer.SetFloat("vMaster", PetToDB(value));
                return;
            case "音乐音量":
                audioMixer.SetFloat("vMusic", PetToDB(value));
                musicVolume = value;
                return;
            case "音效音量":
                audioMixer.SetFloat("vSound", PetToDB(value));
                return;
            case "角色音量":
                audioMixer.GetFloat("vSound", out sound);
                audioMixer.SetFloat("vPlayer", PetToDB(value * (DBToPet(sound) * 0.01f)));
                return;
            case "播报员音量":
                audioMixer.SetFloat("vPresenter", PetToDB(value));
                return;
            case "武器音量":
                audioMixer.GetFloat("vSound",out sound);
                audioMixer.SetFloat("vWeapon", PetToDB(value*(DBToPet(sound)*0.01f)));
                return;
            case "敌人音量":
                audioMixer.GetFloat("vSound", out sound);
                audioMixer.SetFloat("vEnemy", PetToDB(value * (DBToPet(sound) * 0.01f)));
                return;
            case "UI音量":
                audioMixer.SetFloat("vUI", PetToDB(value));
                return;
        }
    }

    /// <summary>由上层 AudioEventBridge 转发（原先是订阅 GlobalEventSub.OnGameStateChange）</summary>
    public void InGameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        switch (entry)
        {
            case GameStateEnum.Front:
                PlayMusic(MusicGroup.Front, 0.5f);
                break;
            case GameStateEnum.Bridge:
                PlayMusic(MusicGroup.Bridge,0.5f);
                break;
            case GameStateEnum.Ready:
                PlayMusic(MusicGroup.Ready, 1f);
                break;
            case GameStateEnum.Transition:
                //PlayMusic(MusicGroup.Transition);
                break;
            case GameStateEnum.Load:
                PlayMusic(MusicGroup.Load, 1);
                break;
            case GameStateEnum.Game:
                PlayMusic(MusicGroup.Game,0.5f);
                break;
            case GameStateEnum.GameEnd:
                //PlayMusic(MusicGroup.Transition);
                break;
        }
    }
    /// <summary>
    /// 音频片段加载器：由上层 AudioEventBridge 在初始化时注入 <c>ResSvc.LoadAudio</c>。
    /// ⚠ 本类位于 03_Audio（01Manager 之下），不能直接引用 ResSvc ⇒ 用委托反转依赖。
    /// </summary>
    public static System.Func<string, bool, AudioClip> ClipLoader;

    /// <summary>
    /// 定时器请求：由上层 AudioEventBridge 注入 <c>GameRoot.CreateTimer</c>（签名与它一致）。
    /// 同样是为避免 03_Audio 反向依赖 GameRoot 所在的 01Manager。
    /// </summary>
    public static System.Func<System.Action, float, int, LogicTimer> TimerRequest;

    protected override AudioClip PathToCilp(string path,bool cache)
    {
        return ClipLoader != null ? ClipLoader(path, cache) : null;
    }

    public static void PlayMusic(MusicGroup type,float volme)
    {
        if (musicLock) return;
        PlayMusic(musicDic[type].RandomTake(), volme);
    }

    public static void SetLockMusic(bool lockMusic)
    {
        musicLock = lockMusic;
    }


    public static void Suppressed(float time)
    {
        //⚠ 计时改走 TimerRequest（由上层注入 GameRoot.CreateTimer），语义与原实现完全一致：
        //   0.05s 一跳 ×20 把音乐压低；等待 time 秒后再跳 1 次恢复（counter 默认值即 1，这里显式传）。
        TimerRequest?.Invoke(() => {
            float nowValue;
            Instance.audioMixer.GetFloat("vMusic", out nowValue);
            Instance.audioMixer.SetFloat("vMusic", Mathf.Lerp(nowValue, PetToDB(Instance.musicVolume / 2), 0.05f));
        }, 0.05f, 20);

        TimerRequest?.Invoke(() => {
            TimerRequest?.Invoke(() => {
                float nowValue;
                Instance.audioMixer.GetFloat("vMusic", out nowValue);
                Instance.audioMixer.SetFloat("vMusic", Mathf.Lerp(nowValue, PetToDB(Instance.musicVolume), 0.05f));
            }, 0.05f, 1);
        }, time, 1);
    }

    /*
    public static AudioSource PlaySound(RuntimeSoundData data)
    {
        return PlaySound(new AudioPlayInfo() {
            cilp = data.Clip,
            group = data.Cfg.group,
            volume = data.Volume,
            delay = data.Delay,
            speed = data.Pitch,
            importance = data.HasFlag(SoundFlag.Importance),
            nonStackable = data.HasFlag(SoundFlag.Unique),
            loop = data.HasFlag(SoundFlag.Loop),
            space = data.HasFlag(SoundFlag.Space) ? 1 : 0,
            range = data.Cfg.range,
            vector = data.Point,
        });
    }*/

    public enum MusicGroup
    {
        /// <summary>初始</summary>
        [InspectorName("初始")] Front,
        /// <summary>舰桥</summary>
        [InspectorName("舰桥")]Bridge,
        /// <summary>准备</summary>
        [InspectorName("准备")] Ready,
        //<summary>转场</summary>
        //[InspectorName("转场")]Transition,
        /// <summary>加载</summary>
        [InspectorName("加载")] Load,
        ///<summary>游戏</summary>
        [InspectorName("游戏")]Game,
        /// <summary>波次</summary>
        [InspectorName("波次")]Wave,
        /// <summary>通用Boss</summary>
        [InspectorName("通用Boss")]Boss,
        /// <summary>开始撤离</summary>
        [InspectorName("开始撤离")]Evacuate,
        /// <summary>完成撤离</summary>
        [InspectorName("完成撤离")] End,
        /// <summary>任务失败</summary>
        [InspectorName("任务失败")] Fail,

    }

}
}
