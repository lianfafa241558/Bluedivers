using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.EditorExt
{
    /// <summary>
    /// 预制体组件批量工具基类。
    /// 封装「遍历 Assets 下所有预制体 → 对每个 <typeparamref name="TComponent"/> 组件做扫描判定 → 预览报告 → 按预制体去重批量写入」的公共管线，
    /// 并统一提供两步式窗口 UI(1.扫描 / 2.应用)、进度条、报告展示与复制按钮。
    /// 适用于所有「给项目里某类组件做批量检查 / 批量写字段」的编辑器窗口工具。
    ///
    /// 使用方式：继承本类并实现钩子，再写一个 [MenuItem] 静态入口调 GetWindow。
    /// 钩子清单：
    /// <list type="bullet">
    /// <item><description><see cref="CreateItem"/>：每个命中的组件生成一条结果(必填，纯判定+报告文本)</description></item>
    /// <item><description><see cref="SupportsApply"/> 与 <see cref="WriteValue"/>：需要写预制体时开启(选填，默认只读)</description></item>
    /// <item><description><see cref="DrawOptions"/>：在扫描按钮上方追加自定义选项开关(选填)</description></item>
    /// <item><description>文案虚属性：<see cref="WindowTitle"/> / <see cref="HelpText"/> / <see cref="ScanButtonText"/> / <see cref="ApplyButtonText"/> 等(选填)</description></item>
    /// <item><description>统计/行格式虚方法：<see cref="BuildScanSummary"/> / <see cref="BuildApplySummary"/> / <see cref="BuildReportLine"/> / <see cref="BuildChangedLine"/> / <see cref="BuildChangeText"/>(选填)</description></item>
    /// </list>
    /// 基类自动提供的通用能力：
    /// <list type="bullet">
    /// <item><description>「跳过嵌套预制体内的组件」选项(默认开启，避免批量写入产生多余 override)</description></item>
    /// <item><description>进度条、错误恢复(LoadPrefabContents 在 finally 中必然 Unload)、写入后 SaveAssets/Refresh</description></item>
    /// <item><description>工具方法：<see cref="IsInNestedPrefab"/> / <see cref="GetTransformPath"/></description></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TComponent">要遍历的组件类型(必须是 UnityEngine.Component)</typeparam>
    /// <example>
    /// [MenuItem("Tools/批量设置XX")]
    /// public static void Open() => GetWindow&lt;MyBatchTool&gt;(false, "批量设置XX");
    /// </example>
    public abstract class PrefabBatchToolBase<TComponent> : EditorWindow where TComponent : Component
    {
        /// <summary>扫描/应用结果中的单行条目，由 <see cref="CreateItem"/> 产出</summary>
        protected sealed class BatchItem
        {
            /// <summary>预制体资源路径</summary>
            public string PrefabPath = "";
            /// <summary>组件在预制体内的相对路径</summary>
            public string ObjectPath = "";
            /// <summary>变更前文本(报告用)</summary>
            public string OldText = "";
            /// <summary>变更后文本(报告用)</summary>
            public string NewText = "";
            /// <summary>是否可应用(决定 Apply 是否处理该条目所在的预制体)</summary>
            public bool Valid;
            /// <summary>不可应用时的原因(Valid=false 时报告展示为「跳过(原因)」)</summary>
            public string Reason = "";
        }

        /// <summary><see cref="WriteValue"/> 的返回结果</summary>
        protected enum WriteResult
        {
            /// <summary>已实际修改(基类会标脏并保存该预制体)</summary>
            Modified,
            /// <summary>新值与现值一致，无需写入(不计入跳过统计)</summary>
            NoChange,
            /// <summary>因条件不满足而主动跳过(计入跳过统计)</summary>
            Skipped,
        }

        private readonly List<BatchItem> _items = new List<BatchItem>();
        private Vector2 _scrollPos;
        private string _report = "";
        private bool _skipNestedPrefab = true;

        // ========== 子类可配置文案 ==========

        /// <summary>窗口标题(用于提示信息，可被子类静态 Open 方法直接复用)</summary>
        protected virtual string WindowTitle => GetType().Name;

        /// <summary>窗口顶部 HelpBox 的说明文字；返回空则不显示</summary>
        protected virtual string HelpText => "";

        /// <summary>「1.扫描」按钮文字</summary>
        protected virtual string ScanButtonText => "1.扫描全部预制体";

        /// <summary>「2.应用」按钮文字</summary>
        protected virtual string ApplyButtonText => "2.应用修改";

        /// <summary>扫描时的进度条标题</summary>
        protected virtual string ScanProgressTitle => "扫描预制体";

        /// <summary>应用时的进度条标题</summary>
        protected virtual string ApplyProgressTitle => "应用修改";

        /// <summary>该工具是否支持写入预制体；为 false 时隐藏「应用」按钮，仅作只读扫描统计用</summary>
        protected virtual bool SupportsApply => true;

        // ========== 核心钩子 ==========

        /// <summary>
        /// 为单个组件生成一条扫描结果(核心钩子，必填)。
        /// 返回 null 表示该组件不纳入本工具处理范围；返回的条目即使 <see cref="BatchItem.Valid"/>=false 也会列入报告，
        /// 用于展示「跳过(原因)」行。
        /// 说明：扫描与应用阶段都会调用本方法(应用阶段用于按最新选项重新判定)，请保持纯计算、不要在此写入。
        /// </summary>
        /// <param name="comp">命中的组件(扫描阶段为资源对象，应用阶段为 LoadPrefabContents 加载的对象)</param>
        /// <param name="prefabPath">所在预制体资源路径</param>
        /// <param name="objectPath">组件相对预制体根的层级路径</param>
        protected abstract BatchItem CreateItem(TComponent comp, string prefabPath, string objectPath);

        /// <summary>
        /// 把新值写入单个组件(核心钩子，选填)。仅当 <see cref="SupportsApply"/> 为 true 且条目 Valid 时被调用。
        /// 建议用 SerializedObject.FindProperty + ApplyModifiedPropertiesWithoutUndo 写入，
        /// 返回 <see cref="WriteResult.Skipped"/> 的情形会被计入跳过统计。
        /// </summary>
        protected virtual WriteResult WriteValue(TComponent comp)
        {
            return WriteResult.NoChange;
        }

        // ========== 报告钩子 ==========

        /// <summary>扫描完成的汇总行</summary>
        protected virtual string BuildScanSummary(int totalPrefabs, int hitPrefabCount, int componentCount, int validCount)
        {
            return $"扫描完成：共 {totalPrefabs} 个预制体，其中 {hitPrefabCount} 个含 {typeof(TComponent).Name}，" +
                   $"共 {componentCount} 个组件，可应用 {validCount} 个";
        }

        /// <summary>应用完成的汇总行</summary>
        protected virtual string BuildApplySummary(int changedCount, int skippedCount, int prefabCount)
        {
            return $"应用完成：修改 {changedCount} 个，跳过 {skippedCount} 个，涉及预制体 {prefabCount} 个";
        }

        /// <summary>扫描报告的单行格式(默认：资源路径  [组件路径]  变更说明)</summary>
        protected virtual string BuildReportLine(BatchItem item)
        {
            return $"{item.PrefabPath}  [{item.ObjectPath}]  {BuildChangeText(item)}";
        }

        /// <summary>应用报告里实际被修改的单行格式</summary>
        protected virtual string BuildChangedLine(BatchItem item)
        {
            return $"{item.PrefabPath}  [{item.ObjectPath}]  {item.NewText}";
        }

        /// <summary>条目变更说明文本：Valid 显示「旧 -> 新」，否则显示「旧 -> 跳过(原因)」</summary>
        protected virtual string BuildChangeText(BatchItem item)
        {
            if (item.Valid)
            {
                return $"{item.OldText} -> {item.NewText}";
            }

            return string.IsNullOrEmpty(item.OldText)
                ? $"跳过({item.Reason})"
                : $"{item.OldText} -> 跳过({item.Reason})";
        }

        // ========== 通用选项 ==========

        /// <summary>「跳过嵌套预制体内的组件」当前开关值；CreateItem 判定时可读取(默认 true)</summary>
        protected bool SkipNestedPrefab => _skipNestedPrefab;

        /// <summary>绘制扫描按钮上方的选项区；子类 override 可追加自己的开关，记得保留 base 以沿用嵌套开关</summary>
        protected virtual void DrawOptions()
        {
            _skipNestedPrefab = EditorGUILayout.ToggleLeft(
                "跳过嵌套预制体内的组件(避免产生多余 override)", _skipNestedPrefab);
        }

        // ========== 通用工具方法 ==========

        /// <summary>该物体是否位于某个嵌套预制体实例内(避免对嵌套内容写入产生多余 override)</summary>
        protected static bool IsInNestedPrefab(GameObject go)
        {
            return PrefabUtility.GetNearestPrefabInstanceRoot(go) != null;
        }

        /// <summary>获取 target 相对 root 的层级路径(根节点本身返回其名称)</summary>
        protected static string GetTransformPath(Transform target, Transform root)
        {
            if (target == root) return root.name;

            var sb = new StringBuilder(target.name);
            Transform parent = target.parent;
            while (parent != null && parent != root)
            {
                sb.Insert(0, parent.name + "/");
                parent = parent.parent;
            }

            return sb.ToString();
        }

        // ========== 管线实现 ==========

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(HelpText))
            {
                EditorGUILayout.HelpBox(HelpText, MessageType.Info);
                EditorGUILayout.Space();
            }

            DrawOptions();

            EditorGUILayout.Space();

            if (GUILayout.Button(ScanButtonText, GUILayout.Height(30)))
            {
                Scan();
            }

            if (SupportsApply && GUILayout.Button(ApplyButtonText, GUILayout.Height(30)))
            {
                Apply();
            }

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(_report))
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("复制结果"))
                {
                    EditorGUIUtility.systemCopyBuffer = _report;
                    Debug.Log("已复制到剪贴板");
                }
            }
        }

        /// <summary>扫描全部预制体并生成预览报告(只读，不写入)</summary>
        protected virtual void Scan()
        {
            _items.Clear();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int hitPrefabCount = 0;
            int componentCount = 0;
            int validCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;

                EditorUtility.DisplayProgressBar(ScanProgressTitle, path, i / (float)guids.Length);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;

                var comps = prefab.GetComponentsInChildren<TComponent>(true);
                if (comps.Length == 0) continue;

                hitPrefabCount++;
                Transform root = prefab.transform;
                for (int j = 0; j < comps.Length; j++)
                {
                    TComponent comp = comps[j];
                    BatchItem item = CreateItem(comp, path, GetTransformPath(comp.transform, root));
                    if (item == null) continue;

                    componentCount++;
                    if (item.Valid) validCount++;
                    _items.Add(item);
                }
            }

            EditorUtility.ClearProgressBar();

            var lines = new StringBuilder();
            for (int i = 0; i < _items.Count; i++)
            {
                lines.AppendLine(BuildReportLine(_items[i]));
            }

            _report = BuildScanSummary(guids.Length, hitPrefabCount, componentCount, validCount)
                      + "\n\n" + lines;
            Debug.Log(_report);
            Repaint();
        }

        /// <summary>把扫描结果按预制体去重后批量写入</summary>
        protected virtual void Apply()
        {
            if (_items.Count == 0)
            {
                Debug.LogWarning($"[{WindowTitle}] 请先执行扫描");
                return;
            }

            // 需要写入的预制体去重
            var paths = new HashSet<string>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Valid) paths.Add(_items[i].PrefabPath);
            }

            int changedCount = 0;
            int skippedCount = 0;
            var changedLines = new StringBuilder();
            int index = 0;

            foreach (string path in paths)
            {
                EditorUtility.DisplayProgressBar(ApplyProgressTitle, path, index++ / (float)paths.Count);

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool dirty = false;
                    var comps = root.GetComponentsInChildren<TComponent>(true);
                    for (int j = 0; j < comps.Length; j++)
                    {
                        TComponent comp = comps[j];
                        BatchItem item = CreateItem(comp, path, GetTransformPath(comp.transform, root.transform));
                        if (item == null || !item.Valid)
                        {
                            skippedCount++;
                            continue;
                        }

                        WriteResult result = WriteValue(comp);
                        if (result == WriteResult.Modified)
                        {
                            dirty = true;
                            changedCount++;
                            changedLines.AppendLine(BuildChangedLine(item));
                        }
                        else if (result == WriteResult.Skipped)
                        {
                            skippedCount++;
                        }
                    }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _report = BuildApplySummary(changedCount, skippedCount, paths.Count) + "\n\n" + changedLines;
            Debug.Log(_report);
            Repaint();
        }
    }
}
