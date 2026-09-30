using UnityEngine;

namespace FPSGame.Attributes
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
}
