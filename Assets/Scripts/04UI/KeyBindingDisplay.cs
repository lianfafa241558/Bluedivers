using System;
using System.Collections.Generic;
using System.Reflection;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Core.InputManagerBase<InputState, Core.WindowStateEnum>;

/// <summary>
/// 快捷键键盘显示类，可复用。支持按键高亮、悬浮提示、绑定颜色显示。
/// 通过 OnButtonClicked 事件与外部修改逻辑对接。
/// </summary>
public class KeyBindingDisplay
{
    public static readonly Color BoundColor = new Color(0.5f, 1f, 0.5f);

    /// <summary>点击了某个按键按钮时触发（用于对接外部修改逻辑）</summary>
    public event Action<KeyCode> OnButtonClicked;

    private GameObject _keyCodeRoot;
    private GameObject _tooltipRoot;
    private Dictionary<KeyCode, Button> _keyCodeDict;
    private Dictionary<KeyCode, List<InputItem>> _keyCodeBindings;
    private Dictionary<KeyCode, Color> _keyCodeDefaultColors;

    private List<InputItem> _inputList => (InputManager.Instance as InputManager).InputList;

    public bool IsActive
    {
        get { return _keyCodeRoot != null && _keyCodeRoot.activeInHierarchy; }
    }

    /// <summary>
    /// 初始化：扫描根物体下的 KeyCodeItem，构建字典并刷新显示
    /// </summary>
    public void Init(GameObject keyCodeRoot, GameObject tooltipRoot)
    {
        _keyCodeRoot = keyCodeRoot;
        _tooltipRoot = tooltipRoot;
        BuildDicts();
    }

    private void BuildDicts()
    {
        _keyCodeDict = new Dictionary<KeyCode, Button>();
        if (_keyCodeRoot == null)
        {
            return;
        }

        var items = _keyCodeRoot.GetComponentsInChildren<KeyCodeItem>(true);
        foreach (var item in items)
        {
            if (item.key == KeyCode.None)
            {
                continue;
            }

            var btn = item.GetComponent<Button>();
            if (btn != null && btn.interactable && !_keyCodeDict.ContainsKey(item.key))
            {
                _keyCodeDict[item.key] = btn;
            }
        }

        foreach (var kvp in _keyCodeDict)
        {
            var btn = kvp.Value;
            var detector = btn.GetComponent<ButtonEnterDetector>();
            if (detector == null)
            {
                detector = btn.gameObject.AddComponent<ButtonEnterDetector>();
            }

            var capturedKeyCode = kvp.Key;
            var capturedBtn = btn;
            detector.Enter += (_) => ShowTooltip(capturedKeyCode, capturedBtn);
            detector.Exit += (_) => HideTooltip();

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnButtonClicked?.Invoke(capturedKeyCode));
        }

