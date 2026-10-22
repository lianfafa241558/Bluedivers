using System.Collections.Generic;
using System.Reflection;
using FPSGame.Data;
using FPSGame.GameContract;
using FPSGame.GameData;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{

/// <summary>
/// CampData_SO 专属检视器（标准 Inspector 与数据编辑器右面板均生效，优先级高于全局 fallback EditorOverride）。
/// 公共绘制/保存逻辑见基类 DataSOEditorBase；
/// 任务区字段一行一项（图标 + 名称 + 选择/×），挑选走 SOPickerPopup&lt;MissionData_SO&gt;，选中后把其 type 写回数组，
/// 单击行文字在 Project 中定位该任务配置；
/// 波次模板 / 巡逻队用折叠卡自绘，摘要把"权重为 0、巡逻队名未定义、重名、没人引用"这类配置坑直接标出来。
/// </summary>
[CustomEditor(typeof(CampData_SO))]
public class CampData_SOEditor : DataSOEditorBase
{
    private CampData_SO So => (CampData_SO)target;

    // 全量任务配置缓存（编辑器内存活，基类 RefreshAssetCache 按 1s 节流刷新）
    private static List<MissionData_SO> s_allMissions = new List<MissionData_SO>();
    private static Dictionary<int, MissionData_SO> s_byType = new Dictionary<int, MissionData_SO>();
    private static double s_lastReload;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureStyles();
        EnsureRowStyles();
        EnsureMissionsLoaded();

        CampData_SO so = So;
        if (so == null)
        {
            DrawDefaultInspector();
            return;
        }

        DrawScriptHeader();
        DrawHero(so);

        EditorGUILayout.Space(2);

        // ================= 基本 =================
        SectionTitle("基本", so.Color);
        EditorGUILayout.Space(2);
        DrawField("enemyVarietyType");
        DrawField("ShowName");
        DrawField("Sprite");
        DrawField("KillSprite");
        DrawField("Color");
        DrawField("Suffix");
        DrawField("Desc");
        DrawField("NestTerrainItem");
        GUILayout.Space(6);

        // ================= 任务 =================
        SectionTitle("任务", so.Color);
        EditorGUILayout.Space(2);
        DrawMissionEnumList("mainTypes", "允许的主线类型", "主线");
        DrawMissionEnumList("extraTypes", "允许的额外类型", "次要");
        DrawMissionEnumList("nestTypes", "巢穴类型", "巢穴");
        DrawField("mainTypesBackup"); // Unity 自带的数组绘制，不做修改
        GUILayout.Space(6);

        // ================= 波次 =================
        SectionTitle("波次", so.Color);
        EditorGUILayout.Space(2);
        DrawField("WaveCool");
        DrawField("WaveUseObject");
        DrawTemplates();
        GUILayout.Space(6);

        // ================= 巡逻队 =================
        SectionTitle("巡逻队", so.Color);
        EditorGUILayout.Space(2);
        DrawField("PatrolCreatValue");
        DrawPatrolCfgs();

        ApplyDirty();
    }

    /// <summary>顶部色条 + 名称/类型标签</summary>
    private void DrawHero(CampData_SO so)
    {
        ColorBar(so.Color);

        EditorGUILayout.BeginHorizontal();
        {
            TagLabel(string.IsNullOrEmpty(so.ShowName) ? so.name : so.ShowName, so.Color);
            GUILayout.Space(4);
            TagLabel(so.enemyVarietyType.ToString(), new Color(0.45f, 0.45f, 0.5f));
            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    // =====================================================
    //  MissionEnum[] 列表：一行一项 + SOPicker 挑选任务配置
    // =====================================================

    /// <summary>单字段：头部（中文名 + 计数 + 清空 + 添加）+ 每个已选类型一行（图标 + 名称 + 选择/×）。
    /// categoryFilter：挑选列表只显示该 [InspectorName] 前缀分类（如"主线"）。</summary>
    private void DrawMissionEnumList(string fieldName, string label, string categoryFilter)
    {
        var prop = serializedObject.FindProperty(fieldName);
        if (prop == null) return;
        int size = prop.arraySize;

        BeginBlockHeader(label, size);
        if (size > 0 && GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(44)))
        {
            prop.ClearArray();
            serializedObject.ApplyModifiedProperties();
        }
        if (GUILayout.Button("＋ 添加任务", EditorStyles.miniButton, GUILayout.Width(88)))
        {
            // 紧跟按钮绘制取 Rect，弹窗才能贴着按下的按钮弹出
            OpenMissionPicker(prop, -1, categoryFilter, GUILayoutUtility.GetLastRect());
        }
        EndBlockHeader();

        if (size == 0)
            GUILayout.Label("暂无。点击右上角 ＋ 添加任务 从全部任务配置中挑选。", s_greyMiniStyle);

        for (int i = 0; i < size; i++)
        {
            int value = prop.GetArrayElementAtIndex(i).intValue;
            MissionData_SO refSO = s_byType.TryGetValue(value, out var found) ? found : null;

            EditorGUILayout.BeginHorizontal();
            {
                // 图标（任务配置 sprite 染该任务自己的颜色；无配置画红块）
                var iconRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
                DrawTintedIcon(iconRect, refSO != null ? refSO.sprite : null, refSO != null ? MissionColor(refSO) : Color.red);

                // 名称行本身就是跳转入口：单击在 Project 中选中该任务配置。
                // 命中区只取文字自身宽度，别扩到右侧按钮上 —— 本帧先执行到这里，按钮还没消费事件，会误触发。
                string text = refSO == null ? $"未知类型：{value}（未找到对应任务配置）" : MissionDisplayName(refSO);
                GUIStyle labelStyle = refSO == null
                    ? RowStyle(new Color(1f, 0.4f, 0.4f))
                    : RowStyle(EditorGUIUtility.isProSkin ? Color.white : Color.black);
                var nameRect = GUILayoutUtility.GetRect(new GUIContent(text), labelStyle, GUILayout.Height(18f));
                GUI.Label(nameRect, new GUIContent(text,
                        refSO == null ? "该枚举值没有对应的任务配置，运行时取不到任务数据"
                                      : $"{AssetDatabase.GetAssetPath(refSO)}\n单击在 Project 中定位"),
                    labelStyle);
                if (refSO != null)
                    LinkClick(nameRect, refSO);

                GUILayout.FlexibleSpace();

                int capture = i;
                if (GUILayout.Button("选择", EditorStyles.miniButton, GUILayout.Width(48)))
                    OpenMissionPicker(prop, capture, categoryFilter, GUILayoutUtility.GetLastRect());
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    prop.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    i--;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        GUILayout.Space(2);
    }

    /// <summary>弹出任务配置选择器（只显示 categoryFilter 分类；排除占位符与本字段已选项）；
    /// replaceIdx &lt; 0 = 追加，否则替换该行；anchor = 按下的按钮 Rect，弹窗贴其弹出</summary>
    private void OpenMissionPicker(SerializedProperty prop, int replaceIdx, string categoryFilter, Rect anchor)
    {
        EnsureMissionsLoaded();
        var selected = ReadSelected(prop);
        var list = s_allMissions.FindAll(o => o != null
            && !IsPlaceholderType(o.type)
            && !selected.Contains((int)o.type)
            && TypeLabel(o.type).StartsWith(categoryFilter + "/"));

        ShowSoPicker(list, anchor,
            picked =>
            {
                if (picked == null) return;
                if (replaceIdx < 0)
                {
                    int n = prop.arraySize;
                    prop.arraySize = n + 1;
                    prop.GetArrayElementAtIndex(n).intValue = (int)picked.type;
                }
                else
                {
                    prop.GetArrayElementAtIndex(replaceIdx).intValue = (int)picked.type;
                }
                prop.serializedObject.ApplyModifiedProperties();
            },
            o => o.sprite,
            o => MissionDisplayName(o),
            o => TypeLabel(o.type),
            o => CategoryColor(o.type));
    }

    private static HashSet<int> ReadSelected(SerializedProperty prop)
    {
        var set = new HashSet<int>();
        for (int i = 0; i < prop.arraySize; i++)
            set.Add(prop.GetArrayElementAtIndex(i).intValue);
        return set;
    }

    // =====================================================
    //  任务配置缓存与显示工具
    // =====================================================

    private static void EnsureMissionsLoaded()
        => RefreshAssetCache("t:MissionData_SO", ref s_lastReload, ref s_allMissions, ref s_byType, o => (int)o.type);

    /// <summary>任务配置显示名 = desc + 枚举名（desc 为空时只显示枚举名）</summary>
    private static string MissionDisplayName(MissionData_SO so)
    {
        if (so == null) return "(空)";
        return so.name;
        //string typeName = TypeLabel(so.type);
        //return string.IsNullOrEmpty(so.desc) ? typeName : $"{so.desc}·{typeName}";
    }

    /// <summary>任务配置自己的颜色：主线配置取其 color 字段；支线配置无颜色字段，回退分类色</summary>
    private static Color MissionColor(MissionData_SO so)
    {
        if (so is MissionMainData_SO main) return main.color;
        return CategoryColor(so.type);
    }

    private static readonly Dictionary<int, string> s_typeLabelCache = new Dictionary<int, string>();

    // 折叠卡行高与行尾小按钮宽度（手工切列用，避免 GUILayout.Width 撞 labelWidth）
    private const float RowH = 18f;
    private const float MiniBtnW = 20f;

    /// <summary>枚举值 → [InspectorName] 中文标签（缓存）</summary>
    private static string TypeLabel(MissionEnum v)
    {
        int key = (int)v;
        if (s_typeLabelCache.TryGetValue(key, out var s)) return s;

        var fi = typeof(MissionEnum).GetField(v.ToString(), BindingFlags.Public | BindingFlags.Static);
        var attr = fi != null ? fi.GetCustomAttribute<InspectorNameAttribute>() : null;
        s = attr != null ? attr.displayName : v.ToString();
        s_typeLabelCache[key] = s;
        return s;
    }

    private static bool IsPlaceholderType(MissionEnum v) => TypeLabel(v).Contains("占位符");

    /// <summary>分类色（按 [InspectorName] 的 "/" 前缀）</summary>
    private static Color CategoryColor(MissionEnum v)
    {
        string label = TypeLabel(v);
        int slash = label.IndexOf('/');
        string cat = slash > 0 ? label.Substring(0, slash) : "其他";
        switch (cat)
        {
            case "主线": return new Color(0.95f, 0.45f, 0.40f);
            case "撤离": return new Color(0.35f, 0.80f, 0.45f);
            case "次要": return new Color(0.40f, 0.65f, 0.95f);
            case "巢穴": return new Color(0.75f, 0.50f, 0.90f);
            case "子任务": return new Color(0.95f, 0.75f, 0.35f);
            default: return new Color(0.6f, 0.6f, 0.6f);
        }
    }

    // =====================================================
    //  波次模板 templates：CampTemplate{ name, TierCfg[], SKVP<string,int>[] }
    // =====================================================

    /// <summary>模板列表：块头（计数/清空/添加）+ 每个模板一张折叠卡。
    /// WaveManager 是"随机抽一个模板"，所以摘要要能横向比对权重与巡逻队配置。</summary>
    private void DrawTemplates()
    {
        var prop = serializedObject.FindProperty("templates");
        if (prop == null) return;

        BeginBlockHeader("波次模板", prop.arraySize, 0f, "个");
        if (prop.arraySize > 0 && GUILayout.Button(new GUIContent("清空", "清空全部波次模板"),
                EditorStyles.miniButton, GUILayout.Width(40f)))
            prop.ClearArray();
        if (GUILayout.Button(new GUIContent("＋", "添加波次模板"), EditorStyles.miniButton, GUILayout.Width(22f)))
        {
            int n = prop.arraySize;
            prop.arraySize = n + 1;
            var added = prop.GetArrayElementAtIndex(n);
            added.isExpanded = true;
            added.FindPropertyRelative("name").stringValue = $"模板{n + 1}";
        }
        EndBlockHeader();

        if (prop.arraySize == 0)
        {
            GUILayout.Label("暂无模板：WaveManager 用 RandomTake 抽一个模板，空列表会直接抛异常。", s_greyMiniStyle);
            return;
        }

        for (int i = 0; i < prop.arraySize; i++)
        {
            if (DrawTemplateItem(prop, i)) i--;
        }
        GUILayout.Space(2);
    }

    /// <summary>单个模板：折叠头（名称可改 + 摘要）+ 展开体（档位表 / 巡逻队引用）。返回 true = 已被删除</summary>
    private bool DrawTemplateItem(SerializedProperty list, int index)
    {
        var item = list.GetArrayElementAtIndex(index);
        var nameProp = item.FindPropertyRelative("name");
        var tiers = item.FindPropertyRelative("template");
        var patrols = item.FindPropertyRelative("patrolTemplate");
        var names = PatrolNames();

        int tierWeight = 0;
        int tierUsed = 0;
        for (int i = 0; i < tiers.arraySize; i++)
        {
            int w = tiers.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue;
            if (w > 0) { tierUsed++; tierWeight += w; }
        }

        int patrolTotal = 0;
        int patrolMissing = 0;
        for (int i = 0; i < patrols.arraySize; i++)
        {
            var e = patrols.GetArrayElementAtIndex(i);
            patrolTotal += Mathf.Max(e.FindPropertyRelative("Value").intValue, 0);
            if (!names.Contains(e.FindPropertyRelative("Key").stringValue)) patrolMissing++;
        }

        Rect row = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.DrawRect(row, index % 2 == 0
            ? new Color(0.35f, 0.35f, 0.4f, 0.10f)
            : new Color(0.35f, 0.35f, 0.4f, 0.20f));

        var foldRect = new Rect(row.x + 2f, row.y + 1f, 14f, 18f);
        item.isExpanded = EditorGUI.Foldout(foldRect, item.isExpanded, GUIContent.none, true);

        var nameRect = new Rect(foldRect.xMax + 2f, row.y + 1f,
            Mathf.Clamp(row.width * 0.40f, 80f, 160f), 18f);
        EditorGUI.BeginChangeCheck();
        string newName = EditorGUI.TextField(nameRect, nameProp.stringValue);
        if (EditorGUI.EndChangeCheck()) nameProp.stringValue = newName;

        string summary = $"档位 {tierUsed}/{tiers.arraySize} · 权重 {tierWeight} · "
                         + $"巡逻队 {patrols.arraySize} 项/{patrolTotal} 人";
        if (tierWeight == 0) summary += "　⚠ 权重合计 0，抽不到单位";
        if (patrolMissing > 0) summary += $"　⚠ {patrolMissing} 个巡逻队名未定义";

        var sumRect = new Rect(nameRect.xMax + 6f, row.y,
            Mathf.Max(0f, row.xMax - nameRect.xMax - 26f), row.height);
        GUI.Label(sumRect, new GUIContent(summary,
            "权重 = 抽到该档的概率；巡逻队名必须与下面「巡逻队模板」的 name 一致"),
            tierWeight == 0 || patrolMissing > 0 ? DimWarnStyle : DimStyle);

        if (GUI.Button(new Rect(row.xMax - 20f, row.y + 1f, 18f, 18f),
                new GUIContent("×", "删除该模板"), EditorStyles.miniButton))
        {
            list.DeleteArrayElementAtIndex(index);
            return true;
        }

        if (!item.isExpanded) return false;

        using (new EditorGUI.IndentLevelScope(1))
        {
            // —— 档位 ——（按钮排在长文字之前：面板变窄时先保按钮，文字换行让位）
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("档位", s_boldStyle, GUILayout.Width(40f));
            if (GUILayout.Button("＋ 档位", EditorStyles.miniButton, GUILayout.Width(60f)))
            {
                tiers.arraySize++;
                var added = tiers.GetArrayElementAtIndex(tiers.arraySize - 1);
                added.FindPropertyRelative("weight").intValue = 10;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            //GUILayout.Label($"{tiers.arraySize} 档，权重 0 = 该档不参与抽取", s_greyMiniStyle);

            for (int i = 0; i < tiers.arraySize; i++)
            {
                if (DrawTierCfg(tiers, i)) i--;
            }

            GUILayout.Space(4);

            // —— 巡逻队引用 ——
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("巡逻队", s_boldStyle, GUILayout.Width(40f));
            if (GUILayout.Button("＋ 巡逻队", EditorStyles.miniButton, GUILayout.Width(68f)))
            {
                AddPatrolEntry(names, patrols);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            //GUILayout.Label($"{patrols.arraySize} 项，名字取自本阵营「巡逻队模板」", s_greyMiniStyle);

            if (patrols.arraySize == 0)
                GUILayout.Label("该模板不带巡逻队。", s_greyMiniStyle);

            for (int i = 0; i < patrols.arraySize; i++)
            {
                if (DrawPatrolEntry(names, patrols, i)) i--;
            }
        }
        return false;
    }

    /// <summary>模板内一档：档位 + 权重 + 该档单位（预制体·权重·占据补给）。返回 true = 已被删除。
    /// ⚠ 这里一律手工切列，不用 <c>GUILayout.Width</c> 的 PropertyField：它的标签吃 labelWidth(150)，
    /// 面板变窄时字段会被压成 0 宽甚至挤出面板（枚举下拉空白、输入框只剩边框）。</summary>
    private static bool DrawTierCfg(SerializedProperty tiers, int index)
    {
        var tier = tiers.GetArrayElementAtIndex(index);
        var units = tier.FindPropertyRelative("unitWeights");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // 头行：档位 / 权重 …… ＋ / ×（按钮先占右侧）
            Rect head = EditorGUILayout.GetControlRect(false, RowH);
            var addRect = new Rect(head.xMax - 2f * MiniBtnW - 4f, head.y, MiniBtnW, RowH);
            var delRect = new Rect(head.xMax - MiniBtnW, head.y, MiniBtnW, RowH);
            bool addUnit = GUI.Button(addRect, new GUIContent("＋", "添加单位"), EditorStyles.miniButton);
            bool removed = GUI.Button(delRect, new GUIContent("×", "删除该档"), EditorStyles.miniButton);

            float x = head.x;
            GUI.Label(new Rect(x, head.y, 28f, RowH), "档位", EditorStyles.miniLabel);
            x += 28f;
            // 档位列吃"扣掉标签/权重/右侧按钮后"的剩余宽度，面板再窄也不会盖到按钮上
            float tierW = Mathf.Clamp(head.width - 2f * MiniBtnW - 4f - 108f, 54f, 120f);
            EditorGUI.PropertyField(new Rect(x, head.y, tierW, RowH), tier.FindPropertyRelative("tier"),
                GUIContent.none);
            x += tierW + 8f;
            GUI.Label(new Rect(x, head.y, 28f, RowH), "权重", EditorStyles.miniLabel);
            x += 28f;
            EditorGUI.PropertyField(new Rect(x, head.y, 44f, RowH), tier.FindPropertyRelative("weight"),
                GUIContent.none);

            // 元素已被删 ⇒ 后面的字段路径全失效，直接收尾
            if (removed)
            {
                tiers.DeleteArrayElementAtIndex(index);
                return true;
            }
            if (addUnit)
            {
                units.arraySize++;
                var added = units.GetArrayElementAtIndex(units.arraySize - 1);
                added.FindPropertyRelative("weight").intValue = 10;
                added.FindPropertyRelative("size").intValue = 1;
            }

            if (units.arraySize == 0)
            {
                GUILayout.Label("该档没有单位：权重 > 0 时会抽到空档。", s_greyMiniStyle);
                return false;
            }

            //GUILayout.Label($"单位 {units.arraySize}：预制体 · 权重 · 占据补给", DimStyle);
            for (int i = 0; i < units.arraySize; i++)
            {
                var u = units.GetArrayElementAtIndex(i);
                Rect r = EditorGUILayout.GetControlRect(false, RowH);
                float numW = 42f;
                float objW = Mathf.Max(70f, r.width - MiniBtnW - numW * 2f - 14f);

                EditorGUI.PropertyField(new Rect(r.x, r.y, objW, RowH), u.FindPropertyRelative("unit"),
                    GUIContent.none);
                float ux = r.x + objW + 4f;
                EditorGUI.PropertyField(new Rect(ux, r.y, numW, RowH), u.FindPropertyRelative("weight"),
                    GUIContent.none);
                ux += numW + 4f;
                EditorGUI.PropertyField(new Rect(ux, r.y, numW, RowH), u.FindPropertyRelative("size"),
                    GUIContent.none);

                if (GUI.Button(new Rect(r.xMax - MiniBtnW, r.y, MiniBtnW, RowH),
                        new GUIContent("×", "删除该单位"), EditorStyles.miniButton))
                {
                    units.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }
        }
        return false;
    }

    /// <summary>模板里的巡逻队条目：名字只能从本阵营已定义的巡逻队里选（手写名字对不上会被静默丢弃）。
    /// 同样手工切列：左侧下拉吃剩余宽度，右侧固定"数量 + ×"。</summary>
    private static bool DrawPatrolEntry(List<string> names, SerializedProperty patrols, int index)
    {
        var entry = patrols.GetArrayElementAtIndex(index);
        var keyProp = entry.FindPropertyRelative("Key");
        string key = keyProp.stringValue;
        int cur = names.IndexOf(key);
        // 空名字也算"没选"：WaveManager 用字符串匹配巡逻队，空串 ⇒ 这条被静默丢掉
        bool missing = cur < 0;

        Rect row = EditorGUILayout.GetControlRect(false, RowH);
        const float numW = 44f;
        const float labW = 26f;
        float popupW = Mathf.Max(70f, row.width - MiniBtnW - numW - labW - 18f);
        var pickRect = new Rect(row.x, row.y, popupW, RowH);

        if (names.Count == 0)
        {
            GUI.Label(pickRect, string.IsNullOrEmpty(key) ? "（本阵营还没定义巡逻队）" : $"⚠ {key}", DimWarnStyle);
        }
        else if (missing)
        {
            // 未定义/未选的名字要能看见也能改：置顶一条 ⚠ 选项，选别的就覆盖掉
            var opts = new List<string>(names.Count + 1)
            {
                string.IsNullOrEmpty(key) ? "（未选择巡逻队）" : $"⚠ 未定义：{key}",
            };
            opts.AddRange(names);
            int pick = EditorGUI.Popup(pickRect, 0, opts.ToArray());
            if (pick > 0) keyProp.stringValue = names[pick - 1];
        }
        else
        {
            int pick = EditorGUI.Popup(pickRect, cur, names.ToArray());
            if (pick != cur) keyProp.stringValue = names[pick];
        }

        float x = pickRect.xMax + 4f;
        GUI.Label(new Rect(x, row.y, labW, RowH), new GUIContent("数量", "该巡逻队在模板里出现的次数"),
            EditorStyles.miniLabel);
        x += labW;
        EditorGUI.PropertyField(new Rect(x, row.y, numW, RowH), entry.FindPropertyRelative("Value"), GUIContent.none);

        if (!GUI.Button(new Rect(row.xMax - MiniBtnW, row.y, MiniBtnW, RowH),
                new GUIContent("×", "删除该巡逻队条目"), EditorStyles.miniButton))
            return false;

        patrols.DeleteArrayElementAtIndex(index);
        return true;
    }

    /// <summary>新加一条巡逻队引用：优先挑还没被引用的名字，避免默认就重复</summary>
    private static void AddPatrolEntry(List<string> names, SerializedProperty patrols)
    {
        string pick = null;
        foreach (string n in names)
        {
            bool used = false;
            for (int i = 0; i < patrols.arraySize && !used; i++)
                used = patrols.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue == n;
            if (!used) { pick = n; break; }
        }
        if (pick == null && names.Count > 0) pick = names[0];

        patrols.arraySize++;
        var added = patrols.GetArrayElementAtIndex(patrols.arraySize - 1);
        added.FindPropertyRelative("Key").stringValue = pick ?? "";
        added.FindPropertyRelative("Value").intValue = 1;
    }

    // =====================================================
    //  巡逻队 patrolCfgs：PatrolCfg{ name, SKVP<UnitTier,int>[] }
    // =====================================================

    /// <summary>巡逻队列表：块头 + 每个巡逻队一张折叠卡；摘要带"被哪些模板引用"，一眼能看出死配置与重名。</summary>
    private void DrawPatrolCfgs()
    {
        var prop = serializedObject.FindProperty("patrolCfgs");
        if (prop == null) return;

        BeginBlockHeader("巡逻队模板", prop.arraySize, 0f, "个");
        if (prop.arraySize > 0 && GUILayout.Button(new GUIContent("清空", "清空全部巡逻队"),
                EditorStyles.miniButton, GUILayout.Width(40f)))
            prop.ClearArray();
        if (GUILayout.Button(new GUIContent("＋", "添加巡逻队"), EditorStyles.miniButton, GUILayout.Width(22f)))
        {
            int n = prop.arraySize;
            prop.arraySize = n + 1;
            var added = prop.GetArrayElementAtIndex(n);
            added.isExpanded = true;
            added.FindPropertyRelative("name").stringValue = $"巡逻队{n + 1}";
        }
        EndBlockHeader();

        if (prop.arraySize == 0)
        {
            GUILayout.Label("暂无巡逻队：模板里的巡逻队引用会全部落空。", s_greyMiniStyle);
            return;
        }

        for (int i = 0; i < prop.arraySize; i++)
        {
            if (DrawPatrolCfgItem(prop, i)) i--;
        }
        GUILayout.Space(2);
    }

    /// <summary>单个巡逻队：折叠头（名称 + 人数摘要 + 引用/重名提示）+ 展开体（档位表）。返回 true = 已被删除</summary>
    private bool DrawPatrolCfgItem(SerializedProperty list, int index)
    {
        var item = list.GetArrayElementAtIndex(index);
        var nameProp = item.FindPropertyRelative("name");
        var units = item.FindPropertyRelative("units");

        int total = 0;
        for (int i = 0; i < units.arraySize; i++)
            total += Mathf.Max(units.GetArrayElementAtIndex(i).FindPropertyRelative("Value").intValue, 0);

        int dup = 0;
        string name = nameProp.stringValue;
        for (int i = 0; i < list.arraySize; i++)
        {
            if (i == index) continue;
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) dup++;
        }

        Rect row = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.DrawRect(row, index % 2 == 0
            ? new Color(0.35f, 0.35f, 0.4f, 0.10f)
            : new Color(0.35f, 0.35f, 0.4f, 0.20f));

        var foldRect = new Rect(row.x + 2f, row.y + 1f, 14f, 18f);
        item.isExpanded = EditorGUI.Foldout(foldRect, item.isExpanded, GUIContent.none, true);

        var nameRect = new Rect(foldRect.xMax + 2f, row.y + 1f,
            Mathf.Clamp(row.width * 0.40f, 80f, 160f), 18f);
        EditorGUI.BeginChangeCheck();
        string newName = EditorGUI.TextField(nameRect, name);
        if (EditorGUI.EndChangeCheck()) nameProp.stringValue = newName;

        // 说明：名字是模板引用的键（字符串匹配），改这里等于断开旧引用
        string usage = PatrolUsage(name);
        string summary = $"单位 {units.arraySize} 种 · {total} 人"
                         + (string.IsNullOrEmpty(usage) ? " · ⚠ 未被任何模板引用" : $" · 被 {usage} 引用");
        if (dup > 0) summary += $" · ⚠ 与另外 {dup} 项重名";

        var sumRect = new Rect(nameRect.xMax + 6f, row.y,
            Mathf.Max(0f, row.xMax - nameRect.xMax - 26f), row.height);
        GUI.Label(sumRect, new GUIContent(summary,
            "名字是模板引用这条巡逻队的键，改了等于断开旧引用；重名时 WaveManager 只取第一个"),
            dup > 0 || string.IsNullOrEmpty(usage) ? DimWarnStyle : DimStyle);

        if (GUI.Button(new Rect(row.xMax - 20f, row.y + 1f, 18f, 18f),
                new GUIContent("×", "删除该巡逻队"), EditorStyles.miniButton))
        {
            list.DeleteArrayElementAtIndex(index);
            return true;
        }

        if (!item.isExpanded) return false;

        using (new EditorGUI.IndentLevelScope(1))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 单位", EditorStyles.miniButton, GUILayout.Width(60f)))
            {
                units.arraySize++;
                units.GetArrayElementAtIndex(units.arraySize - 1).FindPropertyRelative("Value").intValue = 1;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            //GUILayout.Label("档位 + 数量，同档位按数量重复生成", s_greyMiniStyle);

            if (units.arraySize == 0)
                GUILayout.Label("暂无单位：引用它的模板会生成空巡逻队。", s_greyMiniStyle);

            for (int i = 0; i < units.arraySize; i++)
            {
                var u = units.GetArrayElementAtIndex(i);
                Rect r = EditorGUILayout.GetControlRect(false, RowH);
                const float numW = 44f;
                float keyW = Mathf.Max(60f, r.width - MiniBtnW - numW - 24f);

                EditorGUI.PropertyField(new Rect(r.x, r.y, keyW, RowH), u.FindPropertyRelative("Key"),
                    GUIContent.none);
                float x = r.x + keyW + 4f;
                EditorGUI.PropertyField(new Rect(x, r.y, numW, RowH), u.FindPropertyRelative("Value"),
                    GUIContent.none);
                GUI.Label(new Rect(x + numW + 2f, r.y, 16f, RowH), "个", EditorStyles.miniLabel);

                if (GUI.Button(new Rect(r.xMax - MiniBtnW, r.y, MiniBtnW, RowH),
                        new GUIContent("×", "删除该单位"), EditorStyles.miniButton))
                {
                    units.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }
        }
        return false;
    }

    /// <summary>本阵营已定义的巡逻队名（读 SerializedProperty，能拿到本帧刚编辑、尚未 Apply 的值）</summary>
    private List<string> PatrolNames()
    {
        var names = new List<string>();
        var prop = serializedObject.FindProperty("patrolCfgs");
        if (prop == null) return names;
        for (int i = 0; i < prop.arraySize; i++)
        {
            string n = prop.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            if (!string.IsNullOrEmpty(n)) names.Add(n);
        }
        return names;
    }

    /// <summary>某巡逻队被哪些模板引用（"模板A(2)、模板B(1)"），没被引用返回空串</summary>
    private string PatrolUsage(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var templates = serializedObject.FindProperty("templates");
        if (templates == null) return "";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < templates.arraySize; i++)
        {
            var t = templates.GetArrayElementAtIndex(i);
            var patrols = t.FindPropertyRelative("patrolTemplate");
            for (int j = 0; j < patrols.arraySize; j++)
            {
                var e = patrols.GetArrayElementAtIndex(j);
                if (e.FindPropertyRelative("Key").stringValue != name) continue;
                if (sb.Length > 0) sb.Append('、');
                sb.Append(t.FindPropertyRelative("name").stringValue)
                  .Append('(').Append(e.FindPropertyRelative("Value").intValue).Append(')');
                break;
            }
        }
        return sb.ToString();
    }

    // =====================================================
    //  行样式与"单击定位"（任务行的跳转入口）
    // =====================================================

    private static readonly Dictionary<Color, GUIStyle> s_rowLabelCache = new Dictionary<Color, GUIStyle>();
    private static GUIStyle s_dimStyle;
    private static GUIStyle s_dimWarnStyle;

    /// <summary>不换行的灰色摘要样式（基类 s_greyMiniStyle 是 wordWrap，塞进 20px 行高会溢出）</summary>
    private static void EnsureRowStyles()
    {
        if (s_dimStyle != null) return;
        s_dimStyle = new GUIStyle(s_greyMiniStyle)
        {
            wordWrap = false,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft,
        };
        s_dimWarnStyle = new GUIStyle(s_dimStyle);
        s_dimWarnStyle.normal.textColor = new Color(1f, 0.55f, 0.35f);
    }

    private static GUIStyle DimStyle => s_dimStyle;
    private static GUIStyle DimWarnStyle => s_dimWarnStyle;

    /// <summary>行内左对齐标签样式（按文字色缓存；基类 RowLabel 每次调用都 new GUIStyle）</summary>
    private static GUIStyle RowStyle(Color textColor)
    {
        if (s_rowLabelCache.TryGetValue(textColor, out GUIStyle style)) return style;
        style = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
        };
        style.normal.textColor = textColor;
        s_rowLabelCache[textColor] = style;
        return style;
    }

    /// <summary>rect 内单击 ⇒ 在 Project 中选中并定位 asset（数据编辑器右面板同样生效）</summary>
    private static void LinkClick(Rect rect, UnityEngine.Object asset)
    {
        if (asset == null) return;
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
        if (Event.current.type != EventType.MouseDown || !rect.Contains(Event.current.mousePosition)) return;

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        GUIUtility.keyboardControl = 0;
        Event.current.Use();
    }
}
}
