using UnityEngine;
using static WndTools.WndRootTool;
using UnityEngine.UI;
using GameContract;

public class SubtitleSpecUnit : SubtitleBase
{

    public override SubtitleBase Creat(I_Actor owner, GameObject target,Transform parent, bool alwaysShow)
    {
        //Debug.LogError("创建"+ owner+ target,gameObject);
        base.Creat(owner, target,parent,alwaysShow);
        if (!alwaysShow) SetAlpha(transform, 0);
        var tarActor = target.GetComponent<I_Actor>();
        //unimportant = tarActor.HasFlag(ActorFlag.Unimportant);
        noFade = tarActor.HasFlag(Core.ActorFlag.Boss);
        SetText(title, tarActor.ShowName);
        SetSprite(halo, tarActor.ExtraPortrait);
        SetActive(gameObject, tarActor != owner);
        GlobalEventSub.OnActorSpeech += OnActorSpeech;
        return this;
    }
    void OnDestroy()
    {
        GlobalEventSub.OnActorSpeech -= OnActorSpeech;
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
        if (targetState&&completeTrans&& !noFade)
        {
            var dis = GetDistance();
            show = Mathf.Clamp01((dis - 60) / 40f);
            float scale = 1 - Mathf.Clamp01((dis - 60) / 40f);
            if (dis<7) scale = Mathf.Clamp01((dis-3) / 4f);
            SetAlpha(transform,Mathf.Lerp(GetAlpha(transform), scale, Time.deltaTime*2));
        }
    }

}
