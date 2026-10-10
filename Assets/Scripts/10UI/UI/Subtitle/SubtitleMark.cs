using FPSGame.Core;
using FPSGame.Core.Interface;   // IsValidMono（IActor 是接口引用，不能用 == null 判活）
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Gameplay;
using FPSGame.Utils;            // Tool.IsFinite（屏幕坐标落盘前的兜底）

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

    /// <summary>本次标记只有点位（联机标记不传 target）⇒ 不能用 target 当失效判据。</summary>
    private bool pointMark;
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
        // ⚠ target 允许为 null（联机标记只传点，见 NetActionBridge.OnRemoteMark）⇒ 不能拿它当"失效"判据。
        //   用 pointMark 区分"本来就没目标"和"目标中途被销毁"——后者 Unity 假 null 也是 !target，会误判。
        if ((!pointMark && !target) || (time -= Time.deltaTime) <= 0)
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

    private void OnMark(GameObject markOwner,GameObject markTarget, Vector3 point)
    {
        // ⚠ markTarget 可能为 null：联机标记只带点（引用过不了网）⇒ 全程按"可空"处理，不能直接解引用。
        if (!this.owner.IsValidMono() || markOwner != this.owner.transform.gameObject) return;
        target = markTarget;
        targetPoint = point;
        pointMark = markTarget == null;

        string show = "未知";
        //Debug.LogWarning("目标"+ target + " 组件"+ targetObj);
        var targetObj = markTarget ? markTarget.GetComponentInParent<BaseObject>() : null;
        if (targetObj && !string.IsNullOrEmpty(targetObj.ShowName))
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
        // 兜底：非有限值写进 RectTransform ⇒ Canvas 每帧刷 "Invalid AABB inAABB"（见 SubtitleBase.Follow）
        if (mainCamera == null || !Tool.IsFinite(point)) return;
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(point);
        screenPosition *= Mathf.Sign(screenPosition.z);
        screenPosition.z = 0;
        if (!Tool.IsFinite(screenPosition)) return;
        halo2.position = screenPosition;
        base.Follow(point);
    }

}
}
