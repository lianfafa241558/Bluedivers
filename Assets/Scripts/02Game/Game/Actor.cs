using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using PEMaths;
using Core;
using GameContract;
using UnityEngine.AI;
using System.Collections;
using FPSGame.Attribute;

namespace Unity.FPS.Game
{
    //此类包含描述单位(actor)（玩家或敌人）的一般信息
    //它主要用于AI检测逻辑，并确定参与者是朋友还是敌人
    public class Actor : BaseObject, I_Actor
    {
        private static int GlobalIndexID = 0;
        //public event UnityAction<I_Actor> OnStateChange;
        public event UnityAction<I_Actor> OnPosChange;
        public event UnityAction<I_Actor> OnAngleChange;
        
        public event UnityAction OnDeath;

        #region 接口
        /*
        public new string ShowName { get => base.ShowName; set => base.ShowName = value; }
        public new string Id { get => base.Id; set => base.Id = value; }
        public new Sprite Portrait { get => base.Portrait; set => base.Portrait = value; }
        public new Sprite ExtraPortrait { get => base.ExtraPortrait; set => base.ExtraPortrait = value; }
        
        public new Color Color
        {
            get => base.Color;
            set => base.Color = value;
        }*/


        public int IndexID => indexID;
        private int indexID;

        public ActorState ActorState { 
            get => actorState;
            set{
                actorState = value;
                //OnStateChange?.Invoke(this);
            } 
        }
        [SerializeField]
        [InspectorName("状态")]
        private ActorState actorState = ActorState.Normal;
        public UnitTypeEnum Type => type;
        public IPERange Range=> range;

        bool I_Actor.IsFixed{
            get=>IsFixed;
            set =>IsFixed = value; 
        }

        public override float HalfRange => rangeLength;

        public int Team { get => team; set => team=value; }

        List<UnitQueryGridNode> I_Actor.GridNodes => curQueryGridNodes;

        public Transform AimPoint=> aimPoint;

        public I_Damagable[] Damageables => damageables;

        public float Threat => threat;

        public override Vector3 CenterPos {
            get {
                if (this == null) return default;
                return AimPoint != null ? AimPoint.position : base.CenterPos;
            }
        }

        public Vector3 HpPos => AimPoint.position + Vector3.up * HpHeight;

        //I_Actor I_Actor.Owner => this.Owner;

        public bool HasFlag(ActorFlag flag) => this.flag.HasFlag(flag);


        public void AddFlag(ActorFlag flag) => this.flag|=flag;

        public void RemoveFlag(ActorFlag flag) => this.flag &= ~flag;



        /// <summary>是否为固定单位(只对EnemyMoble有效)</summary>
        [InspectorName("固定单位(只对EnemyMoble有效)")]
        public bool IsFixed;

        public bool Equals(I_Actor obj) => (obj!=null)&&obj.IndexID == IndexID;

        public override bool Equals(object other)=>this == other as Actor;

        public override int GetHashCode()
        {
            return indexID.GetHashCode();
        }

        // 重载 == 运算符
        public static bool operator == (Actor left, Actor right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;
            return left.Equals(right);
        }

        // 重载 != 运算符
        public static bool operator !=(Actor left, Actor right)
        {
            return !(left == right);
        }

        public void AddTag(ActorFlag tagToAdd)
        {
            flag |= tagToAdd;
        }

        public void RemoveTag(ActorFlag tagToRemove)
        {
            flag &= ~tagToRemove;
        }

        #endregion

        #region 属性
        [SerializeField]
        [InspectorName("标识")]
        private ActorFlag flag;

        [InspectorName("类型")]
        [SerializeField]
        private UnitTypeEnum type;

        [SerializeField]
        [InspectorName("队伍")]
        private int team;

        [SerializeField]
        [InspectorName("仇恨系数")]
        private float threat = 1;

   
        [SerializeField]
        [InspectorName("瞄准点")]
        private Transform aimPoint;

        [InspectorName("显示血条")]
        public bool UseHpBar = true;
        [InspectorName("血条的额外高度")]
        [Compare("UseHpBar",1,CompareOperate.Equal)]
        public float HpHeight=1;


        #region 逻辑碰撞
        [Foldout("逻辑碰撞", true)]
        public ShapeType shape= ShapeType.Circle;
        [SerializeField]
        [InspectorName("半径/半长度")]
        private float rangeLength;

        //[DisplayField]
        //[InspectorName("召唤者")]
        public I_Actor Owner { get; set; }

        private Vector3 lastAngle;
        private IPERange range;
        public List<UnitQueryGridNode> curQueryGridNodes = new List<UnitQueryGridNode>();
        [HideInInspector]
        public Damageable[] damageables;

        #endregion

        #endregion

        private bool isInitialized;

