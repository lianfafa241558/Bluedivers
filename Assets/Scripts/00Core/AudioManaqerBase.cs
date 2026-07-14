
using System.Collections.Generic;
using Core.Interface;

using UnityEngine;
using UnityEngine.Audio;

namespace Core
{
    public abstract class AudioManaqerBase<T> : Singleton<T>, I_GlobaManager where T : AudioManaqerBase<T>
    {
       
        private static Dictionary<AudioGroups, AudioMixerGroup> mixerDic=new();
        private static Dictionary<AudioGroups, AudioSource> ShotGroup;
        private static AutoObjectPool<AudioSource> sourcePool;
        private static MusicItem[] audMusics;
        private static List<AudioPlayInfo> delayAudios;
        public AudioMixer audioMixer;

        public static AudioMixerGroup GetMixGroup(AudioGroups type) => mixerDic[type];

        #region 生命周期
        void I_GlobaManager.Init()
        {
           
            foreach (AudioGroups item in System.Enum.GetValues(typeof(AudioGroups)))
            {
                mixerDic.TryAdd(item, audioMixer.FindMatchingGroups(item.ToString())[0]);
            }
            var mixerMusic = GetMixGroup(AudioGroups.Music);
            var mixerGeneral = GetMixGroup(AudioGroups.General);
            delayAudios = new();
            audMusics = new MusicItem[2];
            for (int i = 0; i < audMusics.Length; ++i)
            {
                AudioSource aud = gameObject.AddComponent<AudioSource>();
                aud.outputAudioMixerGroup = mixerMusic;

                audMusics[i] = new(aud);
            }

            ShotGroup = new();
            foreach (AudioGroups item in System.Enum.GetValues(typeof(AudioGroups)))
            {
                var source= gameObject.AddComponent<AudioSource>();
                source.outputAudioMixerGroup = GetMixGroup(item);
                ShotGroup.TryAdd(item, source);
            }

            sourcePool = new(
                (item) => item.isPlaying,
                () => {
                    AudioSource aud = new GameObject().AddComponent<AudioSource>();
                    aud.outputAudioMixerGroup = mixerGeneral;
                    aud.gameObject.name = "Sound";
                    aud.transform.parent = transform;
                    aud.maxDistance = 25;
                    aud.rolloffMode = AudioRolloffMode.Linear;
                    return aud;
                },
                (item) => {
                    item.gameObject.SetActive(true);
                },
                (item) => {
                    //item.Stop();
                    item.gameObject.SetActive(false);
                }
            , 6);
        }
        public void UnInit()
        {

        }
        protected virtual void Update()
        {
            sourcePool.Update();
            for(int i = 0; i < audMusics.Length; ++i)
            {
                audMusics[i].Update();
            }
            for (int i = delayAudios.Count-1; i >=0 ; --i)
            {
                if((delayAudios[i].delay -= Time.deltaTime) <= 0)
                {
                    PlaySound(delayAudios[i]);
                    delayAudios.RemoveAt(i);
                }
            }
        }
        #endregion


        #region 音效



        public static AudioSource PlaySound(AudioPlayInfo info)
        {
            AudioSource NormalPlay(AudioClip cilp)
            {
                var source = info.source ?? sourcePool.Get();
                _Play(cilp, source, info.loop, info.volume, info.speed);
                source.spatialBlend = info.space;
                source.minDistance = 5;
                source.maxDistance = info.range;
                source.outputAudioMixerGroup = GetMixGroup(info.group);
                if (info.vector != default)
                {
                    source.transform.position = info.vector;
                }
                return source;
            }


            if (info.delay > 0)
            {
                delayAudios.Add(info);
                return null;
            }
            if (sourcePool==null) return null;
            var cilp = info.cilp ??(!string.IsNullOrEmpty(info.path)? Instance.PathToCilp("SFX/" + info.path, info.cache):null);
            if (!cilp)
            {
                Debug.LogError("找不到音效"+ info.cilp+"或者"+ info.path);
                return null;
            }
            //禁止重复
            if (info.nonStackable && sourcePool.Contains(item=>item.isPlaying&&item.clip.name==cilp.name)) return null;
            if (info.space==0)
            {
                var source = ShotGroup[info.group];
                source.PlayOneShot(cilp);
                return null;
            }
            else if (!info.importance)//允许合并的(只有)
            {
                //stop会打断shot，但active不会
                //距离越远允许偏差的越大
                //一定要group相等！！！
                var group = GetMixGroup(info.group);
                var cam = Camera.main;
                if (!cam)
                {
                    foreach (var c in Camera.allCameras)
                    {
                        if (c.enabled && c.gameObject.activeInHierarchy)
                        {
                            cam = c;
                            break;
                        }
                    }
                }
                var camPos = cam ? cam.transform.position : Vector3.zero;
                var distance = Mathf.Sqrt(Vector3.Distance(info.vector, camPos));//100m可以偏差10格
                var tmpSource = sourcePool.Find(item => Vector3.Distance(item.transform.position,info.vector)<Mathf.Max(2, distance) && group == item.outputAudioMixerGroup);
                if (tmpSource)
                {
                    tmpSource.PlayOneShot(cilp);
                    return null;
                }
                else
                {
                    return NormalPlay(cilp);
                }
            }
            return NormalPlay(cilp);

        }

        //最终指向
        private static void _Play(AudioClip clip, AudioSource source, bool loop = false, float volume = 1,float speed=1)
        {
            if (source.clip == clip && source.isPlaying) return;
            source.volume = volume;
            source.loop = loop;
            source.clip = clip;
            source.pitch = speed;
            source.time = 0;
            source.Play();
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="name">音频名称</param>
        public static void Stop(string name = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                sourcePool.Release();
            }
            else
            {
                sourcePool.Release(item => item.clip?.name == name);
            }
        }
        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="clip">音频</param>
        public static void Stop(AudioClip clip)
        {
            Stop(clip.name);
        }

