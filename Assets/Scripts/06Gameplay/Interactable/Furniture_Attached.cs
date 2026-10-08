using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using FPSGame.Audio;
using FPSGame.Gameplay;

namespace FPSGame.Gameplay
{


    public class Furniture_Attached : BaseMono , IFurniture
    {
        public static Dictionary<int, IFurniture> list = new();

        /// <summary>同步键 → 家具 的**跨端稳定**索引（键 = <see cref="SyncId"/>）。联机同步用。</summary>
        public static readonly Dictionary<int, Furniture_Attached> listBySyncId = new();

        private static int nowID = 0;
        private static int GetID => ++nowID;

        /// <summary>
        /// 跨端稳定的同步键 = <c>FNV1a(身份Id + 世界坐标 0.1m 量化)</c>，**创建时算一次并缓存**。
        /// <para>▍为什么不能只用 <c>Id</c>：同类家具存在多个实例（同 Id 多份）。</para>
        /// <para>▍为什么不每帧重算：家具会位移，键必须稳定。</para>
        /// <para>▍为什么不用 <c>NumberID</c>：那是各端静态自增且从不复位（同 <c>KeyScreenControl</c> 的结论）。</para>
        /// <para>▍量化到 0.1m 是为了吃掉两端浮点抖动；位置来自场景 / 已同步的生成落点 ⇒ 跨端一致。</para>
        /// </summary>
        public int SyncId { get; private set; }

        public event Action OnOperate;

        [Foldout("配置", true)]
        /// <summary>身份源（Actor 或 BaseObject）。留空则自动在本物体、再在本物体子树查找。</summary>
        [SerializeField]
        [InspectorName("身份源")]
        protected MonoBehaviour identitySource;

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

        #region 家具通用配置（2026-10-02 由 Furniture_Base 上提，身份字段已删除）
        [Foldout("关联", true)]
        [SerializeField]
        public Transform relatedTrans;
        [SerializeField]
        protected Transform relatedTrans2;
        [InspectorName("外部浮点数参数")]
        public float ExtFloatParameter;

        [DisplayField(DisplayFieldEnum.RunRead)]
        [SerializeField]
        protected ParticleSystem particle;
        [DisplayField(DisplayFieldEnum.RunRead)]
        [SerializeField]
        protected NavMeshObstacle obs;
        #endregion


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



        /// <summary>身份提供者（Actor 或 BaseObject）：名称/ID/头像/颜色 全工程只存这一份。</summary>
        protected IEntity Identity { get; private set; }

        private Vector3 _colliderCenterOffset = Vector3.up;

        public int NumberID { get; private set; }
        
        public virtual string ShowName => Identity.IsValidMono() ? Identity.ShowName : string.Empty;
        public virtual string Id => Identity.IsValidMono() ? Identity.Id : string.Empty;

        Sprite IFurniture.Portrait => Icon; 

        protected virtual Sprite Icon => Identity.IsValidMono() ? Identity.Portrait : null;

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
            if (!anim) anim = GetComponent<Animator>();

            // 家具通用关联件（2026-10-02 由 Furniture_Base 上提到本类）
            particle = GetComponentInChildren<ParticleSystem>(true);
            obs = GetComponent<NavMeshObstacle>();

            // 缓存碰撞器中心偏移，避免每帧访问bounds.center导致AABB重算抖动
            if (TryGetComponent<Collider>(out var col))
                _colliderCenterOffset = col.bounds.center - transform.position;

            ResolveIdentity();

            // 首次分配唯一ID
            if (NumberID == 0)
                NumberID = GetID;

            ComputeSyncId();
        }

        /// <summary>算一次 <see cref="SyncId"/>（必须在 <see cref="ResolveIdentity"/> 之后，因为要用到 <see cref="Id"/>）。</summary>
        private void ComputeSyncId()
        {
            Vector3 p = transform.position;
            int qx = Mathf.RoundToInt(p.x * 10f);
            int qy = Mathf.RoundToInt(p.y * 10f);
            int qz = Mathf.RoundToInt(p.z * 10f);
            SyncId = Fnv1a($"{Id}|{qx}|{qy}|{qz}");
        }

