using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.BaseTool;
using PEMaths;
using Core;
using GameContract;

namespace Unity.FPS.Game
{
    //此类包含描述单位(actor)（玩家或敌人）的一般信息。
    //它主要用于AI检测逻辑，并确定参与者是朋友还是敌人
    public class Actor : BaseObject, I_Actor
    {
        private static int GlobalIndexID = 0;
        //public event UnityAction<I_Actor> OnStateChange;
        public event UnityAction<I_Actor> OnPosChange;
        public event UnityAction<I_Actor> OnAngleChange;
        /// <summary>
        /// 这个死亡是倒计时结束的死亡
        /// </summary>
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
        [CustomLabel("状态")]
        private ActorState actorState = ActorState.Normal;
        public UnitTypeEnum Type => type;
        public IPERange Range=> range;

        public override float HalfRange => rangeLength;

        public int Team { get => team; set => team=value; }

        List<UnitQueryGridNode> I_Actor.GridNodes => curQueryGridNodes;

        public Transform AimPoint=> aimPoint;

        public I_Damagable[] Damageables => damageables;

        public float Threat => threat;

        public override Vector3 CenterPos => AimPoint?AimPoint.position: base.CenterPos;

        public Vector3 HpPos => AimPoint.position + Vector3.up * HpHeight;


        public bool HasFlag(ActorFlag flag) => this.flag.HasFlag(flag);

        public bool Equals(I_Actor obj) => (obj!=null)&&obj.IndexID == IndexID;

        public override bool Equals(object other)=>this == other as Actor;

        public override int GetHashCode()
        {
            return indexID.GetHashCode();
        }

        // 重载 == 操作符
        public static bool operator == (Actor left, Actor right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;
            return left.Equals(right);
        }

        // 重载 != 操作符
        public static bool operator !=(Actor left, Actor right)
        {
            return !(left == right);
        }


        #endregion

        #region 属性
        [SerializeField]
        [CustomLabel("标旗")]
        private ActorFlag flag;

        [CustomLabel("类型")]
        [SerializeField]
        private UnitTypeEnum type;

        [SerializeField]
        [CustomLabel("队伍")]
        private int team;

        [SerializeField]
        [CustomLabel("仇恨系数")]
        private float threat = 1;

        [NullCheck]
        [CustomLabel("瞄准点")]
        public Transform aimPoint;

        [CustomLabel("显示血条")]
        public bool UseHpBar = true;
        [CustomLabel("血条的额外高度", "UseHpBar",1,CompareOperate.Equal)]
        public float HpHeight=1;


        #region 逻辑碰撞
        [Foldout("逻辑碰撞", true)]
        public ShapeType shape= ShapeType.Circle;
        [SerializeField]
        [CustomLabel("半径/半长度")]
        private float rangeLength;

        [DisplayField]
        [CustomLabel("召唤者")]
        public I_Actor Owner;

        private Vector3 lastAngle;
        private IPERange range;
        public List<UnitQueryGridNode> curQueryGridNodes = new List<UnitQueryGridNode>();
        [HideInInspector]
        public Damageable[] damageables;

        #endregion

        #endregion


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
            if(m_Health) m_Health.OnDie += OnDie;

            damageables = GetComponentsInChildren<Damageable>();


#if UNITY_EDITOR
            if (GameRoot.Instance.IsLocal)
            {
                Invoke("Init", Time.fixedDeltaTime*2);
                //Init();
            }
            else
            {
                Init();
            }
#else
            OnStart();
#endif
        }

        void Init()
        {
            if(!HasFlag(ActorFlag.AllowFloating)) transform.position = TerrainUtils.WSToTS(transform.position);
            Range.SetXY((PEVector2)Pos);
            lastAngle = transform.eulerAngles;
            GlobalEventManager.UnitPosChange(this);
            switch (type)
            {
                case UnitTypeEnum.Enemy:
                    GlobalEventManager.EnemyCreate(this);
                    break;
                case UnitTypeEnum.Player:
                    GlobalEventManager.PlayerCreate(this);
                    break;
                case UnitTypeEnum.Friend:
                    GlobalEventManager.FriendCreate(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    GlobalEventManager.SpecUnitCreate(this);
                    //Debug.LogError("创建特殊单位"+ShowName);
                    break;
                case UnitTypeEnum.Other:
                    if (HasFlag(ActorFlag.AutoRegister)) GlobalEventManager.SpecUnitCreate(this);
                    break;
            }
        }

        void OnDie(GameObject source)
        {
            GlobalEventManager.UnitDeath(this);
            ActorState = ActorState.Dead;
            if(source.IsValid()) GlobalEventManager.UnitKill(source.GetComponent<Actor>(),this);
            switch (type)
            {
                case UnitTypeEnum.Player:
                    GlobalEventManager.PlayerDead(this);
                    break;
                case UnitTypeEnum.Friend:
                    GlobalEventManager.FriendDead(this);
                    break;
                case UnitTypeEnum.Enemy:
                    GlobalEventManager.EnemyDead(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    GlobalEventManager.SpecUnitDead(this);
                    break;
                case UnitTypeEnum.Other:
                    if (HasFlag(ActorFlag.AutoRegister)) GlobalEventManager.SpecUnitDead(this);
                    break;
            }

        }

        void OnDestroy()
        {
            /*
            if (ActorState != ActorState.Dead)
            {
                Debug.LogError("非正常死亡"+gameObject,gameObject);
                OnStateChange?.Invoke(ActorState.Dead);
            }*/
            OnDeath?.Invoke();
            //OnStateChange = null;
            OnPosChange = null;
            OnAngleChange = null;
        }

        private void FixedUpdate()
        {
            if (Range.GetXY() != LogicPos)
            {
                Range.SetXY(LogicPos);
                GlobalEventManager.UnitPosChange(this);
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
/// <summary> 转向目标(需要每帧调用)</summary>
/// <param name="lookPosition"></param>
public void OrientTowards(Vector3 lookPosition,float speed)
{
   //计算两者的差值得到方向，投影在(x,z)平面上得到y轴旋转方向
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