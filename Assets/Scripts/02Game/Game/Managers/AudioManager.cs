using System.Collections.Generic;
using BaseLibrary;
using Core;
using Unity.BaseTool;
using UnityEngine;
using UnityEngine.Audio;
using Utils;

public class AudioManager : AudioManaqerBase<AudioManager>
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
        GameRoot.OnGameStateChange += InGameStateChange;
        GlobalEventManager.OnSettingCange += OnSettingCange;
    }

    private float musicVolume;
    private void OnSettingCange(string key, float value)
    {
        float sound;
        switch (key)
        {
            case "主音量":
                audioMixer.SetFloat("vMaster", Tool.PetToDB(value));
                return;
            case "音乐音量":
                audioMixer.SetFloat("vMusic", Tool.PetToDB(value));
                musicVolume = value;
                return;
            case "音效音量":
                audioMixer.SetFloat("vSound", Tool.PetToDB(value));
                return;
            case "角色音量":
                audioMixer.GetFloat("vSound", out sound);
                audioMixer.SetFloat("vPlayer", Tool.PetToDB(value * (Tool.DBToPet(sound) * 0.01f)));
                return;
            case "播报员音量":
                audioMixer.SetFloat("vPresenter", Tool.PetToDB(value));
                return;
            case "武器音量":
                audioMixer.GetFloat("vSound",out sound);
                audioMixer.SetFloat("vWeapon", Tool.PetToDB(value*(Tool.DBToPet(sound)*0.01f)));
                return;
            case "敌人音量":
                audioMixer.GetFloat("vSound", out sound);
                audioMixer.SetFloat("vEnemy", Tool.PetToDB(value * (Tool.DBToPet(sound) * 0.01f)));
                return;
            case "UI音量":
                audioMixer.SetFloat("vUI", Tool.PetToDB(value));
                return;
        }
    }

    private void InGameStateChange(GameStateEnum exit, GameStateEnum entry)
    {
        switch (entry)
        {
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
       return ResManager.Instance.LoadAudio(path, cache);
    }
    public static void PlayMusic(MusicGroup type,float volme)
    {
        PlayMusic(musicDic[type].RandomTake(), volme);
    }
    public static void Suppressed(float time)
    {
        GameRoot.CreateTimer(()=>{
            float nowValue;
            Instance.audioMixer.GetFloat("vMusic", out nowValue);
            Instance.audioMixer.SetFloat("vMusic", Mathf.Lerp(nowValue,Tool.PetToDB(Instance.musicVolume /2),0.05f));
        },0.05f,20);

        GameRoot.CreateTimer(() => {
            GameRoot.CreateTimer(() => {
                float nowValue;
                Instance.audioMixer.GetFloat("vMusic", out nowValue);
                Instance.audioMixer.SetFloat("vMusic", Mathf.Lerp(nowValue, Tool.PetToDB(Instance.musicVolume), 0.05f));
            }, 0.05f, 1);
        }, time);
         

    }



    public static AudioSource CreatSource(GameObject gameObject, AudioGroups groups)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = GetMixGroup(groups);
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1.0f;
        source.spatialize = true;
        source.minDistance = 10f;
        source.maxDistance = 60f;
        return source;
    }

    public enum MusicGroup
    {
        /// <summary>舰桥</summary>
        [CustomLabel("舰桥")]Bridge,
        /// <summary>准备</summary>
        [CustomLabel("准备")] Ready,
        //<summary>转场</summary>
        //[CustomLabel("转场")]Transition,
        /// <summary>加载</summary>
        [CustomLabel("加载")] Load,
        ///<summary>游戏</summary>
        [CustomLabel("游戏")]Game,
        /// <summary>波次</summary>
        [CustomLabel("波次")]Wave,
        /// <summary>通用Boss</summary>
        [CustomLabel("通用Boss")]Boss,
        /// <summary>开始撤离</summary>
        [CustomLabel("开始撤离")]Evacuate,
        /// <summary>完成撤离</summary>
        [CustomLabel("完成撤离")] End,
        /// <summary>任务失败</summary>
        [CustomLabel("任务失败")] Fail,
    }

}
