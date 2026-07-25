using System.Collections.Generic;
using System.Reflection;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Core.InputManagerBase<InputState, Core.WindowStateEnum>;
using static WndTools.WndRootTool;

public partial class SettingWnd : Window
{
    private static readonly Color LightGreen = new Color(0.5f, 1f, 0.5f);

    private enum RebindState
    {
        None,
        WaitingForKey
    }

    [Header("快捷键修改")]
    [SerializeField] private GameObject _keyCodeRoot;
    [SerializeField] private GameObject _tooltipRoot;
    [SerializeField] private GameObject _rebindSelectionRoot;
    [SerializeField] private GameObject _rebindSelectionTemplate;
    [SerializeField] private TMP_Text _rebindHintText;

    private Dictionary<KeyCode, Button> _keyCodeDict;
    private Dictionary<KeyCode, List<InputItem>> _keyCodeBindings;
    private Dictionary<KeyCode, Color> _keyCodeDefaultColors;

    private RebindState _rebindState;
    private KeyCode _rebindingKeyCode;
    private InputItem _rebindingItem;

    private List<InputItem> InputList => (InputManager.Instance as InputManager).InputList;

    private bool IsKeyCodeModuleActive
    {
        get { return _keyCodeRoot != null && _keyCodeRoot.activeInHierarchy; }
    }

