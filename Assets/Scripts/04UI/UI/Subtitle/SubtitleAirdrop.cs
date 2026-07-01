using System.Collections;
using System.Collections.Generic;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static UnityEngine.ParticleSystem;
using static WndTools.WndRootTool;

public class SubtitleAirdrop : SubtitleBase
{
    [SerializeField]
    private Transform stateText;
    private AirdropController.AirdropData data;
    private int lastTime;
    private LimitedLife m_particle;
    public override SubtitleBase Creat(I_Actor owner, GameObject target, Transform parent, bool alwaysShow)
    {
        base.Creat(owner, target, parent,alwaysShow);
        SetActive(gameObject, false);
        return this;
    }


    protected override void Update()
    {
        if (!m_particle.IsAlive())
        {
            SetActive(gameObject, false);
            data = null;
            return;
        }
        Follow(targetPoint);
        if (lastTime != (int)data.time)
        {
            lastTime = (int)data.time;
            SetText(stateText,(data.State== AirdropController.AirdropState.Arrive?"即将抵达":"正在进行")+": "+ Tool.FloatToTime(data.time));
        }
        
    }

    public override void TryActive(bool state)
    {
        //不受影响
    }
    public void OnAirdrop(GameObject owner,GameObject target, Vector3 point)
    {
        if (owner != this.owner.gameObject) return;
        this.target = target;
        this.targetPoint = point;
        var ownerObj = owner.GetComponent<Actor>();
        data = target.GetComponent<VFXAirdropEffect>().data;
        m_particle = target.GetComponent<LimitedLife>();
        SetText(desc, data.cfg.showName);

        
        SetText(title, ownerObj.ShowName);
        SetSprite(halo, data.cfg.icon);

        SetActive(gameObject, true);
    }
    protected override void Follow(Vector3 point)
    {
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(point);
        screenPosition *= Mathf.Sign(screenPosition.z);
        screenPosition.z = 0;
        base.Follow(point + Vector3.up * (Mathf.Sqrt(2*Vector3.Distance(point,owner.Pos))));
    }

}