        private void Awake()
        {
            indexID = GlobalIndexID++;
            switch (shape)
            {
                case ShapeType.Circle:
                    range = new PECircle(LogicPos, (PEInt)rangeLength);
                    break;
                case ShapeType.Rectangle:
                    range = new PERect(LogicPos, (PEInt)rangeLength, (PEInt)rangeLength);
                    break;
            }
            if (!ActorsManager.Actors.Contains(this))
            {
                ActorsManager.Actors.Add(this);
            }
            var m_Health = GetComponent<Health>();
            if (m_Health)
            {
                m_Health.OnDie += OnDie;
                m_Health.OnRevive += OnRevive;
            }

            damageables = GetComponentsInChildren<Damageable>();

            Range.SetXY((PEVector2)Pos);
            lastAngle = transform.eulerAngles;
            BattleEventSub.UnitPosChange(this);
            StartCoroutine(WaitSetPos());
        }

      
        private IEnumerator WaitSetPos()
        {
            while(!FpsHelper.IsMainStage())
            {
                yield return null;
            }
            //Debug.Log("创建了单位" + this.gameObject, this.gameObject);
            switch (type)
            {
                case UnitTypeEnum.Enemy:
                    BattleEventSub.EnemyCreate(this);
                    break;
                case UnitTypeEnum.Player:
                    //Debug.Log("创建了玩家" + this.gameObject, this.gameObject);
                    GlobalEventSub.PlayerCreate(this);
                    break;
                case UnitTypeEnum.Friend:
                    GlobalEventSub.FriendCreate(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    //Debug.LogError("特殊单位出生" + ShowName, this);
                    if (!HasFlag(ActorFlag.Unimportant)) BattleEventSub.SpecUnitCreate(this);
                    //Debug.LogError("创建特殊单位"+ShowName);
                    break;
                case UnitTypeEnum.Other:
                    if (HasFlag(ActorFlag.AutoRegister)) BattleEventSub.SpecUnitCreate(this);
                    break;
            }
            isInitialized = true;


            if (!HasFlag(ActorFlag.AllowFloating))
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 100, UnityEngine.AI.NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                }
                //transform.position = TerrainUtils.WSToTS(transform.position);
            }
            Range.SetXY(LogicPos);
            BattleEventSub.UnitPosChange(this);
            OnPosChange?.Invoke(this);

        }


        void OnRevive()
        {
            ActorState = ActorState.Normal;
            switch (type)
            {
                case UnitTypeEnum.Player:
                    BattleEventSub.PlayerRevive(this);
                    break;
                case UnitTypeEnum.Friend:
                    //GlobalEventManager.FriendDead(this);
                    break;
                case UnitTypeEnum.Enemy:
                    //GlobalEventManager.EnemyDead(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    //GlobalEventManager.SpecUnitDead(this);
                    break;
                case UnitTypeEnum.Other:
                    //if (HasFlag(ActorFlag.AutoRegister)) GlobalEventManager.SpecUnitDead(this);
                    break;
            }
        }
        void OnDie(GameObject source)
        {
            //Debug.LogError("单位死亡"+gameObject,gameObject);
            BattleEventSub.UnitDeath(this);
            OnDeath?.Invoke();
            ActorState = ActorState.Dead;
            var m_Health = GetComponent<Health>();
            if (m_Health&&m_Health.CurrentHealth>0)
            {
                Debug.LogError($"[Health] 单位死亡时生命值>0！CurrentHealth={m_Health.CurrentHealth.RawFloat}, MaxHealth={m_Health.MaxHealth}", gameObject);
            }
            if (source.IsValid()) BattleEventSub.UnitKill(source.GetComponent<Actor>(),this);
            switch (type)
            {
                case UnitTypeEnum.Player:
                    BattleEventSub.PlayerDead(this);
                    //StartCoroutine(WaitSetPos());//防止死天上
                    break;
                case UnitTypeEnum.Friend:
                    BattleEventSub.FriendDead(this);
                    break;
                case UnitTypeEnum.Enemy:
                    BattleEventSub.EnemyDead(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    BattleEventSub.SpecUnitDead(this);
                    break;
                case UnitTypeEnum.Other:
                    if (HasFlag(ActorFlag.AutoRegister)) BattleEventSub.SpecUnitDead(this);
                    break;
            }
            //Debug.LogError("剩余的移动回调"+ OnPosChange, gameObject);
        }

        void OnDestroy()
        {
            /*
            if (ActorState != ActorState.Dead)
            {
                Debug.LogError("非正常死亡"+gameObject,gameObject);
                OnStateChange?.Invoke(ActorState.Dead);
            }*/
            //Debug.LogError("单位被移除"+gameObject);
            //OnStateChange = null;
            OnDeath = null;
            OnPosChange = null;
            OnAngleChange = null;
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (Range.GetXY() != LogicPos)
            {
                Range.SetXY(LogicPos);
                BattleEventSub.UnitPosChange(this);
                OnPosChange?.Invoke(this);
            }
            if (lastAngle!= transform.eulerAngles)
            {
                lastAngle = transform.eulerAngles;
                OnAngleChange?.Invoke(this);
            }
        }


        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(CenterPos, rangeLength);
        }





        /*
/// <summary> 转向目标(需要每帧调调整</summary>
/// <param name="lookPosition"></param>
public void OrientTowards(Vector3 lookPosition,float speed)
{
   //计算两者的差值得到方向，投影??x,z)平面上得到y轴旋转方??
   Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - transform.position, Vector3.up).normalized;
   if (lookDirection.sqrMagnitude != 0f)
   {
       Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
       transform.rotation =
           Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
   }
}
*/
    }


}