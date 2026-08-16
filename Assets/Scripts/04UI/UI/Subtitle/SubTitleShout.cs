using Core;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;

public class SubTitleShout : SubtitleBase
{
    private Vector2 baseVector;
    private float lastTime,showTime;
    public override SubtitleBase Creat(I_Actor owner, GameObject _, Transform parent,bool alwaysShow)
    {
        base.Creat(owner, _, parent, alwaysShow);
        GlobalEventSub.OnMark += OnMark;
        GlobalEventSub.OnActorSpeech += OnActorSpeech;
        if(owner.transform.TryGetComponent(out Health health))
        {
            health.OnRevive += OnRevive;
            health.OnDie += OnDeath;
        }
        SetSprite(halo, owner.ExtraPortrait);
        baseVector = transform.GetRect().anchoredPosition;//复活后恢复位置
        SetActive(gameObject, false);
        SetActive(distance, false);
        //Debug.LogWarning("创建喊叫组件"+owner,gameObject);
        return this;
    }

    private void OnDestroy()
    {
        GlobalEventSub.OnMark -= OnMark;
        GlobalEventSub.OnActorSpeech -= OnActorSpeech;
        if (owner.IsValid() && owner.transform.TryGetComponent(out Health health))
        {
            health.OnRevive += OnRevive;
            health.OnDie += OnDeath;
        }
    }

    protected override void Update()
    {
        if (!target || Time.time >= lastTime+showTime)
        {
            SetActive(gameObject, false);
            return;
        }
        if(owner.ActorState == ActorState.Dead) Follow(owner.CenterPos+Vector3.up);
    }

    public override void TryActive(bool state)
    {
        //不受影响
    }

    private void OnMark(GameObject markOwner, GameObject markTarget, Vector3 point)
    {
        if (markOwner != owner.transform.gameObject) return;
        //自己(活着的时候)的喊叫不显示
        //if (markOwner == owner.gameObject&&owner.actorState!=ActorState.Dead) return;
        SetActive(gameObject, true);
        //这个不对，要换成玩家配置里面的东西
        SetText(desc, MarkDesc(markTarget.GetComponentInChildren<BaseObject>()));
        /*
        if (string.IsNullOrEmpty(GetText(title)))
        {
            var targetObj=target.GetComponent<BaseObject>();
            SetText(title, targetObj.ShowName);
            SetSprite(halo, targetObj.ExtraPortrait);
        }*/
        lastTime = Time.time;
        showTime = 5;
    }


    private void OnActorSpeech(GameObject go, RuntimeSoundData data)
    {
        if (go != owner.gameObject || GetDistance() > 100) return;
        SetActive(gameObject, true);
        SetText(desc, data.Desc);
        //Debug.LogError("喊叫"+ data.Desc);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)desc.parent);
        AudioSvc.PlaySound(data);
        
        lastTime = Time.time;
        showTime = data.Clip.length + 2;
    }


    protected override float GetDistance()
    {
        return Vector3.Distance(Camera.main.transform.position, TargetPos);
    }

    private string MarkDesc(BaseObject obj)
    {
        if (!obj || string.IsNullOrEmpty(obj.ShowName)) return "嘿，看这里！";
        switch (obj.ShowName)
        {
            case "蘑菇":
                return "马喜";
            default:
                return "嘿，看这里！";
        }
    }
    void OnDeath(GameObject _)
    {
        //mainCamera = Camera.main;
        transform.GetRect().anchoredPosition = baseVector;
    }
    void OnRevive()
    {
        //mainCamera = Camera.main;
        transform.GetRect().anchoredPosition = baseVector;
    }
}
