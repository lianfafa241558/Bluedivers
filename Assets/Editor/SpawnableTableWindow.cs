using System.Collections.Generic;
using FPSGame.Data;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{
    /// <summary>
    /// `Tools/可生成单位表` —— 把表的 Inspector 直接嵌进窗口，外加扫描/建表/定位三个按钮。
    ///
    /// <para>▍为什么开窗而不是只靠检视器：扫描要在 1100+ 个预制体上刷进度条、建表要落到固定路径，
    /// 这些是"操作"而不是"字段"，塞进 Inspector 会把表格挤得很长。</para>
    ///
    /// <para>⚠ 内嵌的是 `Editor.CreateEditor` 拿到的专属检视器（<see cref="SpawnableTable_SOEditor"/>）⇒ 改显示
    /// 只需改检视器一处，窗口自动跟着变。</para>
    /// </summary>
    public class SpawnableTableWindow : EditorWindow
    {
        private static GUIStyle s_warnLabelStyle;

        private SpawnableTable_SO _table;
        private Editor _inspector;
        private Vector2 _scroll;

        [MenuItem("Tools/可生成单位表")]
        private static void Open()
        {
            var wnd = GetWindow<SpawnableTableWindow>("可生成单位表");
            wnd.minSize = new Vector2(460f, 420f);
            wnd.Bind(wnd._table != null ? wnd._table : SpawnableTableTools.FindTable(false));
            wnd.Show();
        }

        private void OnEnable() => Bind(_table != null ? _table : SpawnableTableTools.FindTable(false));

        private void OnDisable() => ReleaseInspector();

        private void OnGUI()
        {
            DrawToolbar();

            if (_table == null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("工程里还没有「可生成单位表」资产。\n点上面的「新建表资产」会在 "
                                        + SpawnableTableTools.AssetPath + " 建一张空表。", MessageType.Warning);
                return;
            }

            DrawConflictSummary();

            // 域重载 / 资产被删之后 _inspector 会变假 null ⇒ 这里重建（不能只靠 OnEnable）
            if (_inspector == null || _inspector.target == null) Bind(_table);
            if (_inspector == null) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                var picked = (SpawnableTable_SO)EditorGUILayout.ObjectField(_table, typeof(SpawnableTable_SO), false, GUILayout.Width(220f));
                if (picked != _table) Bind(picked);

                GUILayout.Space(6f);

                using (new EditorGUI.DisabledScope(_table == null))
                {
                    if (GUILayout.Button(new GUIContent("扫描全仓重建", "扫描全工程带 NetObject 的预制体（只认根）并覆盖本表"),
                            EditorStyles.toolbarButton, GUILayout.Width(96f)))
                        Rebuild();
                }

                if (GUILayout.Button(new GUIContent("新建表资产", "在 Resources/GameData 下建一张空表（已存在则直接打开）"),
                        EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    Bind(SpawnableTableTools.FindTable(true));

                using (new EditorGUI.DisabledScope(_table == null))
                {
                    if (GUILayout.Button("在 Project 定位", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    {
                        Selection.activeObject = _table;
                        EditorGUIUtility.PingObject(_table);
                    }
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>冲突摘要（读**已保存**的资产；未落盘的编辑看内嵌检视器顶部那一行）。</summary>
        private void DrawConflictSummary()
        {
            var dups = SpawnableTableTools.CountDuplicates(_table);
            if (dups.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"⚠ 该表有 {dups.Count} 组编号重复（已保存的部分）", EnsureWarnStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("按编号排序", EditorStyles.miniButton, GUILayout.Width(84f)))
            {
                var so = new SerializedObject(_table);
                SortEntries(so);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_table);
                AssetDatabase.SaveAssets();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("重建可生成单位表",
                    "将扫描全工程所有带「网络对象编号」的预制体（只认根组件），并**覆盖**当前表全部条目。\n\n继续？",
                    "重建", "取消"))
                return;

            var table = _table;
            // 延到 GUI 之外：条目数会变，这一帧的布局作废（也能避免打断内嵌检视器的 Begin/End 配对）
            EditorApplication.delayCall += () =>
            {
                if (this == null || table == null) return;
                if (!SpawnableTableTools.Rebuild(table)) return;

                if (_inspector != null) _inspector.serializedObject.Update();
                Repaint();
            };
        }

        /// <summary>按编号升序排（0 排最后）——与检视器里那个按钮同一口径。</summary>
        private static void SortEntries(SerializedObject so)
        {
            var arr = so.FindProperty("_entries");
            if (arr == null) return;

            var ids = new List<int>(arr.arraySize);
            var pfs = new List<Object>(arr.arraySize);
            for (int i = 0; i < arr.arraySize; ++i)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                ids.Add(elem.FindPropertyRelative("Id").intValue);
                pfs.Add(elem.FindPropertyRelative("Prefab").objectReferenceValue);
            }

            var order = new List<int>(ids.Count);
            for (int i = 0; i < ids.Count; ++i) order.Add(i);
            order.Sort((a, b) => Key(ids[a]).CompareTo(Key(ids[b])));

            for (int i = 0; i < arr.arraySize; ++i)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Id").intValue = ids[order[i]];
                elem.FindPropertyRelative("Prefab").objectReferenceValue = pfs[order[i]];
            }
        }

        private static int Key(int id) => id == 0 ? int.MaxValue : id;

        private void Bind(SpawnableTable_SO table)
        {
            if (_table == table && _inspector != null) return;

            _table = table;
            ReleaseInspector();
            if (_table != null) _inspector = Editor.CreateEditor(_table);
            Repaint();
        }

        private void ReleaseInspector()
        {
            if (_inspector == null) return;
            DestroyImmediate(_inspector);
            _inspector = null;
        }

        /// <summary>样式哨兵同基类 <c>EnsureStyles</c>：域重载会把静态字段清空 ⇒ null 就重建。</summary>
        private static GUIStyle EnsureWarnStyle()
        {
            if (s_warnLabelStyle != null) return s_warnLabelStyle;

            s_warnLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                richText = true,
                wordWrap = false,
                normal = { textColor = new Color(1f, 0.45f, 0.45f) },
            };
            return s_warnLabelStyle;
        }
    }
}
