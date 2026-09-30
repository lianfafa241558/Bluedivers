using UnityEngine;

namespace FPSGame.Attributes
{
    /// <summary>
    /// 单行内联绘制（配合 EditorOverride 的单行数组/字段绘制）。
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
    public class SinglelineAttribute : PropertyAttribute
    {
        // 可选的：是否显示字段名（默认显示缩写名）
        public bool showLabels = true;

        public SinglelineAttribute(bool showLabels = false)
        {
            this.showLabels = showLabels;
        }
    }
}
