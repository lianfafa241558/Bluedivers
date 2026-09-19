using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 材质引用查询窗口：指定一个材质，列出所有使用它的预制体/资产。
/// 结果按来源分四组：
/// <list type="bullet">
/// <item><description>预制体（直接引用）：Assets 下直接引用该材质的 .prefab，定位到具体子物体、材质槽位或组件字段</description></item>
/// <item><description>预制体变体：本身不直接引用该材质，但基体（预制体或模型）引用了它，于是从基体继承了该材质。
/// 「嵌套了候选预制体的外层预制体」不算，不列出</description></item>
/// <item><description>其他资产：ScriptableObject、其它材质、模型（FBX 材质重映射）等以资产级引用该材质的资源</description></item>
/// <item><description>仅资产级依赖：反查表命中但未能在序列化数据里定位到具体引用点，仅供参考</description></item>
/// </list>
/// 实现分两步：
/// <list type="number">
/// <item><description>建索引：遍历 Assets 下每个资产的直接依赖（<see cref="AssetDatabase.GetDependencies(string, bool)"/>），
/// 得到「材质资产路径 → 引用它的资产路径」反查表，以及「预制体/模型资产 → 依赖它的预制体」派生表。
/// 只在首次查询/资产变动后的首次查询时执行，带可取消进度条。</description></item>
/// <item><description>查询：用反查表取出候选资产，再沿派生表扩散出预制体变体，逐个加载并定位到具体对象与引用点。
/// 因此换材质查询是秒级的。</description></item>
/// </list>
/// 已知不覆盖的情况：场景中的对象（本工具只看资产）、运行时动态赋值的材质（MPB、代码 new 出的材质实例、Graphics.DrawMesh）。
/// </summary>
public class MaterialUsageFinder : EditorWindow
{
    // ========== 常量 ==========

    private const string MaterialExtension = ".mat";
    private const string PrefabExtension = ".prefab";
    private const string SceneExtension = ".unity";

    /// <summary>模型资产扩展名：用于识别 FBX 内嵌材质（依赖关系里内嵌材质可能被报成模型资产路径）</summary>
    private static readonly string[] ModelExtensions =
    {
        ".fbx", ".obj", ".dae", ".3ds", ".dxf", ".blend", ".max", ".ma", ".mb", ".c4d", ".lwo", ".stl", ".ply", ".skp",
    };

    // ========== 查询条件 ==========

    /// <summary>当前查询的材质</summary>
    private Material _material;

    /// <summary>跟随 Project / Hierarchy 的选中项自动切换材质</summary>
    private bool _followSelection = true;

    /// <summary>是否扫描预制体资产</summary>
    private bool _scanPrefab = true;

    /// <summary>是否扩散到预制体变体（从基体继承材质、自己没覆盖）</summary>
    private bool _includeDerived = true;

    /// <summary>是否扫描其他资产（SO / 材质 / 未打开的场景等）</summary>
    private bool _scanOtherAsset = true;

    // ========== 索引状态 ==========

    /// <summary>索引是否已建立</summary>
    private bool _indexBuilt;

    /// <summary>索引是否过期（资产变动后置位，下次查询时重建）</summary>
    private bool _indexStale;

    /// <summary>建索引时被用户取消（索引不完整）</summary>
    private bool _indexCancelled;

    /// <summary>已索引的资产数量</summary>
    private int _indexedAssetCount;

    /// <summary>建索引耗时（秒）</summary>
    private float _indexBuildSeconds;

    /// <summary>材质资产路径 → 直接引用它的资产路径</summary>
    private Dictionary<string, List<string>> _materialRefs = new();

    /// <summary>模型资产路径 → 直接引用它的资产路径（用于 FBX 内嵌材质）</summary>
    private Dictionary<string, List<string>> _modelRefs = new();

    /// <summary>模型内的子资产路径 → 直接引用它的资产路径</summary>
    private Dictionary<string, List<string>> _subAssetRefs = new();

    /// <summary>
    /// 基体资产路径 → 依赖它的预制体路径。
    /// 索引阶段不区分依赖种类（变体、嵌套、以模型为基体都会记进来），
    /// 查询时只保留「预制体变体」这一种关系（见 <see cref="ExpandDerivedPrefabs"/>）。
    /// </summary>
    private Dictionary<string, List<string>> _prefabUsers = new();

