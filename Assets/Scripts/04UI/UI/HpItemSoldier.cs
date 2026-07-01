using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;

public class HpItemSoldier : HpItemBase
{
    private const int _ShowTime=5;
    public float time = _ShowTime;


    public override void Set(GameObject enemy)
    {
        base.Set(enemy);
        time = _ShowTime;
        //transform.localScale = Vector3.one;
    }
    public override void Refresh()
    {
        base.Refresh();
        time = _ShowTime;
    }

    public override void Tick()
    {
        base.Tick();
        time -= Time.fixedDeltaTime;
        SetAlpha(transform,Mathf.Clamp01(time));
        SetActive(FillW, time>1);
    }

    public override bool CanRecycle()
    {
        return base.CanRecycle()|| time < 0;
    }
}
