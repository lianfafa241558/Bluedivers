using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Game;

namespace FPSGame.AI
{
    /// <summary>
    /// 单个事件时机的特效配置（音效组/音频/粒子/挂点/护甲破坏表现），由 EnemyFxData_SO.fxDic 持有。
    /// 清理完成后的正式形态：过渡期 IFxSet 已随旧 FxSet 一并移除，直接消费本类字段。
    /// </summary>
    [System.Serializable]
    public class FxSetConfig
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
    }
}