    /// <summary>
    /// 查询中 / 建索引中的重入锁。
    /// 进度条（<see cref="EditorUtility.DisplayCancelableProgressBar"/> 与 DisplayProgressBar）会派发编辑器事件，
    /// 使本窗口的 OnGUI 在索引还没建完时再次触发查询，进而把半成品索引清掉 —— 必须挡住。
    /// </summary>
    private bool _searching;
    private bool _indexBuilding;

    // ========== 查询结果 ==========

    private readonly List<UsageEntry> _prefabHits = new();
    private readonly List<UsageEntry> _derivedHits = new();
    private readonly List<UsageEntry> _assetHits = new();
    private readonly List<UsageEntry> _looseHits = new();

    private Vector2 _scroll;
    private string _selectedKey;
    private bool _dirtyQuery = true;
    private int _candidateCount;
    private int _derivedCount;

    /// <summary>结果分组（决定显示顺序与分组标题）</summary>
    private enum HitGroup
    {
        /// <summary>直接引用材质</summary>
        Prefab = 0,

        /// <summary>通过变体继承间接使用材质</summary>
        Derived = 1,

        Asset = 2,
        Loose = 3,
    }

    /// <summary>一条「某对象使用了目标材质」的记录</summary>
    private sealed class UsageEntry
    {
        /// <summary>所属分组</summary>
        public HitGroup Group;

        /// <summary>点击后选中的对象：预制体内的子物体 / 资产本身</summary>
        public Object Target;

        /// <summary>Ping 的目标：预制体条目指向预制体资产，其余指向自身</summary>
        public Object PingTarget;

        /// <summary>预制体或资产路径</summary>
        public string AssetPath;

        /// <summary>相对路径：预制体对象相对预制体根</summary>
        public string ObjectPath = "";

        /// <summary>附加说明，如「变体（基体：xxx）」</summary>
        public string Note = "";

        /// <summary>具体引用点，如 MeshRenderer.sharedMaterials[1]</summary>
        public string Detail = "";

        /// <summary>行标题</summary>
        public string Title = "";

        /// <summary>选中高亮用唯一键</summary>
        public string Key = "";
    }

    // ========== 窗口入口 ==========

    [MenuItem("Tools/材质引用查询")]
    private static void Open()
    {
        var window = GetWindow<MaterialUsageFinder>("材质引用查询");
        window.minSize = new Vector2(780, 520);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += MarkIndexStale;
        _searching = false;
        _indexBuilding = false;

        // 扫描范围是「过滤器」而不是配置：若被 Unity 存档/重载后残留成关闭状态，结果会静默为空，
        // 所以每次启用窗口（首次打开 / 域重载后恢复）都复位成默认全开，当次会话内仍可随意开关。
        _scanPrefab = true;
        _scanOtherAsset = true;
        _includeDerived = true;

        _dirtyQuery = true;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= MarkIndexStale;
    }

    /// <summary>资产发生变动：索引过期，下次查询时重建</summary>
    private void MarkIndexStale()
    {
        if (_indexBuilt)
        {
            _indexStale = true;
        }
    }

    /// <summary>选中的对象里若含材质则切换过去；选中的不是材质时保留当前材质</summary>
    private void OnSelectionChange()
    {
        if (!_followSelection) return;

        Material material = FindMaterialInSelection();
        if (material == null || material == _material) return;

        SetMaterial(material);
        Repaint();
    }