        /// <summary>
        /// 停止播放(必须是非管理器中的音频源)
        /// </summary>
        //这里直接enquene会导致不是池里面的东西加进去，所以直接用stop
        public static void Stop(AudioSource source,string path=null)
        {
            if(string.IsNullOrEmpty(path)&&source) source.Stop();
            else if (source?.clip?.name==path)source.Stop();
        }

        #endregion

        #region 音乐
        /// <summary>播放Bgm专用，播放新的时，会使其他的逐渐淡出</summary>
        public static void PlayMusic(string key, float volume = 1,float speed = 1)
        {
            PlayMusic(Instance.PathToCilp("BGM/" + key), volume,speed);
        }

        protected abstract AudioClip PathToCilp(string path,bool cache=false);

        public static void PlayMusic(AudioClip clip, float volume = 1, float speed = 1)
        {
            if (!clip) return;
            //Debug.LogWarning("切换播放" + clip);
            bool completed = false;
            for (int i = 0; i < audMusics.Length; i++)
            {
                //只要不是空闲，统一进入淡出阶段
                if (audMusics[i].state != MusicItem.State.Free)
                {
                    audMusics[i].Exit();
                }
                //还没成功且空闲的音源
                else if (!completed)
                {
                    completed = true;
                    _Play(clip, audMusics[i].source, true, 0,speed);
                    audMusics[i].Enetry(volume);
                }
            }
            if (!completed)
            {
                //如果短时间之内有第三个出现，就直接清空所有的，转为播放新的
                StopMusic(true);
                PlayMusic(clip, volume, speed);
            }
        }

        public static void StopMusic(bool immediately = false)
        {
            for (int i = 0; i < audMusics.Length; i++)
            {
                audMusics[i].Exit(immediately);
            }
        }

        class MusicItem
        {
            private const float _SwitchTime = 5;

            public AudioSource source { private set; get; }

            public MusicItem(AudioSource source)
            {
                this.source = source;
            }

            public float volume { private set; get; }
            public State state { private set; get;}

            private float suppressedTime;

            public enum State
            {
                Free,
                Entry,
                Play,
                Exit,
            }
            public void Enetry(float volume)
            {
                state = State.Entry;
                this.volume = volume;
            }

            /// <param name="immediately">立即停止</param>
            public void Exit(bool immediately=false)
            {
                //Debug.LogWarning("被停止");   
                if (immediately)
                {
                    source.Stop();
                    state = State.Free;
                }
                else if(state != State.Free
                    )
                {
                    state = State.Exit;
                }
            }
            public void Update()
            {
                switch (state)
                {
                    case State.Entry:
                        if((source.volume += volume / _SwitchTime * Time.deltaTime )>volume)
                        {
                            state = State.Play;
                        }
                        break;
                    case State.Exit:
                        if ((source.volume -= volume / _SwitchTime * Time.deltaTime*2) <= 0)
                        {
                            source.Stop();
                            state = State.Free;
                        }
                        break;
                }
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将分贝值转换为百分比浮点数(0分贝=100)
        /// </summary>
        public static float DBToPet(float dB)
        {
            return Mathf.Pow(10f, dB / 20f) * 100;
        }
        /// <summary>
        /// 将百分比浮点数转换为分贝值(1=0dB)
        /// </summary>
        public static float PetToDB(float percentage)
        {
            if (percentage <= 1f)
                return -80f;
            return 20f * Mathf.Log10(percentage / 100);
        }

        /// <summary>
        /// 为指定GameObject创建并配置AudioSource
        /// </summary>
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

        #endregion

    }

    public class AudioPlayInfo{
        public string path;
        public bool cache = true;
        public AudioGroups group;
        public AudioClip cilp;
        public AudioSource source;

        public float delay;
        public float volume = 1;
        public float speed = 1;
        public bool loop = false;
        public float space = 0;
        public Vector3 vector;
        public float range = 20;
        public bool nonStackable = false;//不可堆叠的：要是已有，就不播了
        public bool importance = false;//重要性(是否独立使用一个音频源)

        public AudioPlayInfo()
        {

        }

        private AudioPlayInfo(AudioGroups group, float volume, float delay , bool importance)
        {
            this.group = group;
            this.volume = volume;
            this.delay = delay;
            this.importance = importance;
        }

        public AudioPlayInfo(string path, AudioGroups group = AudioGroups.General, float volume = 1, float delay = 0, bool importance = false) : this(group,volume,delay,importance)
        {
            this.path = path;
        }
        public AudioPlayInfo(AudioClip cilp, AudioGroups group = AudioGroups.General, float volume = 1, float delay = 0, bool importance = false) : this(group, volume, delay, importance)
        {
            this.cilp = cilp;
        }
        public AudioPlayInfo(AudioClip cilp, Vector3 vector, float range=30, AudioGroups group = AudioGroups.General, float volume = 1, float delay = 0, bool importance = false) :this(group, volume, delay, importance)
        {
            this.cilp = cilp;
            this.space = 1;
            this.vector = vector;
            this.range = range;
        }
        public AudioPlayInfo(string path, Vector3 vector, float range=30, AudioGroups group = AudioGroups.General, float volume = 1, float delay = 0, bool importance = false) : this(group, volume, delay, importance)
        {
            this.path = path;
            this.space = 1;
            this.vector = vector;
            this.range = range;
        }
    }

    public enum AudioGroups
    {
        General,
        Music,
        Impact,
        Pickup,
        Weapon,
        Enemy,
        Player,
        UI,
        Presenter,
    }
}
