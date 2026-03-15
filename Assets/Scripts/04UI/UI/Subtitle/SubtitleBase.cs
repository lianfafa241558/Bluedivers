using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using static WndTools.WndRootTool;
using static Utils.Tool;
using Unity.BaseTool;
using GameContract;

public abstract class SubtitleBase : MonoBehaviour
{
    [Foldout("基础",true)]
    [DisplayField]
    public I_Actor owner;
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
    protected Camera mainCamera;
    [SerializeField]
    protected bool targetState,completeTrans;

    public virtual SubtitleBase Creat(I_Actor owner, GameObject target,Transform parent,bool alwaysShow)
    {
        this.owner = owner;
        this.target = target;
        targetActor = target?.GetComponent<Actor>();
        mainCamera = Camera.main;
        transform.SetParent(parent,false);
        if(direction) SetActive(direction, false);
        root = (RectTransform)transform;
        SetShow(alwaysShow);
        return this;
    }

    protected virtual void Update()
    {
        if (!target)
        {
            SetActive(gameObject, false);
            return;
        }
        float alpha = GetAlpha(transform);
        if (!completeTrans)
        {
            if (targetState)
            {
                float a = Mathf.Lerp(alpha, 1.1f, 3 * Time.deltaTime);
                SetAlpha(transform, a);
                if (a >= 1)
                {
                    completeTrans = true;
                }
            }
            else if (!targetState)
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
        //(从屏幕中点到目标的)差值
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
        return Vector3.Distance(owner.Pos, TargetPos);
    }

    protected Vector3 TargetPos => targetPoint != default ? targetPoint : (targetActor ? targetActor.CenterPos + targetActor.HpHeight * Vector3.up : target.transform.position + Vector3.up * 2);

}
