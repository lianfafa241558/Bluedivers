using UnityEngine;

namespace FPSGame.Attributes
{
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
}
