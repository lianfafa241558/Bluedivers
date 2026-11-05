using System;
using System.Collections.Generic;
using System.Linq;

using FPSGame.Game;
using UnityEditorInternal;

using UnityEditor;
using UnityEngine;
using FPSGame.EditorExt;
using FPSGame.GameData;
using FPSGame.Weapon;
using FPSGame.Core;
using FPSGame.Gameplay;

namespace FPSGame.Game.Editor
{
    /// <summary>
    /// 武器升级/模组可视化编辑器窗口。
    /// <para>分部文件（同一个 partial 类，按职责拆开，改之前先看对文件）：</para>
    /// <para>· <c>_Score.cs</c> 评分页的权重表/锚点/明细行/对比表/评级（只画）；</para>
    /// <para>· <c>_ScoreCompute.cs</c> <c>BuildScoreReport</c> 与取值/曲线工具（评分口径改这里）；</para>
    /// <para>· <c>_Derivative.cs</c> 衍生物折算（持续效果/临时武器/地面火焰）与伤害成分口径；</para>
    /// <para>· <c>_Upgrade.cs</c> 改装树/显示属性/模组/底部 SO 检查器/增删选项；</para>
    /// <para>· <c>_Common.cs</c> 样式构建与内部数据结构（评分报告/列表项）。</para>
    /// </summary>
    public partial class WeaponUpgradeEditorWindow : EditorWindow
    {
        // ========== 状态 ==========
        private Vector2 _weaponListScroll;
        private Vector2 _upgradeScroll;
        private Vector2 _moduleScroll;
        private Vector2 _inspectorScroll;
        private Vector2 _propertyScroll;
        private Vector2 _scoreScroll;
        private Vector2 _renameScroll;
        private Vector2 _uniqueScroll;
        private Vector2 _showAttrScroll;
        private ReorderableList _showAttrList;
        private string _showAttrListCacheKey;

        private string _searchFilter = "";
        private int _selectedWeaponIndex = -1;

        // 右侧面板标签
        private enum RightPanelTab { General, WeaponParams, BulletParams, UpgradeModules, Score }
        private RightPanelTab _rightPanelTab;
        private int _selectedLevelIndex = -1;          // 选中的升级等级
        private int _selectedUpgradeIndex = -1;         // 选中的升级选项在该等级中的下标
        private int _selectedModuleIndex = -1;          // 选中的模组下标
        private string _selectedSOPath = "";            // 当前聚焦检查的 SO 路径（底部面板）

        // 缓存从 Resources/Weapons 加载的武器预设与控制器
        private List<WeaponAssetEntry> _weaponEntries = new();
        private WeaponPlayerController _currentController;
        private SerializedObject _serializedWeapon;

        // 评分页的对比目标（与当前武器分开存，切左边列表时可以一直拿它当参照）
        private WeaponPlayerController _compareController;
        private SerializedObject _serializedCompare;

        // 缓存 SO 的 Inspector Editor（保持折叠状态）
        private UnityEditor.Editor _cachedSOEditor;
        private ScriptableObject _cachedSOTarget;


        // ========== 样式 ==========
        private GUIStyle _cardStyle;
        private GUIStyle _cardSelectedStyle;
        private GUIStyle _activeCardStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _scoreBigStyle;
        private GUIStyle _compareHeaderStyle;
        private GUIStyle _compareCellStyle;
        private GUIStyle _compareDeltaStyle;
        private bool _stylesBuilt;


        // ========== 窗口入口 ==========
        [MenuItem("Tools/武器升级编辑器")]
        public static void ShowWindow()
        {
            var window = GetWindow<WeaponUpgradeEditorWindow>("武器升级编辑器");
            window.minSize = new Vector2(960, 640);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshWeaponList();
        }

        private void OnDisable()
        {
            DestroyCachedSOEditor();
        }

        private void OnFocus()
        {
            RefreshWeaponList();
            // 回到本窗口前多半刚在 Inspector / 别的窗口改过武器配置 ⇒ 顺手把属性表缓存也重建。
            // 这里不动选中状态（_selectedWeaponIndex / ClearSelection），避免点一下窗口就丢武器。
            if (!Application.isPlaying)
            {
                for (var i = 0; i < _weaponEntries.Count; i++) RebuildCfg(_weaponEntries[i].Controller);
            }
            Repaint();
        }

