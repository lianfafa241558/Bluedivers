
using System.Collections.Generic;
using Core;
using Core.Interface;
using PEMaths;
using UnityEngine;
using UnityEngine.Events;

namespace GameContract
{
    public interface IVfxEffect
    {
        public void SetOwner(GameObject owner, GameObject weaponRoot, Collider target, Vector3 point);

    }

    public interface I_Damagable
    {
        GameObject gameObject { get; }
        I_Damagable Source { get; }
        float GetArmor(DamageTypeEnum type);
        bool IsExplosionImmunity();

        public void InflictDamage(I_Damagable source, PEInt damage, List<SKVP<DamageTypeEnum, float>> damageGroups,PEInt WeaknessBonus,bool noSource, GameObject damageSource, Vector3 pos);
        GameObject ActorGo { get; }
        bool IsWeakness { get; }
    }   






    public interface I_MissionPoint : I_Entity
    {
        public bool HaveTag(MissionTag tag);

        public float IconSizeScale { get; }
        //public bool IsMain { get;}
        public float AreaRange { get; set; }

    }
    [System.Flags]//flag不能跳位，必须占位符
    public enum MissionTag
    {
        /// <summary>跟随地图缩放</summary>
        [InspectorName("初始暴露")] StratDiscovered = 1 << 0,
        /// <summary>显示一个区域/summary>
        [InspectorName("显示一个区域")] IsArea = 1 << 1,
        /// <summary>跟随地图缩放</summary>
        [InspectorName("跟随地图缩放")] FollowAreaScale = 1 << 2,
        /// <summary>完成时隐藏图标/summary>
        [InspectorName("完成时隐藏图标")] CompleHide = 1 << 3,
        /// <summary>显示边框</summary>
        [InspectorName("显示边框")] DisplayFrame = 1 << 4,
        /// <summary>暴露后不隐藏</summary>
        [InspectorName("暴露后不隐藏")] OneDiscovered = 1 << 5,
        /// <summary>产生热度</summary>
        [InspectorName("产生热度")] HeatPoint = 1 << 6,

        [InspectorName("占位符")] placeholder3 = 1 << 7,
        [InspectorName("占位符")] placeholder4 = 1 << 8,
        /// <summary>完成时不播放音效</summary>
        [InspectorName("不播放音效")] NoAudio = 1 << 9,
        /// <summary>是否隐藏和子任务(在ui层)</summary>
        [InspectorName("是否隐藏和子任务(在ui层)")] hideAll = 1 << 10,
        /// <summary>是否隐藏自身(在uui层)</summary>
        [InspectorName("是否隐藏自身(在ui层)")] hideSelf = 1 << 11,
        /// <summary>显示进度条/summary>
        [InspectorName("显示进度条")] DisplayProgress = 1 << 12,
        /// <summary>激活<summary>
        [InspectorName("激活")] IsActive = 1 << 13,
    }

    public enum UnitTier
    {
        /// <summary>无/summary>
        [InspectorName("无")] None = 0,
        /// <summary>小型单位</summary>
        [InspectorName("小型单位")] Small = 1,
        /// <summary>中型单位</summary>
        [InspectorName("中型单位")] Medium = 2,
        /// <summary>精英单位</summary>
        [InspectorName("精英单位")] Elite = 3,
        /// <summary>重型单位</summary>
        [InspectorName("重型单位")] Heavy = 4,
        /// <summary>巨型单位</summary>
        [InspectorName("巨型单位")] Giant = 5,
        /// <summary>警戒单位</summary>
        [InspectorName("警戒单位")] Alert = 6,
        /// <summary>特殊单位1</summary>
        [InspectorName("特殊单位1")] Special1 = 7,
        /// <summary>特殊单位2</summary>
        [InspectorName("特殊单位2")] Special2 = 8,
        /// <summary>首领</summary>
        [InspectorName("首领")] Boss = 9,
    }

    public interface I_Actor: I_Entity
    {
        //public event UnityAction<I_Actor> OnStateChange;
        public event UnityAction<I_Actor> OnPosChange;
        public event UnityAction<I_Actor> OnAngleChange;
        public event UnityAction OnDeath;

        public int IndexID { get; }

        public UnitTypeEnum Type { get; }
        public IPERange Range { get; }
        public ActorState ActorState { get; set; }

        public I_Actor Owner { get; set; }

        public int Team { get; set; }

        /// <summary>仇恨系数</summary>
        public float Threat { get;}

        public Transform AimPoint { get; }

        public Vector3 HpPos { get; }

        public List<UnitQueryGridNode> GridNodes { get; }
        public I_Damagable[] Damageables { get; }

        /// <summary>是否为地图单位只对EnemyMoble有效</summary>
        bool IsFixed { get; set; }

        public bool HasFlag(ActorFlag flag);

        public void AddFlag(ActorFlag flag);

        public void RemoveFlag(ActorFlag flag);

        public bool Equals(I_Actor obj);

        public int GetHashCode();


        // 转换为布尔值的转换函数
        //public static implicit operator bool(I_Actor obj);
    }
    [System.Serializable]
    public class TargetData
    {
        [SerializeField]
        private Vector3 pos;
        private I_Actor actor;

        //public Vector3 Pos => actor != null && !ReferenceEquals(actor, null) && !actor.Equals(null) ? actor.CenterPos : pos;
        public Vector3 Pos => actor == null ? pos:(actor.CenterPos == default ? pos : actor.CenterPos);

        public I_Actor Actor => actor;
        
        public TargetData()
        {
            pos = Vector3.zero;
            actor = null;
        }
        public void Set(I_Actor entity)
        {
            this.actor = entity;
            pos = entity != null?entity.CenterPos:default;
        }
        public void Set(Vector3 vector)
        {
            pos = vector;
            this.actor = null;
        }

        //public static implicit operator Vector3(TargetData target)=> target.Pos;

        
        /*
        public TargetData(Vector3 vector)
        {
            pos = vector;
            entity = null;
        }
        public TargetData(I_Entity entity)
        {
            pos = entity.CenterPos;
            this.entity = entity;
        }*/

        /*
        public static implicit operator TargetData(Vector3 vector)
        {
            return new TargetData(vector);
        }*/

    }


    public struct UnitQueryGridNode: System.IEquatable<UnitQueryGridNode>
    {
        //与此节点相交的单位，key：teamID
        public Dictionary<UnitTypeEnum, List<I_Actor>> units;

        public PERect rect;
        public int x;
        public int y;

        public UnitQueryGridNode(PERect rect, int x, int y)
        {
            this.rect = rect;
            this.x = x;
            this.y = y;

            units = new();
        }

        public bool IsVaild() => rect.x != 0 && rect.y != 0;

        /// <summary>
        /// 基于x/y坐标判等（IEquatable接口实现，无装箱)        /// </summary>
        public bool Equals(UnitQueryGridNode other)
        {
            // 两个节点坐标相同，即为同一个节
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            // 先判断类型，再调用强类型Equals
            return obj is UnitQueryGridNode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine((int)x * 1000, (int)y * 1000);
        }

    }



    /*
    public interface I_Locatable
    {
        Vector3 Pos { get; }
        Vector3 Angles { get; }
    }*/




}
