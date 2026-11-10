using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Data;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{
    /// <summary>
    /// 可生成单位表 专属检视器（标准 Inspector、数据编辑器右面板、`Tools/可生成单位表` 窗口里都生效）。
    ///
    /// <para>▍提供：① 顶部色条 + 汇总（条目数 / 重复编号组数 / 空引用数）；② 冲突红卡逐条列出重复编号；
    /// ③ 每行「编号 + 预制体 + 定位 + 删除」，重复编号整行标红；④ 扫描全仓 <see cref="NetObject"/> 一键重建。</para>
    ///
    /// <para>⚠ 重复检测读的是 <c>SerializedProperty</c>（编辑中未落盘的值），所以改一个数字立刻变红，不用等保存。</para>
    /// </summary>
    [CustomEditor(typeof(SpawnableTable_SO))]
    public class SpawnableTable_SOEditor : DataSOEditorBase
    {
        private static GUIStyle s_dupBadgeStyle;
        private static GUIStyle s_warnMiniStyle;
        private static readonly Color Accent = new Color(0.35f, 0.72f, 1f);

        private static readonly Color RowDup = new Color(0.85f, 0.18f, 0.18f, 0.30f);
        private static readonly Color RowEmpty = new Color(0.95f, 0.62f, 0.10f, 0.22f);
        private static readonly Color RowAlt = new Color(0.5f, 0.5f, 0.5f, 0.07f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();
            EnsureStatic();

            DrawScriptHeader();

            var arr = serializedObject.FindProperty("_entries");
            if (arr == null)
            {
                EditorGUILayout.HelpBox("找不到 _entries 字段（资产是旧版本脚本生成的？）⇒ 回退为默认绘制。", MessageType.Warning);
                DrawDefaultInspector();
                return;
            }

            // —— 统计：读序列化值，所以未落盘的编辑也立刻生效 ——
            var dupIds = new Dictionary<int, List<string>>();
            int emptyCount = 0;
            int zeroCount = 0;
            for (int i = 0; i < arr.arraySize; ++i)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                int id = elem.FindPropertyRelative("Id").intValue;
                var pf = elem.FindPropertyRelative("Prefab").objectReferenceValue as GameObject;

                if (pf == null) { ++emptyCount; continue; }
                if (id == 0) { ++zeroCount; continue; }

                if (!dupIds.TryGetValue(id, out var names)) dupIds[id] = names = new List<string>();
                names.Add(pf.name);
            }

            // 只留**真正重复**的编号（出现 ≥2 次）：唯一出现的那条不是冲突（否则"共 1 项 / 重复 1 组"）
            var singles = new List<int>();
            foreach (var kv in dupIds) if (kv.Value.Count < 2) singles.Add(kv.Key);
            for (int i = 0; i < singles.Count; ++i) dupIds.Remove(singles[i]);

            DrawHero(arr.arraySize, dupIds, emptyCount, zeroCount);
            DrawConflictCard(dupIds);

            SectionTitle("条目", Accent);
            DrawToolbar(arr);

            if (arr.arraySize == 0)
            {
                GUILayout.Label("表是空的。先给要同步创建的预制体根挂上「网络对象编号」，再点「扫描全仓重建」。", s_greyMiniStyle);
            }
            else
            {
                for (int i = 0; i < arr.arraySize; ++i)
                    DrawRow(arr, i, dupIds);
            }

            GUILayout.Space(6);
            if (dupIds.Count > 0)
                GUILayout.Label($"⚠ 有 {dupIds.Count} 组编号重复：运行时每组只有第一条生效，其余会被字典丢掉。", s_redTextStyle);

            ApplyDirty();
        }

        // =====================================================
        //  顶部
        // =====================================================

        private static void DrawHero(int count, Dictionary<int, List<string>> dupIds, int emptyCount, int zeroCount)
        {
            ColorBar(Accent);

            EditorGUILayout.BeginHorizontal();
            TagLabel("可生成单位表", Accent);
            GUILayout.Space(4);
            GUILayout.Label($"共 {count} 项", s_boldStyle);

            GUILayout.FlexibleSpace();

            if (dupIds.Count > 0)
                GUILayout.Label($"重复 {dupIds.Count} 组", new GUIStyle(s_redTextStyle) { wordWrap = false });
            if (emptyCount > 0)
                GUILayout.Label($"空引用 {emptyCount}", new GUIStyle(s_greyMiniStyle) { normal = { textColor = new Color(1f, 0.75f, 0.2f) }, wordWrap = false });
            if (dupIds.Count == 0 && emptyCount == 0 && zeroCount == 0)
                GUILayout.Label("✓ 无冲突", s_greyMiniStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
        }

        /// <summary>冲突红卡：逐组列出重复编号与其预制体名</summary>
        private static void DrawConflictCard(Dictionary<int, List<string>> dupIds)
        {
            if (dupIds.Count == 0) return;

            EditorGUILayout.BeginVertical(s_warnBoxStyle);
            GUILayout.Label("⚠ 编号冲突", s_redTextStyle);
            foreach (var kv in dupIds)
            {
                if (kv.Value.Count < 2) continue;
                GUILayout.Label($"● 编号 {kv.Key} 被 {kv.Value.Count} 条使用：{string.Join("、", kv.Value)}", s_redTextStyle);
            }
            GUILayout.Label("同一个编号只能对应一个预制体；发布后改编号等于改协议。", s_greyMiniStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar(SerializedProperty arr)
        {
            EditorGUILayout.BeginHorizontal();
            {
                // 「扫描全仓重建」只在 `Tools/可生成单位表` 窗口里（那是项目级操作，别让两处各有一个入口）
                if (GUILayout.Button(new GUIContent("按编号排序", "编号升序，0 排最后"), EditorStyles.miniButton, GUILayout.Width(84), GUILayout.Height(22)))
                    SortEntries(arr);

                if (GUILayout.Button(new GUIContent("＋ 添加", "手动加一条（可拖入未挂 NetObject 的预制体）"), EditorStyles.miniButton, GUILayout.Width(64), GUILayout.Height(22)))
                {
                    arr.arraySize = arr.arraySize + 1;
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).FindPropertyRelative("Id").intValue = NextId(arr);
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        // =====================================================
        //  行
        // =====================================================

        private static void DrawRow(SerializedProperty arr, int i, Dictionary<int, List<string>> dupIds)
        {
            var elem = arr.GetArrayElementAtIndex(i);
            var idProp = elem.FindPropertyRelative("Id");
            var pfProp = elem.FindPropertyRelative("Prefab");

            int id = idProp.intValue;
            var prefab = pfProp.objectReferenceValue as GameObject;

            int dupCount = 0;
            if (id != 0 && dupIds.TryGetValue(id, out var same)) dupCount = same.Count;
            bool dup = dupCount > 1;
            bool empty = prefab == null;

            Rect row = EditorGUILayout.GetControlRect(false, 22f);
            if (dup) EditorGUI.DrawRect(row, RowDup);
            else if (empty) EditorGUI.DrawRect(row, RowEmpty);
            else if ((i & 1) == 0) EditorGUI.DrawRect(row, RowAlt);

            float y = row.y + 2f;
            float h = row.height - 4f;
            float x = row.x + 4f;

            GUI.Label(new Rect(x, row.y, 18f, row.height), (i + 1).ToString(), s_greyMiniStyle);
            x += 18f;

            EditorGUI.PropertyField(new Rect(x, y, 58f, h), idProp, GUIContent.none);
            x += 62f;

            if (dup) GUI.Label(new Rect(x, row.y, 26f, row.height), $"×{dupCount}", s_dupBadgeStyle);
            x += 28f;

            const float delW = 20f;
            const float locW = 44f;
            Rect delRect = new Rect(row.xMax - 2f - delW, y, delW, h);
            Rect locRect = new Rect(delRect.x - 4f - locW, y, locW, h);
            Rect pfRect = new Rect(x, y, Mathf.Max(locRect.x - 6f - x, 40f), h);

            EditorGUI.PropertyField(pfRect, pfProp, GUIContent.none);

            using (new EditorGUI.DisabledScope(prefab == null))
            {
                if (GUI.Button(locRect, "定位", EditorStyles.miniButton))
                {
                    EditorGUIUtility.PingObject(prefab);
                    Selection.activeObject = prefab;
                }
            }

            if (GUI.Button(delRect, "×", EditorStyles.miniButton))
            {
                arr.DeleteArrayElementAtIndex(i);
                return;
            }

            // —— 行下提示（追加一行小字，不改行高）——
            if (empty)
                GUILayout.Label("　预制体为空：这条在运行时会被跳过。", s_warnMiniStyle);
            else if (id == 0)
                GUILayout.Label("　编号为 0：运行时会被跳过，请填一个 ≥1 的编号。", s_warnMiniStyle);
            else
            {
                var net = prefab.GetComponent<NetObject>();
                if (net == null)
                    GUILayout.Label("　该预制体根上没有「网络对象编号」，重建表时不会被收录。", s_warnMiniStyle);
                else if (net.Id != id)
                    GUILayout.Label($"　与预制体上的编号 {net.Id} 不一致（重建表会以预制体为准）。", s_warnMiniStyle);
            }
        }

        // =====================================================
        //  操作（排序 / 添加；「扫描全仓重建」在 Tools/可生成单位表 窗口里）
        // =====================================================

        private static void SortEntries(SerializedProperty arr)
        {
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
            // 0 = 未配置 ⇒ 排最后，免得一直占着第 1 行
            order.Sort((a, b) => SortKey(ids[a]).CompareTo(SortKey(ids[b])));

            var newIds = new List<int>(ids.Count);
            var newPfs = new List<Object>(ids.Count);
            foreach (int i in order) { newIds.Add(ids[i]); newPfs.Add(pfs[i]); }

            for (int i = 0; i < arr.arraySize; ++i)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Id").intValue = newIds[i];
                elem.FindPropertyRelative("Prefab").objectReferenceValue = newPfs[i];
            }
        }

        private static int SortKey(int id) => id == 0 ? int.MaxValue : id;

        private static int NextId(SerializedProperty arr)
        {
            int max = 0;
            for (int i = 0; i < arr.arraySize; ++i)
                max = Mathf.Max(max, arr.GetArrayElementAtIndex(i).FindPropertyRelative("Id").intValue);
            return max + 1;
        }

        // =====================================================
        //  样式
        // =====================================================

        private static void EnsureStatic()
        {
            if (s_dupBadgeStyle != null) return;

            s_dupBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.45f, 0.45f) },
            };
            s_warnMiniStyle = new GUIStyle(s_greyMiniStyle)
            {
                normal = { textColor = new Color(1f, 0.72f, 0.25f) },
            };
        }
    }
}
