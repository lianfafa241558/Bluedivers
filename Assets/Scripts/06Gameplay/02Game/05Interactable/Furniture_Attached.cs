using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Audio;
using FPSGame.Gameplay;

namespace FPSGame.Furn
{


    public class Furniture_Attached : BaseMono , IFurniture
    {
        public static Dictionary<int, IFurniture> list = new();

        private static int nowID = 0;
        private static int GetID => ++nowID;

        public event Action OnOperate;

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

        [DisplayField(DisplayFieldEnum.RunRead)]
        [SerializeField]
        [InspectorName("音频源")]
        private AudioSource _audioSource;


        [Foldout("状态", true)]
         [DisplayField(DisplayFieldEnum.RunRead)]
        public float lastOperatetime;
         [DisplayField(DisplayFieldEnum.RunRead)]
        [SerializeField]
        protected GameObject owner;
         [DisplayField(DisplayFieldEnum.RunRead)]
        [SerializeField]
        protected float time;
         [DisplayField(DisplayFieldEnum.RunRead)]
        [SerializeField]
        protected int count;

        private I_Actor _actor;
        private Vector3 _colliderCenterOffset = Vector3.up;

        public int NumberID { get; private set; }
        
        public virtual string ShowName { get => _actor.ShowName; }
        public virtual string Id { get => _actor.Id; }

        Sprite IFurniture.Portrait => Icon; 

        protected virtual Sprite Icon { get => _actor.Portrait; }

        public bool InOperate => inOperate; 
        /// <summary>
        /// 长按时间。交互控制器按 <c>MeetTime == 0</c> 判定"瞬间操作"（按下即完成，不做长按），
        /// UI(OperationWnd / SubtitleWnd) 也据此显示"按"或"长按"。子类可重写以按情境动态决定。
        /// </summary>
        public virtual float MeetTime=> meetTime; 
        public AudioClip AudioPress=> audioPress;
        public virtual string Desc { get => desc; }

        public override Vector3 CenterPos
        {
            get
            {
                // 防止对象已被销毁后访问（切场景等情况）
                if (this == null || gameObject == null)
                    return Vector3.zero;
                if (_collider) return _collider.bounds.center;
                return transform.position + _colliderCenterOffset;
            }
        }
        [SerializeField]
        private Collider _collider;

        public override Vector3 Forward =>/*Quaternion.Euler(90*ForwardAxis) **/transform.forward;

        public bool HaveFlag(FurnitureFlag flag) => flags.HasFlag(flag);

        public float Press
        {
            get => pressTime;
            set
            {
                pressTime = value;
                if (pressTime > 0 && HaveFlag(FurnitureFlag.ControlAnim) && anim != null)
                    anim.Play(Constants.k_AnimEntry, 0, value / meetTime);
            }
        }

        protected virtual void Awake()
        {
            if(!_collider)_collider = GetComponent<Collider>();
            _audioSource = GetComponent<AudioSource>();
            _actor = GetComponent<I_Actor>();
            if (!anim) anim = GetComponent<Animator>();

            // 缓存碰撞器中心偏移，避免每帧访问bounds.center导致AABB重算抖动
            if (TryGetComponent<Collider>(out var col))
                _colliderCenterOffset = col.bounds.center - transform.position;

            // 首次分配唯一ID
            if (NumberID == 0)
                NumberID = GetID;
        }

        protected virtual void OnEnable()
        {
            list[NumberID] = this;
        }

        protected virtual void OnDisable()
        {
            list.Remove(NumberID);
        }

        private void OnDestroy()
        {
            OnOperate = null;
            list.Remove(NumberID);
        }

        protected virtual void Update()
        {
            if (inOperate) InOperateUpdate();
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
        /// 执行家具交互操作（使用上一次 Handle 时记录的交互者）
        /// </summary>
        public virtual void Operate()
        {
            var user = owner;
            if (HaveFlag(FurnitureFlag.SwitchState))
            {
                inOperate = !inOperate;
                if (!inOperate) owner = null;
            }
            else
            {
                inOperate = true;
            }

            if (HaveFlag(FurnitureFlag.Disposable))
            {
                canOperate = false;
                inOperate = false;
                owner = null;
            }
            else if (HaveFlag(FurnitureFlag.Immediately))
            {
                inOperate = false;
            }

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

        protected void PlaySound(AudioClip clip)
        {
            AudioSvc.PlaySound(new(clip, Pos) { importance = true });
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
