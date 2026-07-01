using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class MissionDataEditorWindow : EditorWindow
{
    private enum TabType { Mission, Camp, Airdrop, Map, Update, WeaponModule, WeaponUpgrade }

    private TabType _currentTab = TabType.Mission;

    // Tab state fields (generic wrappers for left/right scroll + items + selected + search)
    private Vector2 _leftScrollMission, _rightScrollMission;
    private Vector2 _leftScrollCamp, _rightScrollCamp;
    private Vector2 _leftScrollAirdrop, _rightScrollAirdrop;
    private Vector2 _leftScrollMap, _rightScrollMap;
    private Vector2 _leftScrollUpdate, _rightScrollUpdate;
    private Vector2 _leftScrollWeaponModule, _rightScrollWeaponModule;
    private Vector2 _leftScrollWeaponUpgrade, _rightScrollWeaponUpgrade;

    private List<MissionItem> _missionItems = new List<MissionItem>();
    private List<CampItem> _campItems = new List<CampItem>();
    private List<AirdropItem> _airdropItems = new List<AirdropItem>();
    private List<MapItem> _mapItems = new List<MapItem>();
    private List<UpdateItem> _updateItems = new List<UpdateItem>();
    private List<WeaponModuleItem> _weaponModuleItems = new List<WeaponModuleItem>();
    private List<WeaponUpgradeItem> _weaponUpgradeItems = new List<WeaponUpgradeItem>();

    private MissionItem _selectedMissionItem;
    private CampItem _selectedCampItem;
    private AirdropItem _selectedAirdropItem;
    private MapItem _selectedMapItem;
    private UpdateItem _selectedUpdateItem;
    private WeaponModuleItem _selectedWeaponModuleItem;
    private WeaponUpgradeItem _selectedWeaponUpgradeItem;

    private string _searchFilterMission = "";
    private string _searchFilterCamp = "";
    private string _searchFilterAirdrop = "";
    private string _searchFilterMap = "";
    private string _searchFilterUpdate = "";
    private string _searchFilterWeaponModule = "";
    private string _searchFilterWeaponUpgrade = "";

    private GUIStyle _mainTaskStyle;
    private GUIStyle _subTaskStyle;

    // Shared cached editor
    private UnityEditor.Editor _cachedEditor;

    [MenuItem("Tools/数据编辑器")]
    private static void Open()
    {
        var window = GetWindow<MissionDataEditorWindow>();
        window.titleContent = new GUIContent("数据编辑器");
        window.Show();
    }

    #region Data Structures

    private struct MissionItem
    {
        public MissionData_SO data;
        public string label;
        public bool isMain;
    }

    private struct CampItem
    {
        public CampData_SO data;
    }

    private struct AirdropItem
    {
        public AirdropData_SO data;
    }

    private struct MapItem
    {
        public MapData_SO data;
    }

    private struct UpdateItem
    {
        public UpdateData_SO data;
    }

    private struct WeaponModuleItem
    {
        public WeaponModuleData_SO data;
    }

    private struct WeaponUpgradeItem
    {
        public WeaponUpgradeData_SO data;
    }

    #endregion

    private void OnEnable()
    {
        RefreshAll();
    }

    private void OnDisable()
    {
        DestroyCachedEditor();
    }

    private void DestroyCachedEditor()
    {
        if (_cachedEditor != null)
        {
            DestroyImmediate(_cachedEditor);
            _cachedEditor = null;
        }
    }

    private void RefreshAll()
    {
        DestroyCachedEditor();
        RefreshMissionData();
        RefreshCampData();
        RefreshAirdropData();
        RefreshMapData();
        RefreshUpdateData();
        RefreshWeaponModuleData();
        RefreshWeaponUpgradeData();
    }

    #region Generic Helpers (abstracted patterns)

    /// <summary>Create a colored GUIStyle that keeps color in all states</summary>
    private static GUIStyle ColoredLabel(GUIStyle baseStyle, Color color)
    {
        var style = new GUIStyle(baseStyle);
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        style.onNormal.textColor = color;
        style.onHover.textColor = color;
        style.onActive.textColor = color;
        style.onFocused.textColor = color;
        return style;
    }

    /// <summary>Draw the common left panel boilerplate</summary>
    private void DrawLeftPanel<T>(ref Vector2 scrollPos, List<T> items, string countLabel,
        Func<List<T>, List<T>> filter, Action<T> drawItem, Action drawFooter = null)
    {
        float panelWidth = Mathf.Max(position.width * 0.35f, 200);
        EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth));
        GUILayout.Label(countLabel, EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUI.skin.box, GUILayout.ExpandHeight(true));

        foreach (var item in filter(items))
            drawItem(item);

        EditorGUILayout.EndScrollView();

        // Footer toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
        if (GUILayout.Button("刷新列表", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
        {
            DestroyCachedEditor();
            RefreshCurrentTab();
        }
        drawFooter?.Invoke();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>Draw the common right panel (empty state + title + inspector)</summary>
    private void DrawRightPanel(ref Vector2 scrollPos, bool hasSelection, string emptyMsg, string title, Action drawExtra)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        if (!hasSelection || _cachedEditor == null)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(emptyMsg, MessageType.Info);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        GUILayout.Label(title, EditorStyles.boldLabel);
        drawExtra?.Invoke();
        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();
        _cachedEditor.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck() && _cachedEditor.serializedObject != null && _cachedEditor.serializedObject.hasModifiedProperties)
        {
            _cachedEditor.serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>Draw selection highlight background</summary>
    private void DrawSelectionHighlight(Rect rowRect, bool isSelected)
    {
        if (isSelected)
            EditorGUI.DrawRect(new Rect(0, rowRect.y, position.width * 0.35f, rowRect.height), new Color(0.3f, 0.5f, 1f, 0.2f));
    }

    /// <summary>Handle click for selecting an item in a list item</summary>
    private bool HandleClick<T>(T item, Action<T> selectAction)
    {
        var clickRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && clickRect.Contains(Event.current.mousePosition))
        {
            selectAction(item);
            Event.current.Use();
            return true;
        }
        return false;
    }

    /// <summary>Select an item: create cached editor</summary>
    private void SelectItem<T>(ref T selectedField, T item) where T : struct
    {
        selectedField = item;
        DestroyCachedEditor();
        // We need the data field via reflection or interface. Use a separate pattern per type.
        // This is a generic shell; actual implementations call the typed version.
    }

    /// <summary>Refresh data from a folder: load all assets of type T, then sort</summary>
    private void RefreshFromFolder<TItem, TData>(string rootPath, string typeName, List<TItem> items,
        Func<string, TData> loader, Action<List<TItem>> sorter)
        where TItem : struct
        where TData : ScriptableObject
    {
        items.Clear();
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets($"t:{typeName}", new[] { rootPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TData data = loader(path);
            if (data == null) continue;
        }

        sorter(items);
    }

    /// <summary>Refresh from folder with subdirectories</summary>
    private void RefreshFromFolderWithSubDirs<TItem, TData>(string rootPath, string typeName, List<TItem> items,
        Func<string, TData> loader, Action<List<TItem>> sorter)
        where TItem : struct
        where TData : ScriptableObject
    {
        items.Clear();
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] subDirs = AssetDatabase.GetSubFolders(rootPath);
        string[] allPaths = new[] { rootPath }.Concat(subDirs).ToArray();
        string[] guids = AssetDatabase.FindAssets($"t:{typeName}", allPaths);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TData data = loader(path);
            if (data == null) continue;
        }

        sorter(items);
    }

    /// <summary>Create editor for a ScriptableObject</summary>
    private UnityEditor.Editor CreateEditorFor(Object target)
    {
        return target != null ? UnityEditor.Editor.CreateEditor(target) : null;
    }

    #endregion

    #region Data Refresh

    private void RefreshMissionData()
    {
        _missionItems.Clear();
        string rootPath = "Assets/Resources/GameData/Mission";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] subDirs = AssetDatabase.GetSubFolders(rootPath);
        foreach (string dir in subDirs)
        {
            string[] guids = AssetDatabase.FindAssets("t:MissionData_SO", new[] { dir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MissionData_SO data = AssetDatabase.LoadAssetAtPath<MissionData_SO>(path);
                if (data == null) continue;

                string label = GetEnumLabel(data.type);
                bool isMain = data is MissionMainData_SO;
                _missionItems.Add(new MissionItem { data = data, label = label, isMain = isMain });
            }
        }

        _missionItems = _missionItems.OrderBy(i => (int)i.data.type).ToList();
    }

    private void RefreshCampData()
    {
        _campItems.Clear();
        string rootPath = "Assets/Resources/GameData/Camp";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:CampData_SO", new[] { rootPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CampData_SO data = AssetDatabase.LoadAssetAtPath<CampData_SO>(path);
            if (data == null) continue;
            _campItems.Add(new CampItem { data = data });
        }

        _campItems = _campItems.OrderBy(i => (int)i.data.enemyVarietyType).ToList();
    }

    private void RefreshAirdropData()
    {
        _airdropItems.Clear();
        string rootPath = "Assets/Resources/GameData/Airdrop";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:AirdropData_SO", new[] { rootPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AirdropData_SO data = AssetDatabase.LoadAssetAtPath<AirdropData_SO>(path);
            if (data == null) continue;
            _airdropItems.Add(new AirdropItem { data = data });
        }

        _airdropItems = _airdropItems.OrderBy(i => i.data.ID).ToList();
    }

    private void RefreshMapData()
    {
        _mapItems.Clear();
        string rootPath = "Assets/Resources/GameData/Map";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:MapData_SO", new[] { rootPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MapData_SO data = AssetDatabase.LoadAssetAtPath<MapData_SO>(path);
            if (data == null) continue;
            _mapItems.Add(new MapItem { data = data });
        }

        _mapItems = _mapItems.OrderBy(i => i.data.name).ToList();
    }

    private void RefreshUpdateData()
    {
        _updateItems.Clear();
        string rootPath = "Assets/Resources/GameData/Update";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:UpdateData_SO", new[] { rootPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UpdateData_SO data = AssetDatabase.LoadAssetAtPath<UpdateData_SO>(path);
            if (data == null) continue;
            _updateItems.Add(new UpdateItem { data = data });
        }

        _updateItems = _updateItems.OrderBy(i => i.data.time).ThenBy(i => i.data.name).ToList();
    }

    private void RefreshWeaponModuleData()
    {
        _weaponModuleItems.Clear();
        string rootPath = "Assets/Resources/GameData/WeaponModule";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] subDirs = AssetDatabase.GetSubFolders(rootPath);
        string[] allSearchPaths = new[] { rootPath }.Concat(subDirs).ToArray();
        string[] guids = AssetDatabase.FindAssets("t:WeaponModuleData_SO", allSearchPaths);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponModuleData_SO data = AssetDatabase.LoadAssetAtPath<WeaponModuleData_SO>(path);
            if (data == null) continue;
            _weaponModuleItems.Add(new WeaponModuleItem { data = data });
        }

        _weaponModuleItems = _weaponModuleItems.OrderBy(i => i.data.type).ThenBy(i => i.data.name).ToList();
    }

    private void RefreshWeaponUpgradeData()
    {
        _weaponUpgradeItems.Clear();
        string rootPath = "Assets/Resources/GameData/WeaponUpgrade";
        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            Debug.LogError($"路径不存在: {rootPath}");
            return;
        }

        string[] subDirs = AssetDatabase.GetSubFolders(rootPath);
        string[] allSearchPaths = new[] { rootPath }.Concat(subDirs).ToArray();
        string[] guids = AssetDatabase.FindAssets("t:WeaponUpgradeData_SO", allSearchPaths);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponUpgradeData_SO data = AssetDatabase.LoadAssetAtPath<WeaponUpgradeData_SO>(path);
            if (data == null) continue;
            _weaponUpgradeItems.Add(new WeaponUpgradeItem { data = data });
        }

        _weaponUpgradeItems = _weaponUpgradeItems.OrderBy(i =>
        {
            string path = AssetDatabase.GetAssetPath(i.data);
            string folderName = Path.GetFileName(Path.GetDirectoryName(path));
            return string.IsNullOrEmpty(folderName) || folderName == "WeaponUpgrade" ? i.data.name : $"{folderName}/{i.data.name}";
        }).ToList();
    }

    #endregion

    #region GUI

    private void OnGUI()
    {
        // Init styles
        if (_mainTaskStyle == null)
        {
            _mainTaskStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow },
            };
        }
        if (_subTaskStyle == null)
        {
            _subTaskStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
            };
        }

        DrawToolbar();
        DrawTabs();

        EditorGUILayout.BeginHorizontal();
        switch (_currentTab)
        {
            case TabType.Mission:
                DrawMissionLeftPanel();
                DrawResizeHandle();
                DrawMissionRightPanel();
                break;
            case TabType.Camp:
                DrawCampLeftPanel();
                DrawResizeHandle();
                DrawCampRightPanel();
                break;
            case TabType.Airdrop:
                DrawAirdropLeftPanel();
                DrawResizeHandle();
                DrawAirdropRightPanel();
                break;
            case TabType.Map:
                DrawMapLeftPanel();
                DrawResizeHandle();
                DrawMapRightPanel();
                break;
            case TabType.Update:
                DrawUpdateLeftPanel();
                DrawResizeHandle();
                DrawUpdateRightPanel();
                break;
            case TabType.WeaponModule:
                DrawWeaponModuleLeftPanel();
                DrawResizeHandle();
                DrawWeaponModuleRightPanel();
                break;
            case TabType.WeaponUpgrade:
                DrawWeaponUpgradeLeftPanel();
                DrawResizeHandle();
                DrawWeaponUpgradeRightPanel();
                break;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            RefreshAll();

        GUILayout.Space(10);
        DrawSearchField();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSearchField()
    {
        switch (_currentTab)
        {
            case TabType.Mission:
                _searchFilterMission = EditorGUILayout.TextField(_searchFilterMission, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                break;
            case TabType.Camp:
                _searchFilterCamp = EditorGUILayout.TextField(_searchFilterCamp, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                break;
            case TabType.Airdrop:
                _searchFilterAirdrop = EditorGUILayout.TextField(_searchFilterAirdrop, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                break;
            case TabType.Map:
                _searchFilterMap = EditorGUILayout.TextField(_searchFilterMap, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                break;
            case TabType.Update:
                _searchFilterUpdate = EditorGUILayout.TextField(_searchFilterUpdate, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                break;
            case TabType.WeaponModule:
                _searchFilterWeaponModule = EditorGUILayout.TextField(_searchFilterWeaponModule, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                break;
            case TabType.WeaponUpgrade:
                _searchFilterWeaponUpgrade = EditorGUILayout.TextField(_searchFilterWeaponUpgrade, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                break;
        }
    }

    private void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);

        DrawTabButton("任务数据", TabType.Mission);
        DrawTabButton("阵营数据", TabType.Camp);
        DrawTabButton("战备数据", TabType.Airdrop);
        DrawTabButton("地图数据", TabType.Map);
        DrawTabButton("更新说明", TabType.Update);
        DrawTabButton("武器模组", TabType.WeaponModule);
        DrawTabButton("武器升级", TabType.WeaponUpgrade);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTabButton(string label, TabType tab)
    {
        bool selected = _currentTab == tab;
        if (GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(100)) != selected)
        {
            _currentTab = tab;
            DestroyCachedEditor();
        }
    }

    #endregion

    #region Mission Tab

    private void DrawMissionLeftPanel()
    {
        DrawLeftPanel(ref _leftScrollMission, _missionItems, $"共 {_missionItems.Count} 项",
            items => string.IsNullOrEmpty(_searchFilterMission)
                ? items
                : items.Where(i => i.label.Contains(_searchFilterMission) || i.data.name.Contains(_searchFilterMission)).ToList(),
            DrawMissionListItem, DrawLocateButton);
    }

    private void DrawMissionListItem(MissionItem item)
    {
        var data = item.data;
        bool isSelected = _selectedMissionItem.data == data;

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.sprite != null)
        {
            Color tint = Color.white;
            if (item.isMain && data is MissionMainData_SO mainData)
                tint = mainData.color;
            GUI.DrawTexture(iconRect, data.sprite.texture, ScaleMode.ScaleToFit, true, 0, tint, 0, 0);
        }
        else
        {
            GUI.Box(iconRect, "");
        }

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(item.label, item.isMain ? _mainTaskStyle : _subTaskStyle);
            GUILayout.Label(string.IsNullOrEmpty(data.desc) ? data.name : data.desc, new GUIStyle(GUI.skin.label) { fontSize = 10 });

            if (item.isMain && data is MissionMainData_SO mainData && mainData.subType != null && mainData.subType.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                foreach (var sub in mainData.subType)
                {
                    if (GUILayout.Button(GetEnumLabel(sub), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        SelectMissionByType(sub);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            SelectMissionItem(item);
            Event.current.Use();
        }
        DrawSelectionHighlight(rowRect, isSelected);
    }

    private void SelectMissionByType(MissionEnum type)
    {
        var found = _missionItems.FirstOrDefault(i => i.data.type == type);
        if (found.data != null)
            SelectMissionItem(found);
        else
            Debug.LogWarning($"未找到类型为 {GetEnumLabel(type)} 的任务数据");
    }

    private void SelectMissionItem(MissionItem item)
    {
        _selectedMissionItem = item;
        _cachedEditor = CreateEditorFor(item.data);
        Repaint();
    }

    private void DrawMissionRightPanel()
    {
        bool hasSel = _selectedMissionItem.data != null;
        DrawRightPanel(ref _rightScrollMission, hasSel, "请从左侧列表中选择一个任务数据",
            hasSel ? _selectedMissionItem.label : "",
            () =>
            {
                GUILayout.Label(_selectedMissionItem.isMain ? "(主线任务)" : "(支线任务)", EditorStyles.miniLabel);
                if (hasSel)
                    GUILayout.Label(AssetDatabase.GetAssetPath(_selectedMissionItem.data), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
            });
    }

    #endregion

    #region Camp Tab

    private void DrawCampLeftPanel()
    {
        DrawLeftPanel(ref _leftScrollCamp, _campItems, $"共 {_campItems.Count} 项",
            items => string.IsNullOrEmpty(_searchFilterCamp)
                ? items
                : items.Where(i => i.data.ShowName.Contains(_searchFilterCamp) || i.data.name.Contains(_searchFilterCamp)).ToList(),
            DrawCampListItem, DrawLocateButton);
    }

    private void DrawCampListItem(CampItem item)
    {
        var data = item.data;
        bool isSelected = _selectedCampItem.data == data;

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.Sprite != null)
            GUI.DrawTexture(iconRect, data.Sprite.texture, ScaleMode.ScaleToFit, true, 0, data.Color, 0, 0);
        else
            EditorGUI.DrawRect(iconRect, data.Color);

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(data.ShowName, EditorStyles.boldLabel);
            GUILayout.Label(string.IsNullOrEmpty(data.Desc) ? data.name : data.Desc, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            SelectCampItem(item);
            Event.current.Use();
        }
        DrawSelectionHighlight(rowRect, isSelected);
    }

    private void SelectCampItem(CampItem item)
    {
        _selectedCampItem = item;
        _cachedEditor = CreateEditorFor(item.data);
        Repaint();
    }

    private void DrawCampRightPanel()
    {
        bool hasSel = _selectedCampItem.data != null;
        DrawRightPanel(ref _rightScrollCamp, hasSel, "请从左侧列表中选择一个阵营数据",
            hasSel ? _selectedCampItem.data.ShowName : "",
            () => { if (hasSel) GUILayout.Label(AssetDatabase.GetAssetPath(_selectedCampItem.data), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } }); });
    }

    #endregion

    #region Airdrop Tab

    private void DrawAirdropLeftPanel()
    {
        DrawLeftPanel(ref _leftScrollAirdrop, _airdropItems, $"共 {_airdropItems.Count} 项",
            items => string.IsNullOrEmpty(_searchFilterAirdrop)
                ? items
                : items.Where(i => i.data.showName.Contains(_searchFilterAirdrop) || i.data.name.Contains(_searchFilterAirdrop)).ToList(),
            DrawAirdropListItem, DrawLocateButton);
    }

    private void DrawAirdropListItem(AirdropItem item)
    {
        var data = item.data;
        bool isSelected = _selectedAirdropItem.data == data;

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        else
            GUI.Box(iconRect, "");

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label($"{data.showName}[{data.ID}]", ColoredLabel(EditorStyles.boldLabel, data.Color));

            string opterStr = "";
            if (data.opter != null && data.opter.Length > 0)
            {
                opterStr = string.Join("", data.opter.Select(o => o switch
                {
                    DirectionEnum.Left => "←",
                    DirectionEnum.Up => "↑",
                    DirectionEnum.Right => "→",
                    DirectionEnum.Down => "↓",
                    _ => "?",
                }));
            }
            string deliveryStr = GetInspectorName<AirdropDeliveryEnum>(data.deliveryType);
            string infoText = string.IsNullOrEmpty(opterStr) ? deliveryStr : $"{opterStr} · {deliveryStr}";
            GUILayout.Label(infoText, new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            SelectAirdropItem(item);
            Event.current.Use();
        }
        DrawSelectionHighlight(rowRect, isSelected);
    }

    private void SelectAirdropItem(AirdropItem item)
    {
        _selectedAirdropItem = item;
        _cachedEditor = CreateEditorFor(item.data);
        Repaint();
    }

    private void DrawAirdropRightPanel()
    {
        bool hasSel = _selectedAirdropItem.data != null;
        DrawRightPanel(ref _rightScrollAirdrop, hasSel, "请从左侧列表中选择一个战备数据",
            hasSel ? $"{_selectedAirdropItem.data.showName}[{_selectedAirdropItem.data.ID}]" : "",
            () => { if (hasSel) GUILayout.Label(AssetDatabase.GetAssetPath(_selectedAirdropItem.data), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } }); },
            hasSel ? ColoredLabel(EditorStyles.boldLabel, _selectedAirdropItem.data.Color) : EditorStyles.boldLabel);
    }

    #endregion

    #region Map Tab

    private void DrawMapLeftPanel()
    {
        DrawLeftPanel(ref _leftScrollMap, _mapItems, $"共 {_mapItems.Count} 项",
            items => string.IsNullOrEmpty(_searchFilterMap)
                ? items
                : items.Where(i => i.data.AreaName.Contains(_searchFilterMap) || i.data.name.Contains(_searchFilterMap)).ToList(),
            DrawMapListItem, DrawLocateButton);
    }

    private void DrawMapListItem(MapItem item)
    {
        var data = item.data;
        bool isSelected = _selectedMapItem.data == data;

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.Icon != null)
            GUI.DrawTexture(iconRect, data.Icon.texture, ScaleMode.ScaleToFit, true, 0, data.color, 0, 0);
        else
            EditorGUI.DrawRect(iconRect, data.color);

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(data.AreaName, EditorStyles.boldLabel);
            GUILayout.Label(string.IsNullOrEmpty(data.AreaDesc) ? data.name : data.AreaDesc, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            SelectMapItem(item);
            Event.current.Use();
        }
        DrawSelectionHighlight(rowRect, isSelected);
    }

    private void SelectMapItem(MapItem item)
    {
        _selectedMapItem = item;
        _cachedEditor = CreateEditorFor(item.data);
        Repaint();
    }

    private void DrawMapRightPanel()
    {
        bool hasSel = _selectedMapItem.data != null;
        DrawRightPanel(ref _rightScrollMap, hasSel, "请从左侧列表中选择一个地图数据",
            hasSel ? _selectedMapItem.data.AreaName : "",
            () => { if (hasSel) GUILayout.Label(AssetDatabase.GetAssetPath(_selectedMapItem.data), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } }); });
    }

    #endregion

    #region Update Tab

    private void DrawUpdateLeftPanel()
    {
        DrawLeftPanel(ref _leftScrollUpdate, _updateItems, $"共 {_updateItems.Count} 项",
            items => string.IsNullOrEmpty(_searchFilterUpdate)
                ? items
                : items.Where(i => i.data.title.Contains(_searchFilterUpdate) || i.data.name.Contains(_searchFilterUpdate)).ToList(),
            DrawUpdateListItem, DrawLocateButton);
    }

    private void DrawUpdateListItem(UpdateItem item)
    {
        var data = item.data;
        bool isSelected = _selectedUpdateItem.data == data;

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        GUI.Box(iconRect, "U");

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(data.title, EditorStyles.boldLabel);
            GUILayout.Label(string.IsNullOrEmpty(data.desc) ? data.name : data.desc, new GUIStyle(GUI.skin.label) { fontSize = 10 });
            if (!string.IsNullOrEmpty(data.time))
                GUILayout.Label(data.time, new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Italic });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            SelectUpdateItem(item);
            Event.current.Use();
        }
        DrawSelectionHighlight(rowRect, isSelected);
    }

    private void SelectUpdateItem(UpdateItem item)
    {
        _selectedUpdateItem = item;
        _cachedEditor = CreateEditorFor(item.data);
        Repaint();
    }

    private void DrawUpdateRightPanel()
    {
        bool hasSel = _selectedUpdateItem.data != null;
        DrawRightPanel(ref _rightScrollUpdate, hasSel, "请从左侧列表中选择一个更新说明",
            hasSel ? _selectedUpdateItem.data.title : "",
            () => { if (hasSel) GUILayout.Label(AssetDatabase.GetAssetPath(_selectedUpdateItem.data), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } }); });
    }

    #endregion

    #region WeaponModule Tab

    private void DrawWeaponModuleLeftPanel()
    {
        DrawLeftPanel(ref _leftScrollWeaponModule, _weaponModuleItems, $"共 {_weaponModuleItems.Count} 项",
            items => string.IsNullOrEmpty(_searchFilterWeaponModule)
                ? items
                : items.Where(i => i.data.name.Contains(_searchFilterWeaponModule)).ToList(),
            DrawWeaponModuleListItem, DrawLocateButton);
    }

    private void DrawWeaponModuleListItem(WeaponModuleItem item)
    {
        var data = item.data;
        bool isSelected = _selectedWeaponModuleItem.data == data;

        EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(60));

        // Frame + icon (frame larger, icon smaller)
        var frameRect = EditorGUILayout.GetControlRect(GUILayout.Width(60), GUILayout.Height(60));
        var iconRect = new Rect(frameRect.x + 10, frameRect.y + 10, 40, 40);

        string framePath = $"Assets/Resources/Images/Icon/Frame_Module{data.type}.png";
        Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);

        if (frameSprite != null)
            GUI.DrawTexture(frameRect, frameSprite.texture, ScaleMode.ScaleToFit, true, 0, data.color, 0, 0);

        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        else if (frameSprite == null)
            EditorGUI.DrawRect(frameRect, data.color);

        // Info
        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(data.name, ColoredLabel(EditorStyles.boldLabel, data.color));

            if (data.desc != null)
            {
                Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Arrow_Down.png");

                foreach (var entry in data.desc)
                {
                    EditorGUILayout.BeginHorizontal();

                    var arrowRect = EditorGUILayout.GetControlRect(GUILayout.Width(12), GUILayout.Height(12));
                    if (arrowSprite != null)
                    {
                        var prevColor = GUI.color;
                        GUI.color = entry.Key ? Color.green : Color.red;
                        GUIUtility.RotateAroundPivot(entry.Key ? 180f : 0f, arrowRect.center);
                        GUI.DrawTexture(arrowRect, arrowSprite.texture, ScaleMode.ScaleToFit);
                        GUI.matrix = Matrix4x4.identity;
                        GUI.color = prevColor;
                    }
                    else
                    {
                        GUILayout.Label(entry.Key ? "↑" : "↓",
                            new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = entry.Key ? Color.green : Color.red } },
                            GUILayout.Width(12));
                    }

                    GUILayout.Label(entry.Value, new GUIStyle(GUI.skin.label) { fontSize = 10 });
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            SelectWeaponModuleItem(item);
            Event.current.Use();
        }
        DrawSelectionHighlight(rowRect, isSelected);
    }

    private void SelectWeaponModuleItem(WeaponModuleItem item)
    {
        _selectedWeaponModuleItem = item;
        _cachedEditor = CreateEditorFor(item.data);
        Repaint();
    }

    private void DrawWeaponModuleRightPanel()
    {
        bool hasSel = _selectedWeaponModuleItem.data != null;
        DrawRightPanel(ref _rightScrollWeaponModule, hasSel, "请从左侧列表中选择一个武器模组",
            hasSel ? _selectedWeaponModuleItem.data.name : "",
            () => { if (hasSel) GUILayout.Label(AssetDatabase.GetAssetPath(_selectedWeaponModuleItem.data), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } }); });
    }

    #endregion

    #region WeaponUpgrade Tab

    private void DrawWeaponUpgradeLeftPanel()
    {
        DrawLeftPanel(ref _leftScrollWeaponUpgrade, _weaponUpgradeItems, $"共 {_weaponUpgradeItems.Count} 项",
            items => string.IsNullOrEmpty(_searchFilterWeaponUpgrade)
                ? items
                : items.Where(i => i.data.name.Contains(_searchFilterWeaponUpgrade)).ToList(),
            DrawWeaponUpgradeListItem, DrawLocateButton);
    }

    private void DrawWeaponUpgradeListItem(WeaponUpgradeItem item)
    {
        var data = item.data;
        bool isSelected = _selectedWeaponUpgradeItem.data == data;

        EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(40));

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        else
            GUI.Box(iconRect, "");

        // Get parent folder name from asset path
        string assetPath = AssetDatabase.GetAssetPath(data);
        string folderName = Path.GetFileName(Path.GetDirectoryName(assetPath));

        EditorGUILayout.BeginVertical();
        {
            string displayName = string.IsNullOrEmpty(folderName) || folderName == "WeaponUpgrade"
                ? data.name
                : $"{folderName}/{data.name}";
            GUILayout.Label(displayName, EditorStyles.boldLabel);
            GUILayout.Label(data.type, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            SelectWeaponUpgradeItem(item);
            Event.current.Use();
        }
        DrawSelectionHighlight(rowRect, isSelected);
    }

    private void SelectWeaponUpgradeItem(WeaponUpgradeItem item)
    {
        _selectedWeaponUpgradeItem = item;
        _cachedEditor = CreateEditorFor(item.data);
        Repaint();
    }

    private void DrawWeaponUpgradeRightPanel()
    {
        bool hasSel = _selectedWeaponUpgradeItem.data != null;
        DrawRightPanel(ref _rightScrollWeaponUpgrade, hasSel, "请从左侧列表中选择一个武器升级",
            hasSel ? _selectedWeaponUpgradeItem.data.name : "",
            () =>
            {
                if (hasSel)
                {
                    string path = AssetDatabase.GetAssetPath(_selectedWeaponUpgradeItem.data);
                    GUILayout.Label(path, new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
                }
            });
    }

    #endregion

    #region Helpers

    private void RefreshCurrentTab()
    {
        switch (_currentTab)
        {
            case TabType.Mission: RefreshMissionData(); break;
            case TabType.Camp: RefreshCampData(); break;
            case TabType.Airdrop: RefreshAirdropData(); break;
            case TabType.Map: RefreshMapData(); break;
            case TabType.Update: RefreshUpdateData(); break;
            case TabType.WeaponModule: RefreshWeaponModuleData(); break;
            case TabType.WeaponUpgrade: RefreshWeaponUpgradeData(); break;
        }
    }

    private string GetSelectedAssetPath()
    {
        Object data = _currentTab switch
        {
            TabType.Mission => _selectedMissionItem.data,
            TabType.Camp => _selectedCampItem.data,
            TabType.Airdrop => _selectedAirdropItem.data,
            TabType.Map => _selectedMapItem.data,
            TabType.Update => _selectedUpdateItem.data,
            TabType.WeaponModule => _selectedWeaponModuleItem.data,
            TabType.WeaponUpgrade => _selectedWeaponUpgradeItem.data,
            _ => null,
        };
        if (data == null) return null;
        return AssetDatabase.GetAssetPath(data);
    }

    private void PingSelectedFolder()
    {
        string path = GetSelectedAssetPath();
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("提示", "请先选择一个数据项", "确定");
            return;
        }
        string folder = Path.GetDirectoryName(path);
        Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(folder);
        if (folderObj != null)
        {
            EditorGUIUtility.PingObject(folderObj);
            Selection.activeObject = folderObj;
        }
    }

    private void DrawLocateButton()
    {
        if (GUILayout.Button("定位到文件夹", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            PingSelectedFolder();
    }

    private void DrawResizeHandle()
    {
        GUILayout.Box("", GUILayout.Width(5), GUILayout.ExpandHeight(true));
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.ResizeHorizontal);
    }

    /// <summary>Overload for DrawRightPanel with a custom title style</summary>
    private void DrawRightPanel(ref Vector2 scrollPos, bool hasSelection, string emptyMsg, string title, Action drawExtra, GUIStyle titleStyle)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        if (!hasSelection || _cachedEditor == null)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(emptyMsg, MessageType.Info);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        GUILayout.Label(title, titleStyle ?? EditorStyles.boldLabel);
        drawExtra?.Invoke();
        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();
        _cachedEditor.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck() && _cachedEditor.serializedObject != null && _cachedEditor.serializedObject.hasModifiedProperties)
        {
            _cachedEditor.serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static string GetEnumLabel(MissionEnum value)
    {
        var fieldInfo = typeof(MissionEnum).GetField(value.ToString());
        if (fieldInfo == null) return value.ToString();

        var customAttrs = fieldInfo.GetCustomAttributes(false);
        foreach (var attr in customAttrs)
        {
            Type attrType = attr.GetType();
            if (attrType.Name == "CustomLabelAttribute")
            {
                var nameProp = attrType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null)
                {
                    string name = nameProp.GetValue(attr) as string;
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
        }

        var inspectorAttr = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
        if (inspectorAttr.Length > 0)
            return (inspectorAttr[0] as InspectorNameAttribute)?.displayName ?? value.ToString();

        return value.ToString();
    }

    private static string GetInspectorName<TEnum>(TEnum value) where TEnum : Enum
    {
        var fieldInfo = typeof(TEnum).GetField(value.ToString());
        if (fieldInfo == null) return value.ToString();

        var inspectorAttr = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
        if (inspectorAttr.Length > 0)
            return (inspectorAttr[0] as InspectorNameAttribute)?.displayName ?? value.ToString();

        return value.ToString();
    }

    #endregion
}
