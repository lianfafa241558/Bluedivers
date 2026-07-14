using System;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;

namespace FPSGame.Furn
{
    [Flags]
    public enum FurnitureFlag
    {
        /// <summary>自动操作</summary>
        [InspectorName("自动操作")] AutoOperate = 1 << 0,
        /// <summary>切换状态</summary>
        [InspectorName("切换状态")] SwitchState = 1 << 1,
        /// <summary>瞬间完成操作</summary>
        [InspectorName("瞬间完成操作")] Immediately = 1 << 2,
        /// <summary>一次性</summary>
        [InspectorName("一次性")] Disposable = 1 << 3,
        /// <summary>(完成操作时播放动画</summary>
        [InspectorName("播放动画")] PlayAnim = 1 << 4,
        /// <summary>喊话</summary>
        [InspectorName("喊话")] Speech = 1 << 5,
        /// <summary>离开保留进度</summary>
        [InspectorName("保留进度")] KeepPress = 1 << 6,
        /// <summary>长按时控制动画</summary>
        [InspectorName("长按时控制动画")] ControlAnim = 1 << 7,
        /// <summary>任意角度</summary>
        [InspectorName("任意角度")] AnyAngle = 1 << 8,
    }

    public interface IFurniture
    {
        float Press { get; set; }

        float MeetTime { get; }
        bool InOperate { get; }
        AudioClip AudioPress { get; }
        string ShowName { get; }
        string Id { get; }
        int NumberID { get; }
        string Desc { get; 
        }
        Sprite Portrait { get; }
        Vector3 CenterPos { get; }
        Vector3 Forward { get; }

        GameObject gameObject { get; }

        void Operate();
        bool CanOperate(GameObject unit);
        bool Handle(GameObject user);

        bool HaveFlag(FurnitureFlag flag);
    }


    public class Furniture_Attached : BaseMono , IFurniture
    {
        public static Dictionary<int, IFurniture> list = new();


        private static int nowID = 0;
        private static int GetID => ++nowID;

        public Action OnOperate;

        [Foldout("配置", true)]

        [InspectorName("长按时间")]
        public float meetTime;
        [InspectorName("长按音效")]
        [Compare("meetTime", 0, CompareOperate.Greater)]
        public AudioClip audioPress;

        [InspectorName("开启音效")]
        public AudioClip audioOper;

        [InspectorName("关闭音效")]
        public AudioClip audioClose;


        [SerializeField]
        [InspectorName("标旗")]
        protected FurnitureFlag flags;


        [SerializeField]
        [InspectorName("已按时间")]
        [DisplayField]
        protected float pressTime;

        [InspectorName("可以操作")]
        public bool canOperate = true;
        [InspectorName("正在运行")]
        public bool inOperate;
        [SerializeField]
        protected string desc = "进行交互";

      
        public Animator anim;

        [DisplayField(true, false)]
        public new AudioSource audio;


        [Foldout("状态", true)]
        [DisplayField(true, false)]
        public float lastOperatetime;
        [DisplayField(true, false)]
        [SerializeField]
        protected GameObject owner;
        [DisplayField(true, false)]
        [SerializeField]
        protected float time;
        [DisplayField(true, false)]
        [SerializeField]
        protected int count;

        public int NumberID { get; private set; }
        
        public virtual string ShowName { get => GetComponent<I_Actor>().ShowName; }
        public virtual string Id { get => GetComponent<I_Actor>().Id; }

        Sprite IFurniture.Portrait => Icon; 

        protected virtual Sprite Icon { get => GetComponent<I_Actor>().Portrait; }

        public bool InOperate => inOperate; 
        public float MeetTime=> meetTime; 
        public AudioClip AudioPress=> audioPress;
        public virtual string Desc { get => desc; }

        public override Vector3 CenterPos
        {
            get
            {
                if (TryGetComponent<Collider>(out var collider))
                    return collider.bounds.center;
                else
                    return transform.position + Vector3.up;
            }
        }

        public override Vector3 Forward =>/*Quaternion.Euler(90*ForwardAxis) **/transform.forward;

        public bool HaveFlag(FurnitureFlag flag) => flags.HasFlag(flag);

        public float Press
        {
            get => pressTime;
            set
            {
                pressTime = value;
                if (pressTime > 0 && HaveFlag(FurnitureFlag.ControlAnim))
                    anim.Play(Constants.k_AnimEntry, 0, value / meetTime);
            }
        }

        protected virtual void Awake()
        {
            audio = GetComponent<AudioSource>();
            if (!anim) anim = GetComponent<Animator>();
        }
        protected virtual void Start()
        {
            list.Add(NumberID = GetID, this);
        }


        private void OnDestroy()
        {
            OnOperate = null;
            list.Remove(NumberID);
        }

        protected virtual void Update()
        {
            if (inOperate) InOperateUpdate();
            if (HaveFlag(FurnitureFlag.ControlAnim))
            {

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
        /// 直接强制交互(以上一个交互者
        /// </summary>
        public virtual void Operate()
        {
            var user = owner;
            if (HaveFlag(FurnitureFlag.SwitchState))
            {
                inOperate = !inOperate;
                if (!inOperate) owner = null;
            }
            else inOperate = true;
            if (HaveFlag(FurnitureFlag.Disposable))
            {
                canOperate = false;
                inOperate = false;
            }
            else if (HaveFlag(FurnitureFlag.Immediately))
            {
                inOperate = false;
            }
            ;
            if (HaveFlag(FurnitureFlag.PlayAnim) && anim)
            {
                anim.enabled = true;
                anim.Play(Constants.k_AnimEntry);
            }
            if (HaveFlag(FurnitureFlag.Speech))
            {
                GlobalEventSub.PlayMeetSpeech(user, SpeechTypeEnum.Responded);
            }
            if (audioOper) PlaySound(audioOper);
            //if (cfg.provideProp) BattleManager.Player.PickUpProp(Instantiate(cfg.provideProp));
            lastOperatetime = Time.time;
            GlobalEventSub.FurnitureOperate(user, this);
            OnOperate?.Invoke();
        }

        public virtual bool CanOperate(GameObject unit)
        {
            //可操作判断
            //1.可操作
            //2.没在运行或有切换状态标志
            return (canOperate && (!inOperate || HaveFlag(FurnitureFlag.SwitchState)));
        }


        /// <summary>
        /// 尝试交互
        /// </summary>
        public bool Handle(GameObject user)
        {
            if (CanOperate(user))
            {
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
            AudioSvc.PlaySound(new(path, Pos) { importance = true });
        }


        #region 实现
        protected struct FurnAction<T> where T : IFurniture
        {
            public Action<T> _Start;
            public Action<T> _Operate;
            public Func<T, GameObject, bool> _CanOperate;
            public Action<T> _InOperateUpdate;
            public Action<T> _EndOperate;
        }
        #endregion

    }
}