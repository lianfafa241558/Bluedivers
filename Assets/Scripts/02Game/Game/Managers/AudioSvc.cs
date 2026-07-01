using System.Collections.Generic;
using Core;

using UnityEngine;
using Utils;

public class AudioSvc : AudioManaqerBase<AudioSvc>
{
    static Dictionary<MusicGroup, List<AudioClip>> musicDic;
    [SerializeField] 
    List<KVP<MusicGroup, List<AudioClip>>> musicList;

    public override void Awake()
    {
        base.Awake();
        musicDic = musicList.ToDictionary();
        musicList.Clear();
        //全局管理器不用考虑死亡时注销的问题
        GlobalEventSub.OnGameStateChange += InGameStateChange;
        GlobalEventSub.OnSettingCange += OnSettingCange;
    }

    private float musicVolume;
    private void OnSettingCange(string key, float value)
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

    private void InGameStateChange(GameStateEnum exit, GameStateEnum entry)
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
                PlayMusic(MusicGroup.Game,0.2f);
                break;
            case GameStateEnum.GameEnd:
                //PlayMusic(MusicGroup.Transition);
                break;
        }
    }
    protected override AudioClip PathToCilp(string path,bool cache)
    {
       return ResSvc.Instance.LoadAudio(path, cache);
    }
    public static void PlayMusic(MusicGroup type,float volme)
    {
        PlayMusic(musicDic[type].RandomTake(), volme);
    }
    public static void Suppressed(float time)
    {
        GameRoot.CreateTimer(() => {
            float nowValue;
            Instance.audioMixer.GetFloat("vMusic", out nowValue);
            Instance.audioMixer.SetFloat("vMusic", Mathf.Lerp(nowValue, PetToDB(Instance.musicVolume / 2), 0.05f));
        }, 0.05f, 20);

        GameRoot.CreateTimer(() => {
            GameRoot.CreateTimer(() => {
                float nowValue;
                Instance.audioMixer.GetFloat("vMusic", out nowValue);
                Instance.audioMixer.SetFloat("vMusic", Mathf.Lerp(nowValue, PetToDB(Instance.musicVolume), 0.05f));
            }, 0.05f, 1);
        }, time);
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
