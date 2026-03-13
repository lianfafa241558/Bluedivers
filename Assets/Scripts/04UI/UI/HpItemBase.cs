using Core;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;


public class HpItemBase : MonoBehaviour
{

    public Transform Name, FillW, FillR, States;
    [SerializeField]
    protected Animator anim;

    protected Actor actor;
    protected Health health;
    protected float animTime;

    public virtual void Set(GameObject enemy)
    {
        SetActive(gameObject, true);
        actor = enemy.GetComponent<Actor>();
        health = enemy.GetComponent<Health>();
        SetText(Name, actor.ShowName);
        SetFill(FillR, health.GetHpRatio());
        anim.Play("Idle");
        animTime = 0.8f;
    }
    public virtual void Refresh()
    {
        SetFill(FillR, health.GetHpRatio());
    }

    public virtual void Tick()
    {
        transform.position = Tool.WorldPosToScreenPos(actor.AimPoint.position + Vector3.up * actor.HpHeight);
        SetFill(FillW, health.GetHpRatio() - 0.02f, Time.deltaTime * 2);
    }

    public virtual void End()
    {
        
    }

    public virtual bool CanRecycle()
    {
        if (actor == null) return true;
        if (actor.ActorState == ActorState.Dead)
        {
            if(animTime>=0.8f) anim.Play("Death");
            if((animTime -= Time.fixedDeltaTime)<0) return true;
        }
        return false;
    }
}
