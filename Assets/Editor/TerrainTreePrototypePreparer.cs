using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.EditorExt
{
    /// <summary>
    /// 地形树原型预处理工具（编辑器）。
    ///
    /// <para><b>为什么需要它：</b>Terrain 引擎通过「树原型预制体的根节点上是否存在 <see cref="LODGroup"/>」
    /// 来判定树的类型——</para>
    /// <list type="bullet">
    /// <item>有 LODGroup → 按 SpeedTree 树处理，走自身 LODGroup 的 LOD / 剔除；</item>
    /// <item>没有 LODGroup → 退回已废弃的 Tree Editor 分支，要求内置 Soft Occlusion 材质 +
    /// 资产必须放在名为 Ambient-Occlusion 的文件夹中，否则树木不会正确渲染（URP 下等同不渲染）。</item>
    /// </list>
    ///
    /// <para>本工具批量给预制体根节点补上「只有一个 LOD0」的 <see cref="LODGroup"/>，
    /// 并把其下所有可见 MeshRenderer / SkinnedMeshRenderer 收进 LOD0，
    /// 使其能被 Terrain 作为正常树木原型使用。</para>
    ///
    /// <para>菜单：Tools/地形树原型预处理/…</para>
    /// </summary>
    public class TerrainTreePrototypePreparer : EditorWindow
    {
        /// <summary>默认目标文件夹（Pandazole 自然包的全部树木预制体）</summary>
        public const string DefaultFolder =
            "Assets/Pandazole_Ultimate_Pack/Pandazole Nature Environment Pack/Tree/Prefabs";

        /// <summary><see cref="EditorUtility.DisplayProgressBar"/> 会派发编辑器事件，用该锁防止重入</summary>
        private static bool _busy;

        /// <summary>最近一次运行的报告（窗口与自动化调用共用）</summary>
        private static string _lastReport = string.Empty;

        [SerializeField] private string _folder = DefaultFolder;
        [SerializeField] private float _lod0Threshold = 0.02f;
        [SerializeField] private bool _onlyMissing = true;
        [SerializeField] private bool _addCapsuleCollider = false;

        /// <summary>是否输出每个预制体的明细（静态：批处理入口为静态方法，供菜单 / 自动化直接调用）</summary>
        private static bool _logDetail;

        private Vector2 _scroll;

        private enum ProcessResult
        {
            /// <summary>已写入 LODGroup（扫描模式下表示「需要处理」）</summary>
            Modified,

            /// <summary>按规则跳过（已有 LODGroup 等）</summary>
            Skipped,

            /// <summary>无法处理（无渲染器 / 异常）</summary>
            Failed,
        }

        #region 菜单入口

        [MenuItem("Tools/地形树原型预处理/打开窗口", false, 1)]
        private static void OpenWindow()
        {
            var wnd = GetWindow<TerrainTreePrototypePreparer>("地形树原型预处理");
            wnd.minSize = new Vector2(520f, 380f);
        }

        /// <summary>只扫描不写盘，先看看会动哪些预制体</summary>
        [MenuItem("Tools/地形树原型预处理/仅扫描报告 Pandazole 树(不修改)", false, 20)]
        private static void MenuDryRunDefault()
        {
            RunBatch(DefaultFolder, 0.02f, true, false, true);
        }

        /// <summary>一键处理默认文件夹（也会被自动化 / MCP 直接调用）</summary>
        [MenuItem("Tools/地形树原型预处理/一键处理 Pandazole 树(Tree/Prefabs)", false, 21)]
        private static void MenuApplyDefault()
        {
            RunBatch(DefaultFolder, 0.02f, true, false, false);
        }

        #endregion

        #region 批处理核心

        /// <summary>
        /// 批量把 <paramref name="folder"/> 下的预制体处理成树木原型形态。
        /// </summary>
        /// <param name="folder">Assets 相对路径</param>
        /// <param name="lod0Threshold">LOD0 的 screenRelativeTransitionHeight（越小越晚被剔除，树建议 0.01~0.05）</param>
        /// <param name="onlyMissing">true = 已有 LODGroup 的跳过</param>
        /// <param name="addCapsuleCollider">true = 顺带给没有 Collider 的预制体补一个胶囊碰撞体（Terrain 树碰撞体只认 Capsule）</param>
        /// <param name="dryRun">true = 只报告不写盘</param>
        /// <returns>文本报告；参数非法或重入时返回 null</returns>
        public static string RunBatch(string folder, float lod0Threshold, bool onlyMissing,
            bool addCapsuleCollider, bool dryRun = false)
        {
            if (_busy)
            {
                Debug.LogWarning("[地形树原型] 上一次处理尚未结束，已忽略本次调用");
                return null;
            }

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError($"[地形树原型] 文件夹不存在：{folder}");
                return null;
            }

            _busy = true;
            try
            {
                List<string> paths = CollectPrefabPaths(folder);

                var sb = new StringBuilder();
                sb.AppendLine($"[地形树原型] {(dryRun ? "扫描报告" : "开始处理")}：{folder}");
                sb.AppendLine($"  预制体 {paths.Count} 个｜LOD0 阈值 {lod0Threshold:0.###}｜"
                              + (onlyMissing ? "仅处理缺少 LODGroup 的" : "覆盖已有 LODGroup")
                              + (addCapsuleCollider ? "｜顺带补 CapsuleCollider" : string.Empty));

                int modified = 0, skipped = 0, failed = 0;

                try
                {
                    for (int i = 0; i < paths.Count; i++)
                    {
                        string path = paths[i];
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "地形树原型预处理", path, (i + 1f) / Mathf.Max(1, paths.Count)))
                        {
                            sb.AppendLine("  已取消（此前处理的结果保持生效）");
                            break;
                        }

                        ProcessResult result = ProcessOne(path, lod0Threshold, onlyMissing,
                            addCapsuleCollider, dryRun, out string note);

                        if (result == ProcessResult.Modified) modified++;
                        else if (result == ProcessResult.Skipped) skipped++;
                        else failed++;

                        // 明细：扫描模式全打印，应用模式只打印动过或失败的
                        if (_logDetail || result != ProcessResult.Skipped)
                        {
                            string tag = dryRun && result == ProcessResult.Modified ? "待处理"
                                : result == ProcessResult.Modified ? "已处理"
                                : result == ProcessResult.Skipped ? "跳过" : "失败";
                            sb.AppendLine($"  [{tag}] {path}｜{note}");
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (!dryRun && modified > 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                sb.AppendLine($"[地形树原型] {(dryRun ? "扫描" : "处理")}结束："
                              + $"{(dryRun ? "待处理" : "已修改")} {modified}｜跳过 {skipped}｜失败 {failed}"
                              + (dryRun ? "（未写盘）" : string.Empty));

                _lastReport = sb.ToString();
                Debug.Log(_lastReport);

                if (HasOpenInstances<TerrainTreePrototypePreparer>())
                {
                    GetWindow<TerrainTreePrototypePreparer>().Repaint();
                }

                return _lastReport;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>收集文件夹下全部预制体的资产路径（已排序，含子目录）</summary>
        private static List<string> CollectPrefabPaths(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            var paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            paths.Sort();
            return paths;
        }

        /// <summary>处理单个预制体；异常一律降级为 <see cref="ProcessResult.Failed"/>，不中断整批</summary>
        private static ProcessResult ProcessOne(string path, float lod0Threshold, bool onlyMissing,
            bool addCapsuleCollider, bool dryRun, out string note)
        {
            note = string.Empty;
            GameObject root = null;

            try
            {
                // 用隔离编辑流程改预制体，避免误改场景里的实例
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    note = "加载预制体失败";
                    return ProcessResult.Failed;
                }

                LODGroup existing = root.GetComponent<LODGroup>();
                if (existing != null && onlyMissing)
                {
                    note = "已存在 LODGroup";
                    return ProcessResult.Skipped;
                }

                // 收集可进 LOD0 的渲染器：只认 Mesh/SkinnedMesh，排除归其它 LODGroup 管的（嵌套 LOD）
                var renderers = new List<Renderer>();
                Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Renderer r = all[i];
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                    LODGroup owner = r.GetComponentInParent<LODGroup>();
                    if (owner != null && owner != existing) continue;

                    renderers.Add(r);
                }

                if (renderers.Count == 0)
                {
                    note = "没有可用的 MeshRenderer / SkinnedMeshRenderer";
                    return ProcessResult.Failed;
                }

                bool needCollider = addCapsuleCollider && root.GetComponent<Collider>() == null;

                if (dryRun)
                {
                    note = $"将写入 LOD0（{renderers.Count} 个渲染器）"
                           + (needCollider ? "，并补 CapsuleCollider" : string.Empty);
                    return ProcessResult.Modified;
                }

                LODGroup group = existing != null ? existing : root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.animateCrossFading = false;

                // 只有一级 LOD：阈值即「整个 group 的剔除阈值」，不能设太大，否则稍远就整体消失
                group.SetLODs(new[]
                {
                    new LOD(Mathf.Clamp(lod0Threshold, 0.001f, 1f), renderers.ToArray()),
                });
                group.RecalculateBounds();

                string colliderNote = string.Empty;
                if (needCollider) AddCapsule(root, renderers, out colliderNote);

                PrefabUtility.SaveAsPrefabAsset(root, path);

                note = $"LOD0 = {renderers.Count} 个渲染器{colliderNote}";
                return ProcessResult.Modified;
            }
            catch (System.Exception e)
            {
                note = e.Message;
                return ProcessResult.Failed;
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>按渲染包围盒补一个胶囊碰撞体（Terrain 树碰撞体只认 CapsuleCollider）</summary>
        private static void AddCapsule(GameObject root, List<Renderer> renderers, out string note)
        {
            Bounds total = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) total.Encapsulate(renderers[i].bounds);

            Vector3 center = root.transform.InverseTransformPoint(total.center);
            Vector3 size = total.size;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // Y

            float height = Mathf.Max(size.y, 0.02f);
            float radius = Mathf.Max(Mathf.Min(size.x, size.z) * 0.5f, 0.01f);
            radius = Mathf.Min(radius, height * 0.5f);

            capsule.center = center;
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);

            note = $"，并补 CapsuleCollider(r={radius:0.##}, h={capsule.height:0.##})";
        }

        #endregion

        #region 窗口 UI

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "把预制体处理成「可被 Terrain 当树木原型使用」的形态：\n" +
                "给根节点补一个只有 LOD0 的 LODGroup，并把其下所有 MeshRenderer / SkinnedMeshRenderer 收进 LOD0。\n\n" +
                "依据：Terrain 引擎靠「根节点是否有 LODGroup」区分树类型。没有 LODGroup 时会被当成已废弃的\n" +
                "Tree Editor 树，要求内置 Soft Occlusion 材质 + Ambient-Occlusion 文件夹，否则不会渲染。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("目标", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("文件夹", _folder);
                if (GUILayout.Button("选择…", GUILayout.Width(60f)))
                {
                    string abs = EditorUtility.OpenFolderPanel("选择预制体文件夹", Application.dataPath, string.Empty);
                    string assetPath = ToAssetPath(abs);
                    if (!string.IsNullOrEmpty(assetPath)) _folder = assetPath;
                    else if (!string.IsNullOrEmpty(abs)) Debug.LogWarning("[地形树原型] 请选择 Assets 目录下的文件夹");
                }

                if (GUILayout.Button("默认", GUILayout.Width(50f))) _folder = DefaultFolder;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);

            _lod0Threshold = EditorGUILayout.Slider(
                new GUIContent("LOD0 阈值", "screenRelativeTransitionHeight：屏幕占比低于该值时不渲染。越小越晚剔除。"),
                _lod0Threshold, 0.005f, 0.2f);
            _onlyMissing = EditorGUILayout.ToggleLeft("仅处理缺少 LODGroup 的（不覆盖已配置）", _onlyMissing);
            _addCapsuleCollider = EditorGUILayout.ToggleLeft(
                "顺带补 CapsuleCollider（⚠ 树会因此产生不可见碰撞，请自行评估）", _addCapsuleCollider);
            _logDetail = EditorGUILayout.ToggleLeft("输出每个预制体的明细", _logDetail);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("扫描（不写盘）", GUILayout.Height(26f)))
                {
                    RunBatch(_folder, _lod0Threshold, _onlyMissing, _addCapsuleCollider, true);
                }

                using (new EditorGUI.DisabledScope(_busy))
                {
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                    if (GUILayout.Button("执行处理", GUILayout.Height(26f)))
                    {
                        RunBatch(_folder, _lod0Threshold, _onlyMissing, _addCapsuleCollider, false);
                    }
                    GUI.backgroundColor = old;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("报告", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(_lastReport, GUILayout.MinHeight(140f));
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>绝对路径 → Assets 相对路径；不在工程内返回 null</summary>
        private static string ToAssetPath(string absolute)
        {
            if (string.IsNullOrEmpty(absolute)) return null;
            string data = Application.dataPath.Replace('\\', '/');
            string p = absolute.Replace('\\', '/');
            if (p == data) return "Assets";
            if (p.StartsWith(data + "/")) return "Assets" + p.Substring(data.Length);
            return null;
        }

        #endregion
    }
}
