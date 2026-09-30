using UnityEngine;

namespace FPSGame.Attributes
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
}
