using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using static Core.InputManagerBase<InputState, Core.WindowStateEnum>;

public partial class SettingWnd : Window
{
    private enum RebindState
    {
        None,
        WaitingForKey
    }

    [Header("快捷键修改")]
    [SerializeField] private GameObject _keyCodeRoot;
    [SerializeField] private GameObject _tooltipRoot;
    [SerializeField] private GameObject _rebindSelectionRoot;
    [SerializeField] private TMP_Text _rebindHintText;

    [Header("快捷键仅显示")]
    [SerializeField] private GameObject _displayOnlyKeyCodeRoot;
    [SerializeField] private GameObject _displayOnlyTooltipRoot;

    private KeyBindingDisplay _keyDisplay;
    private KeyBindingDisplay _displayOnlyKeyDisplay;

    private RebindState _rebindState;
    private KeyCode _rebindingKeyCode;
    private InputItem _rebindingItem;
    private bool _escHandledThisFrame;
    private bool _haveInputChanged;

    private List<InputItem> InputList => (InputManager.Instance as InputManager).InputList;

    private bool IsKeyCodeModuleActive
    {
        get { return (_keyDisplay != null && _keyDisplay.IsActive) || (_displayOnlyKeyDisplay != null && _displayOnlyKeyDisplay.IsActive); }
    }

    // ==================== 初始化 ====================

    private void BuildKeyCodeDict()
    {
        _keyDisplay = new KeyBindingDisplay();
        _keyDisplay.Init(_keyCodeRoot, _tooltipRoot);
        _keyDisplay.OnButtonClicked += OnButtonClicked;

        // 仅显示实例（不可修改）
        if (_displayOnlyKeyCodeRoot != null)
        {
            _displayOnlyKeyDisplay = new KeyBindingDisplay();
            _displayOnlyKeyDisplay.Init(_displayOnlyKeyCodeRoot, _displayOnlyTooltipRoot);
        }

        if (_rebindHintText != null)
        {
            _rebindHintText.gameObject.SetActive(true);
            _rebindHintText.text = "点击按键以修改键位";
        }
    }

    private void RefreshKeyBindingDisplay()
    {
        _keyDisplay?.RefreshDisplay();
    }

    // ==================== 点击按钮 → 修改快捷键 ====================

    private void OnButtonClicked(KeyCode keyCode)
    {
        // 改键等待中：鼠标点击作为新按键输入
        if (_rebindState == RebindState.WaitingForKey)
        {
            TryApplyRebind(keyCode);
            return;
        }

        if (_rebindState != RebindState.None)
        {
            return;
        }

        if (!_keyDisplay.TryGetBindings(keyCode, out var bindings) || bindings.Count == 0)
        {
            return;
        }

        wndManager.PlaySound(new("UI/UI_Bubble"));

        if (bindings.Count == 1)
        {
            StartRebind(keyCode, bindings[0]);
        }
        else
        {
            ShowBindingSelection(keyCode, bindings);
        }
    }

