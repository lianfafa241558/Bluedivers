using UnityEngine;
using UnityEngine.UI;

namespace FPSGame.UI
{

/// <summary>
/// 按钮透明度测试(对应的图片必须的读取/写入必须勾上)
/// </summary>
[AddComponentMenu("UI/控件/图片点击阈值")]
public class ButtonAlphaHitMinThreshold : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Image>().alphaHitTestMinimumThreshold = 0.5f;
        Destroy(this);
    }


}
}
