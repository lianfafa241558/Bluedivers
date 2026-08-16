using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 泛用的 SO 选择弹窗框架（PopupWindowContent）。
/// 数据由外部注入，通过一组委托控制每个条目的显示（图标/名称/类型/颜色/边框）。
/// 支持两种交互模式：
///  - 点击即确认（confirmMode=false，默认）：单击条目立即回调 onPicked 并关闭。
///  - 确认模式（confirmMode=true）：单击仅选中，需双击或点"确定"确认，可点"取消"放弃。
/// </summary>
/// <typeparam name="T">条目数据类型（通常为 ScriptableObject）</typeparam>
public class SOPickerPopup<T> : PopupWindowContent where T : UnityEngine.Object
{
    private readonly List<T> _items;
    private readonly Action<T> _onPicked;
    private readonly Func<T, Sprite> _getIcon;
    private readonly Func<T, string> _getName;
    private readonly Func<T, string> _getType;                       // 可选：次级信息行
    private readonly Func<T, Color> _getTypeColor;                   // 可选：类型文字颜色
    private readonly Func<T, (string path, Color color)> _getFrame;  // 可选：图标边框
    private readonly bool _confirmMode;                              // true=需确定/取消确认

    private string _search = "";
    private Vector2 _scroll;
    private T _selected;

    private const float ItemHeight = 48f;
    private const float ItemHeightNoType = 26f;
    private const float MaxHeight = 360f;
    private const float FooterHeight = 26f;

    public SOPickerPopup(List<T> items, Action<T> onPicked,
        Func<T, Sprite> getIcon, Func<T, string> getName,
        Func<T, string> getType = null,
        Func<T, Color> getTypeColor = null,
        Func<T, (string path, Color color)> getFrame = null,
        bool confirmMode = false)
    {
        _items = items;
        _onPicked = onPicked;
        _getIcon = getIcon;
        _getName = getName;
        _getType = getType;
        _getTypeColor = getTypeColor;
        _getFrame = getFrame;
        _confirmMode = confirmMode;
        _selected = items.Count > 0 ? items[0] : null;
    }

    public override Vector2 GetWindowSize()
    {
        var filtered = GetFiltered();
        int maxItems = Mathf.Min(filtered.Count, 12);
        float itemH = _getType != null ? ItemHeight : ItemHeightNoType;
        float height = 28 + maxItems * (itemH + 2) + 4;
        if (_confirmMode) height += FooterHeight;
        return new Vector2(320, Mathf.Min(height, MaxHeight + (_confirmMode ? FooterHeight : 0)));
    }

    private List<T> GetFiltered()
    {
        if (string.IsNullOrWhiteSpace(_search)) return _items;
        return _items.FindAll(i => _getName(i).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public override void OnGUI(Rect rect)
    {
        // 搜索栏
        _search = EditorGUI.TextField(new Rect(4, 4, rect.width - 8, 20), _search, EditorStyles.toolbarSearchField);

        float footerH = _confirmMode ? FooterHeight : 0f;
        var listRect = new Rect(4, 26, rect.width - 8, rect.height - 30 - footerH);
        var filtered = GetFiltered();
        float itemH = _getType != null ? ItemHeight : ItemHeightNoType;
        float viewHeight = filtered.Count * (itemH + 2);

        _scroll = GUI.BeginScrollView(listRect, _scroll,
            new Rect(0, 0, listRect.width - 16, Mathf.Max(viewHeight, listRect.height)));

        for (int i = 0; i < filtered.Count; i++)
        {
            T item = filtered[i];
            var itemRect = new Rect(0, i * (itemH + 2), listRect.width - 16, itemH);
            bool hover = itemRect.Contains(Event.current.mousePosition);
            bool isSelected = _confirmMode && item == _selected;

            // 背景
            Color bg = hover
                ? new Color(0.3f, 0.5f, 0.8f, 0.4f)
                : (isSelected ? new Color(0.3f, 0.5f, 1f, 0.35f) : new Color(0.25f, 0.25f, 0.28f, 0.5f));
            EditorGUI.DrawRect(itemRect, bg);

            var icon = _getIcon?.Invoke(item);
            bool hasType = _getType != null;

            if (hasType)
            {
                // 高条目（48px）：图标 38x38 + 可选边框 46x46
                var frameInfo = _getFrame?.Invoke(item);
                if (frameInfo.HasValue && !string.IsNullOrEmpty(frameInfo.Value.path))
                {
                    var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(frameInfo.Value.path);
                    if (frameSprite != null)
                    {
                        var fr = new Rect(itemRect.x + 2, itemRect.y + 1, 46, 46);
                        GUI.DrawTexture(fr, frameSprite.texture, ScaleMode.ScaleToFit, true, 0, frameInfo.Value.color, 0, 0);
                    }
                }
                var iconRect = new Rect(itemRect.x + 8, itemRect.y + 5, 38, 38);
                if (icon != null)
                    GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Box(iconRect, "");
            }
            else
            {
                // 矮条目（26px）：图标缩小到贴合行高，不画边框
                float iconSize = itemH - 10f;
                var iconRect = new Rect(itemRect.x + 8, itemRect.y + (itemH - iconSize) / 2f, iconSize, iconSize);
                if (icon != null)
                    GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Box(iconRect, "");
            }

            // 名称
            if (hasType)
            {
                var nameRect = new Rect(itemRect.x + 50, itemRect.y + 4, itemRect.width - 54, 18);
                GUI.Label(nameRect, _getName(item), EditorStyles.boldLabel);

                // 类型行
                var typeRect = new Rect(itemRect.x + 50, itemRect.y + 24, itemRect.width - 54, 16);
                var typeStyle = new GUIStyle(EditorStyles.miniLabel);
                var typeColor = _getTypeColor?.Invoke(item);
                typeStyle.normal.textColor = typeColor.HasValue ? typeColor.Value : Color.white;
                GUI.Label(typeRect, _getType(item), typeStyle);
            }
            else
            {
                var nameRect = new Rect(itemRect.x + 30, itemRect.y + (itemH - 18) / 2f, itemRect.width - 34, 18);
                GUI.Label(nameRect, _getName(item), EditorStyles.boldLabel);
            }

            // 点击
            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                if (_confirmMode)
                {
                    _selected = item;
                    // 双击确认
                    if (Event.current.clickCount >= 2)
                        Confirm();
                }
                else
                {
                    _onPicked?.Invoke(item);
                    editorWindow.Close();
                }
                Event.current.Use();
            }
        }

        GUI.EndScrollView();

        if (filtered.Count == 0)
        {
            GUI.Label(new Rect(4, 40, rect.width - 8, 24), "无匹配项", EditorStyles.centeredGreyMiniLabel);
        }

        // 确认模式底部：确定/取消
        if (_confirmMode)
        {
            float y = rect.height - FooterHeight + 4;
            var btnRect = new Rect(rect.width - 150, y, 70, 18);
            if (GUI.Button(btnRect, "取消"))
            {
                editorWindow.Close();
            }
            var okRect = new Rect(rect.width - 74, y, 70, 18);
            if (GUI.Button(okRect, "确定"))
            {
                Confirm();
            }
        }
    }

    private void Confirm()
    {
        if (_selected != null)
            _onPicked?.Invoke(_selected);
        editorWindow.Close();
    }
}
