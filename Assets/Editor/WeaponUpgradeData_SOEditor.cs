using System;
using System.Collections.Generic;
using FPSGame.GameData;
using FPSGame.Weapon;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{

/// <summary>
/// WeaponUpgradeData_SO 专属检视器（标准 Inspector 与数据编辑器右面板都走这里）。
/// 字段中文化 + 顶部标题卡，末尾追加「反向引用」：实时算出哪些武器 prefab 的 Upgrade 用了它。
/// <para>反向关系**不序列化**：真相只在武器 prefab 的 Upgrade 列表里，SO 内再存一份必然对不上；
/// 重算时机 = 选中目标变化 / 刷新按钮 / EditorApplication.projectChanged（资产落盘后）。</para>
/// </summary>
[CustomEditor(typeof(WeaponUpgradeData_SO))]
public class WeaponUpgradeData_SOEditor : DataSOEditorBase
{
    // 武器 prefab 统一放在 Resources/Weapons（武器升级编辑器 / ModifyAttrDataDrawer 也是这么扫的）
    private const string WeaponResRoot = "Weapons";

    private static readonly Color s_accent = new Color(0.40f, 0.63f, 1.00f);
    private static readonly Color s_costAccent = new Color(0.95f, 0.75f, 0.35f);
    private static readonly Color s_modifyAccent = new Color(0.55f, 0.85f, 0.55f);
    private static readonly Color s_refAccent = new Color(0.35f, 0.85f, 0.80f);

    private static GUIStyle s_heroNameStyle;

    // 反向引用缓存（静态：Inspector 与数据编辑器可能各持一个 Editor 实例）
    private static readonly List<RefEntry> s_refs = new List<RefEntry>();
    private static int s_refWeaponCount;
    private static int s_refTargetId;
    private static bool s_refsDirty = true;

    /// <summary>一条引用记录：某武器 prefab 的第 LevelIndex 档（解锁等级 Level）的第 OptionIndex 个选项</summary>
    private struct RefEntry
    {
        public GameObject Prefab;
        public string WeaponName;
        public Sprite Icon;
        public int LevelIndex;
        public int Level;
        public int OptionIndex;
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged -= InvalidateRefs;   // 幂等：域重载后 OnEnable 会再订阅一次
        EditorApplication.projectChanged += InvalidateRefs;
        s_refsDirty = true;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= InvalidateRefs;
    }

    private static void InvalidateRefs() => s_refsDirty = true;

    public override void OnInspectorGUI()
    {
        var data = target as WeaponUpgradeData_SO;
        if (data == null)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();
        EnsureStyles();
        EnsureLocalStyles();

        DrawScriptHeader();
        ColorBar(s_accent);
        DrawHero(data);

        SectionTitle("基础信息", s_accent);
        DrawField("name");
        DrawField("type");
        DrawField("icon");
        DrawField("desc");
        GUILayout.Space(6f);

        SectionTitle("费用", s_costAccent);
        DrawField("cost");
        GUILayout.Space(6f);

        SectionTitle("修改属性", s_modifyAccent);
        DrawField("modifys");
        GUILayout.Space(6f);

        SectionTitle("反向引用 · 哪些武器用了这个升级", s_refAccent);
        DrawReferences(data);

        ApplyDirty();
    }

    //===============================//
    // 顶部标题卡
    //===============================//

    private static void DrawHero(WeaponUpgradeData_SO data)
    {
        EditorGUILayout.BeginHorizontal();

        var iconRect = GUILayoutUtility.GetRect(52f, 52f);
        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit, true);
        else
            EditorGUI.DrawRect(new Rect(iconRect.x + 2f, iconRect.y + 2f, iconRect.width - 4f, iconRect.height - 4f),
                new Color(1f, 1f, 1f, 0.06f));

        GUILayout.Space(8f);

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Space(3f);
            GUILayout.Label(string.IsNullOrEmpty(data.name) ? "（未命名升级）" : data.name, s_heroNameStyle);

            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(data.type)) TagLabel(data.type, s_accent);
            GUILayout.Label($"{CountOf(data.modifys)} 条属性改动 · {CountOf(data.cost)} 项费用", s_greyMiniStyle);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4f);
    }

    //===============================//
    // 反向引用
    //===============================//

    private static void DrawReferences(WeaponUpgradeData_SO data)
    {
        EnsureRefs(data);

        EditorGUILayout.BeginHorizontal();
        {
            if (s_refs.Count == 0)
            {
                GUILayout.Label("未被任何武器引用", s_redTextStyle);
            }
            else
            {
                GUILayout.Label($"被 {s_refWeaponCount} 把武器的 {s_refs.Count} 处升级位引用", s_boldStyle);
                if (s_refWeaponCount > 1)
                {
                    GUILayout.Space(4f);
                    TagLabel("通用项", s_refAccent);
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", EditorStyles.miniButton, GUILayout.Width(46f)))
            {
                s_refsDirty = true;
                EnsureRefs(data);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (s_refs.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "没有任何武器 prefab 的「改装」列表引用它 —— 这份数据不会出现在改装界面" +
                "（可能是废弃资产，或还没在武器升级编辑器里挂到某一档）。",
                MessageType.Warning);
            return;
        }

        for (int i = 0; i < s_refs.Count; i++) DrawRefRow(s_refs[i]);
    }

    private static void DrawRefRow(RefEntry entry)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        var iconRect = GUILayoutUtility.GetRect(22f, 22f);
        DrawTintedIcon(iconRect, entry.Icon, Color.white);

        string path = AssetDatabase.GetAssetPath(entry.Prefab);
        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(new GUIContent(entry.WeaponName, path), s_boldStyle);
            GUILayout.Label($"解锁等级 {entry.Level} · 第 {entry.LevelIndex + 1} 档 · 选项 {entry.OptionIndex + 1}",
                s_greyMiniStyle);
        }
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44f), GUILayout.Height(22f)))
        {
            Selection.activeObject = entry.Prefab;
            EditorGUIUtility.PingObject(entry.Prefab);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>扫 Resources/Weapons 下所有武器 prefab 的 Upgrade 列表（GetUpgrade 比较引用，不写死路径）</summary>
    private static void EnsureRefs(WeaponUpgradeData_SO data)
    {
        int id = data.GetInstanceID();
        if (!s_refsDirty && s_refTargetId == id) return;

        s_refsDirty = false;
        s_refTargetId = id;
        s_refs.Clear();
        s_refWeaponCount = 0;

        var countedPrefabs = new HashSet<int>();
        foreach (var prefab in Resources.LoadAll<GameObject>(WeaponResRoot))
        {
            if (prefab == null) continue;
            var weapon = prefab.GetComponent<WeaponPlayerController>();
            if (weapon == null) continue;

            int[] counts = weapon.UpgradeCount();
            int[] levels = weapon.UpgradeLevel();
            for (int lv = 0; lv < counts.Length; lv++)
            {
                for (int opt = 0; opt < counts[lv]; opt++)
                {
                    if (weapon.GetUpgrade(lv, opt) != data) continue;

                    s_refs.Add(new RefEntry
                    {
                        Prefab = prefab,
                        WeaponName = string.IsNullOrEmpty(weapon.WeaponName) ? prefab.name : weapon.WeaponName,
                        Icon = weapon.WeaponIcon,
                        LevelIndex = lv,
                        Level = lv < levels.Length ? levels[lv] : 0,
                        OptionIndex = opt,
                    });

                    if (countedPrefabs.Add(prefab.GetInstanceID())) s_refWeaponCount++;
                }
            }
        }

        s_refs.Sort((a, b) =>
        {
            int c = string.Compare(a.WeaponName, b.WeaponName, StringComparison.Ordinal);
            if (c != 0) return c;
            if (a.LevelIndex != b.LevelIndex) return a.LevelIndex - b.LevelIndex;
            return a.OptionIndex - b.OptionIndex;
        });
    }

    //===============================//
    // 字段绘制
    //===============================//

    protected override void DrawField(string fieldName, bool children = true)
    {
        var p = serializedObject.FindProperty(fieldName);
        if (p == null)
        {
            EditorGUILayout.HelpBox($"找不到序列化字段 {fieldName}", MessageType.Error);
            return;
        }

        string label = LabelOf(fieldName);

        // cost 是 List<SKVP<...>>（[Singleline]）：走全局内联绘制，否则会退化成"每个元素 Key/Value 两行"
        if (InlineFieldDrawer.IsInlineField(p, target))
        {
            if (p.isArray)
                InlineFieldDrawer.DrawInlineListLayout(p, target, label);
            else
                InlineFieldDrawer.DrawInlineObjectLayout(p, target, label);
            return;
        }

        EditorGUILayout.PropertyField(p, new GUIContent(label), children);
    }

    //===============================//
    // 小工具
    //===============================//

    private static void EnsureLocalStyles()
    {
        if (s_heroNameStyle != null) return;
        s_heroNameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
    }

    private static int CountOf<T>(List<T> list) => list != null ? list.Count : 0;
}
}
