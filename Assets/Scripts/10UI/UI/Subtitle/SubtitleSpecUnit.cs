using UnityEngine;
using FPSGame.Audio;

namespace FPSGame.UI
{
using FPSGame.Gameplay;
using static FPSGame.WndTools.WndRootTool;
using UnityEngine.UI;
using FPSGame.GameContract;
using FPSGame.Data;

/// <summary>
/// 特殊单位对话字幕。
/// </summary>
[AddComponentMenu("UI/字幕/特殊单位")]
public class SubtitleSpecUnit : SubtitleBase
{

    public override SubtitleBase Creat(IActor owner, GameObject target,Transform parent, bool alwaysShow)
    {
        //Debug.LogError("创建"+ owner+ target,gameObject);
        base.Creat(owner, target,parent,alwaysShow);
        if (!alwaysShow) SetAlpha(transform, 0);
        var tarActor = target.GetComponent<IActor>();
        //unimportant = tarActor.HasFlag(ActorFlag.Unimportant);
        noFade = tarActor.HasFlag(FPSGame.Core.ActorFlag.Boss);
        SetText(title, tarActor.ShowName);
        SetSprite(halo, tarActor.ExtraPortrait);
        SetActive(gameObject, tarActor != owner);
        GlobalEventBus.OnActorSpeech += OnActorSpeech;
        return this;
    }
    void OnDestroy()
    {
        GlobalEventBus.OnActorSpeech -= OnActorSpeech;
    }
    bool noFade;
    float lastSpeechTime = Mathf.NegativeInfinity;
    float showTime;
    //[SerializeField]
    //bool unimportant;

    public override void TryActive(bool state)
    {
        //SetActive(gameObject, state);
        targetState = state;
    }
    private void OnActorSpeech(GameObject go, RuntimeSoundData data)
    {
        if (go != target|| GetDistance()>100) return;
        SetText(desc ,data.Desc);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)desc.parent);

        AudioSvc.PlaySound(data);
        lastSpeechTime = Time.time;
        showTime = data.Clip.length+2;
    }
    public float show;
    protected override void Update()
    {
        base.Update();
        if (!target) return;



        if (lastSpeechTime >0&& Time.time> lastSpeechTime+ showTime)
        {
            lastSpeechTime = -1;
            SetText(desc,"");
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)desc.parent);
        }
        if (targetState&&TargetEnabled&&completeTrans&& !noFade)
        {
            var dis = GetDistance();
            show = Mathf.Clamp01((dis - 60) / 40f);
            float scale = 1 - Mathf.Clamp01((dis - 60) / 40f);
            if (dis<7) scale = Mathf.Clamp01((dis-3) / 4f);
            SetAlpha(transform,Mathf.Lerp(GetAlpha(transform), scale, Time.deltaTime*2));
        }
    }

}
}
