using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FPSGame.Attribute;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AirdropData_SO 专属检视器（DataEditorWindow 右面板与标准 Inspector 均生效，优先级高于全局 fallback EditorOverride）。
/// 提供：
///  1) 顶部类型色标题条 + 数据冲突(同 ID / 同操作序列)红卡警示；
///  2) opter 以"单行方向键序列"编辑，每一步一个可直接选择方向的下拉框，＋/－增删步骤；
///  3) subAirdrop 使用 SOPickerPopup 从所有战备中挑选，将选中战备的 int ID 写入数组；
///  4) coolGroup 字段下方列出同冷却组的其它战备；
///  5) 保留字段中文名([InspectorName]) 与 [Compare] 条件显隐。
/// </summary>
[CustomEditor(typeof(AirdropData_SO))]
public class AirdropData_SOEditor : Editor
{
    private AirdropData_SO So => (AirdropData_SO)target;

    private static readonly string[] DirGlyphs = { "←", "↑", "→", "↓" };
    /// <summary>下拉框展示文案（索引与 DirectionEnum 顺序一致，纯箭头）</summary>
    private static readonly string[] DirOptions = { "←", "↑", "→", "↓" };

    // 全量战备引用缓存（编辑器内存活，依赖 editor.timeSinceStartup 节流刷新）
    private static List<AirdropData_SO> s_allSO = new List<AirdropData_SO>();
    private static Dictionary<int, AirdropData_SO> s_byId = new Dictionary<int, AirdropData_SO>();
    private static double s_lastReload;

    // 样式缓存
    private static Texture2D s_whiteTex;
    private static GUIStyle s_boldStyle;
    private static GUIStyle s_warnBoxStyle;
    private static GUIStyle s_redTextStyle;
    private static GUIStyle s_greyMiniStyle;
    private static GUIStyle s_sectionStyle;
    private static GUIStyle s_opterPopupStyle;      // 方向下拉框正常态
    private static GUIStyle s_opterPopupRedStyle;   // 方向下拉框冲突标红态
    private static readonly Dictionary<Color, GUIStyle> s_tagCache = new Dictionary<Color, GUIStyle>();

    // 保存去重
    private static bool s_saveScheduled;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureStatic();
        EnsureAllLoaded();

        AirdropData_SO so = So;
        if (so == null)
        {
            DrawDefaultInspector();
            return;
        }

        // —— 读取"正在编辑但仍未落盘"的当前值 ——
        int curId = PropInt("ID");
        string curGroup = PropString("coolGroup");
        List<DirectionEnum> curOpter = ReadOpterProp();

        // —— 关联战备计算 ——
        List<AirdropData_SO> idOthers = s_allSO.Where(o => o != so && o.ID == curId).ToList();
        List<AirdropData_SO> opOthers = curOpter.Count > 0
            ? s_allSO.Where(o => o != so && o.opter != null && o.opter.Length > 0
                                 && curOpter.SequenceEqual(o.opter)).ToList()
            : new List<AirdropData_SO>();
        List<AirdropData_SO> coolMembers = string.IsNullOrEmpty(curGroup)
            ? new List<AirdropData_SO>()
            : s_allSO.Where(o => o != so && string.Equals(o.coolGroup, curGroup, StringComparison.Ordinal)).ToList();

        DrawScriptHeader();
        DrawHero(so);

        // —— 冲突红卡 ——
        DrawConflictCard(so, curId, curOpter, idOthers, opOthers);

        EditorGUILayout.Space(2);

        // ================= 基础信息 =================
        SectionTitle("基础信息", so.Color);
        EditorGUILayout.Space(2);
        DrawField("isHide");
        DrawIdField("ID", idOthers.Count > 0);
        DrawField("showName");
        DrawField("desc");
        DrawField("icon");
        GUILayout.Space(6);

        // ================= 呼叫指令 =================
        SectionTitle("呼叫指令", so.Color);
        EditorGUILayout.Space(2);
        DrawOpterEditor(curOpter, opOthers.Count > 0);
        DrawField("type");
        DrawField("deliveryType");
        GUILayout.Space(6);

