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

        CanvasGroup canvasGroup;
        int width;
        void Awake()
        {
            //bar = (RectTransform)transform;
            canvasGroup = slider.GetComponent<CanvasGroup>();
            width = (int)bar.rectTransform.GetRectWidth();
        }

        public void SetBar(float scale)
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
                slider.anchoredPosition = Vector2.right * width * fill;
                canvasGroup.alpha = (scale - fill) * stage * 0.7f;

            }
        }

    }
}