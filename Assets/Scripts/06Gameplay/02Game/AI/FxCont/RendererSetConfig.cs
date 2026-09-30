using System;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>MPB 变化类型</summary>
    public enum MPBTypeEnum
    {
        /// <summary>触发式：事件后按渐变在 duration 内闪变</summary>
        Trigger,
        /// <summary>切换式：两种时机间 2 秒内从 defaultColor 渐变到 switchColor</summary>
        Switch,
    }

    /// <summary>
    /// 单组 MPB 颜色变化的纯配置，由 EnemyFxData_SO 共享持有。
    /// 运行时由 EnemyControllerFX 据此构建实例私有的运行态 RendererSet，不随 prefab 实例复制。
    /// </summary>
    [Serializable]
    public class RendererSetConfig
    {
        /// <summary>目标材质：Init 时按 sharedMaterials[i] == material 匹配渲染器槽位</summary>
        [InspectorName("目标材质")]
        public Material material;
        /// <summary>MPB 变化类型</summary>
        [InspectorName("变化类型")]
        public MPBTypeEnum type;
        /// <summary>触发/起始时机</summary>
        [InspectorName("触发时机")]
        public OccasionTypeEnum occasion;
        /// <summary>颜色属性名（留空自动兜底 _HitColor/_EmissionColor）</summary>
        [InspectorName("属性名")]
        public string colorName;

        // 切换式用
        [InspectorName("默认色")]
        [ColorUsage(true, true)]
        public Color defaultColor;
        [InspectorName("切换时机")]
        public OccasionTypeEnum switchOccasion;
        [InspectorName("切换色")]
        [ColorUsage(true, true)]
        public Color switchColor;

        // 触发式用
        [InspectorName("渐变")]
        [GradientUsage(true)]
        public Gradient gradient;
        [InspectorName("持续时间")]
        public float duration = 0.1f;
    }
}
