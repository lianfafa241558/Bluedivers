
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace FPSGame.WndTools
{
    /// <summary>
    /// 让多个 UI 元素使用联动颜色。
    /// </summary>
    [AddComponentMenu("UI/工具/联动颜色")]
    public class LinkColor : MonoBehaviour
    {
        public List<Graphic> link;
        public Color overlay = new(0.5f, 0.5f, 0.5f, 0.5f);
    }
}