        RefreshDisplay();
    }

    /// <summary>
    /// 根据 InputManager 当前绑定刷新所有按键的颜色和文本
    /// </summary>
    public void RefreshDisplay()
    {
        _keyCodeBindings = new Dictionary<KeyCode, List<InputItem>>();
        _keyCodeDefaultColors = new Dictionary<KeyCode, Color>();

        if (_keyCodeDict == null)
        {
            return;
        }

        var mgr = InputManager.Instance;
        if (mgr == null || _inputList == null)
        {
            return;
        }

        foreach (var inputItem in _inputList)
        {
            RegisterBinding(inputItem.positiveMainValue, inputItem);
            RegisterBinding(inputItem.positiveSpareValue, inputItem);
            RegisterBinding(inputItem.negativeMainValue, inputItem);
            RegisterBinding(inputItem.negativeSpareValue, inputItem);
        }

        foreach (var kvp in _keyCodeDict)
        {
            var keyCode = kvp.Key;
            var btn = kvp.Value;
            var img = btn.image;

            Color defaultColor;
            string displayText = "";

            if (_keyCodeBindings.TryGetValue(keyCode, out var bindings) && bindings.Count > 0)
            {
                defaultColor = BoundColor;
                displayText = bindings.Count.ToString();
            }
            else
            {
                defaultColor = Color.white;
            }

            _keyCodeDefaultColors[keyCode] = defaultColor;
            if (img != null)
            {
                img.color = defaultColor;
            }

            if (btn.transform.childCount > 1)
            {
                var childText = btn.transform.GetChild(1).GetComponent<TMP_Text>();
                if (childText != null)
                {
                    childText.text = displayText;
                }
            }
        }
    }

    private void RegisterBinding(KeyCode keyCode, InputItem item)
    {
        if (keyCode == KeyCode.None) return;

        if (!_keyCodeBindings.TryGetValue(keyCode, out var list))
        {
            list = new List<InputItem>();
            _keyCodeBindings[keyCode] = list;
        }

        if (!list.Contains(item))
        {
            list.Add(item);
        }
    }

    // ==================== 输入高亮 ====================

    /// <summary>
    /// 每帧调用，处理键盘按键高亮（按下黄色，抬起恢复绑定颜色）
    /// </summary>
    public void HandleInput()
    {
        if (_keyCodeDict == null) return;

        foreach (var kvp in _keyCodeDict)
        {
            var img = kvp.Value.image;
            if (img == null) continue;

            if (Input.GetKeyDown(kvp.Key))
            {
                img.color = Color.yellow;
            }
            else if (Input.GetKeyUp(kvp.Key))
            {
                if (_keyCodeDefaultColors.TryGetValue(kvp.Key, out var defaultColor))
                {
                    img.color = defaultColor;
                }
                else
                {
                    img.color = Color.white;
                }
            }
        }
    }

    // ==================== 查询接口 ====================

    /// <summary>获取某个按键的绑定列表</summary>
    public bool TryGetBindings(KeyCode keyCode, out List<InputItem> bindings)
    {
        return _keyCodeBindings.TryGetValue(keyCode, out bindings);
    }

    /// <summary>检测所有键盘按键是否有按下，返回被按下的 KeyCode</summary>
    public bool TryGetKeyDown(out KeyCode pressedKey)
    {
        if (_keyCodeDict != null)
        {
            foreach (var kvp in _keyCodeDict)
            {
                if (Input.GetKeyDown(kvp.Key))
                {
                    pressedKey = kvp.Key;
                    return true;
                }
            }
        }
        pressedKey = default;
        return false;
    }

    // ==================== 悬浮窗 ====================

    private void ShowTooltip(KeyCode keyCode, Button btn)
    {
        if (_tooltipRoot == null) return;
        if (!_keyCodeBindings.TryGetValue(keyCode, out var bindings) || bindings.Count == 0) return;

        var tipRect = _tooltipRoot.transform as RectTransform;
        var parentRect = _tooltipRoot.transform.parent as RectTransform;
        if (tipRect != null && parentRect != null)
        {
            var canvas = _tooltipRoot.GetComponentInParent<Canvas>();
            var cam = canvas != null ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, Input.mousePosition, cam, out Vector2 localPoint);
            var localPos = tipRect.localPosition;
            localPos.x = localPoint.x;
            localPos.y = localPoint.y - tipRect.rect.height * 0.5f - 20f;
            tipRect.localPosition = localPos;
        }

        _tooltipRoot.SetActive(true);

        var keyName = "";
        if (btn.transform.childCount > 0)
        {
            var keyLabel = btn.transform.GetChild(0).GetComponent<TMP_Text>();
            if (keyLabel != null) keyName = keyLabel.text;
        }

        if (_tooltipRoot.transform.childCount > 0)
        {
            var titleText = _tooltipRoot.transform.GetChild(0).GetComponent<TMP_Text>();
            if (titleText != null) titleText.text = keyName;
        }

        var detailText = "";
        foreach (var item in bindings)
        {
            var windowName = GetEnumDisplayName(item.window);
            var inputName = GetEnumDisplayName(item.key);
            var slotName = GetSlotName(keyCode, item);
            detailText += windowName + "-" + inputName + "-" + slotName + "\n";
        }

        if (_tooltipRoot.transform.childCount > 1)
        {
            var detailTextComp = _tooltipRoot.transform.GetChild(1).GetComponent<TMP_Text>();
            if (detailTextComp != null) detailTextComp.text = detailText.TrimEnd('\n');
        }
    }

    private void HideTooltip()
    {
        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);
        }
    }

    // ==================== 工具方法 ====================

    public static string GetSlotName(KeyCode keyCode, InputItem item)
    {
        if (item.positiveMainValue == keyCode) return "主键";
        if (item.positiveSpareValue == keyCode) return "备用键";
        if (item.negativeMainValue == keyCode) return "反向主键";
        if (item.negativeSpareValue == keyCode) return "反向备用键";
        return "";
    }

    public static string GetEnumDisplayName(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field != null)
        {
            var attr = field.GetCustomAttribute<InspectorNameAttribute>();
            if (attr != null) return attr.displayName;
        }
        return value.ToString();
    }
}
