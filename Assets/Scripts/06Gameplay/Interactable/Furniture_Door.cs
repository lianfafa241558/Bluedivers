using FPSGame.Core.Interface;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Game;
using UnityEngine;

namespace FPSGame.Gameplay
{
using static FPSGame.WndTools.WndRootTool;
/// <summary>
/// 可开关的门。
/// </summary>
[AddComponentMenu("交互/门")]
public class Furniture_Door : Furniture_Attached
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
            // ⚠ 先判有效再取 Pos：注册表里可能残留"已销毁"的接口引用（盟友离场被 Destroy），
            //   而接口引用不能用 == null 判（不走 Unity 的 Object 重载）⇒ 用工程约定的 IsValidMono()，
            //   否则访问 item.Pos 会抛 MissingReferenceException（2026-10-07 实测）。
            // 手写循环替代 ActorsManager.Actors.FirstOrDefault(闭包)：门每帧都要跑，
            // 原来每帧每扇门建一个捕获闭包 + 一套 LINQ 枚举器（实测 168B/扇/帧）
            FPSGame.GameContract.IActor unit = null;
            var actors = ActorsManager.Actors;
            for (int i = 0, l = actors.Count; i < l; ++i)
            {
                FPSGame.GameContract.IActor item = actors[i];
                if (item.IsValidMono() && Vector3.Distance(Pos, item.Pos) < 3) { unit = item; break; }
            }
            if (unit.IsValidMono()) {
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

    public void Lock()
    {
        lockState = true;
    }
}
}
