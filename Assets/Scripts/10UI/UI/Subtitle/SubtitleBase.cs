using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using static FPSGame.Utils.Tool;

using FPSGame.GameContract;
using FPSGame.Core.Interface;   // IsValidMono（IActor 是接口引用：单位被销毁后不是 Unity 的 null，必须用它判活）
using FPSGame.Attributes;

public abstract class SubtitleBase : MonoBehaviour
{
    [Foldout("基础",true)]
    [DisplayField]
    public IActor owner;
    [DisplayField]
    public GameObject target;
    [DisplayField]
    public Vector3 targetPoint;

    public bool forever;
    [DisplayField]
    public RectTransform root;
    public int offest;
    [Foldout("组件", true)]
    [SerializeField]
    protected Transform title, desc, halo, distance, direction;
    Actor targetActor;

    /// <summary>
    /// 目标单位是否启用：目标上没有 <see cref="Actor"/> 组件时视为启用。
    /// 供字幕跟随单位组件 enabled（含 prefab 上初始关闭的单位）—— 与小地图图标口径一致。
    /// </summary>
    protected bool TargetEnabled => targetActor == null || targetActor.enabled;

    /// <summary>上一帧的 <see cref="TargetEnabled"/>，用于在翻转时重走一次淡入/淡出</summary>
    private bool lastTargetEnabled = true;

    //[SerializeField]
    protected Camera mainCamera=> Camera.main;
    [SerializeField]
    protected bool targetState,completeTrans;

    public virtual SubtitleBase Creat(IActor owner, GameObject target,Transform parent,bool alwaysShow)
    {
        this.owner = owner;
        this.target = target;
        targetActor = target?.GetComponent<Actor>();
        //mainCamera = Camera.main;
        transform.SetParent(parent,false);
        if(direction) SetActive(direction, false);
        root = (RectTransform)transform;
        //⚠ 基线必须保持 true，才能与首帧真实的 TargetEnabled 比较：
        //   写成 = TargetEnabled 会让"初始就禁用"的单位首帧判定为"无变化"，淡出永不触发。
        lastTargetEnabled = true;
        SetShow(alwaysShow);
        return this;
    }

    protected virtual void Update()
    {
        // ⚠ owner 是 **IActor 接口引用**：单位被销毁后 `!target` 能判出 target 死了，但 **owner 判不出来**
        //   ⇒ 后面 `owner.Pos` 会 NRE（2026-10-07 实测：SubtitleBase.GetDistance ← Follow ← Update）。
        //   ⚠ 这里**只隐藏、不销毁**：销毁交给 ① SubtitleWnd.OnFriendLeave（盟友离场）
        //     ② OnActorDeath（单位死亡）③ OnSceneChange（换场景整体清）—— 因为空投类是**池化**的，
        //     在这里直接 Destroy 会让池子里留下死引用。
        //   ⚠ 另外 target 为空也不能当"失效"处理：有些标记创建时 target 就是 null
        //     （SubtitleMark / 空投，由 OnMark 之后再补 target）。
        if (!owner.IsValidMono() || !target)
        {
            SetActive(gameObject, false);
            return;
        }
        //目标单位组件被禁用 ⇒ 立刻按"不可见"处理，启用后自动淡回（见 TargetEnabled）
        bool targetEnabled = TargetEnabled;
        if (targetEnabled != lastTargetEnabled)
        {
            lastTargetEnabled = targetEnabled;
            completeTrans = false;   //目标态翻转 ⇒ 重走一次淡入/淡出
        }

        float alpha = GetAlpha(transform);
        bool visible = targetState && targetEnabled;
        if (!completeTrans)
        {
            if (visible)
            {
                float a = Mathf.Lerp(alpha, 1.1f, 3 * Time.deltaTime);
                SetAlpha(transform, a);
                if (a >= 1)
                {
                    completeTrans = true;
                }
            }
            else
            {
                float a = Mathf.Lerp(alpha, -0.1f, 3 * Time.deltaTime);
                SetAlpha(transform, a);
                if (a <= 0)
                {
                    completeTrans = true;
                }
            }
        }
        
        Follow(TargetPos);
    }

    public void SetShow(bool state)
    {
        TryActive(state);
        completeTrans = state;
    }
    
    public abstract void TryActive(bool state);

    protected virtual void Follow(Vector3 point)
    {
        var dis = GetDistance();
        point += Vector3.up * dis / 20;
        //point += Vector3.up * (Mathf.Log(dis+1,2)-0.5f);
        //Tool.DrawLabel(point, (Mathf.Log(dis + 1, 2)-0.5f)+" "+ (dis + 1), Time.deltaTime);
        //Debug.DrawLine(point- Vector3.up * Mathf.Log(dis + 1, 2), point,Color.red,Time.deltaTime);
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(point);
        screenPosition *= Mathf.Sign(screenPosition.z);
        screenPosition.z = 0;

        //这个计算方式还是有点不对
        Vector3 modiflyPos = screenPosition;
        //(从屏幕中点到目标的差值)
        Vector3 dir = (screenPosition - ScreenSize * 0.5f);
        //从一个椭圆形改成一个圆角矩形的限制(或者说是一个矢量和这个矩形的交点)
        Vector3 limit = ClampRoundedRectangle(dir, new Vector2(0.5f, 0.45f) * ScreenSize2D, new Vector2(0.425f, 0.4f) * ScreenSize2D);


        if (!InRoundedRectangle(dir, new Vector2(0.5f, 0.45f) * ScreenSize2D, new Vector2(0.425f, 0.4f) * ScreenSize2D))
        {
            modiflyPos = limit + ScreenSize / 2;
            SetActive(direction, true);
        }
        else
        {
            SetActive(direction, false);
        }

        //限制在屏幕内的坐标
        //Vector3 modiflyPos = Clamp(screenPosition,(Vector3.one*0.5f- vector)*ScreenSize2D, (Vector3.one * 0.5f + vector) * ScreenSize2D);
        root.position = modiflyPos;

        var dirdir = ClampRoundedRectangle((screenPosition - root.position).normalized, new(0.5f, 0.45f), new(0.425f, 0.4f));
        direction.localPosition = dirdir * (root.sizeDelta + Vector2.one * offest);
        float angle = VectorAngle((screenPosition - direction.position).normalized, Vector2.up);
        direction.localEulerAngles = new(0, 0, -angle);

        SetText(distance, Mathf.FloorToInt(dis) + " 米");
    }

    protected virtual float GetDistance() {
        if (!owner.IsValidMono()) return 0f;   // 兜底：本方法也可能被别处调用（不止 Update）
        return Vector3.Distance(owner.Pos, TargetPos);
    }

    /// <summary>
    /// 标记点（世界坐标）：显式目标点 &gt; 目标单位的血条位置 &gt; 目标物体上方 &gt; 兜底回宿主自己。
    /// <para>⚠ `targetActor`（组件）与 `target`（GameObject）可能**一个死一个活**（组件被销毁、GO 还在，
    /// 或反之）⇒ 必须分别判有效性，不能只判其中一个。</para>
    /// </summary>
    protected Vector3 TargetPos
    {
        get
        {
            if (targetPoint != default) return targetPoint;
            if (targetActor) return targetActor.CenterPos + targetActor.HpHeight * Vector3.up;
            if (target != null) return target.transform.position + Vector3.up * 2;
            return owner.IsValidMono() ? owner.transform.position : Vector3.zero;
        }
    }

}
}
