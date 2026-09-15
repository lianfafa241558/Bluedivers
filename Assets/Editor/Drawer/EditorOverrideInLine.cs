using UnityEditor;

namespace Pixeye.Unity
{
    /// <summary>
    /// EditorOverride（全局兜底 Inspector）的 [Singleline] 单行内联绘制入口。
    ///
    /// 具体实现已统一收敛到 <see cref="InlineFieldDrawer"/>（Layout 版），本文件只保留
    /// EditorOverride.cs 依赖的私有方法签名，避免兜底 Inspector 的调用点被改动。
    /// </summary>
    public partial class EditorOverride
    {
        /// <summary>该字段是否为单行内联字段（自身或元素类型带 [Singleline]）。</summary>
        private bool IsInlineField(SerializedProperty prop)
            => InlineFieldDrawer.IsInlineField(prop, target);

        /// <summary>绘制内联单行对象（所有子字段在一行）。</summary>
        private void DrawInlineObject(SerializedProperty prop)
            => InlineFieldDrawer.DrawInlineObjectLayout(prop, target, LabelOf(prop));

        /// <summary>绘制内联数组/List（自绘头部 + ReorderableList，元素内联单行）。</summary>
        private void DrawInlineArrayNative(SerializedProperty prop)
            => InlineFieldDrawer.DrawInlineListLayout(prop, target, LabelOf(prop));

        /// <summary>取字段的中文显示名（[InspectorName]），无则返回 null 交给公共实现兜底。</summary>
        private string LabelOf(SerializedProperty prop)
            => prop != null && InspectorNames.TryGetValue(prop.name, out var name) ? name : null;
    }
}
