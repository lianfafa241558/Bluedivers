using FPSGame.Attribute;
using FPSGame.Furn;
using UnityEngine;

public class Furniture_NPCChat : Furniture_Attached
{
    [Foldout("配置", true)]
    [SerializeField]
    [InspectorName("NPC语音组")]
    private SoundGroup_SO _soundGroup;
    [SerializeField]
    [InspectorName("散步组件")]
    private NPCWalk NPCWalk;

    [DisplayField]
    [SerializeField]
    [InspectorName("冷却剩余")]
    private float _cooldownRemain;

    [DisplayField]
    [SerializeField]
    private float _nextAvailableTime;

    /// <summary>
    /// 冷却中或没有语音组时不可交互
    /// </summary>
    public override bool CanOperate(GameObject unit)
    {
        if (!base.CanOperate(unit)) return false;
        if (_soundGroup == null || Time.time < _nextAvailableTime) return false;
        return true;
    }

    /// <summary>
    /// 交互时播放NPC语音，语音时长+1秒后冷却结束才能再次交互
    /// </summary>
    public override void Operate()
    {
        base.Operate();
        RuntimeSoundData soundData = _soundGroup.Get(transform.position);
        //AudioSource source = AudioSvc.PlaySound(soundData);
        GlobalEventSub.ActorSpeech(gameObject, soundData);
        //Debug.LogError(source+"播放音效"+ soundData.Desc,source);
        //人物停下来和你对话
        NPCWalk?.PauseWandering();
        Vector3 targetPos = owner.transform.position;
        targetPos.y = transform.position.y;
        transform.LookAt(targetPos);

        float clipLength = soundData.Clip != null ? soundData.Clip.length : 0f;
        _nextAvailableTime = Time.time + clipLength + 1f;
    }

    protected override void Update()
    {
        base.Update();
        _cooldownRemain = Mathf.Max(0f, _nextAvailableTime - Time.time);
    }

    protected void OnDisable()
    {
        _nextAvailableTime = 0f;
        _cooldownRemain = 0f;
    }
}
