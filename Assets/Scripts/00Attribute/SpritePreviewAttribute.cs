using UnityEngine;
namespace FPSGame.Attribute
{
    public class SpritePreviewAttribute : PropertyAttribute
    {
        public int height;
        public int width;

        public SpritePreviewAttribute(int width, int height)
        {
            this.height = height;
            this.width = width;
        }
        public SpritePreviewAttribute(int height = 4)
        {
            this.height = height;
            this.width = height;
        }
    }


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