    private static Material FindMaterialInSelection()
    {
        Object[] objects = Selection.objects;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is Material material) return material;
        }
        return null;
    }

    private void SetMaterial(Material material)
    {
        if (_material == material) return;
        _material = material;
        _selectedKey = null;
        _dirtyQuery = true;
        Repaint();
    }

    // ========== 界面 ==========

    private void OnGUI()
    {
        DrawConditionRow();
        DrawScopeRow();

        // 条件变化后在本次 OnGUI 内立刻查询，保证 Layout 与 Repaint 用同一份结果
        if (_dirtyQuery)
        {
            _dirtyQuery = false;
            RunSearch();
        }

        DrawSummary();
        DrawResults();
    }

    /// <summary>第一行：材质选择 + 取当前选中 + 跟随选择</summary>
    private void DrawConditionRow()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("材质", EditorStyles.label, GUILayout.Width(30));

            EditorGUI.BeginChangeCheck();
            Material material = (Material)EditorGUILayout.ObjectField(_material, typeof(Material), false, GUILayout.Width(240));
            if (EditorGUI.EndChangeCheck()) SetMaterial(material);

            if (GUILayout.Button("取当前选中", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SetMaterial(FindMaterialInSelection());
            }

            EditorGUI.BeginChangeCheck();
            _followSelection = GUILayout.Toggle(_followSelection, " 跟随选择", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck() && _followSelection)
            {
                SetMaterial(FindMaterialInSelection());
            }

            GUILayout.FlexibleSpace();

            string materialPath = _material != null ? AssetDatabase.GetAssetPath(_material) : "";
            EditorGUILayout.LabelField(string.IsNullOrEmpty(materialPath) ? "—" : materialPath,
                EditorStyles.miniLabel, GUILayout.Width(320));
        }
    }

    /// <summary>第二行：扫描范围 + 刷新 + 重建索引 + 索引状态</summary>
    private void DrawScopeRow()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            _scanPrefab = GUILayout.Toggle(_scanPrefab, " 预制体资产", EditorStyles.toolbarButton, GUILayout.Width(90));
            _scanOtherAsset = GUILayout.Toggle(_scanOtherAsset, " 其他资产", EditorStyles.toolbarButton, GUILayout.Width(80));
            _includeDerived = GUILayout.Toggle(_includeDerived,
                new GUIContent(" 包含变体", "包含预制体变体（含以模型为基体的变体）：它们自己没覆盖材质，是从基体继承的"),
                EditorStyles.toolbarButton, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck()) _dirtyQuery = true;

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                _dirtyQuery = true;
            }

            if (GUILayout.Button("重建索引", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                BuildIndex();
                _dirtyQuery = true;
            }

            GUILayout.FlexibleSpace();

            string indexText = _indexBuilt
                ? $"索引：{_indexedAssetCount} 个资产 / {_indexBuildSeconds:F1}s" + (_indexCancelled ? "（已取消，不完整）" : "")
                : "索引：未建立";
            if (_indexStale) indexText += "　·　资产已变动，查询时重建";
            EditorGUILayout.LabelField(indexText, EditorStyles.miniLabel, GUILayout.Width(300));
        }
    }

    private void DrawSummary()
    {
        int total = _prefabHits.Count + _derivedHits.Count + _assetHits.Count + _looseHits.Count;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(_material == null ? "未选择材质" : $"共 {total} 处引用",
                EditorStyles.boldLabel, GUILayout.Width(140));

            if (_material != null)
            {
                var sb = new StringBuilder();
                sb.Append($"预制体 {_prefabHits.Count}　变体 {_derivedHits.Count}　其他资产 {_assetHits.Count}");
                if (_looseHits.Count > 0) sb.Append($"　未定位 {_looseHits.Count}");
                sb.Append($"　（直接候选 {_candidateCount}，变体 {_derivedCount}）");

                // 明确提示被关掉的筛选，避免「结果为空」时不知道该开哪个开关
                var off = new StringBuilder();
                if (!_scanPrefab) off.Append("预制体 ");
                if (!_scanOtherAsset) off.Append("其他资产 ");
                if (!_includeDerived) off.Append("包含变体 ");
                if (off.Length > 0) sb.Append($"　已关闭：{off}");
                EditorGUILayout.LabelField(sb.ToString(), EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(total == 0))
            {
                if (GUILayout.Button("复制结果", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    CopyReport();
                }
            }
        }
    }

    private void DrawResults()
    {
        if (_material == null)
        {
            EditorGUILayout.HelpBox(
                "指定一个材质即可列出所有使用它的预制体与资产。\n" +
                "· 在 Project 窗口选中材质 → 点「取当前选中」，或勾选「跟随选择」后直接点材质；也可把材质拖进输入框。\n" +
                "· 点击结果行：选中并高亮该对象；双击预制体条目：打开预制体编辑。\n" +
                "· 「预制体变体」分组列出的是：自己没引用材质、但基体（预制体或模型）引用了它从而继承过来的变体 —— 这种变体在资产依赖里查不到材质，只有靠继承关系才能找到。\n" +
                "· 只统计静态引用，不含运行时动态赋值（MPB、代码 new 的材质实例）、场景中的对象、以及嵌套了候选预制体的外层预制体。",
                MessageType.Info);
            return;
        }

        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_material)))
        {
            EditorGUILayout.HelpBox(
                "该材质不是项目资产（内置资源或运行时创建的材质实例），无法在资产依赖里查到引用。",
                MessageType.Warning);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
        DrawGroup("预制体（直接引用）", _prefabHits);
        DrawGroup("预制体变体（从基体继承材质）", _derivedHits);
        DrawGroup("其他资产（SO / 材质 / 模型等）", _assetHits);
        DrawGroup("仅资产级依赖（未定位到具体引用点）", _looseHits);
        EditorGUILayout.EndScrollView();
    }

    private void DrawGroup(string title, List<UsageEntry> list)
    {
        if (list.Count == 0) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"{title}　({list.Count})", EditorStyles.boldLabel);

        for (int i = 0; i < list.Count; i++)
        {
            DrawHitRow(list[i]);
        }
    }

    /// <summary>绘制单条结果行（两行：标题 / 路径 + 引用点），支持单击选中、双击定位</summary>
    private void DrawHitRow(UsageEntry entry)
    {
        const float lineHeight = 17f;

        Rect row = EditorGUILayout.GetControlRect(GUILayout.Height(lineHeight * 2f));
        bool selected = entry.Key == _selectedKey;
        bool hovered = row.Contains(Event.current.mousePosition);

        if (Event.current.type == EventType.Repaint)
        {
            if (selected)
            {
                EditorGUI.DrawRect(row, new Color(0.24f, 0.48f, 0.9f, 0.35f));
            }
            else if (hovered)
            {
                EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.06f));
            }
        }

        var titleRect = new Rect(row.x + 4f, row.y + 1f, row.width - 8f, lineHeight);
        var subRect = new Rect(row.x + 4f, row.y + lineHeight, row.width - 8f, lineHeight);

        GUI.Label(titleRect, entry.Title, EditorStyles.label);
        GUI.Label(subRect, BuildSubLine(entry), EditorStyles.miniLabel);

        EditorGUIUtility.AddCursorRect(row, MouseCursor.Link);

        if (Event.current.type == EventType.MouseDown && Event.current.clickCount > 0 && hovered)
        {
            _selectedKey = entry.Key;
            FocusTarget(entry, Event.current.clickCount > 1);
            Event.current.Use();
            Repaint();
        }
    }

    private static string BuildSubLine(UsageEntry entry)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(entry.Note)) builder.Append(entry.Note);
        if (!string.IsNullOrEmpty(entry.ObjectPath))
        {
            if (builder.Length > 0) builder.Append("　·　");
            builder.Append(entry.ObjectPath);
        }
        if (!string.IsNullOrEmpty(entry.Detail))
        {
            if (builder.Length > 0) builder.Append("　·　");
            builder.Append(entry.Detail);
        }
        return builder.ToString();
    }

    /// <summary>定位到结果对应的对象：单击选中并 Ping，双击打开预制体</summary>
    private static void FocusTarget(UsageEntry entry, bool doubleClick)
    {
        if (doubleClick && IsPrefabPath(entry.AssetPath))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
            if (prefab != null)
            {
                AssetDatabase.OpenAsset(prefab);
                return;
            }
        }

        Object target = entry.Target != null ? entry.Target : entry.PingTarget;
        if (target == null) return;

        Selection.activeObject = target;
        EditorGUIUtility.PingObject(entry.PingTarget != null ? entry.PingTarget : target);
    }

    // ========== 查询 ==========

    private void RunSearch()
    {
        if (_searching || _indexBuilding) return;
        _searching = true;
        try
        {
            RunSearchInternal();
        }
        finally
        {
            _searching = false;
        }
    }

    private void RunSearchInternal()
    {
        _prefabHits.Clear();
        _derivedHits.Clear();
        _assetHits.Clear();
        _looseHits.Clear();
        _candidateCount = 0;
        _derivedCount = 0;

        if (_material == null) return;

        EnsureIndex();

        string materialPath = AssetDatabase.GetAssetPath(_material);
        if (string.IsNullOrEmpty(materialPath)) return;

        List<string> candidates = CollectCandidates(materialPath);
        _candidateCount = candidates.Count;

        // 预制体变体：本身不引用材质，但从引用了该材质的基体（预制体或模型）继承过来
        List<KeyValuePair<string, string>> derived = null;
        if (_includeDerived)
        {
            derived = ExpandDerivedPrefabs(candidates);
            _derivedCount = derived.Count;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            string path = candidates[i];

            // 本工具不看场景：场景资产里的引用点无法在不加载场景的前提下定位，直接跳过
            if (path.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase)) continue;

            if (IsPrefabPath(path))
            {
                if (_scanPrefab) ScanPrefabAsset(path, HitGroup.Prefab, null);
            }
            else if (_scanOtherAsset)
            {
                ScanOtherAsset(path);
            }
        }

        if (derived == null || !_scanPrefab) return;

        for (int i = 0; i < derived.Count; i++)
        {
            ScanPrefabAsset(derived[i].Key, HitGroup.Derived, derived[i].Value);
        }
    }

    // ---------- 预制体资产 ----------

    /// <summary>扫描一个预制体，定位到具体子物体与引用点</summary>
    /// <param name="group">结果分组（直接引用 / 变体）</param>
    /// <param name="viaSourcePath">变体的基体路径，null 表示直接引用</param>
    private void ScanPrefabAsset(string prefabPath, HitGroup group, string viaSourcePath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            AddLooseEntry(prefabPath, "预制体加载失败");
            return;
        }

        List<UsageEntry> hits = GetGroupList(group);
        int before = hits.Count;

        string note = group == HitGroup.Derived ? BuildDerivedNote(prefab, viaSourcePath) : null;
        CollectSlotRefs(prefab, group, prefabPath, prefab, note);
        CollectSerializedRefsOnHierarchy(prefab, group, prefabPath, prefab, note);

        // 预制体条目统一 Ping 预制体资产，便于在 Project 窗口里定位
        for (int i = before; i < hits.Count; i++)
        {
            hits[i].PingTarget = prefab;
        }

        if (hits.Count == before && group == HitGroup.Prefab)
        {
            AddLooseEntry(prefabPath, "仅为资产级依赖，未在渲染槽位或组件字段中定位到引用点");
        }
    }

    /// <summary>变体条目的说明文字：写明它的基体</summary>
    private static string BuildDerivedNote(GameObject prefab, string viaSourcePath)
    {
        return string.IsNullOrEmpty(viaSourcePath) ? "" : $"变体（基体：{ShortenPath(viaSourcePath)}）";
    }

    /// <summary>把资产路径压缩成「上级目录/文件名」，用于区分同名但不同目录的资产</summary>
    private static string ShortenPath(string assetPath)
    {
        string fileName = Path.GetFileName(assetPath);
        int lastSlash = assetPath.LastIndexOf('/');
        if (lastSlash <= 0) return fileName;

        int previousSlash = assetPath.LastIndexOf('/', lastSlash - 1);
        return previousSlash < 0 ? fileName : assetPath.Substring(previousSlash + 1);
    }

    // ---------- 其他资产 ----------

    private void ScanOtherAsset(string assetPath)
    {
        // 模型资产：模型内的材质槽引用了该材质（FBX 内嵌材质被重映射到外部 .mat 时就是这种情况）
        if (TryGetModelContainer(assetPath, out string container) && container == assetPath)
        {
            _assetHits.Add(CreateAssetEntry(assetPath, AssetDatabase.LoadMainAssetAtPath(assetPath),
                "模型资产：模型内的材质槽引用了该材质（内嵌材质的重映射）"));
            return;
        }

        Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (asset == null)
        {
            AddLooseEntry(assetPath, "资产加载失败");
            return;
        }

        int found = CollectSerializedRefsOnAsset(asset, assetPath, out string detail);
        if (found == 0)
        {
            AddLooseEntry(assetPath, "仅为资产级依赖，未在序列化字段中定位到引用点");
            return;
        }

        var entry = CreateAssetEntry(assetPath, asset, detail);
        _assetHits.Add(entry);
    }

    // ========== 收集引用 ==========

    /// <summary>
    /// 扫描层级里的「渲染槽位」引用：Renderer 家族的 sharedMaterials、Terrain 的渲染材质、UI Graphic 的材质。
    /// </summary>
    /// <param name="prefabAsset">非空表示扫描预制体资产，用于设置 Ping 目标</param>
    /// <returns>命中数量</returns>
    private int CollectSlotRefs(GameObject root, HitGroup group, string assetPath, Object prefabAsset, string note)
    {
        int count = 0;
        Transform rootTransform = root.transform;

        // Renderer 家族：Mesh / SkinnedMesh / Particle / Trail / Line / Sprite 等
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Material[] materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                if (materials[slot] != _material) continue;
                count++;
                AddHierarchyEntry(group, renderer.gameObject, rootTransform, assetPath, prefabAsset, note,
                    $"{renderer.GetType().Name}.sharedMaterials[{slot}]");
            }
        }

        // Terrain 的渲染材质不在 Renderer 上（地形自带材质模板）
        Terrain[] terrains = root.GetComponentsInChildren<Terrain>(true);
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i].materialTemplate != _material) continue;
            count++;
            AddHierarchyEntry(group, terrains[i].gameObject, rootTransform, assetPath, prefabAsset, note,
                "Terrain.materialTemplate");
        }

        // UI：Graphic.material 在未显式赋值时会回退到默认 UI 材质，故先用属性快速筛掉，再用序列化字段确认
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic.material != _material) continue;
            if (!HasSerializedMaterialRef(graphic, "m_Material")) continue;
            count++;
            AddHierarchyEntry(group, graphic.gameObject, rootTransform, assetPath, prefabAsset, note,
                $"{graphic.GetType().Name}.material");
        }

        return count;
    }

    /// <summary>
    /// 扫描层级里「非渲染类组件」的序列化字段，找出直接引用目标材质的地方
    /// （脚本字段、URP DecalProjector.m_Material 等都走这里）。
    /// </summary>
    private int CollectSerializedRefsOnHierarchy(GameObject root, HitGroup group, string assetPath, Object prefabAsset, string note)
    {
        int count = 0;
        Transform rootTransform = root.transform;
        Component[] components = root.GetComponentsInChildren<Component>(true);

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null) continue;                                                          // 缺失脚本
            if (component is Renderer || component is Terrain || component is Graphic) continue;      // 已由槽位扫描精确覆盖

            var serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            if (!property.Next(true)) continue;

            do
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.name == "m_Script" || property.name == "m_ObjectHideFlags") continue;
                if (property.objectReferenceValue != _material) continue;

                count++;
                AddHierarchyEntry(group, component.gameObject, rootTransform, assetPath, prefabAsset, note,
                    $"{component.GetType().Name}.{property.propertyPath}");
            }
            while (property.Next(true));
        }

        return count;
    }

    /// <summary>扫描单个资产的序列化字段（递归整棵属性树），返回第一个命中项的属性路径</summary>
    private int CollectSerializedRefsOnAsset(Object asset, string assetPath, out string detail)
    {
        detail = "";
        int count = 0;

        var serializedObject = new SerializedObject(asset);
        SerializedProperty property = serializedObject.GetIterator();
        if (!property.Next(true)) return 0;

        do
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (property.name == "m_Script" || property.name == "m_ObjectHideFlags") continue;
            if (property.objectReferenceValue != _material) continue;

            count++;
            if (count == 1) detail = property.propertyPath;
        }
        while (property.Next(true));

        return count;
    }

    /// <summary>读取组件的指定序列化字段是否恰好引用目标材质（用于 Graphic.material 这类会回退默认值的属性）</summary>
    private bool HasSerializedMaterialRef(Component component, string propertyName)
    {
        var serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null
               && property.propertyType == SerializedPropertyType.ObjectReference
               && property.objectReferenceValue == _material;
    }

    // ========== 条目构造 ==========

    private void AddHierarchyEntry(HitGroup group, GameObject target, Transform root,
        string assetPath, Object prefabAsset, string note, string detail)
    {
        var entry = new UsageEntry
        {
            Group = group,
            Target = target,
            PingTarget = prefabAsset != null ? prefabAsset : target,
            AssetPath = assetPath,
            ObjectPath = BuildObjectPath(target.transform, root),
            Note = note ?? "",
            Detail = detail,
            Title = target.name,
        };
        entry.Key = $"{group}|{target.GetInstanceID()}|{detail}";

        GetGroupList(group).Add(entry);
    }

    private UsageEntry CreateAssetEntry(string assetPath, Object asset, string detail)
    {
        var entry = new UsageEntry
        {
            Group = HitGroup.Asset,
            Target = asset,
            PingTarget = asset,
            AssetPath = assetPath,
            ObjectPath = assetPath,
            Detail = detail,
            Title = Path.GetFileName(assetPath),
        };
        entry.Key = $"Asset|{assetPath}|{detail}";
        return entry;
    }

    private void AddLooseEntry(string assetPath, string reason)
    {
        var entry = new UsageEntry
        {
            Group = HitGroup.Loose,
            AssetPath = assetPath,
            ObjectPath = assetPath,
            Detail = reason,
            Title = Path.GetFileName(assetPath),
        };
        entry.Key = $"Loose|{assetPath}";
        _looseHits.Add(entry);
    }

    private List<UsageEntry> GetGroupList(HitGroup group)
    {
        switch (group)
        {
            case HitGroup.Prefab: return _prefabHits;
            case HitGroup.Derived: return _derivedHits;
            case HitGroup.Asset: return _assetHits;
            default: return _looseHits;
        }
    }

    /// <summary>构造相对路径：预制体内的对象相对预制体根</summary>
    private static string BuildObjectPath(Transform target, Transform root)
    {
        var builder = new StringBuilder(target.name);
        Transform current = target.parent;
        while (current != null && current != root)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    // ========== 索引 ==========

    private void EnsureIndex()
    {
        if (_indexBuilt && !_indexStale) return;
        BuildIndex();
    }

    /// <summary>
    /// 遍历 Assets 下全部资产的直接依赖，建立：
    /// ①「材质资产 → 引用它的资产」反查表（模型内嵌材质会同时记录「模型资产 → 引用者」与「模型子资产路径 → 引用者」两种键）；
    /// ②「预制体/模型资产 → 依赖它的预制体」表（索引阶段不做区分，变体、嵌套、以模型为基体都会记，查询时只取变体）。
    /// 索引建在局部字典里，最后一次性替换字段，避免进度条派发事件期间被重入查询读到半成品。
    /// </summary>
    private void BuildIndex()
    {
        if (_indexBuilding) return;
        _indexBuilding = true;

        var stopwatch = Stopwatch.StartNew();
        var materialRefs = new Dictionary<string, List<string>>();
        var modelRefs = new Dictionary<string, List<string>>();
        var subAssetRefs = new Dictionary<string, List<string>>();
        var prefabUsers = new Dictionary<string, List<string>>();

        string[] allPaths = AssetDatabase.GetAllAssetPaths();
        int indexed = 0;
        bool cancelled = false;

        try
        {
            for (int i = 0; i < allPaths.Length; i++)
            {
                string path = allPaths[i];
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;

                if (EditorUtility.DisplayCancelableProgressBar("材质引用查询 · 建立索引",
                        $"({i + 1}/{allPaths.Length}) {path}", i / (float)allPaths.Length))
                {
                    cancelled = true;
                    break;
                }

                indexed++;
                bool isPrefab = IsPrefabPath(path);
                string[] dependencies = AssetDatabase.GetDependencies(path, false);
                for (int d = 0; d < dependencies.Length; d++)
                {
                    string dependency = dependencies[d];
                    if (string.IsNullOrEmpty(dependency) || dependency == path) continue;

                    if (dependency.EndsWith(MaterialExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        AddReference(materialRefs, dependency, path);
                        continue;
                    }

                    if (TryGetModelContainer(dependency, out string container))
                    {
                        if (container == dependency) AddReference(modelRefs, container, path);
                        else AddReference(subAssetRefs, dependency, path);

                        // 派生表：哪个预制体用到了这个模型（变体/嵌套都算）
                        if (isPrefab) AddReference(prefabUsers, container, path);
                        continue;
                    }

                    // 派生表：哪个预制体以这个预制体为基体（变体）或嵌套了它
                    if (isPrefab && IsPrefabPath(dependency)) AddReference(prefabUsers, dependency, path);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            _indexBuilding = false;
        }

        _materialRefs = materialRefs;
        _modelRefs = modelRefs;
        _subAssetRefs = subAssetRefs;
        _prefabUsers = prefabUsers;
        _indexedAssetCount = indexed;
        _indexBuildSeconds = (float)stopwatch.Elapsed.TotalSeconds;
        _indexCancelled = cancelled;
        _indexBuilt = true;
        _indexStale = false;
    }

    /// <summary>用反查表取出「可能引用了该材质」的候选资产路径（去重）</summary>
    private List<string> CollectCandidates(string materialPath)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();

        AppendCandidates(seen, result, _materialRefs, materialPath);
        AppendCandidates(seen, result, _subAssetRefs, materialPath);

        // 材质来自模型（FBX 内嵌材质）时，依赖关系很可能记在模型资产上，故把引用模型的资产一并作为候选，后续逐个验证
        if (TryGetModelContainer(materialPath, out string container))
        {
            AppendCandidates(seen, result, _modelRefs, container);
        }

        return result;
    }

    /// <summary>
    /// 从候选资产出发，沿「基体 → 依赖它的预制体」派生表扩散，收集预制体变体。
    /// 变体若没有覆盖材质，它自己的资产依赖里查不到材质，只有靠这层继承关系才能找到。
    /// 只看变体：嵌套了候选预制体的外层预制体不算，也不继续从它往下走。
    /// </summary>
    /// <returns>每一项为「变体预制体路径 → 它的基体路径」</returns>
    private List<KeyValuePair<string, string>> ExpandDerivedPrefabs(List<string> candidates)
    {
        var result = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(candidates);
        var queue = new Queue<string>(candidates);

        while (queue.Count > 0)
        {
            string source = queue.Dequeue();
            if (!_prefabUsers.TryGetValue(source, out List<string> users)) continue;

            for (int i = 0; i < users.Count; i++)
            {
                string user = users[i];
                if (!seen.Add(user)) continue;
                if (!IsVariantPrefab(user)) continue;      // 嵌套引用/普通引用不算

                result.Add(new KeyValuePair<string, string>(user, source));
                queue.Enqueue(user);                      // 变体还可以再派生变体，继续往下走
            }
        }

        return result;
    }

    /// <summary>该预制体是否是预制体变体（继承了基体的内容与材质）</summary>
    private static bool IsVariantPrefab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant;
    }

    private static bool IsPrefabPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.EndsWith(PrefabExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendCandidates(HashSet<string> seen, List<string> result,
        Dictionary<string, List<string>> map, string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!map.TryGetValue(key, out List<string> list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            if (seen.Add(list[i])) result.Add(list[i]);
        }
    }

    private static void AddReference(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out List<string> list))
        {
            list = new List<string>();
            map[key] = list;
        }

        if (!list.Contains(value)) list.Add(value);
    }

    /// <summary>
    /// 路径是否为「模型资产」或「模型内的子资产路径」，是则输出模型资产路径（容器）。
    /// 只检查每种扩展名的首次出现，路径里出现重名目录的极端情况可能漏判。
    /// </summary>
    private static bool TryGetModelContainer(string assetPath, out string container)
    {
        container = null;
        int bestIndex = int.MaxValue;
        int bestLength = 0;

        for (int i = 0; i < ModelExtensions.Length; i++)
        {
            int index = assetPath.IndexOf(ModelExtensions[i], StringComparison.OrdinalIgnoreCase);
            if (index < 0 || index >= bestIndex) continue;

            int end = index + ModelExtensions[i].Length;
            // 扩展名必须落在路径末尾（模型资产本身）或后面紧跟 '/'（模型内的子资产）
            if (end != assetPath.Length && assetPath[end] != '/') continue;

            bestIndex = index;
            bestLength = ModelExtensions[i].Length;
        }

        if (bestIndex == int.MaxValue) return false;

        container = assetPath.Substring(0, bestIndex + bestLength);
        return true;
    }

    // ========== 报告 ==========

    private void CopyReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"材质：{_material.name}　路径：{AssetDatabase.GetAssetPath(_material)}");
        builder.AppendLine(
            $"共 {_prefabHits.Count + _derivedHits.Count + _assetHits.Count + _looseHits.Count} 处引用" +
            $"（预制体 {_prefabHits.Count} / 变体 {_derivedHits.Count} / 其他资产 {_assetHits.Count} / 未定位 {_looseHits.Count}）");

        AppendGroupReport(builder, "预制体（直接引用）", _prefabHits);
        AppendGroupReport(builder, "预制体变体", _derivedHits);
        AppendGroupReport(builder, "其他资产", _assetHits);
        AppendGroupReport(builder, "仅资产级依赖", _looseHits);

        string report = builder.ToString();
        EditorGUIUtility.systemCopyBuffer = report;
        UnityEngine.Debug.Log(report);
    }

    private static void AppendGroupReport(StringBuilder builder, string title, List<UsageEntry> list)
    {
        if (list.Count == 0) return;

        builder.AppendLine();
        builder.AppendLine($"【{title}】{list.Count}");

        for (int i = 0; i < list.Count; i++)
        {
            UsageEntry entry = list[i];
            builder.Append(string.IsNullOrEmpty(entry.AssetPath) ? entry.ObjectPath : entry.AssetPath);
            if (!string.IsNullOrEmpty(entry.Note))
            {
                builder.Append("　");
                builder.Append(entry.Note);
            }
            if (!string.IsNullOrEmpty(entry.AssetPath) && !string.IsNullOrEmpty(entry.ObjectPath))
            {
                builder.Append("　");
                builder.Append(entry.ObjectPath);
            }
            if (!string.IsNullOrEmpty(entry.Detail))
            {
                builder.Append("　");
                builder.Append(entry.Detail);
            }
            builder.AppendLine();
        }
    }
}
