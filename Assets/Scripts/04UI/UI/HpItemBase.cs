using System.Collections.Generic;
using Core;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static WndTools.WndRootTool;


public class HpItemBase : MonoBehaviour
{
    [SerializeField]
    private GameObject StatePrefab;
    [SerializeField]
    protected Transform Name, FillW, FillR, States;
    [SerializeField]
    protected Animator anim;

    protected Actor actor;
    protected Health health;
    protected float animTime;

    private readonly List<Health.AboStateViewInfo> _aboStates = new List<Health.AboStateViewInfo>();

    public virtual void Set(GameObject enemy)
    {
        SetActive(gameObject, true);
        actor = enemy.GetComponent<Actor>();
        health = enemy.GetComponent<Health>();
        SetText(Name, actor.ShowName);
        SetFill(FillR, health.GetHpRatio());
        anim.Play("Idle");
        animTime = 0.8f;
        RefreshStates();
    }
    public virtual void Refresh()
    {
        SetFill(FillR, health.GetHpRatio());
        RefreshStates();
    }

    public virtual void Tick()
    {
        transform.position = Tool.WorldPosToScreenPos(actor.CenterPos + Vector3.up * actor.HpHeight);
        SetFill(FillW, health.GetHpRatio() - 0.02f, Time.deltaTime * 2);
    }

    /// <summary>刷新异常状态显示：动态实例化/复用 States 下的物体</summary>
    private void RefreshStates()
    {
        health.GetActiveAboStates(_aboStates);
        int activeCount = _aboStates.Count;

        // 不为零的异常状态数量大于 States 下现有物体数量，则实例化补充
        while (States.childCount < activeCount)
        {
            Instantiate(StatePrefab, States);
        }

        // 配置前 activeCount 个物体
        for (int i = 0; i < activeCount; i++)
        {
            Transform child = States.GetChild(i);
            SetActive(child, true);

            var info = _aboStates[i];
            float ratio = info.Max > 0f ? info.Current / info.Max : 0f;
            ratio = Mathf.Clamp01(ratio);

            // 第0个子物体为 fill Image，第1个子物体为图标 Image
            Transform fillT = child.GetChild(0);
            Transform iconT = child.GetChild(1);
            SetFill(fillT, ratio);

            Image fillImg = fillT.GetComponent<Image>();
            Image iconImg = iconT.GetComponent<Image>();
            // 图标在显示时替换为对应状态的图标
            SetSprite(iconImg, info.Icon);

            if (ratio >= 1f)
            {
                // 满：fill 白色，图标为异常状态颜色
                fillImg.color = Color.white;
                iconImg.color = info.Color;
            }
            else
            {
                // 未满：fill 为异常状态颜色，图标白色
                fillImg.color = info.Color;
                iconImg.color = Color.white;
            }
        }

        // 不为零的异常状态数量小于 States 下现有物体数量，则隐藏多余物体
        for (int i = activeCount; i < States.childCount; i++)
        {
            SetActive(States.GetChild(i), false);
        }
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
