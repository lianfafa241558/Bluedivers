using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Game;

namespace FPSGame.AI
{
    /// <summary>
    /// 事件特效项只读访问接口：过渡期让旧内嵌 FxSet 与新 FxSetConfig 走同一套 TriggerFX 逻辑。
    /// </summary>
    public interface IFxSet
    {
        /// <summary>音频剪辑（无音效组时使用）</summary>
        AudioClip Clip { get; }
        /// <summary>音效组（优先于音频剪辑）</summary>
        SoundGroup_SO SoundGroup { get; }
        /// <summary>粒子系统</summary>
        ParticleSystem Particle { get; }
        /// <summary>实例化到命中点的物体</summary>
        GameObject SpawnObject { get; }
        /// <summary>护甲破坏表现</summary>
        IReadOnlyList<ArmorBreakEffect> Effects { get; }
    }

    /// <summary>
    /// 单个事件时机的特效配置（音效组/音频/粒子/挂点/护甲破坏表现），由 EnemyFxData_SO 持有。
    /// 字段名与旧 EnemyControllerFX.FxSet 保持一致，便于迁移工具逐字段拷贝。
    /// </summary>
    [System.Serializable]
    public class FxSetConfig : IFxSet
    {
        /// <summary>音效组（优先）</summary>
        [InspectorName("音效组")]
        public SoundGroup_SO SG;
        /// <summary>音频剪辑（无音效组时使用）</summary>
        [InspectorName("音频剪辑")]
        public AudioClip cilp;
        /// <summary>粒子系统</summary>
        [InspectorName("粒子")]
        public ParticleSystem ps;
        /// <summary>实例化到命中点的物体</summary>
        [InspectorName("挂点物体")]
        public GameObject trans;
        /// <summary>护甲破坏表现列表</summary>
        [InspectorName("护甲破坏表现")]
        public List<ArmorBreakEffect> go = new();

        AudioClip IFxSet.Clip => cilp;
        SoundGroup_SO IFxSet.SoundGroup => SG;
        ParticleSystem IFxSet.Particle => ps;
        GameObject IFxSet.SpawnObject => trans;
        IReadOnlyList<ArmorBreakEffect> IFxSet.Effects => go;
    }
}
