using System;
using UnityEngine;
namespace FPSGame.Attribute
{
    /// <summary>
    /// 检视器折叠
    /// </summary>
    public class FoldoutAttribute : PropertyAttribute
{
    public string name;

    public bool foldEverything;

    /// <summary>
    /// 将属性添加到指定的文件夹组
    /// </summary>
    /// <param name="name">文件夹的名称</param>
    /// <param name="foldEverything">切换以将所有属性放入指定组</param>
    public FoldoutAttribute(string name, bool foldEverything = false)
    {
        this.foldEverything = foldEverything;
        this.name = name;
    }
}


/// <summary>
/// 使字段在Inspector中根据比较结果自定义显示
/// </summary>
public class CompareAttribute : PropertyAttribute
{

    public string contField;

    public int enumValue;

    public CompareOperate operate;



    public CompareAttribute(string contField)
    {
        this.contField = contField;
    }

    public CompareAttribute(string contField, int enumValue, CompareOperate operate = CompareOperate.Greater)
    {
        this.contField = contField;
        this.enumValue = enumValue;
        this.operate = operate;
    }
}


    public enum CompareOperate
    {
        /// <summary>等于</summary>
        [InspectorName("等于")]
        Equal,
        /// <summary>不等于</summary>
        [InspectorName("不等于")]
        NotEqual,
        /// <summary>小于</summary>
        [InspectorName("小于)")]
        Less,
        /// <summary>小于等于</summary>
        [InspectorName("小于等于")]
        LessEqual,
        /// <summary>大于</summary>
        [InspectorName("大于")]
        Greater,
        /// <summary>大于等于</summary>
        [InspectorName("大于等于")]
        GreaterEqual,
        /// <summary>包含(Flags)</summary>
        [InspectorName("包含(Flags)")]
        Contain,
        /// <summary>不包含(Flags)</summary>
        [InspectorName("不包含(Flags)")]
        NotContain
    }
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


    /// <summary>
    /// 在检视面板中绘制一条分割线
    /// </summary>
    public class DividerAttribute : PropertyAttribute
    {
        public float height = 2f;           // 分割线高度
        public float spacing = 6f;          // 上下间距
        public Color color;                // 自定义颜色（可选）

        public DividerAttribute()
        {
            // 默认颜色会根据编辑器皮肤自动适配
            color = Color.gray;
        }

        public DividerAttribute(float height, float spacing = 6f)
        {
            this.height = height;
            this.spacing = spacing;
            color = Color.gray;
        }

        public DividerAttribute(float r, float g, float b, float height = 2f, float spacing = 6f)
        {
            this.height = height;
            this.spacing = spacing;
            color = new Color(r, g, b);
        }
    }


    /// <summary>
    /// 默认为运行时只读
    /// </summary>
#if UNITY_EDITOR
    [AttributeUsage(AttributeTargets.Field)]
#endif
    
    public class DisplayField : PropertyAttribute
    {
        public bool read = true;
        public bool run = true;
        public bool editor = false;
        public DisplayField() { }

        public DisplayField(DisplayFieldEnum type)
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


}