        // ================= 部署参数 =================
        SectionTitle("部署参数", so.Color);
        EditorGUILayout.Space(2);
        DrawField("cool");
        DrawField("arriveTime");
        DrawField("arriveHeight");
        DrawField("arriveCount");
        GUILayout.Space(6);

        // ================= 附属战备与冷却组 =================
        SectionTitle("附属战备与冷却组", so.Color);
        EditorGUILayout.Space(2);
        DrawSubAirdropEditor();
        DrawField("coolGroup");
        DrawCoolGroupBlock(curGroup, coolMembers);
        GUILayout.Space(6);

        // ================= 部署表现 =================
        SectionTitle("部署表现", so.Color);
        EditorGUILayout.Space(2);
        DrawField("showRange");
        DrawField("sustainTime");
        DrawField("creatObect");
        DrawField("useNormalPod");
        DrawField("permanentPod");
        GUILayout.Space(6);

        // ================= 行为开关 =================
        SectionTitle("行为开关", so.Color);
        EditorGUILayout.Space(2);
        DrawField("sustainHideBeacon");
        DrawField("useWarning");
        DrawField("authorize");
        DrawField("unAuthorizeVisible");
        DrawField("isDirect");
        DrawField("deathEnable");
        DrawField("teamCool");

        ApplyDirty();
    }

    // =====================================================
    //  绘制辅助
    // =====================================================

    private void DrawScriptHeader()
    {
        var scriptProp = serializedObject.FindProperty("m_Script");
        if (scriptProp != null)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(scriptProp, true);
        }
        EditorGUILayout.Space(2);
    }

    /// <summary>顶部色条 + 类型/投送/操作摘要</summary>
    private void DrawHero(AirdropData_SO so)
    {
        Color accent = so.Color;

        Rect bar = EditorGUILayout.GetControlRect(false, 3f);
        bar.width = Mathf.Max(bar.width - 1, 10f);
        EditorGUI.DrawRect(bar, accent);
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        {
            TagLabel(so.TypeName, accent);
            GUILayout.Space(4);
            TagLabel(DataEditorWindow.GetInspectorName(so.deliveryType), new Color(0.45f, 0.45f, 0.5f));

            GUILayout.FlexibleSpace();

            List<DirectionEnum> op = ReadOpterProp();
            if (op.Count > 0)
            {
                string opStr = OpterToText(op);
                GUILayout.Label(opStr, new GUIStyle(s_boldStyle) { fontSize = 16, normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.1f, 0.1f, 0.15f) } });
            }
            else
            {
                GUILayout.Label("无操作序列", s_greyMiniStyle);
            }
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    /// <summary>同 ID / 同操作序列 冲突红卡</summary>
    private void DrawConflictCard(AirdropData_SO so, int curId, List<DirectionEnum> curOpter,
        List<AirdropData_SO> idOthers, List<AirdropData_SO> opOthers)
    {
        bool hasId = idOthers.Count > 0;
        bool hasOp = opOthers.Count > 0;
        if (!hasId && !hasOp)
        {
            // ID 为 0 时给一个温和提醒
            if (curId == 0)
            {
                EditorGUILayout.HelpBox("ID 为 0，运行时将被 airdropDic 收录；若存在多个 ID=0 的战备会导致字典键冲突。", MessageType.Warning);
            }
            return;
        }

        EditorGUILayout.BeginVertical(s_warnBoxStyle);
        {
            GUILayout.Label("⚠ 数据冲突", s_redTextStyle);

            if (hasId)
            {
                string names = string.Join("、", idOthers.Select(o => $"「{DisplayNameOf(o)}」[{o.ID}]"));
                GUILayout.Label($"● ID 冲突：与 {names} 使用了相同 ID = {curId}", s_redTextStyle);
            }
            if (hasOp)
            {
                string opStr = OpterToText(curOpter);
                string names = string.Join("、", opOthers.Select(o => $"「{DisplayNameOf(o)}」[{o.ID}]"));
                GUILayout.Label($"● 操作序列冲突：{opStr} 与 {names} 相同，输入时无法区分调用", s_redTextStyle);
            }

            GUILayout.Space(2);
            GUILayout.Label("点击上方冲突对象可在 Project 中定位", s_greyMiniStyle);
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>opter 单行方向键序列编辑器</summary>
    private void DrawOpterEditor(List<DirectionEnum> curOpter, bool conflict)
    {
        EditorGUILayout.BeginHorizontal();
        {
            // 标签
            var labelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = conflict ? Color.red : (EditorGUIUtility.isProSkin ? Color.white : Color.black);
            GUILayout.Label("操作", labelStyle, GUILayout.Width(EditorGUIUtility.labelWidth - 8));

            // 方向序列：每一步一个下拉框，直接选择方向（无需逐个循环点击）
            int n = _opterProp().arraySize;
            GUIStyle popupStyle = conflict ? s_opterPopupRedStyle : s_opterPopupStyle;
            for (int i = 0; i < n; i++)
            {
                var element = _opterProp().GetArrayElementAtIndex(i);
                int cur = Mathf.Clamp(element.enumValueIndex, 0, DirOptions.Length - 1);

                var rect = GUILayoutUtility.GetRect(44f, 22f);
                int pick = EditorGUI.Popup(rect, cur, DirOptions, popupStyle);
                if (pick != cur)
                    element.enumValueIndex = pick;
            }

            // ＋ 添加一步 / － 删除最后一步
            if (GUILayout.Button(new GUIContent("＋", "添加一步"), GUILayout.Width(26), GUILayout.Height(26)))
            {
                int size = _opterProp().arraySize;
                _opterProp().arraySize = size + 1;
            }
            if (n > 0 && GUILayout.Button(new GUIContent("－", "删除最后一步"), GUILayout.Width(26), GUILayout.Height(26)))
            {
                _opterProp().arraySize = n - 1;
            }

            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.EndHorizontal();

        // 辅助说明
        if (curOpter.Count == 0)
        {
            GUILayout.Label("无操作序列：无法通过方向键呼叫，可作为附属战备/任务战备被直接呼出。", s_greyMiniStyle);
        }
        else if (conflict)
        {
            string opStr = OpterToText(curOpter);
            string names = string.Join("、", s_allSO
                .Where(o => o != So && o.opter != null && o.opter.Length > 0 && curOpter.SequenceEqual(o.opter))
                .Select(o => $"「{DisplayNameOf(o)}」[{o.ID}]"));
            GUILayout.Label($"该序列 {opStr} 与 {names} 冲突", new GUIStyle(s_greyMiniStyle) { normal = { textColor = Color.red } });
        }
        GUILayout.Space(4);
    }

    /// <summary>subAirdrop：SOPickerPopup 挑选战备，将其 int ID 写入数组</summary>
    private void DrawSubAirdropEditor()
    {
        var prop = _subProp();
        int size = prop.arraySize;

        // Header：标签 + 计数 + 添加按钮
        EditorGUILayout.BeginHorizontal();
        {
            GUILayout.Label("附属战备", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 8));
            GUILayout.Label($"{size} 项", s_greyMiniStyle);

            GUILayout.FlexibleSpace();
            if (size > 0)
            {
                if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(44)))
                    prop.ClearArray();
            }
            if (GUILayout.Button("＋ 添加战备", EditorStyles.miniButton, GUILayout.Width(88)))
            {
                OpenAirdropPicker(picked =>
                {
                    // 已存在则不重复添加
                    for (int i = 0; i < prop.arraySize; i++)
                    {
                        if (prop.GetArrayElementAtIndex(i).intValue == picked.ID)
                            return;
                    }
                    prop.arraySize = prop.arraySize + 1;
                    prop.GetArrayElementAtIndex(prop.arraySize - 1).intValue = picked.ID;
                });
            }
        }
        EditorGUILayout.EndHorizontal();

        if (size == 0)
        {
            GUILayout.Label("无附属战备。附属战备会在装载主战备时一并加入可用列表（如飞鹰的多种攻击方式）。", s_greyMiniStyle);
        }
        else
        {
            for (int i = 0; i < size; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                int subId = element.intValue;
                AirdropData_SO refSO = subId > 0 && s_byId.TryGetValue(subId, out var found) ? found : null;
                bool invalid = refSO == null;

                EditorGUILayout.BeginHorizontal();
                {
                    // 类型色圆点 / 图标（与冷却组行共用画法）
                    var dotRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
                    DrawSoIcon(dotRect, refSO);

                    // 名称与有效性提示
                    string text;
                    Color textColor;
                    if (invalid)
                    {
                        text = $"无效 ID：{subId}（未找到对应战备）";
                        textColor = Color.red;
                    }
                    else
                    {
                        text = $"{DisplayNameOf(refSO)} [{refSO.ID}]";
                        textColor = refSO == So ? new Color(1f, 0.8f, 0.2f) : (EditorGUIUtility.isProSkin ? Color.white : Color.black);
                        if (refSO == So)
                            text += "（自身）";
                    }

                    var labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
                    labelStyle.normal.textColor = textColor;
                    GUILayout.Label(new GUIContent(text, refSO == null ? "该 ID 未找到对应战备" : $"{refSO.TypeName}\n点击右侧按钮可替换"), labelStyle);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("选择", EditorStyles.miniButton, GUILayout.Width(48)))
                    {
                        int capture = i;
                        OpenAirdropPicker(picked => { prop.GetArrayElementAtIndex(capture).intValue = picked.ID; });
                    }
                    if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        prop.DeleteArrayElementAtIndex(i);
                        i--;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        GUILayout.Space(2);
    }

    /// <summary>coolGroup 同组战备展示：与附属战备同款"图标 + 文本"行（非按钮），单击行可定位</summary>
    private void DrawCoolGroupBlock(string group, List<AirdropData_SO> members)
    {
        if (string.IsNullOrEmpty(group))
        {
            GUILayout.Label("留空 = 独立冷却，不与其他战备共享冷却。", s_greyMiniStyle);
            return;
        }

        int total = members.Count + 1; // 含当前自身
        EditorGUILayout.BeginHorizontal();
        {
            GUILayout.Label($"同冷却组「{group}」 · 共 {total} 项（冷却共享）",
                new GUIStyle(s_boldStyle) { normal = { textColor = So.Color } });
            GUILayout.FlexibleSpace();
            GUILayout.Label("单击行定位", s_greyMiniStyle);
        }
        EditorGUILayout.EndHorizontal();

        // 全体成员（含当前自身）按 ID 升序排列，当前项以黄色「（当前）」标记
        var rows = new List<AirdropData_SO>(members.Count + 1) { So };
        rows.AddRange(members);
        rows.Sort((a, b) => a.ID.CompareTo(b.ID));

        if (members.Count == 0)
            GUILayout.Label("组内暂无其它成员。", s_greyMiniStyle);

        foreach (var m in rows)
        {
            bool isSelf = m == So;
            var rowRect = EditorGUILayout.GetControlRect(false, 20f);
            DrawSoIcon(new Rect(rowRect.x, rowRect.y + 2, 16f, 16f), m);

            var textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            textStyle.normal.textColor = isSelf
                ? new Color(1f, 0.8f, 0.2f)
                : (EditorGUIUtility.isProSkin ? Color.white : Color.black);
            string label = $"{DisplayNameOf(m)} [{m.ID}]" + (isSelf ? "　（当前）" : "");
            GUI.Label(new Rect(rowRect.x + 22, rowRect.y, rowRect.width - 22, rowRect.height),
                new GUIContent(label, isSelf ? "当前编辑的战备" : m.TypeName + "\n单击定位到该战备"), textStyle);

            if (!isSelf)
            {
                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    EditorGUIUtility.PingObject(m);
                    Selection.activeObject = m;
                    Event.current.Use();
                }
            }
        }
        GUILayout.Space(2);
    }

    /// <summary>绘制战备小图标：有 icon 用 IconColor 染色，否则画类型色块（null 画红块）</summary>
    private static void DrawSoIcon(Rect rect, AirdropData_SO so)
    {
        if (so == null)
        {
            GUI.DrawTexture(rect, s_whiteTex, ScaleMode.ScaleToFit, true, 0, Color.red, 0, 0);
            return;
        }

        if (so.icon != null)
            GUI.DrawTexture(rect, so.icon.texture, ScaleMode.ScaleToFit, true, 0, so.IconColor, 0, 0);
        else
            GUI.DrawTexture(rect, s_whiteTex, ScaleMode.ScaleToFit, true, 0, so.Color, 0, 0);
    }

    /// <summary>弹出全部战备选择器（排除当前 SO）</summary>
    private void OpenAirdropPicker(Action<AirdropData_SO> onPicked)
    {
        EnsureAllLoaded();
        var list = s_allSO.Where(o => o != So).ToList();
        Rect anchor = GUILayoutUtility.GetLastRect();

        var popup = new SOPickerPopup<AirdropData_SO>(
            list,
            picked =>
            {
                if (picked == null) return;
                onPicked?.Invoke(picked);
                ApplyDirty();
                Repaint();
            },
            o => o.icon,
            o => string.IsNullOrEmpty(o.showName) ? o.name : o.showName,
            o => $"ID:{o.ID} · {o.TypeName}",
            o => o.Color);

        PopupWindow.Show(anchor, popup);
    }

    // =====================================================
    //  普通字段绘制（中文化 + Compare 条件显隐）
    // =====================================================

    private void DrawField(string name)
    {
        var p = serializedObject.FindProperty(name);
        if (p == null) return;

        if (!ShouldShow(p)) return;
        EditorGUILayout.PropertyField(p, new GUIContent(LabelOf(name)), true);
    }

    private void DrawIdField(string name, bool conflict)
    {
        var p = serializedObject.FindProperty(name);
        if (p == null) return;

        Color old = GUI.color;
        if (conflict) GUI.color = Color.red;
        EditorGUILayout.PropertyField(p, new GUIContent(LabelOf(name)), true);
        GUI.color = old;

        if (conflict)
        {
            string names = string.Join("、", s_allSO
                .Where(o => o != So && o.ID == p.intValue)
                .Select(o => $"「{DisplayNameOf(o)}」[{o.ID}]"));
            GUILayout.Label($"与 {names} 的 ID 重复，运行时 airdropDic 将发生键冲突。",
                new GUIStyle(s_greyMiniStyle) { normal = { textColor = Color.red } });
        }
    }

    /// <summary>Compare 条件显隐（与全局 EditorOverride 保持一致）</summary>
    private bool ShouldShow(SerializedProperty p)
    {
        var fieldInfo = typeof(AirdropData_SO).GetField(p.name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fieldInfo == null) return true;
        var cmp = fieldInfo.GetCustomAttribute<CompareAttribute>();
        if (cmp == null) return true;
        return CustomLabelDrawer.ShouldDisplayField(p, cmp);
    }

    // =====================================================
    //  值读取工具（保证读到"编辑中未落盘"的当前值）
    // =====================================================

    private SerializedProperty _opterProp() => serializedObject.FindProperty("opter");
    private SerializedProperty _subProp() => serializedObject.FindProperty("subAirdrop");

    private int PropInt(string name) => serializedObject.FindProperty(name)?.intValue ?? 0;
    private string PropString(string name) => serializedObject.FindProperty(name)?.stringValue ?? "";

    private List<DirectionEnum> ReadOpterProp()
    {
        var list = new List<DirectionEnum>();
        var p = _opterProp();
        if (p == null) return list;
        for (int i = 0; i < p.arraySize; i++)
            list.Add((DirectionEnum)p.GetArrayElementAtIndex(i).enumValueIndex);
        return list;
    }

    private static string OpterToText(List<DirectionEnum> opter)
    {
        if (opter == null || opter.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var d in opter)
            sb.Append(DirGlyphs[(int)d]);
        return sb.ToString();
    }

    private static string DisplayNameOf(AirdropData_SO so)
        => so == null ? "(空)" : (string.IsNullOrEmpty(so.showName) ? so.name : so.showName);

    // =====================================================
    //  静态资源与样式
    // =====================================================

    private void ApplyDirty()
    {
        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            ScheduleSave();
        }
    }

    private static void ScheduleSave()
    {
        if (s_saveScheduled) return;
        s_saveScheduled = true;
        EditorApplication.delayCall += () =>
        {
            s_saveScheduled = false;
            AssetDatabase.SaveAssets();
        };
    }

    private static void EnsureAllLoaded()
    {
        double now = EditorApplication.timeSinceStartup;
        if (s_allSO != null && now - s_lastReload < 1.0) return;
        s_lastReload = now;

        var list = new List<AirdropData_SO>();
        var byId = new Dictionary<int, AirdropData_SO>();
        foreach (string guid in AssetDatabase.FindAssets("t:AirdropData_SO"))
        {
            var so = AssetDatabase.LoadAssetAtPath<AirdropData_SO>(AssetDatabase.GUIDToAssetPath(guid));
            if (so == null) continue;
            list.Add(so);
            if (!byId.ContainsKey(so.ID))
                byId[so.ID] = so;
        }
        s_allSO = list;
        s_byId = byId;
    }

    private static void EnsureStatic()
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

        if (s_opterPopupStyle == null)
        {
            s_opterPopupStyle = new GUIStyle(EditorStyles.popup)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fixedHeight = 22,
            };
            s_opterPopupRedStyle = new GUIStyle(s_opterPopupStyle);
            s_opterPopupRedStyle.normal.textColor = Color.red;
        }
    }

    private void TagLabel(string text, Color bgColor)
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

    private static Color ContrastColor(Color c)
        => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) > 0.55f ? Color.black : Color.white;

    private static string LabelOf(string name)
    {
        switch (name)
        {
            case "isHide": return "在战备配置界面隐藏";
            case "ID": return "ID";
            case "showName": return "显示名称";
            case "desc": return "描述";
            case "icon": return "图标";
            case "opter": return "操作";
            case "type": return "类型";
            case "deliveryType": return "投送方式";
            case "cool": return "冷却";
            case "arriveTime": return "部署时间";
            case "arriveHeight": return "部署高度";
            case "arriveCount": return "部署次数";
            case "subAirdrop": return "附属战备";
            case "coolGroup": return "冷却组";
            case "showRange": return "影响范围的显示";
            case "sustainTime": return "持续时间";
            case "creatObect": return "创建的物体";
            case "useNormalPod": return "使用标准空投舱";
            case "sustainHideBeacon": return "持续时间时隐藏信息";
            case "useWarning": return "危险警告";
            case "permanentPod": return "空投舱永久存在";
            case "authorize": return "需要授权";
            case "unAuthorizeVisible": return "未授权时可见";
            case "isDirect": return "直接释放";
            case "deathEnable": return "死亡时可用";
            case "teamCool": return "团队冷却";
            default: return ObjectNames.NicifyVariableName(name);
        }
    }

    /// <summary>小节标题（彩色左条 + 标题 + 细分隔线）</summary>
    private void SectionTitle(string title, Color accent)
    {
        var lineRect = EditorGUILayout.GetControlRect(false, 22f);
        EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y + 4, 3, 14), accent);
        GUI.Label(new Rect(lineRect.x + 9, lineRect.y + 2, lineRect.width - 12, 18), title, s_sectionStyle);
        EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.yMax + 1, lineRect.width, 1),
            EditorGUIUtility.isProSkin ? new Color(0.5f, 0.5f, 0.5f, 0.25f) : new Color(0, 0, 0, 0.12f));
        EditorGUILayout.Space(2);
    }
}