        // ========== 刷新武器列表 ==========
        /// <summary>
        /// 顶部「刷新」按钮：重建武器列表，并把**评分用到的缓存**全部作废。
        /// <para>▍为什么必须单独作废：① <see cref="SerializedObject"/> 是"上次 Update 的序列化快照"，
        /// 改了 prefab 光 <c>Update()</c> 有时仍读到旧结构，重建最稳；② <c>WeaponCfg</c> 的属性表是**懒加载缓存**
        /// （<c>attrs</c> 只在第一次 <c>cfg[type]</c> 时建），Inspector 改 <c>_fakeAttrs</c> 不会让它失效
        /// ⇒ 表现就是"改了武器配置，分数和对比还是原来的值"。</para>
        /// <para>⚠ 运行中不重建属性表：那会把武器身上已应用的改装/GameAttribute 全部换掉。</para>
        /// </summary>
        private void RefreshAllData()
        {
            RefreshWeaponList();

            // 重建 SerializedObject（当前武器 + 对比武器）
            _serializedWeapon = _currentController != null ? new SerializedObject(_currentController) : null;
            _serializedCompare = _compareController != null ? new SerializedObject(_compareController) : null;

            // 重建属性表：列表里每把枪都重建，避免"刷新后再点另一把还是旧值"
            if (!Application.isPlaying)
            {
                for (var i = 0; i < _weaponEntries.Count; i++) RebuildCfg(_weaponEntries[i].Controller);
            }

            _selectedWeaponIndex = -1;
            ClearSelection();
            Repaint();
        }

        /// <summary>丢到 <c>WeaponCfg.attrs</c> 这个懒加载缓存，下次 <c>cfg[type]</c> 会按 <c>_fakeAttrs</c> 重建。</summary>
        private static void RebuildCfg(WeaponPlayerController weapon)
        {
            if (weapon == null || weapon.cfg == null) return;
            weapon.cfg.Init();
        }

        private void RefreshWeaponList()
        {
            _weaponEntries.Clear();
            var prefabs = Resources.LoadAll<GameObject>("Weapons");
            foreach (var prefab in prefabs)
            {
                var controller = prefab.GetComponent<WeaponPlayerController>();
                if (controller == null) continue;
                _weaponEntries.Add(new WeaponAssetEntry
                {
                    Prefab = prefab,
                    Controller = controller,
                    DisplayName = controller.WeaponName ?? prefab.name,
                    Icon = controller.WeaponIcon,
                    TypeName = controller.WeaponType ?? "",
                    TypeEnum = (int)controller.WeaponTypeEnum,
                });
            }
            _weaponEntries.Sort((a, b) =>
            {
                var typeCmp = a.TypeEnum.CompareTo(b.TypeEnum);
                return typeCmp != 0 ? typeCmp : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });
        }

        // ========== 主 GUI ==========
        private void OnGUI()
        {
            BuildStyles();

            var totalRect = new Rect(0, 0, position.width, position.height);

            // 顶部工具栏
            DrawToolbar();

            // 主体：左侧列表 + 右侧内容
            var bodyRect = new Rect(0, 22, position.width, position.height - 22);
            var leftRect = new Rect(bodyRect.x, bodyRect.y, 240, bodyRect.height);
            var rightRect = new Rect(leftRect.xMax + 4, bodyRect.y, bodyRect.width - leftRect.width - 4, bodyRect.height);

            DrawWeaponList(leftRect);
            DrawRightPanel(rightRect);
        }

