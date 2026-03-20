using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WndTools
{
    public static class WndRootTool
    {

        private static ViewTimerController viewTimer;
        static WndRootTool()
        {
            if (Application.isPlaying && GameObject.Find("GameRoot"))
            {
                viewTimer = GameObject.Find("GameRoot").GetComponent<ViewTimerController>();
            }
        }


        public static RectTransform RectTransform(this Transform transform) => transform.GetComponent<RectTransform>();

        //设置窗口UI的激活状态
        public static bool SetActive(GameObject go, bool state = true)
        {
            go.SetActive(state);
            return state;
        }
        public static bool SetActive(MonoBehaviour go, bool state = true)
        {
            go.gameObject.SetActive(state);
            return state;
        }
        public static bool SetActive(Transform trans, bool state = true)
        {
            trans.gameObject.SetActive(state);
            return state;
        }


        //设置窗口UI的激活状态
        public static bool SetActive(bool state, params Component[] trans)
        {
            foreach (var obj in trans)
            {
                if(obj)obj.gameObject.SetActive(state);
            }
            return state;
        }


        public static bool GetActive(GameObject go) => go.activeInHierarchy;
        public static bool GetActive(MonoBehaviour go) => go.gameObject.activeInHierarchy;
        public static bool GetActive(Transform trans) => trans.gameObject.activeInHierarchy;


        //获取组件
        public static Transform GetTrans(Transform trans, string name) => trans.Find(name);

        public static Image GetImage(Transform trans, string path) => trans.Find(path).GetComponent<Image>();

        public static Image GetImage(Transform trans) => trans.GetComponent<Image>();

        public static string GetText(Transform trans)
        {
            if (trans.TryGetComponent(out TMPro.TextMeshProUGUI tmpu)) return tmpu.text;
            if (trans.TryGetComponent(out TMPro.TextMeshPro tmp)) return tmp.text;
            if (trans.TryGetComponent(out Text text)) return text.text;
            return default;
        }

        public static float GetFill(Transform trans)
        {
            return GetOrAddComponent<Image>(trans.gameObject).fillAmount;
        }
        public static void SetFill(Transform trans, float value)
        {
            GetOrAddComponent<Image>(trans.gameObject).fillAmount = value;
        }
        public static void SetFill(Transform trans, float value, float speed)
        {
            var image = GetOrAddComponent<Image>(trans.gameObject);
            image.fillAmount = Mathf.Lerp(image.fillAmount, value, speed);
        }

        public static Sprite GetSprite(Transform trans)
        {
            return trans.GetComponent<Image>().sprite;
        }

        public static void CopySprite(Transform from, Transform to)
        {
            Image formI = from.GetComponent<Image>();
            Image toI = to.GetComponent<Image>();
            toI.sprite = formI.sprite;
            toI.color = formI.color;
            if (to.TryGetComponent(out LinkColor linkComp))
            {
                var color = formI.color + (linkComp.overlay - new Color(0.5f, 0.5f, 0.5f, 0.5f));
                linkComp.link.ForEach(item => item.color = color);
            }
        }

        public static void SetSprite(Transform trans, Sprite path)
        {
            GetOrAddComponent<Image>(trans.gameObject).sprite = path;
        }

        public static void SetSprite(Image image, Sprite path)
        {
            if (image.sprite != path) image.sprite = path;
        }

        public static void SetSizeDelta(Transform trans, float width, float height)
        {
            ((RectTransform)trans).sizeDelta = new(width, height);
        }
        public static Vector2 GetSizeDelta(Transform trans)
        {
            return ((RectTransform)trans).sizeDelta;
        }

        public static void SetSizeDelta(Transform trans, int startX, int startY, int targetX, int targetY, int timeMs)
        {
            viewTimer.CreateTimer((count) => SetSizeDelta(trans, (int)Mathf.Lerp(startX, targetX, count * 20f / timeMs), (int)Mathf.Lerp(startY, targetY, count * 20f / timeMs)), 0.02f, timeMs / 20, 
                () => {
                    SetSizeDelta(trans, targetX, targetY);
                });
        }

        //private static System.Text.StringBuilder sb = new(8);

        public static void SetText(Transform trans, int num = 0)
        {
            //此处导致的GC无解
            SetText(trans, num.ToString());
        }
        public static void SetText(Transform trans, string context = "")
        {
            if (trans==null) return;
            if (trans.TryGetComponent<TMPro.TextMeshProUGUI>(out var tmpu))
            {
                tmpu.text = context;
                return;
            }
            if (trans.TryGetComponent<TMPro.TextMeshPro>(out var tmp))
            {
                tmp.text = context;
                return;
            }
            if (trans.TryGetComponent<Text>(out var text))
            {
                text.text = context;
                return;
            }
        }



        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T t = go.GetComponent<T>();
            if (t == null)
            {
                t = go.AddComponent<T>();
            }
            return t;
        }

        public static void RefreshLayout(Transform transform)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(transform.RectTransform());
        }

        public static void SetToggle(Transform tran, bool state)
        {
            tran.GetComponent<Toggle>().isOn = state;
        }

        public static void SetColor(Transform trans, Color color)
        {
            if (trans==null) return;
            if (trans.TryGetComponent<LinkColor>(out var linkComp))
            {

                var color2 = color + (linkComp.overlay - new Color(0.5f, 0.5f, 0.5f, 0.5f));
                linkComp.link.ForEach(item => item.color = color2);
            }

            if (trans.TryGetComponent<Image>(out var image))
            {
                image.color = color;
                return;
            }
            if (trans.TryGetComponent<TMPro.TextMeshProUGUI>(out var tmpu))
            {
                tmpu.color = color;
                return;
            }

            if (trans.TryGetComponent<Text>(out var text))
            {
                text.color = color;
                return;
            }
            if (trans.TryGetComponent<TMPro.TextMeshPro>(out var tmp))
            {
                tmp.color = color;
                return;
            }


        }

        public static void CopyColor(Transform from, Transform to)
        {
            Image formI = from.GetComponent<Image>();
            Image toI = to.GetComponent<Image>();
            toI.color = formI.color;
            if (to.TryGetComponent<LinkColor>(out var linkComp))
            {

                var color = formI.color + (linkComp.overlay - new Color(0.5f, 0.5f, 0.5f, 0.5f));
                linkComp.link.ForEach(item => item.color = color);
            }
        }
        public static void SetAlpha(Transform trans, float start, float target, int timeMs)
        {
            SetAlpha(trans, start);
            viewTimer.CreateTimer((count) => SetAlpha(trans, Mathf.Lerp(start, target, count * 20f / timeMs)), 0.02f, timeMs / 20, () => SetAlpha(trans, target));
        }
        public static void SetText(Transform trans, int start, int target, int timeMs)
        {
            SetText(trans, start);
            viewTimer.CreateTimer((count) => SetText(trans, (int)Mathf.Lerp(start, target, count * 20f / timeMs)), 0.02f, timeMs / 20, () => SetText(trans, target));
        }

        public static void SetActive(GameObject go, bool state, int timeMs)
        {
            viewTimer.CreateTimer(()=>go.SetActive(state), timeMs/1000f);

        }
        public static void SetActive(MonoBehaviour go, bool state, int timeMs)
        {
            viewTimer.CreateTimer(() => go.gameObject.SetActive(state), timeMs / 1000f);

        }
        public static void SetActive(Transform trans, bool state, int timeMs)
        {
            viewTimer.CreateTimer(() => trans.gameObject.SetActive(state), timeMs / 1000f);
        }

        public static void SetAlpha(Transform trans, float value)
        {
            if (trans==null) return;
            if (trans.TryGetComponent<LinkColor>(out var linkComp))
            {

                var color2 = value + (linkComp.overlay.a - 0.5f);
                linkComp.link.ForEach(item => item.color = new(item.color.r, item.color.g, item.color.b, color2));
            }

            if (trans.TryGetComponent<CanvasGroup>(out var group))
            {
                group.alpha = value;
                return;
            }


            if (trans.TryGetComponent<Image>(out var image))
            {
                image.color = new(image.color.r, image.color.g, image.color.b, value);
                return;
            }

            if (trans.TryGetComponent<TMPro.TextMeshProUGUI>(out var tmpu))
            {
                tmpu.color = new(tmpu.color.r, tmpu.color.g, tmpu.color.b, value);
                return;
            }

            if (trans.TryGetComponent<TMPro.TextMeshPro>(out var tmp))
            {
                tmp.color = new(tmp.color.r, tmp.color.g, tmp.color.b, value);
                return;
            }
        }
        public static float GetAlpha(Transform trans)
        {
            if (trans.TryGetComponent<CanvasGroup>(out var group))
            {
                return group.alpha;
            }

            if (trans.TryGetComponent<Image>(out var image))
            {
                return image.color.a;
            }

            if (trans.TryGetComponent<TMPro.TextMeshProUGUI>(out var tmpu))
            {
                return tmpu.color.a;
            }

            if (trans.TryGetComponent<TMPro.TextMeshPro>(out var tmp))
            {
                return tmp.color.a;
            }
            return 0;
        }

        public static void ClearButton(Transform btn) => btn.GetComponent<Button>().onClick.RemoveAllListeners();

        public static void SetCilck(Transform btn, UnityAction action) => btn.GetComponent<Button>().onClick.AddListener(action);

        public static void ClickButton(Transform btn) => btn.GetComponent<Button>().onClick.Invoke();

        public static void SetButtonInteractable(Transform btn, bool state) => btn.GetComponent<Button>().interactable = state;

        public static void SetButtonEnter(Transform btn, Action<UnityEngine.EventSystems.PointerEventData> action) => btn.GetComponent<ButtonEnterDetector>().Enter = action;

        public static void SetButtonExit(Transform btn, Action<UnityEngine.EventSystems.PointerEventData> action) => btn.GetComponent<ButtonEnterDetector>().Exit = action;

        public static void SetButtonIn(Transform btn, Action<UnityEngine.EventSystems.PointerEventData> action) => btn.GetComponent<ButtonEnterDetector>().In = action;

        public static T TryGetOrAddComponent<T>(Transform trans) where T : MonoBehaviour
        {

            if (trans.TryGetComponent<T>(out var re))
            {
                return re;
            }
            else
            {
                return trans.gameObject.AddComponent<T>();
            }
        }
    }
}