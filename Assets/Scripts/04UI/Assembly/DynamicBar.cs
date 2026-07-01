using UnityEngine;
using UnityEngine.UI;
using Utils;


namespace FPSGame.UI
{
    public class DynamicBar : MonoBehaviour
    {
        [SerializeField]
        int stage;
        [SerializeField]
        Image bar;
        [SerializeField]
        RectTransform slider;
        [SerializeField]
        bool isYAxis;
        [SerializeField]
        CanvasGroup canvasGroup;
        [SerializeField]
        int value;
        void Awake()
        {
            //bar = (RectTransform)transform;
            // canvasGroup = slider.GetComponent<CanvasGroup>();
            //if (isYAxis) value = (int)bar.rectTransform.GetRectHeight();
            //else value = (int)bar.rectTransform.GetRectWidth();

            if (isYAxis) value = (int)bar.rectTransform.rect.height;
            else value = (int)bar.rectTransform.rect.width;
        }

        public void SetFill(float scale)
        {
            float fill = Mathf.Floor(scale * stage) / stage;
            bar.fillAmount = fill;

            if (scale <= 0.01f || fill == 1)
            {
                slider.gameObject.SetActive(false);
            }
            else
            {
                slider.gameObject.SetActive(true);
                slider.anchoredPosition = (isYAxis? Vector2.up : Vector2.right) * value * fill;
                canvasGroup.alpha = (scale - fill) * stage * 0.7f;

            }
        }

        public void SetColor(Color color)
        {
            color.a = 1;
            slider.GetComponent<Image>().color = color;
            color.a = 0.7f;
            bar.GetComponent<Image>().color = color;
            color.a = 0.04f;
            GetComponent<Image>().color = MultiplySaturationSimple(color,0.5f);
        }


        public Color MultiplySaturationSimple(Color color, float multiplier)
        {
            // 计算当前颜色的灰度值（亮度)
            float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;

            // 插值：灰度值+ (当前颜色 - 灰度) * 倍数
            // multiplier = 0 时，完全变成灰度
            // multiplier = 1 时，保持原色
            color.r = gray + (color.r - gray) * multiplier;
            color.g = gray + (color.g - gray) * multiplier;
            color.b = gray + (color.b - gray) * multiplier;

            // Alpha 通道保持不变
            return color;
        }

    }
}