        // ========== 顶部工具栏 ==========
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var refreshTip = new GUIContent("刷新列表", "重建武器列表，并作废评分缓存（SerializedObject / 属性表）");
            if (GUILayout.Button(refreshTip, EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RefreshAllData();
            }
            GUILayout.FlexibleSpace();
            if (_currentController != null)
            {
                EditorGUILayout.LabelField($"当前武器: {_currentController.WeaponName}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ========== 左侧武器列表 ==========
        private void DrawWeaponList(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            // 搜索框
            var searchRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, 20);
            _searchFilter = EditorGUI.TextField(searchRect, _searchFilter, EditorStyles.toolbarSearchField);

            // 左下角定位按钮占位，列表高度要扣掉它，否则最后几行会被按钮压住
            const float locateBtnHeight = 24f;
            const float locateBtnGap = 4f;

            // 列表
            var listRect = new Rect(rect.x + 4, searchRect.yMax + 4, rect.width - 8,
                rect.height - 28 - locateBtnHeight - locateBtnGap);

            var filtered = string.IsNullOrWhiteSpace(_searchFilter)
                ? _weaponEntries
                : _weaponEntries.Where(e =>
                    e.DisplayName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            const float rowHeight = 46f;
            var viewRect = new Rect(0, 0, listRect.width - 16, filtered.Count * rowHeight);
            _weaponListScroll = GUI.BeginScrollView(listRect, _weaponListScroll, viewRect);

            for (var i = 0; i < filtered.Count; i++)
            {
                var entry = filtered[i];
                var entryRect = new Rect(0, i * (rowHeight + 2), viewRect.width, rowHeight);
                var isSelected = _weaponEntries.IndexOf(entry) == _selectedWeaponIndex;

                var bgColor = isSelected
                    ? new Color(0.3f, 0.5f, 0.8f, 0.6f)
                    : new Color(0.25f, 0.25f, 0.25f, 0.4f);
                EditorGUI.DrawRect(entryRect, bgColor);

                // 图标（左侧）
                var iconRect = new Rect(entryRect.x + 4, entryRect.y + 5, 36, 36);
                if (entry.Icon != null)
                {
                    GUI.DrawTexture(iconRect, entry.Icon.texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f));
                    GUI.Label(iconRect, "?", EditorStyles.centeredGreyMiniLabel);
                }

                // 名称（右上）
                var nameRect = new Rect(entryRect.x + 46, entryRect.y + 4, entryRect.width - 50, 18);
                EditorGUI.LabelField(nameRect, entry.DisplayName, EditorStyles.boldLabel);

                // 类型（右下）
                var typeRect = new Rect(entryRect.x + 46, entryRect.y + 24, entryRect.width - 50, 16);
                EditorGUI.LabelField(typeRect, entry.TypeName, EditorStyles.miniLabel);

                // 点击选择
                if (Event.current.type == EventType.MouseDown && entryRect.Contains(Event.current.mousePosition))
                {
                    var realIndex = _weaponEntries.IndexOf(entry);
                    SelectWeapon(realIndex);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUI.EndScrollView();

            // 左下角：把当前选中的武器 prefab 同步选中到 Project 窗口
            var locateRect = new Rect(rect.x + 4, rect.yMax - locateBtnHeight - 4, rect.width - 8, locateBtnHeight);
            var hasSelection = _selectedWeaponIndex >= 0 && _selectedWeaponIndex < _weaponEntries.Count;
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUI.Button(locateRect, "定位选中武器 (Project)"))
                {
                    SelectAssetInProject(hasSelection ? _weaponEntries[_selectedWeaponIndex].Prefab : null);
                }
            }
        }

        /// <summary>
        /// 在 Project 窗口中选中并高亮资产。只在用户显式点击时调用 ——
        /// 若挂进 OnGUI 每帧执行，Project 窗口的选中项会被编辑器窗口一直抢走。
        /// </summary>
        private static void SelectAssetInProject(UnityEngine.Object asset)
        {
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void SelectWeapon(int index)
        {
            if (index < 0 || index >= _weaponEntries.Count) return;
            _selectedWeaponIndex = index;
            _currentController = _weaponEntries[index].Controller;
            _serializedWeapon = new SerializedObject(_currentController);
            ClearSelection();
            ResetFocus();
        }

        private void ClearSelection()
        {
            _selectedLevelIndex = -1;
            _selectedUpgradeIndex = -1;
            _selectedModuleIndex = -1;
            _selectedSOPath = "";
        }

        private static void ResetFocus()
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        // ========== 右侧面板 ==========
        private void DrawRightPanel(Rect rect)
        {
            if (_currentController == null || _serializedWeapon == null)
            {
                GUI.Box(rect, "", EditorStyles.helpBox);
                var labelRect = new Rect(rect.x, rect.y + rect.height / 2 - 12, rect.width, 24);
                GUI.Label(labelRect, "请从左侧列表选择武器", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _serializedWeapon.Update();

            // 标签栏
            var tabHeight = 24f;
            var tabRect = new Rect(rect.x, rect.y, rect.width, tabHeight);
            DrawTabButtons(tabRect);

            // 内容区
            var contentRect = new Rect(rect.x, tabRect.yMax + 2, rect.width, rect.height - tabHeight - 2);

            switch (_rightPanelTab)
            {
                case RightPanelTab.UpgradeModules:
                    DrawUpgradeModulesContent(contentRect);
                    break;
                case RightPanelTab.General:
                    DrawPropertiesContent(contentRect, GeneralProps);
                    break;
                case RightPanelTab.WeaponParams:
                    DrawPropertiesContent(contentRect, WeaponProps);
                    break;
                case RightPanelTab.BulletParams:
                    DrawPropertiesContent(contentRect, BulletProps);
                    break;
                case RightPanelTab.Score:
                    DrawScoreContent(contentRect);
                    break;
            }

            _serializedWeapon.ApplyModifiedProperties();
        }

        private void DrawTabButtons(Rect rect)
        {
            var tabs = new[] { "通用属性", "武器属性", "子弹属性", "改装模块", "评分系统" };
            var values = new[] { RightPanelTab.General, RightPanelTab.WeaponParams, RightPanelTab.BulletParams, RightPanelTab.UpgradeModules, RightPanelTab.Score };
            // 按钮宽度按标签数均分，加 Tab 时只改上面两个数组，别再写死 /4
            var btnWidth = rect.width / tabs.Length;

            for (var i = 0; i < tabs.Length; i++)
            {
                var btnRect = new Rect(rect.x + i * btnWidth, rect.y, btnWidth - 2, rect.height);
                var isActive = _rightPanelTab == values[i];
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? new Color(0.35f, 0.5f, 0.75f) : new Color(0.25f, 0.25f, 0.25f);
                if (GUI.Button(btnRect, tabs[i], EditorStyles.miniButton))
                {
                    _rightPanelTab = values[i];
                    ResetFocus();
                }
                GUI.backgroundColor = oldBg;
            }
        }

        private void DrawUpgradeModulesContent(Rect rect)
        {
            // 顶部：重命名属性 + 专有属性
            var topHeight = 160;
            var topRect = new Rect(rect.x, rect.y, rect.width, topHeight);
            DrawRenameAndUniqueSection(topRect);

            // 剩余空间
            var restY = topRect.yMax + 4;
            var restHeight = rect.height - topHeight - 4;
            var upgradeHeight = restHeight * 0.45f;
            var moduleHeight = restHeight * 0.28f;
            var inspectorHeight = restHeight - upgradeHeight - moduleHeight - 8;

            // 改装区分左右：左=升级树, 右=显示的属性
            var attrWidth = Mathf.Max(rect.width * 0.22f, 130f);
            var upgradeTreeWidth = rect.width - attrWidth - 4;

            var upgradeRect = new Rect(rect.x, restY, upgradeTreeWidth, upgradeHeight);
            var attrRect = new Rect(rect.x + upgradeTreeWidth + 4, restY, attrWidth, upgradeHeight);
            var moduleRect = new Rect(rect.x, upgradeRect.yMax + 4, rect.width, moduleHeight);
            var inspectorRect = new Rect(rect.x, moduleRect.yMax + 4, rect.width, inspectorHeight);

            DrawUpgradeSection(upgradeRect);
            DrawShowAttrSection(attrRect);
            DrawModuleSection(moduleRect);
            DrawInspectorSection(inspectorRect);
        }

        private void DrawRenameAndUniqueSection(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            // 标题
            var titleRect = new Rect(rect.x + 4, rect.y + 2, rect.width - 8, 20);
            EditorGUI.LabelField(titleRect, "■ 属性", _titleStyle);

            var contentY = titleRect.yMax + 2;
            var contentHeight = rect.height - 24;

            var renameProp = _serializedWeapon.FindProperty("m_RenameAttr");
            var uniqueProp = _serializedWeapon.FindProperty("m_UniqueAttr");

            var usableWidth = rect.width - 16; // 左边距6 + 右边距6 + 中间间隔4
            var leftWidth = usableWidth * 0.6f;
            var rightWidth = usableWidth * 0.4f;

            // 左：重命名属性 (60%)
            var leftRect = new Rect(rect.x + 6, contentY, leftWidth, contentHeight);
            DrawInlineArray(leftRect, renameProp, "重命名属性", ref _renameScroll);

            // 右：专有属性 (40%)
            var rightRect = new Rect(leftRect.xMax + 4, contentY, rightWidth, contentHeight);
            DrawInlineArray(rightRect, uniqueProp, "专有属性", ref _uniqueScroll);
        }

        /// <summary>
        /// 绘制带折叠箭头的内联数组（标签在三角形右边，元素单行显示）
        /// </summary>
        private static void DrawInlineArray(Rect rect, SerializedProperty arrayProp, string label, ref Vector2 scroll)
        {
            if (arrayProp == null) return;

            // 头部：折叠箭头 + 标签 + 数量
            var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            arrayProp.isExpanded = EditorGUI.Foldout(
                new Rect(headerRect.x, headerRect.y, 16, headerRect.height),
                arrayProp.isExpanded, GUIContent.none, true);

            var labelWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(label)).x;
            var labelRect = new Rect(headerRect.x + 18, headerRect.y, labelWidth, headerRect.height);
            EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);

            var countRect = new Rect(headerRect.xMax - 30, headerRect.y, 30, headerRect.height);
            var newSize = EditorGUI.IntField(countRect, arrayProp.arraySize);
            if (newSize >= 0 && newSize != arrayProp.arraySize)
                arrayProp.arraySize = newSize;

            if (!arrayProp.isExpanded) return;

            // 元素列表（带框 + 单行绘制）
            var listY = headerRect.yMax + 2;
            var btnHeight = EditorGUIUtility.singleLineHeight + 4;
            var listHeight = rect.height - EditorGUIUtility.singleLineHeight - btnHeight - 4;
            var listRect = new Rect(rect.x, listY, rect.width, listHeight);

            // 外框
            GUI.Box(new Rect(listRect.x, listRect.y, listRect.width, listRect.height + btnHeight), "", EditorStyles.helpBox);

            var itemCount = arrayProp.arraySize;
            var totalHeight = itemCount * (EditorGUIUtility.singleLineHeight + 2);
            var innerRect = new Rect(listRect.x + 4, listRect.y + 2, listRect.width - 8, listRect.height - 4);
            var viewRect = new Rect(0, 0, innerRect.width - 16, Mathf.Max(totalHeight, innerRect.height));

            scroll = GUI.BeginScrollView(innerRect, scroll, viewRect);

            for (var i = 0; i < itemCount; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                var elemRect = new Rect(0, i * (EditorGUIUtility.singleLineHeight + 2),
                    viewRect.width, EditorGUIUtility.singleLineHeight);
                DrawStructSingleLine(elemRect, element, arrayProp);
            }

            GUI.EndScrollView();

            // 底部 +/- 按钮
            var btnY = listRect.y + listRect.height + 2;
            var btnW = 22f;
            var addRect = new Rect(listRect.x + listRect.width - btnW * 2 - 4, btnY, btnW, EditorGUIUtility.singleLineHeight);
            var delRect = new Rect(addRect.xMax + 2, btnY, btnW, EditorGUIUtility.singleLineHeight);

            if (GUI.Button(addRect, "+"))
                arrayProp.arraySize++;
            if (GUI.Button(delRect, "-") && arrayProp.arraySize > 0)
                arrayProp.arraySize--;
        }

        /// <summary>
        /// 将 struct 的所有子字段一行绘制
        /// </summary>
        private static void DrawStructSingleLine(Rect rect, SerializedProperty element, SerializedProperty arrayProp)
        {
            var childNames = new List<string>();
            var iterator = element.Copy();
            var end = element.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (SerializedProperty.EqualContents(iterator, end)) break;
                childNames.Add(iterator.name);
            }

            if (childNames.Count == 0) return;

            // 解析元素类型以读取 InspectorName
            var elementType = GetArrayElementType(arrayProp);
            var childWidth = (rect.width - 4) / childNames.Count;
            var x = rect.x;

            for (var i = 0; i < childNames.Count; i++)
            {
                var child = element.FindPropertyRelative(childNames[i]);
                if (child == null) continue;

                var label = GetStructFieldLabel(elementType, childNames[i]);
                var labelW = EditorStyles.miniLabel.CalcSize(new GUIContent(label)).x + 2;

                var lblRect = new Rect(x, rect.y, labelW, rect.height);
                var fldRect = new Rect(x + labelW, rect.y, childWidth - 4 - labelW, rect.height);

                EditorGUI.LabelField(lblRect, label, EditorStyles.miniLabel);
                EditorGUI.PropertyField(fldRect, child, GUIContent.none);
                x += childWidth;
            }
        }

        private static Type GetArrayElementType(SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            if (so == null || so.targetObject == null) return null;
            var targetType = so.targetObject.GetType();
            var field = GetFieldInHierarchy(targetType, arrayProp.name);
            if (field == null) return null;
            var fieldType = field.FieldType;
            if (fieldType.IsArray) return fieldType.GetElementType();
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                return fieldType.GetGenericArguments()[0];
            return fieldType;
        }

        private static System.Reflection.FieldInfo GetFieldInHierarchy(Type type, string name)
        {
            while (type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject))
            {
                var f = type.GetField(name, System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.DeclaredOnly);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }

        private static string GetStructFieldLabel(Type elementType, string fieldName)
        {
            if (elementType != null)
            {
                var childField = elementType.GetField(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (childField != null)
                {
                    var attr = (InspectorNameAttribute)System.Attribute.GetCustomAttribute(
                        childField, typeof(InspectorNameAttribute));
                    if (attr != null) return attr.displayName;
                }
            }
            return ObjectNames.NicifyVariableName(fieldName);
        }

        private void DrawPropertiesContent(Rect rect, string[] propPaths)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);
            var contentRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);

            // 预先计算实际内容高度
            var totalHeight = 4f;
            foreach (var pp in propPaths)
            {
                var p = _serializedWeapon.FindProperty(pp);
                if (p != null)
                    totalHeight += EditorGUI.GetPropertyHeight(p, true) + 2;
            }

            _propertyScroll = GUI.BeginScrollView(contentRect, _propertyScroll,
                new Rect(0, 0, contentRect.width - 16, Mathf.Max(totalHeight, contentRect.height)));

            var y = 2f;
            foreach (var propPath in propPaths)
            {
                var prop = _serializedWeapon.FindProperty(propPath);
                if (prop == null) continue;

                var label = GetInspectorLabel(propPath);
                var propHeight = EditorGUI.GetPropertyHeight(prop, true);
                var propRect = new Rect(4, y, contentRect.width - 20, propHeight);
                EditorGUI.PropertyField(propRect, prop, new GUIContent(label), true);
                y += propHeight + 2;
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// 从类型继承链中查找字段/属性的 InspectorName，找不到则返回字段原名
        /// </summary>
        private static string GetInspectorLabel(string fieldName)
        {
            var type = typeof(WeaponPlayerController);
            while (type != null && type != typeof(MonoBehaviour))
            {
                // 先查 field
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    var attr = field.GetCustomAttributes(typeof(InspectorNameAttribute), true);
                    if (attr.Length > 0)
                        return ((InspectorNameAttribute)attr[0]).displayName;
                    return fieldName;
                }

                // 再查 property
                var prop = type.GetProperty(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                if (prop != null)
                {
                    var attr = prop.GetCustomAttributes(typeof(InspectorNameAttribute), true);
                    if (attr.Length > 0)
                        return ((InspectorNameAttribute)attr[0]).displayName;
                    return fieldName;
                }

                type = type.BaseType;
            }
            return fieldName;
        }

        // ========== 属性路径定义 ==========
        private static readonly string[] GeneralProps =
        {
            // [Foldout("点位和信息")]
            "WeaponRoot", "WeaponMuzzle", "WeaponMuzzle2",
            "PlayerIndex", "WeaponName", "WeaponType", "WeaponTypeEnum", "WeaponIcon",
            "Sight", "ScopeGo", "LHand", "RHand", "ShowRoot", "desc",
            // [Foldout("特效和动画")]
            "MuzzleFlashPrefab", "UnparentMuzzleFlash", "SFXRange",
            "ShootSfx", "UseContinuousShootSound",
            "ContinuousShootStartSfx", "ContinuousShootLoopSfx", "ContinuousShootEndSfx",
            "ReloadSfx", "ChangeWeaponSfx", "ChargeVfx", "WeaponAnimator",
        };

        private static readonly string[] WeaponProps =
        {
            // [Foldout("武器参数")]
            "cfg", "ShootType", "WeaponFlag", "AutomaticReleaseOnCharged", "Lockshape",
            "RecoilForce", "AimingHideCrosshair", "AimZoomRatio", "AimOffset", "playerAngle",
        };

        private static readonly string[] BulletProps =
        {
            // [Foldout("子弹参数")]
            "ProjectilePrefab", "BulletFlag", "Damages", "UseDamageIndex",
        };

    }
}