        /// <summary>FNV-1a 32 位（零分配、跨端稳定；与任务指纹同族做法）。</summary>
        private static int Fnv1a(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; ++i)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return (int)h;
            }
        }

        /// <summary>【联机】按 <see cref="SyncId"/> 找本地家具（找不到 = 本端没这个家具）。</summary>
        public static Furniture_Attached FindBySyncId(int syncId)
        {
            return listBySyncId.TryGetValue(syncId, out Furniture_Attached f) ? f : null;
        }

        /// <summary>
        /// 解析身份源：显式引用 &gt; 本物体上的 IEntity &gt; 子树中的 IEntity。
        /// <para>家具自身不再保存名称/头像等身份数据（否则与 Actor/BaseObject 形成两份数据）。</para>
        /// </summary>
        private void ResolveIdentity()
        {
            if (identitySource != null) Identity = identitySource as IEntity;
            if (Identity == null) Identity = GetComponent<IEntity>();
            if (Identity == null) Identity = GetComponentInChildren<IEntity>(true);

            if (!Identity.IsValidMono())
                Debug.LogError($"[家具] {name} 缺少身份组件（Actor 或 BaseObject）", this);
        }

        protected virtual void OnEnable()
        {
            list[NumberID] = this;
            if (SyncId != 0) listBySyncId[SyncId] = this;
        }

        protected virtual void OnDisable()
        {
            list.Remove(NumberID);
            UnregisterSyncId();
        }

        private void OnDestroy()
        {
            OnOperate = null;
            list.Remove(NumberID);
            UnregisterSyncId();
        }

        /// <summary>摘掉同步索引 —— ⚠ 只在"当前登记的就是我"时删，避免同键的另一份实例被误删。</summary>
        private void UnregisterSyncId()
        {
            if (SyncId == 0) return;
            if (listBySyncId.TryGetValue(SyncId, out Furniture_Attached cur) && ReferenceEquals(cur, this))
                listBySyncId.Remove(SyncId);
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
                GlobalEventBus.PlayMeetSpeech(user, SpeechTypeEnum.Responded);
            }

            if (audioOper) PlaySound(audioOper);
            lastOperatetime = Time.time;
            GlobalEventBus.FurnitureOperate(user, this);
            OnOperate?.Invoke();
        }

        /// <summary>
        /// 【联机】远端重放一次交互：把操作者设成**远端的那个单位**，再走同一条 <see cref="Operate"/> 链。
        ///
        /// <para>▍为什么"重放本地逻辑"而不是"只派发事件"：共享状态的推进（<c>TaskState</c> / 谜题进度 /
        /// 物件消失 / 欧帕兹计数）都写在各自的 <c>Operate</c> 覆写里 ⇒ 只有真跑一遍，两端状态才会自然一致
        /// （所以 <c>OnOOPartCollect</c>/<c>OnSubmitOOPart</c>/<c>OnKeiSubmit</c> 都不需要单独同步）。</para>
        ///
        /// <para>▍⚠ 操作者必须传**远端单位**（该端的盟友实例；解析不到传 null），**绝不能传本机玩家**：
        /// <c>PlayerInputHandler.OnOperation</c> / <c>PlayerWeaponsManager.OnOperation</c> 靠 <c>user == gameObject</c>
        /// 判定"是不是我在操作" ⇒ 传远端单位它们自然早退，不会误动本机玩家。</para>
        ///
        /// <para>▍⚠ "给操作者个人收益"的部分在远端会因拿不到对应组件而自然跳过
        /// （例：<c>OOPart</c> 走 <c>owner.TryGetComponent&lt;PlayerOOPartInventory&gt;</c>）；仍需逐类复核。</para>
        /// </summary>
        public void ApplyRemoteOperate(GameObject remoteUser)
        {
            owner = remoteUser;
            Operate();
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
