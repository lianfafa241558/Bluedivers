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
    public override SubtitleBase Creat(I_Actor owner, GameObject target, Transform parent,bool alwaysShow)
    {
        base.Creat(owner, target, parent, alwaysShow);
        GlobalEventManager.OnMark += OnMark;
        GlobalEventManager.OnActorSpeech += OnActorSpeech;
        baseVector = transform.GetRect().anchoredPosition;//复活后恢复位置
        SetActive(gameObject, false);
        SetActive(distance, false);
        //Debug.LogWarning("创建喊叫组件"+owner,gameObject);
        return this;
    }

    private void OnDestroy()
    {
        GlobalEventManager.OnMark -= OnMark;
        GlobalEventManager.OnActorSpeech -= OnActorSpeech;
    }

    protected override void Update()
    {
        if (!target || Time.time >= lastTime+showTime)
        {
            SetActive(gameObject, false);
            return;
        }
        if(owner.ActorState == ActorState.Dead) Follow(TargetPos);
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


    private void OnActorSpeech(GameObject go, NoticeData_SO data)
    {
        if (go != owner.transform.gameObject || GetDistance() > 100) return;
        SetActive(gameObject, true);
        SetText(desc, data.Desc);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)desc.parent);
        AudioManager.PlaySound(new() {
            cilp = data.Clip,
            vector =  go.transform.position,
            range = 40,
            group = AudioGroups.Player,
            volume = 1,
            delay=  data.Delay,
            space = data.Space?1:0
        });
        
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
                return "马喜荣!";
            default:
                return "嘿，看这里！";
        }
    }
}
