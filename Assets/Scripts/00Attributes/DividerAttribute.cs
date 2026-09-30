using UnityEngine;

namespace FPSGame.Attributes
{
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
}
