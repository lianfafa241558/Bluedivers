using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using PEMaths;
using UnityEngine;
using UnityEngine.Events;

namespace FPSGame.GameContract
{
    public interface IActor : IEntity
    {
        //public event UnityAction<I_Actor> OnStateChange;
        public event UnityAction<IActor> OnPosChange;
        public event UnityAction<IActor> OnAngleChange;
        public event UnityAction OnDeath;

        public int IndexID { get; }

        public UnitTypeEnum Type { get; }
        public IPERange Range { get; }
        public ActorState ActorState { get; set; }

        public IActor Owner { get; set; }

        public int Team { get; set; }

        /// <summary>仇恨系数</summary>
        public float Threat { get; }

        public Transform AimPoint { get; }

        public Vector3 HpPos { get; }

        public List<UnitQueryGridNode> GridNodes { get; }

        public IDamageable MainDamageable { get; }
        public IDamageable[] Damageables { get; }

        /// <summary>是否为地图单位只对EnemyMoble有效</summary>
        bool IsFixed { get; set; }

        public bool HasFlag(ActorFlag flag);

        public void AddFlag(ActorFlag flag);

        public void RemoveFlag(ActorFlag flag);

        public bool Equals(IActor obj);

        public int GetHashCode();



        // 转换为布尔值的转换函数
        //public static implicit operator bool(I_Actor obj);
    }
}
