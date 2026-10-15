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
/// 任务区字段一行一项（图标 + 名称 + 选择/×），挑选走 SOPickerPopup&lt;MissionData_SO&gt;，选中后把其 type 写回数组。
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
        DrawField("templates");
        GUILayout.Space(6);

        // ================= 巡逻队 =================
        SectionTitle("巡逻队", so.Color);
        EditorGUILayout.Space(2);
        DrawField("PatrolCreatValue");
        DrawField("patrolCfgs");

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

                if (refSO == null)
                    RowLabel($"未知类型：{value}（未找到对应任务配置）", Color.red);
                else
                    RowLabel(MissionDisplayName(refSO), EditorGUIUtility.isProSkin ? Color.white : Color.black);

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
        string typeName = TypeLabel(so.type);
        return string.IsNullOrEmpty(so.desc) ? typeName : $"{so.desc}·{typeName}";
    }

    /// <summary>任务配置自己的颜色：主线配置取其 color 字段；支线配置无颜色字段，回退分类色</summary>
    private static Color MissionColor(MissionData_SO so)
    {
        if (so is MissionMainData_SO main) return main.color;
        return CategoryColor(so.type);
    }

    private static readonly Dictionary<int, string> s_typeLabelCache = new Dictionary<int, string>();

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
}
}
