using System.Collections;
using System.Collections.Generic;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

/// <summary>
/// 载具组件
/// </summary>
public class Furniture_Vehicle : Furniture_Base
{

    private IDrivable controller;
    private I_Actor actor;
    private bool isDead;

    public override void Operate()
    {
        if (isDead)
        {
            return;
        }
        if (owner)
        {
            owner.transform.position = transform.TransformPoint(0,0,1);
        }
        base.Operate();
        controller = relatedTrans2.GetComponent<IDrivable>();
        actor = relatedTrans2.GetComponent<I_Actor>();
        controller.SetOwener(owner);
        desc = inOperate ? "退出驾驶" : "进入驾驶";
        if (owner)
        {
            owner.transform.parent = relatedTrans2;
            actor.OnDeath += OnActorDeath;
        }
    }

    protected override void InOperateUpdate()
    {
        var delay = Time.time - lastOperatetime;

        if (Tool.In(delay, -1f, 1) && owner)
        {
            var owner = this.owner.transform;
            var point = relatedTrans;
            owner.rotation = Quaternion.Slerp(owner.rotation, point.rotation, Time.deltaTime * 10);
            if (Vector3.Distance(point.position, owner.position) > 0.15f)
            {
                owner.position = Vector3.Lerp(owner.position, point.position, Time.deltaTime * 4);
            }
        }
        else
        {
            if (owner && controller.TryExit())
            {
                Operate();
                actor.OnDeath -= OnActorDeath;
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        isDead = false;
    }

    private void OnActorDeath()
    {
        actor.OnDeath -= OnActorDeath;
        Operate();
        isDead = true;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (actor != null)
        {
            actor.OnDeath -= OnActorDeath;
        }
    }

    public override bool CanOperate(GameObject unit)
    {
        return !isDead && base.CanOperate(unit);
    }


}
