using System;
using System.Collections.Generic;
using UnityEngine;
using static WndTools.WndRootTool;

public class WheelUI : MonoBehaviour
{

    [SerializeField]
    private RectTransform centerText, mask;
    [SerializeField]
    protected Sprite cancelIcon;

    [SerializeField]
    private GameObject prefab;
    private List<Transform> list=new();
    private List<WheelItemIfon> infos;
    private int currentHoverIndex = -1;
    private int iconDis;

    private Camera uiCamera;

    protected virtual void Awake()
    {
        //A/2+(B-A)/4=(A+B)/4
        iconDis = (int)(transform.RectTransform().sizeDelta.x + transform.GetChild(0).RectTransform().sizeDelta.x)/ 4;
        // 获取 Canvas 对应的相机
        var canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null ? canvas.worldCamera : null;
    }


    [ContextMenu("测试")]
    protected void Test()
    {
        ShowWnd(new() {
            new() {
                name = "放下 [传送背包]",
                icon = cancelIcon,
                cb=null,
            },
            new() {
                name = "放下 [护卫犬]",
                icon = cancelIcon,
                cb=null,
            },
        });
    }

    public void ShowWnd(List<WheelItemIfon> infos)
    {
        SetActive(gameObject, true);
        infos.Add(new() {
            name = "取消",
            icon = cancelIcon,
            cb = Cancel,
        });
        this.infos = infos;
        Resetwheel(infos.Count);
        for (int i = 0; i < infos.Count; i++)
        {
            var item = list[i].transform;
            SetSprite(item.GetChild(1), infos[i].icon);
        }
    }

    private void AddItem()
    {
        var item = Instantiate(prefab, mask).transform;
        list.Add(item);
        var a = list.Count - 1;
        SetButtonEnter(item, p => {
            SetText(centerText, infos[a].name);
            SetColor(item.GetChild(0),new(1,1,1,0.3f));
        });
        SetButtonExit(item, p => {
            SetColor(item.GetChild(0), new(0, 0, 0, 0.8f));
        });
        SetCilck(item.GetChild(0), () => {
            infos[a].cb?.Invoke(infos[a].name);
            SetActive(gameObject, false);
        });
    }

    private void Resetwheel(int count)
    {
        //如果项数不够补上
        for (int i = list.Count;i < count;++i)
        {
            AddItem();
        }
        var rad = 180f / count * Mathf.Deg2Rad;
        //每一项
        for (int i = 0; i < count; i++)
        {
            var item = list[i];
            SetFill(item.GetChild(0), 1f / count);
            item.GetChild(1).RectTransform().anchoredPosition = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad))* iconDis;
            item.eulerAngles = new(0,0,-360/count*i);
            item.GetChild(1).eulerAngles = Vector3.zero;
        }
        //隐藏多余项数
        for (int i = count; i < list.Count; ++i)
        {
            var item = list[i];
            SetActive(item,false);
        }
    }



    private void Update()
    {
        if (infos == null || !gameObject.activeSelf) return;

        // 将 mask 中心转换到屏幕坐标（兼容所有 Canvas 模式）
        Vector3 maskScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, mask.position);
        Vector2 dir = Input.mousePosition - maskScreenPos;
        // Atan2(-y, x) = 顺时针角度，与 UI 的 Z 旋转方向一致（3点钟=0°，顺时针递增）
        float angle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int count = infos.Count;
        float sectorAngle = 360f / count;
        int hoverIndex = -1;

        for (int i = 0; i < count; i++)
        {
            float startAngle = sectorAngle * i;
            float endAngle = sectorAngle * (i + 1);
            if (angle >= startAngle && angle < endAngle)
            {
                hoverIndex = i;
                break;
            }
        }

        if (hoverIndex != currentHoverIndex)
        {

            // 退出上一个悬停项
            if (currentHoverIndex >= 0 && currentHoverIndex < list.Count)
            {
                var prev = list[currentHoverIndex];
                if (prev != null && prev.gameObject.activeSelf)
                {
                    // ButtonEnterDetector 在 item 本体上（同 SetButtonEnter）
                    prev.GetComponent<ButtonEnterDetector>().InEnter = false;
                }
            }

            // 进入新悬停项
            currentHoverIndex = hoverIndex;
            if (currentHoverIndex >= 0 && currentHoverIndex < list.Count)
            {
                var cur = list[currentHoverIndex];
                if (cur != null && cur.gameObject.activeSelf)
                {
                    cur.GetComponent<ButtonEnterDetector>().InEnter = true;
                }
            }
        }

        // 鼠标抬起，执行当前项点击
        if (TriggerConditions() && currentHoverIndex >= 0 && currentHoverIndex < list.Count)
        {
            var cur = list[currentHoverIndex];
            if (cur != null && cur.gameObject.activeSelf)
            {
                cur.GetChild(0).GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();
            }
        }
    }

    protected virtual bool TriggerConditions()
    {
        return Input.GetMouseButtonUp(0);
    }


    private void Cancel(string _) {
        SetActive(gameObject,false);
    }

    [Serializable]
    public struct WheelItemIfon
    {
        public Sprite icon;
        public string name;
        public Action<string> cb;
    }
}