    private void ShowBindingSelection(KeyCode keyCode, List<InputItem> bindings)
    {
        if (_rebindSelectionRoot == null)
        {
            return;
        }
        if (_rebindSelectionRoot.transform.childCount < 1)
        {
            return;
        }

        // layout 是 _rebindSelectionRoot 的第0个子物体的第0个子物体
        var layout = _rebindSelectionRoot.transform.GetChild(0,1);
        if (layout == null || layout.childCount == 0)
        {
            return;
        }

        for (int i = 0; i < layout.childCount; ++i)
        {
            var option = layout.GetChild(i);
            var isLastItem = i == layout.childCount - 1;

            if (isLastItem)
            {
                option.gameObject.SetActive(true);
                var textComp = option.childCount > 0
                    ? option.GetChild(0).GetComponent<TMP_Text>()
                    : option.GetComponentInChildren<TMP_Text>();
                if (textComp != null) textComp.text = "取消";

                var cancelBtn = option.GetComponent<Button>();
                if (cancelBtn != null)
                {
                    cancelBtn.onClick.RemoveAllListeners();
                    cancelBtn.onClick.AddListener(() =>
                    {
                        wndManager.PlaySound(new("UI/UI_Button_Back"));
                        HideSelection();
                    });
                }
            }
            else if (i < bindings.Count)
            {
                var item = bindings[i];
                option.gameObject.SetActive(true);

                var windowName = KeyBindingDisplay.GetEnumDisplayName(item.window);
                var inputName = KeyBindingDisplay.GetEnumDisplayName(item.key);
                var slotName = KeyBindingDisplay.GetSlotName(keyCode, item);
                var textComp = option.childCount > 0
                    ? option.GetChild(0).GetComponent<TMP_Text>()
                    : option.GetComponentInChildren<TMP_Text>();
                if (textComp != null) textComp.text = windowName + "-" + inputName + "-" + slotName;

                var optionBtn = option.GetComponent<Button>();
                if (optionBtn != null)
                {
                    var capturedKeyCode = keyCode;
                    var capturedItem = item;
                    optionBtn.onClick.RemoveAllListeners();
                    optionBtn.onClick.AddListener(() => SelectBinding(capturedKeyCode, capturedItem));
                }
            }
            else
            {
                option.gameObject.SetActive(false);
            }
        }

        _rebindSelectionRoot.SetActive(true);
    }

    private void SelectBinding(KeyCode keyCode, InputItem item)
    {
        wndManager.PlaySound(new("UI/UI_Bubble"));
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

    private bool IsKeyConflict(KeyCode newKey, InputItem targetItem)
    {
        foreach (var inputItem in InputList)
        {
            if (inputItem.window.GetHashCode() != targetItem.window.GetHashCode()) continue;
            if (inputItem == targetItem) continue;

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
        if (_keyDisplay != null && _keyDisplay.TryGetKeyDown(out var newKey))
        {
            TryApplyRebind(newKey);
        }
    }

    private void TryApplyRebind(KeyCode newKey)
    {
        if (IsKeyConflict(newKey, _rebindingItem))
        {
            wndManager.PlaySound(new("UI/UI_Button_Back"));
            wndManager.CreatTip(new TipWndInfo
            {
                title = "按键冲突",
                desc = "\n该窗口下已有其他功能绑定了此按键，请选择其他按键。",
                optA_Text = "确定"
            });
            return;
        }

        SetSlotValue(_rebindingItem, _rebindingKeyCode, newKey);
        _haveInputChanged = true;
        wndManager.PlaySound(new("UI/UI_Ready"));
        ResetRebindState();
        RefreshKeyBindingDisplay();
    }

    private void ResetRebindState()
    {
        _rebindState = RebindState.None;
        _rebindingItem = null;

        if (_rebindHintText != null)
        {
            _rebindHintText.text = "点击按键以修改键位";
        }
    }

    private void HandleKeyCodeInput()
    {
        _escHandledThisFrame = false;

        if (_keyDisplay == null) return;

        // 改键模块内优先处理 Esc/右键 取消
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            if (_rebindSelectionRoot != null && _rebindSelectionRoot.activeSelf)
            {
                wndManager.PlaySound(new("UI/UI_Button_Back"));
                HideSelection();
                _escHandledThisFrame = true;
                return;
            }
            if (_rebindState == RebindState.WaitingForKey)
            {
                wndManager.PlaySound(new("UI/UI_Button_Back"));
                ResetRebindState();
                _escHandledThisFrame = true;
                return;
            }
        }

        // 等待按键状态：拦截按键用于修改绑定
        if (_rebindState == RebindState.WaitingForKey)
        {
            HandleRebindInput();
            return;
        }

        // 正常高亮状态
        _keyDisplay.HandleInput();
        _displayOnlyKeyDisplay?.HandleInput();
    }
}
