using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>一次音频播放请求的参数包（路径或 clip、分组、音量、延迟、空间化等）。</summary>
    public class AudioPlayInfo
    {
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

    /// <summary>音频分组（与 AudioMixer 中的分组名一一对应）。</summary>
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
