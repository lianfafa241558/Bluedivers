using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{

/// <summary>
/// SO 专属检视器基类：收敛 AirdropData_SOEditor / CampData_SOEditor 的公共件 ——
/// 样式缓存、脚本头、小节标题、色条、类型标签、字段/行绘制、资产缓存（1s 节流）、SOPicker 弹窗、延迟保存。
/// 子类只需实现 OnInspectorGUI（按需重写 LabelOf / DrawField / 追加自己的样式）。
/// </summary>
public abstract class DataSOEditorBase : Editor
{
    private const double ReloadInterval = 1.0;

    //===============================//
    // 共享样式（子类沿用 s_* 命名，避免逐处改名）
    //===============================//

    protected static Texture2D s_whiteTex;
    protected static GUIStyle s_boldStyle;
    protected static GUIStyle s_greyMiniStyle;
    protected static GUIStyle s_sectionStyle;
    protected static GUIStyle s_redTextStyle;
    protected static GUIStyle s_warnBoxStyle;
    protected static readonly Dictionary<Color, GUIStyle> s_tagCache = new Dictionary<Color, GUIStyle>();

    // 保存去重（所有子类共用一个调度位）
    private static bool s_saveScheduled;

    /// <summary>建共享样式（幂等；子类可在 OnInspectorGUI 开头调用后再建自己的样式）</summary>
    protected static void EnsureStyles()
    {
        if (s_whiteTex == null)
        {
            s_whiteTex = new Texture2D(1, 1);
            s_whiteTex.SetPixel(0, 0, Color.white);
            s_whiteTex.Apply();
        }

        if (s_boldStyle == null)
            s_boldStyle = new GUIStyle(EditorStyles.boldLabel);

        if (s_greyMiniStyle == null)
            s_greyMiniStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.45f, 0.45f, 0.45f) },
                wordWrap = true,
            };

        if (s_sectionStyle == null)
            s_sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

        if (s_redTextStyle == null)
            s_redTextStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = new Color(1f, 0.4f, 0.4f) },
                wordWrap = true,
            };

        if (s_warnBoxStyle == null)
        {
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.5f, 0.05f, 0.05f, 0.32f));
            bg.Apply();
            s_warnBoxStyle = new GUIStyle
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 4, 4),
                normal = { background = bg },
            };
        }
    }

    //===============================//
    // 通用绘制件
    //===============================//

    /// <summary>脚本头（灰色不可编辑的 m_Script）</summary>
    protected void DrawScriptHeader()
    {
        var scriptProp = serializedObject.FindProperty("m_Script");
        if (scriptProp != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(scriptProp, true);
        }
        EditorGUILayout.Space(2);
    }

    /// <summary>小节标题（彩色左条 + 标题 + 细分隔线）</summary>
    protected static void SectionTitle(string title, Color accent)
    {
        var lineRect = EditorGUILayout.GetControlRect(false, 22f);
        EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y + 4, 3, 14), accent);
        GUI.Label(new Rect(lineRect.x + 9, lineRect.y + 2, lineRect.width - 12, 18), title, s_sectionStyle);
        EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.yMax + 1, lineRect.width, 1),
            EditorGUIUtility.isProSkin ? new Color(0.5f, 0.5f, 0.5f, 0.25f) : new Color(0, 0, 0, 0.12f));
        EditorGUILayout.Space(2);
    }

    /// <summary>顶部类型色条（3px + 下留白）</summary>
    protected static void ColorBar(Color accent)
    {
        Rect bar = EditorGUILayout.GetControlRect(false, 3f);
        bar.width = Mathf.Max(bar.width - 1, 10f);
        EditorGUI.DrawRect(bar, accent);
        GUILayout.Space(6);
    }

    /// <summary>类型色标签（背景色 + 自动黑/白字，按底色缓存样式）</summary>
    protected static void TagLabel(string text, Color bgColor)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!s_tagCache.TryGetValue(bgColor, out GUIStyle style))
        {
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, bgColor);
            bg.Apply();
            style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 2, 2),
                normal = { background = bg, textColor = ContrastColor(bgColor) },
            };
            s_tagCache[bgColor] = style;
        }
        GUILayout.Label(text, style);
    }

    protected static Color ContrastColor(Color c)
        => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) > 0.55f ? Color.black : Color.white;

    /// <summary>列表块头：加粗标题 + "N 项"（右侧按钮由调用方补，须配 EndBlockHeader）。
    /// labelWidth &gt; 0 时标题用固定宽度（与字段标签对齐）。</summary>
    protected static void BeginBlockHeader(string label, int count, float labelWidth = 0f, string countSuffix = "项")
    {
        EditorGUILayout.BeginHorizontal();
        if (labelWidth > 0f)
            GUILayout.Label(label, s_boldStyle, GUILayout.Width(labelWidth));
        else
            GUILayout.Label(label, s_boldStyle);
        GUILayout.Label($"{count} {countSuffix}", s_greyMiniStyle);
        GUILayout.FlexibleSpace();
    }

    protected static void EndBlockHeader() => EditorGUILayout.EndHorizontal();

    /// <summary>列表行文字（左对齐 + 指定颜色）</summary>
    protected static void RowLabel(string text, Color textColor, string tooltip = null)
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        style.normal.textColor = textColor;
        GUILayout.Label(new GUIContent(text, tooltip), style);
    }

    /// <summary>行内小图标：有 sprite 则以 tint 染色绘制，否则画 tint 色块（无效项传红色）</summary>
    protected static void DrawTintedIcon(Rect rect, Sprite sprite, Color tint)
    {
        if (sprite != null)
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true, 0, tint, 0, 0);
        else
            GUI.DrawTexture(rect, s_whiteTex, ScaleMode.ScaleToFit, true, 0, tint, 0, 0);
    }

    //===============================//
    // 字段绘制
    //===============================//

    /// <summary>字段中文名：[InspectorName] → Nicify；子类可重写补自己的映射表</summary>
    protected virtual string LabelOf(string fieldName)
    {
        var f = target != null
            ? target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            : null;
        var attr = f != null ? f.GetCustomAttribute<InspectorNameAttribute>() : null;
        return attr != null ? attr.displayName : ObjectNames.NicifyVariableName(fieldName);
    }

    /// <summary>普通字段绘制（中文标签 + PropertyField）</summary>
    protected virtual void DrawField(string fieldName, bool children = true)
    {
        var p = serializedObject.FindProperty(fieldName);
        if (p == null) return;
        EditorGUILayout.PropertyField(p, new GUIContent(LabelOf(fieldName)), children);
    }

    //===============================//
    // 资产缓存 / SO 选择弹窗
    //===============================//

    /// <summary>全量扫描某类 SO（1 秒节流）并按 key 建索引，写回传入的 list/dict</summary>
    protected static void RefreshAssetCache<T>(string typeFilter, ref double lastReload,
        ref List<T> list, ref Dictionary<int, T> byKey, Func<T, int> keyOf) where T : UnityEngine.Object
    {
        double now = EditorApplication.timeSinceStartup;
        if (list != null && list.Count > 0 && now - lastReload < ReloadInterval) return;
        lastReload = now;

        var newList = new List<T>();
        var newByKey = new Dictionary<int, T>();
        foreach (string guid in AssetDatabase.FindAssets(typeFilter))
        {
            var so = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (so == null) continue;
            newList.Add(so);
            int key = keyOf(so);
            if (!newByKey.ContainsKey(key)) newByKey[key] = so;
        }
        list = newList;
        byKey = newByKey;
    }

    /// <summary>弹 SO 选择器（SOPickerPopup），贴在 anchor（通常取按钮的 GetLastRect）旁显示</summary>
    protected static void ShowSoPicker<T>(List<T> items, Rect anchor, Action<T> onPicked,
        Func<T, Sprite> getIcon, Func<T, string> getName,
        Func<T, string> getType = null, Func<T, Color> getTypeColor = null) where T : UnityEngine.Object
    {
        PopupWindow.Show(anchor, new SOPickerPopup<T>(items, onPicked, getIcon, getName, getType, getTypeColor));
    }

    //===============================//
    // 保存
    //===============================//

    /// <summary>有改动才 Apply + SetDirty + 节流 SaveAssets（数据编辑器右面板靠它落盘）</summary>
    protected void ApplyDirty()
    {
        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            ScheduleSave();
        }
    }

    protected static void ScheduleSave()
    {
        if (s_saveScheduled) return;
        s_saveScheduled = true;
        EditorApplication.delayCall += () =>
        {
            s_saveScheduled = false;
            AssetDatabase.SaveAssets();
        };
    }
}
}
