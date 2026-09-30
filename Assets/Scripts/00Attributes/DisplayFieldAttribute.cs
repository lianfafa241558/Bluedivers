using System;
using UnityEngine;

namespace FPSGame.Attributes
{
    /// <summary>
    /// 默认为运行时只读
    /// </summary>
#if UNITY_EDITOR
    [AttributeUsage(AttributeTargets.Field)]
#endif
    public class DisplayFieldAttribute : PropertyAttribute
    {
        public bool read = true;
        public bool run = true;
        public bool editor = false;
        public DisplayFieldAttribute() { }

        public DisplayFieldAttribute(DisplayFieldEnum type)
        {
            switch (type)
            {
                case DisplayFieldEnum.EditorOnly:
                    this.editor = true;
                    this.run = false;
                    this.read = false;
                    break;
                case DisplayFieldEnum.RunOnly:
                    this.editor = false;
                    this.run = true;
                    this.read = false;
                    break;
                case DisplayFieldEnum.RunRead:
                    this.editor = false;
                    this.run = true;
                    this.read = true;
                    break;
                case DisplayFieldEnum.ReadOnly:
                    this.editor = false;
                    this.run = false;
                    this.read = true;
                    break;

                default:
                    this.editor = false;
                    this.run = true;
                    this.read = true;
                    break;
            }
        }

    }

    /// <summary>DisplayField 的预设组合</summary>
    public enum DisplayFieldEnum
    {
        /// <summary>仅编辑器</summary>
        [InspectorName("仅编辑器")]
        EditorOnly,
        /// <summary>仅运行</summary>
        [InspectorName("仅运行")]
        RunOnly,
        /// <summary>仅运行可读</summary>
        [InspectorName("仅运行可读")]
        RunRead,
        /// <summary>只读</summary>
        [InspectorName("只读")]
        ReadOnly,
    }
}
