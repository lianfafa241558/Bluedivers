using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;

[Flags]
public enum FurnitureFlag
{
    /// <summary>自动操作</summary>
    [CustomLabel("自动操作")] AutoOperate=1<<0,
    /// <summary>切换状态</summary>
    [CustomLabel("切换状态")] SwitchState = 1 << 1,
    /// <summary>瞬间完成操作</summary>
    [CustomLabel("瞬间完成操作")] Immediately = 1 << 2,
    /// <summary>一次性</summary>
    [CustomLabel("一次性")] Disposable = 1 << 3,
    /// <summary>(完成操作后)播放动画</summary>
    [CustomLabel("播放动画")] PlayAnim = 1 << 4,
    /// <summary>喊话</summary>
    [CustomLabel("喊话")] Speech = 1 << 5,
    /// <summary>离开保留进度</summary>
    [CustomLabel("保留进度")] KeepPress = 1 << 6,
    /// <summary>长按时控制动画</summary>
    [CustomLabel("长按时控制动画")] ControlAnim = 1 << 7,
    /// <summary>任意角度</summary>
    [CustomLabel("任意角度")] AnyAngle = 1 << 8,
}

public class Furniture_Base : BaseObject
{


    public static Dictionary<int,Furniture_Base> list=new();
    private static int nowID=0;
    private static int GetID => ++nowID;

    [Foldout("配置", true)]

    [CustomLabel("长按时间")]
    public float meetTime;

    [CustomLabel("长按音效", "meetTime",0,CompareOperate.Greater)]
    public AudioClip audioPress;

    [CustomLabel("开启音效")]
    public AudioClip audioOper;

    [CustomLabel("关闭音效")]
    public AudioClip audioClose;


    [SerializeField]
    [CustomLabel("标旗")]
    protected FurnitureFlag flags;


    [SerializeField]
    [CustomLabel("已按时间")]
    [DisplayField]
    private float pressTime;

    [CustomLabel("可以操作")]
    public bool canOperate = true;
    [CustomLabel("正在运行")]
    public bool inOperate;
    [SerializeField]
    protected string desc = "进行交互";
    public Vector3Int ForwardAxis;

    [Foldout("关联", true)]
    [SerializeField]
    protected Transform relatedTrans;
    [CustomLabel("外部浮点数参数")]
    public float ExtFloatParameter;
    [CustomLabel("外部布尔参数")]
    public bool ExtBoolParameter;

    [DisplayField(true, false, true)]
    [SerializeField]
    protected ParticleSystem particle;
    [DisplayField(true,false,true)]
    public Animator anim;
    [DisplayField(true, false, true)]
    [SerializeField]
    protected NavMeshObstacle obs;
    [DisplayField(true, false, true)]
    public new AudioSource audio;


    [Foldout("状态", true)]
    [DisplayField(true, false, true)]
    public float lastOperatetime;
    [DisplayField(true, false, true)]
    [SerializeField]
    protected GameObject owner ;
    [DisplayField(true, false, true)]
    [SerializeField]
    protected float time;
    [DisplayField(true, false, true)]
    [SerializeField]
    protected int count;


    private int ID;

    public virtual string Desc { get => desc; }

    public override Vector3 CenterPos => transform.position + (GetComponent<BoxCollider>()?transform.TransformVector(GetComponent<BoxCollider>().center):Vector3.up);
    public override Vector3 Forward =>/*Quaternion.Euler(90*ForwardAxis) **/transform.forward;

    //public static bool IsTargetProp(Prop_Base prop, string name) => prop != null && prop.propName == name;
    public bool HaveFlag(FurnitureFlag flag) => flags.HasFlag(flag);


    protected virtual void Awake()
    {
        particle = GetComponentInChildren<ParticleSystem>(true);
        audio = GetComponent<AudioSource>();
        anim = GetComponent<Animator>();
        obs = GetComponent<NavMeshObstacle>();
        //renderers = Tool.GetRendererGroup(transform);
        list.Add(ID=GetID, this);
    }

    private void OnDestroy()
    {
        list.Remove(ID);
    }

    protected virtual void Update()
    {
        if (inOperate) InOperateUpdate();
        if (HaveFlag(FurnitureFlag.ControlAnim))
        {

        }
    }
    public float Press
    {
        get => pressTime;
        set
        {
            pressTime = value;
            if(HaveFlag( FurnitureFlag.ControlAnim)) anim.Play(Constants.k_AnimEntry, 0, value/meetTime);
        }
    }


    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawLine(CenterPos, CenterPos + Forward);
    }

    public virtual void Look(PlayerController player)
    {

    }


    protected virtual void InOperateUpdate()
    {
        
    }
    /// <summary>
    /// 直接强制交互(以上一个交互者)
    /// </summary>
    public virtual void Operate()
    {
        var user = owner;
        if (HaveFlag(FurnitureFlag.SwitchState))
        {
            inOperate = !inOperate;
            if(!inOperate) owner = null;
        }
        else inOperate = true;
        if (HaveFlag(FurnitureFlag.Disposable)) {
            canOperate = false;
            inOperate = false;
        }
        else if (HaveFlag(FurnitureFlag.Immediately))
        {
            inOperate = false;
        };
        if (HaveFlag(FurnitureFlag.PlayAnim)&&anim)
        {
            anim.enabled = true;
            anim.Play(Constants.k_AnimEntry);
        }
        if (HaveFlag(FurnitureFlag.Speech))
        {
            GlobalEventManager.PlayMeetSoeech(user,SpeechTypeEnum.Responded);
        }
        if (audioOper) PlaySound(audioOper);
        //if (cfg.provideProp) BattleManager.Player.PickUpProp(Instantiate(cfg.provideProp));
        lastOperatetime = Time.time;
        GlobalEventManager.FurnitureOperate(user, this);
    }

    public virtual bool CanOperate(GameObject unit)
    {
        //可操作判断:
        //1.可操作
        //2.没在运行或有切换状态标旗
        return (canOperate && (!inOperate||HaveFlag(FurnitureFlag.SwitchState)));
    }

    /// <summary>
    /// 尝试交互
    /// </summary>
    public bool Handle(GameObject user)
    {
        if (CanOperate(user)) {
            owner = user;
            Operate();
            return true;
        }
        return false;
    }
    public virtual void EndHandle()
    {
        inOperate = false;
        owner = null;
    }


    /// <summary>仅anim使用 </summary>
    protected void CloseAnim()
    {
        anim.enabled = false;
        EndHandle();
    }
    /// <summary>仅anim使用 </summary>
    protected void CloseAnimAble()
    {
        anim.enabled = false;
    }

    protected void PlaySound(AudioClip path)
    {
        AudioManager.PlaySound(new(path, Pos));
    }


    #region 实现
    protected class FurnAction<T> where T: Furniture_Base
    {
        public Action<T> _Start;
        public Action<T> _Operate;
        public Func<T, GameObject, bool> _CanOperate;
        public Action<T> _InOperateUpdate;
        public Action<T> _EndOperate;
    }

    #endregion

}
