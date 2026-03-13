using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;
public class Furniture_Door : Furniture_Base
{
    public bool lockState;
    private bool front;

    public override void Operate()
    {
        base.Operate();
        time = 0;
        front = Vector3.Angle(owner.transform.position - Forward, Forward)<90;
    }

    protected override void Update()
    {
        base.Update();
        if (!inOperate)
        {
            var unit = ActorsManager.Actors.FirstOrDefault(item =>Vector3.Distance(Pos, item.Pos) < 3);
            if (unit.IsValid()) {
                Handle(unit.gameObject); 
            }
            if (lockState)
            {
                Handle(gameObject);
            }
        }
    }

    protected override void InOperateUpdate()
    {
        if (!lockState&&(time+=Time.deltaTime)>4&& Vector3.Distance(Pos, owner.transform.position) > 3f )
        {
            anim.Play("Exit");
            PlaySound(audioClose);
            inOperate = false;
        }
    }
}
