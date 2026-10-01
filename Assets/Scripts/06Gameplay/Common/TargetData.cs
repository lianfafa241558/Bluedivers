using FPSGame.Core.Interface;
using UnityEngine;
using FPSGame.GameContract;

namespace FPSGame.Gameplay
{
    [System.Serializable]
    public class TargetData
    {
        [SerializeField] private Vector3 pos;
        private IActor actor;
#if UNITY_EDITOR
        [SerializeField] private GameObject show;
#endif
        //public Vector3 Pos => actor != null && !ReferenceEquals(actor, null) && !actor.Equals(null) ? actor.CenterPos : pos;
        public Vector3 Pos => actor == null ? pos : (actor.CenterPos == default ? pos : actor.CenterPos);

        public IActor Actor => actor;

        public TargetData()
        {
            pos = Vector3.zero;
            actor = null;
        }
        public void Set(IActor entity)
        {
            this.actor = entity;
            pos = entity.IsValidMono() ? entity.CenterPos : default;
#if UNITY_EDITOR
            show = actor.IsValidMono() ? actor.gameObject:null;
#endif
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
}
