using FPSGame.Core;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 通用场景标记字幕。
/// </summary>
[AddComponentMenu("UI/字幕/标记")]
public class SubtitleMark : SubtitleBase
{
    [SerializeField]
    private Transform halo2;
    const float continueTime = 5;
    private float time;
    public override SubtitleBase Creat(IActor owner, GameObject target, Transform parent, bool alwaysShow)
    {
        base.Creat(owner, target, parent, alwaysShow);
        GlobalEventBus.OnMark += OnMark;

        SetText(title, owner.ShowName);
        SetSprite(halo, owner.ExtraPortrait);
        SetSprite(halo2, owner.ExtraPortrait);

        SetActive(gameObject, false);
        return this;
    }

    private void OnDestroy()
    {
        GlobalEventBus.OnMark -= OnMark;
    }

    protected override void Update()
    {
        if (!target|| (time -= Time.deltaTime) <= 0)
        {
            SetActive(gameObject, false);
            return;
        }
        Follow(targetPoint);

    }

    public override void TryActive(bool state)
    {
        //不受影响
    }

    private void OnMark(GameObject owner,GameObject target, Vector3 point)
    {
        if (owner != this.owner.transform.gameObject) return;
        this.target = target;
        this.targetPoint = point;

        var targetObj = target.GetComponentInParent<BaseObject>();
        string show = "未知";
        //Debug.LogWarning("目标"+ target + " 组件"+ targetObj);
        if (targetObj &&!string.IsNullOrEmpty(targetObj.ShowName))
        {
            show = targetObj.ShowName;
        }
        SetText(desc, show);
        
        if (string.IsNullOrEmpty(GetText(title)))
        {
           
            SetText(title, this.owner.ShowName);
            SetSprite(halo, this.owner.ExtraPortrait);
        }
        time=continueTime;
        SetActive(gameObject, true);
    }

    protected override void Follow(Vector3 point)
    {
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(point);
        screenPosition *= Mathf.Sign(screenPosition.z);
        screenPosition.z = 0;
        halo2.position = screenPosition;
        base.Follow(point);
    }

}
}
