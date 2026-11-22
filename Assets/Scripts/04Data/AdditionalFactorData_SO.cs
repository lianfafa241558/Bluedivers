using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.Core;
using UnityEngine;

namespace FPSGame.Data
{
    /// <summary>
    /// 附加因素配置：一种"本局额外压到某个任务点上"的敌方阵营（喷射旅 / 焚化兵团 …）。
    ///
    /// <para>▍谁派它：<c>TaskManager.CreatAllTask</c> —— 每张图从 <c>MapData_SO.additionalFactors</c>
    /// 里取出枚举，随机摊到本图**已启用**的任务点上（不同枚举可叠同一点，同一枚举只派一次），
    /// 结果落在 <c>TaskCfg.additionalFactors</c>，选图界面按它点亮任务标记上的附加图标。</para>
    ///
    /// <para>▍为什么按枚举建索引（与 <see cref="CampData_SO"/> 同口径）：地图只存轻量枚举，
    /// 名字/图标/描述都在资产里，改文案不用动地图资产。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "new Data", menuName = "Data/附加因素")]
    public class AdditionalFactorData_SO : ScriptableObject
    {
        /// <summary>类型</summary>
        [Foldout("基本", true)]
        [InspectorName("类型")]
        public AdditionalFactorType type;

        /// <summary>名称</summary>
        [InspectorName("名称")]
        public string ShowName;

        /// <summary>图标</summary>
        [SpritePreview(4, 3)]
        [InspectorName("图标")]
        public Sprite Sprite;

        /// <summary>颜色（任务标记上的附加图标 + 右侧详情条染色）</summary>
        [InspectorName("颜色")]
        public Color Color;

        /// <summary>描述</summary>
        [TextArea(3, 5)]
        [InspectorName("描述")]
        public string Desc;

        /// <summary>波次模板（与 <see cref="CampData_SO.CampTemplate.template"/> 同一套 TierCfg）。
        /// <para>▍复用 <see cref="CampData_SO.TierCfg"/> 而不是另抄一份：绘制器
        /// <c>TierCfgDrawer</c> 按类型绑定，抄一份就得多养一个 Drawer。</para></summary>
        [Foldout("波次", true)]
        [InspectorName("波次模板")]
        public List<CampData_SO.TierCfg> template;
    }
}
