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

    IDrivable controller;
    I_Actor actor;

    public override void Operate()
    {
        base.Operate();
        controller = relatedTrans2.GetComponent<IDrivable>();
        actor = relatedTrans2.GetComponent<I_Actor>();
        controller.SetOwener(owner);
        desc = inOperate ? "进入驾驶" : "退出驾驶";
        if (owner)
        {
            owner.transform.parent = relatedTrans2;
            actor.OnDeath += Operate;
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
                actor.OnDeath -= Operate;
            }
        }
    }


}
