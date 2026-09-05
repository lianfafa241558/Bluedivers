using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>特效触发时机</summary>
    public enum OccasionTypeEnum
    {
        /// <summary>攻击时</summary>
        [InspectorName("攻击时")]
        Attack,
        /// <summary>发现目标</summary>
        [InspectorName("发现目标")]
        DetectedTarget,
        /// <summary>丢失目标</summary>
        [InspectorName("丢失目标")]
        LostTarget,
        /// <summary>受击时</summary>
        [InspectorName("受击时")]
        Hit,
        /// <summary>死亡时</summary>
        [InspectorName("死亡时")]
        Die,
        /// <summary>诞生时</summary>
        [InspectorName("诞生时")]
        Birth,
        /// <summary>移动时</summary>
        [InspectorName("移动时")]
        Movement,
        /// <summary>闲置时</summary>
        [InspectorName("闲置时")]
        Free,
    }
}