    private void BuildKeyCodeDict()
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
            if (btn != null && !_keyCodeDict.ContainsKey(item.key))
            {
                _keyCodeDict[item.key] = btn;
            }
        }

        // 为每个按钮挂载悬浮事件和点击事件
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
            btn.onClick.AddListener(() => OnButtonClicked(capturedKeyCode));
        }

        RefreshKeyBindingDisplay();
    }

    /// <summary>
    /// 根据 InputManager 的当前绑定刷新所有按键按钮的颜色和文本
    /// </summary>
    private void RefreshKeyBindingDisplay()
    {
        _keyCodeBindings = new Dictionary<KeyCode, List<InputItem>>();
        _keyCodeDefaultColors = new Dictionary<KeyCode, Color>();

        if (_keyCodeDict == null)
        {
            return;
        }

        var mgr = InputManager.Instance;
        if (mgr == null || InputList == null)
        {
            return;
        }

        foreach (var inputItem in InputList)
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
                defaultColor = LightGreen;
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
                SetText(btn.transform.GetChild(1), displayText);
            }
        }
    }

    private void RegisterBinding(KeyCode keyCode, InputItem item)
    {
        if (keyCode == KeyCode.None)
        {
            return;
        }

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

    // ==================== 点击按钮 → 修改快捷键 ====================

    private void OnButtonClicked(KeyCode keyCode)
    {
        if (_rebindState != RebindState.None)
        {
            return;
        }

        if (!_keyCodeBindings.TryGetValue(keyCode, out var bindings) || bindings.Count == 0)
        {
            return;
        }

        if (bindings.Count == 1)
        {
            // 只有一个绑定，直接进入等待按键
            StartRebind(keyCode, bindings[0]);
        }
        else
        {
            // 多个绑定，显示选项框
            ShowBindingSelection(keyCode, bindings);
        }
    }

    private void ShowBindingSelection(KeyCode keyCode, List<InputItem> bindings)
    {
        if (_rebindSelectionRoot == null || _rebindSelectionTemplate == null)
        {
            return;
        }

        // 清理旧选项（保留模板）
        for (int i = _rebindSelectionRoot.transform.childCount - 1; i >= 0; --i)
        {
            var child = _rebindSelectionRoot.transform.GetChild(i);
            if (child.gameObject != _rebindSelectionTemplate)
            {
                Destroy(child.gameObject);
            }
        }

        _rebindSelectionTemplate.SetActive(false);

        foreach (var item in bindings)
        {
            var option = Instantiate(_rebindSelectionTemplate, _rebindSelectionRoot.transform);
            option.SetActive(true);

            // 设置选项文本：窗口-功能-键位类型
            var windowName = GetEnumDisplayName(item.window);
            var inputName = GetEnumDisplayName(item.key);
            var slotName = GetSlotName(keyCode, item);
            if (option.transform.childCount > 0)
            {
                var textComp = option.transform.GetChild(0).GetComponent<TMP_Text>();
                if (textComp != null)
                {
                    textComp.text = windowName + "-" + inputName + "-" + slotName;
                }
            }

            // 绑定点击
            var optionBtn = option.GetComponent<Button>();
            if (optionBtn != null)
            {
                var capturedKeyCode = keyCode;
                var capturedItem = item;
                optionBtn.onClick.RemoveAllListeners();
                optionBtn.onClick.AddListener(() => SelectBinding(capturedKeyCode, capturedItem));
            }
        }

        _rebindSelectionRoot.SetActive(true);
    }

    private void SelectBinding(KeyCode keyCode, InputItem item)
    {
        HideSelection();
        StartRebind(keyCode, item);
    }

    private void HideSelection()
    {
        if (_rebindSelectionRoot != null)
        {
            _rebindSelectionRoot.SetActive(false);
        }
    }

    private void StartRebind(KeyCode keyCode, InputItem item)
    {
        _rebindState = RebindState.WaitingForKey;
        _rebindingKeyCode = keyCode;
        _rebindingItem = item;

        if (_rebindHintText != null)
        {
            _rebindHintText.gameObject.SetActive(true);
            _rebindHintText.text = "请按下新的按键...";
        }
    }

    // ==================== 校验与修改 ====================

    /// <summary>
    /// 检查同一个 window 下该 KeyCode 是否已被其他 InputItem 使用
    /// </summary>
    private bool IsKeyConflict(KeyCode newKey, InputItem targetItem)
    {
        foreach (var inputItem in InputList)
        {
            // 只检查同一 window
            if (inputItem.window.GetHashCode() != targetItem.window.GetHashCode())
            {
                continue;
            }

            // 跳过自身
            if (inputItem == targetItem)
            {
                continue;
            }

            if (inputItem.positiveMainValue == newKey
                || inputItem.positiveSpareValue == newKey
                || inputItem.negativeMainValue == newKey
                || inputItem.negativeSpareValue == newKey)
            {
                return true;
            }
        }
        return false;
    }

    private static void SetSlotValue(InputItem item, KeyCode oldKey, KeyCode newKey)
    {
        if (item.positiveMainValue == oldKey) item.positiveMainValue = newKey;
        else if (item.positiveSpareValue == oldKey) item.positiveSpareValue = newKey;
        else if (item.negativeMainValue == oldKey) item.negativeMainValue = newKey;
        else if (item.negativeSpareValue == oldKey) item.negativeSpareValue = newKey;
    }

    // ==================== 处理按键输入 ====================

    private void HandleRebindInput()
    {
        foreach (var kvp in _keyCodeDict)
        {
            if (!Input.GetKeyDown(kvp.Key))
            {
                continue;
            }

            var newKey = kvp.Key;

            if (IsKeyConflict(newKey, _rebindingItem))
            {
                wndManager.CreatTip(new TipWndInfo
                {
                    title = "按键冲突",
                    desc = "\n该窗口下已有其他功能绑定了此按键，请选择其他按键。",
                    optA_Text = "确定"
                });
                return;
            }

            // 修改绑定
            SetSlotValue(_rebindingItem, _rebindingKeyCode, newKey);
            ResetRebindState();
            RefreshKeyBindingDisplay();
            return;
        }
    }

    private void ResetRebindState()
    {
        _rebindState = RebindState.None;
        _rebindingItem = null;

        if (_rebindHintText != null)
        {
            _rebindHintText.gameObject.SetActive(false);
        }
    }

    private void HandleKeyCodeInput()
    {
        if (_keyCodeDict == null)
        {
            return;
        }

        // 等待按键状态：拦截按键用于修改绑定
        if (_rebindState == RebindState.WaitingForKey)
        {
            HandleRebindInput();
            return;
        }

        // 正常高亮状态
        foreach (var kvp in _keyCodeDict)
        {
            var img = kvp.Value.image;
            if (img == null)
            {
                continue;
            }

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

    // ==================== 悬浮窗 ====================

    private void ShowTooltip(KeyCode keyCode, Button btn)
    {
        if (_tooltipRoot == null)
        {
            return;
        }

        if (!_keyCodeBindings.TryGetValue(keyCode, out var bindings) || bindings.Count == 0)
        {
            return;
        }

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
            if (keyLabel != null)
            {
                keyName = keyLabel.text;
            }
        }

        if (_tooltipRoot.transform.childCount > 0)
        {
            var titleText = _tooltipRoot.transform.GetChild(0).GetComponent<TMP_Text>();
            if (titleText != null)
            {
                titleText.text = keyName;
            }
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
            if (detailTextComp != null)
            {
                detailTextComp.text = detailText.TrimEnd('\n');
            }
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

    private static string GetSlotName(KeyCode keyCode, InputItem item)
    {
        if (item.positiveMainValue == keyCode) return "主键";
        if (item.positiveSpareValue == keyCode) return "备用键";
        if (item.negativeMainValue == keyCode) return "反向主键";
        if (item.negativeSpareValue == keyCode) return "反向备用键";
        return "";
    }

    private static string GetEnumDisplayName(System.Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field != null)
        {
            var attr = field.GetCustomAttribute<InspectorNameAttribute>();
            if (attr != null)
            {
                return attr.displayName;
            }
        }
        return value.ToString();
    }
}
