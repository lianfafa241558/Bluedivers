using System;
using System.Collections.Generic;
using System.Linq;

using Unity.FPS.Game;
using UnityEditorInternal;

using UnityEditor;
using UnityEngine;

namespace Unity.FPS.Game.Editor
{
    /// <summary>
    /// 武器升级/模组可视化编辑器窗口
    /// </summary>
    public class WeaponUpgradeEditorWindow : EditorWindow
    {
        // ========== 状态 ==========
        private Vector2 _weaponListScroll;
        private Vector2 _upgradeScroll;
        private Vector2 _moduleScroll;
        private Vector2 _inspectorScroll;
        private Vector2 _propertyScroll;
        private Vector2 _renameScroll;
        private Vector2 _uniqueScroll;
        private Vector2 _showAttrScroll;
        private ReorderableList _showAttrList;
        private string _showAttrListCacheKey;

        private string _searchFilter = "";
        private int _selectedWeaponIndex = -1;

        // 右侧面板标签
        private enum RightPanelTab { General, WeaponParams, BulletParams, UpgradeModules }
        private RightPanelTab _rightPanelTab;
        private int _selectedLevelIndex = -1;          // 选中的升级等级
        private int _selectedUpgradeIndex = -1;         // 选中的升级选项在该等级中的下标
        private int _selectedModuleIndex = -1;          // 选中的模组下标
        private string _selectedSOPath = "";            // 当前聚焦检查的 SO 路径（底部面板）

        // 缓存从 Resources/Weapons 加载的武器预设与控制器
        private List<WeaponAssetEntry> _weaponEntries = new();
        private WeaponPlayerController _currentController;
        private SerializedObject _serializedWeapon;

        // 缓存 SO 的 Inspector Editor（保持折叠状态）
        private UnityEditor.Editor _cachedSOEditor;
        private ScriptableObject _cachedSOTarget;


        // ========== 样式 ==========
        private GUIStyle _cardStyle;
        private GUIStyle _cardSelectedStyle;
        private GUIStyle _activeCardStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _titleStyle;
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
        }

        // ========== 刷新武器列表 ==========
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
            if (GUILayout.Button("刷新列表", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                RefreshWeaponList();
                _selectedWeaponIndex = -1;
                ClearSelection();
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

            // 列表
            var listRect = new Rect(rect.x + 4, searchRect.yMax + 4, rect.width - 8, rect.height - 28);

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
            }

            _serializedWeapon.ApplyModifiedProperties();
        }

        private void DrawTabButtons(Rect rect)
        {
            var tabs = new[] { "通用属性", "武器属性", "子弹属性", "改装模块" };
            var values = new[] { RightPanelTab.General, RightPanelTab.WeaponParams, RightPanelTab.BulletParams, RightPanelTab.UpgradeModules };
            var btnWidth = rect.width / 4f;

            for (var i = 0; i < 4; i++)
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

        // ========== 升级树可视化区块（竖向行=等级，横向=选项） ==========
        private void DrawUpgradeSection(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            // 标题栏
            var titleRect = new Rect(rect.x + 4, rect.y + 2, rect.width - 8, 20);
            EditorGUI.LabelField(titleRect, "■ 改装 (Upgrades)", _titleStyle);

            // 获取 Upgrade 属性
            var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
            if (upgradeProp == null) return;

            var upgradeCount = upgradeProp.arraySize;

            // 内容区
            var contentRect = new Rect(rect.x + 4, titleRect.yMax + 2, rect.width - 8, rect.height - 26);

            // 竖向滚动：行 = 等级
            const float rowHeight = 100f;
            //const float cardWidth = 155f;
            var totalHeight = upgradeCount * (rowHeight + 4) + 40;

            var viewRect = new Rect(0, 0, contentRect.width - 16, Mathf.Max(totalHeight, contentRect.height - 4));
            _upgradeScroll = GUI.BeginScrollView(contentRect, _upgradeScroll, viewRect);

            var y = 2f;

            for (var levelIdx = 0; levelIdx < upgradeCount; levelIdx++)
            {
                var rowRect = new Rect(0, y, viewRect.width, rowHeight);
                DrawUpgradeLevelRow(levelIdx, rowRect, upgradeProp);
                y += rowHeight + 4;
            }

            // "添加等级" 按钮
            var addBtnRect = new Rect(4, y, 120, 26);
            if (GUI.Button(addBtnRect, "+ 添加等级"))
            {
                AddUpgradeLevel();
            }

            GUI.EndScrollView();
        }

        private void DrawUpgradeLevelRow(int levelIdx, Rect rowRect,
            SerializedProperty upgradeProp)
        {
            if (upgradeProp == null || levelIdx >= upgradeProp.arraySize) return;

            var levelElem = upgradeProp.GetArrayElementAtIndex(levelIdx);
            var keyProp = levelElem.FindPropertyRelative("Key");
            var valueProp = levelElem.FindPropertyRelative("Value");

            if (keyProp == null || valueProp == null) return;

            // 左侧：等级标签 + 删除按钮
            var labelRect = new Rect(rowRect.x, rowRect.y, 70, 20);
            var levelNum = keyProp.intValue;
            EditorGUI.LabelField(labelRect, $"Level {levelNum}", _headerStyle);

            var delLevelRect = new Rect(rowRect.x + 72, rowRect.y, 18, 18);
            if (GUI.Button(delLevelRect, "×"))
            {
                if (EditorUtility.DisplayDialog("删除等级",
                        $"确认删除 Level {levelNum} 及其所有升级选项？", "删除", "取消"))
                {
                    upgradeProp.DeleteArrayElementAtIndex(levelIdx);
                    _serializedWeapon.ApplyModifiedProperties();
                    Repaint();
                    return;
                }
            }

            // 横向排列：选项卡片（最多3个）
            var optionCount = valueProp.arraySize;
            const float cardWidth = 155f;
            const float cardHeight = 72f;
            const int maxPerLevel = 3;
            var cardStartX = rowRect.x + 4;
            var cardY = rowRect.y + 24;

            for (var optIdx = 0; optIdx < optionCount; optIdx++)
            {
                var optionElem = valueProp.GetArrayElementAtIndex(optIdx);
                var soRef = optionElem.objectReferenceValue as WeaponUpgradeData_SO;

                var cardX = cardStartX + optIdx * (cardWidth + 6);
                var cardRect = new Rect(cardX, cardY, cardWidth, cardHeight);

                var isSelected = _selectedLevelIndex == levelIdx && _selectedUpgradeIndex == optIdx;
                DrawUpgradeCard(cardRect, soRef, levelIdx, optIdx, isSelected);
            }

            // 添加按钮（未满3个时显示）
            if (optionCount < maxPerLevel)
            {
                var addX = cardStartX + optionCount * (cardWidth + 6);
                var addRect = new Rect(addX, cardY, cardWidth, 28);
                if (GUI.Button(addRect, "+ 添加选项"))
                {
                    ShowUpgradePicker(levelIdx);
                }

                var newRect = new Rect(addX, cardY + 32, cardWidth, 24);
                if (GUI.Button(newRect, "新建选项..."))
                {
                    CreateNewUpgradeOption(levelIdx);
                }
            }

            // 拖拽区域
            var dragRect = new Rect(rowRect.x, cardY, rowRect.width, cardHeight);
            HandleDragDropOnRow(dragRect, levelIdx);
        }

        private void HandleDragDropOnRow(Rect dropRect, int levelIdx)
        {
            var evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                {
                    var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
                    var currentCount = 0;
                    if (upgradeProp != null && levelIdx < upgradeProp.arraySize)
                    {
                        var levelElem = upgradeProp.GetArrayElementAtIndex(levelIdx);
                        var vp = levelElem.FindPropertyRelative("Value");
                        currentCount = vp != null ? vp.arraySize : 0;
                    }
                    var wouldFit = currentCount < 3;
                    var hasValid = wouldFit && DragAndDrop.objectReferences.OfType<WeaponUpgradeData_SO>().Any();
                    DragAndDrop.visualMode = hasValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    evt.Use();
                    break;
                }
                case EventType.DragPerform:
                {
                    var soList = DragAndDrop.objectReferences.OfType<WeaponUpgradeData_SO>().ToList();
                    if (soList.Count > 0)
                    {
                        var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
                        if (upgradeProp != null && levelIdx < upgradeProp.arraySize)
                        {
                            var levelElem = upgradeProp.GetArrayElementAtIndex(levelIdx);
                            var valueProp = levelElem.FindPropertyRelative("Value");
                            if (valueProp == null) break;
                            var remaining = 3 - valueProp.arraySize;
                            for (var i = 0; i < soList.Count && remaining > 0; i++, remaining--)
                            {
                                var idx = valueProp.arraySize;
                                valueProp.arraySize++;
                                valueProp.GetArrayElementAtIndex(idx).objectReferenceValue = soList[i];
                            }
                            _serializedWeapon.ApplyModifiedProperties();
                        }
                        DragAndDrop.AcceptDrag();
                        evt.Use();
                        Repaint();
                    }
                    break;
                }
            }
        }

        private void DrawUpgradeCard(Rect rect, WeaponUpgradeData_SO so, int levelIdx, int optIdx, bool isSelected)
        {
            // 背景
            var bgStyle = isSelected ? _cardSelectedStyle : _cardStyle;
            if (Event.current.type == EventType.Repaint)
            {
                bgStyle.Draw(rect, false, false, false, false);
            }

            var iconRect = new Rect(rect.x + 4, rect.y + 4, 40, 40);

            if (so != null)
            {
                // 图标
                if (so.icon != null)
                {
                    GUI.DrawTexture(iconRect, so.icon.texture, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(iconRect, new Color(0.3f, 0.3f, 0.3f));
                    GUI.Label(iconRect, "无图", EditorStyles.centeredGreyMiniLabel);
                }

                // 名称和类型
                var nameRect = new Rect(rect.x + 48, rect.y + 4, rect.width - 72, 18);
                EditorGUI.LabelField(nameRect, so.name, EditorStyles.boldLabel);

                var typeRect = new Rect(rect.x + 48, rect.y + 24, rect.width - 72, 16);
                EditorGUI.LabelField(typeRect, so.type, EditorStyles.miniLabel);

                // 描述（截断）
                var descRect = new Rect(rect.x + 48, rect.y + 42, rect.width - 52, 14);
                if (!string.IsNullOrEmpty(so.desc))
                {
                    var desc = so.desc.Length > 20 ? so.desc.Substring(0, 20) + "..." : so.desc;
                    EditorGUI.LabelField(descRect, desc, EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(iconRect, "空", EditorStyles.centeredGreyMiniLabel);
                var emptyRect = new Rect(rect.x + 48, rect.y + 12, rect.width - 56, 20);
                EditorGUI.LabelField(emptyRect, "拖入或点击选择", EditorStyles.miniLabel);
            }

            // 删除按钮
            var delRect = new Rect(rect.xMax - 20, rect.y + 2, 16, 16);
            GUI.contentColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUI.Button(delRect, "×", EditorStyles.miniLabel))
            {
                RemoveUpgradeOption(levelIdx, optIdx);
            }
            GUI.contentColor = Color.white;

            // 点击选择
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedLevelIndex = levelIdx;
                _selectedUpgradeIndex = optIdx;
                _selectedModuleIndex = -1;
                _selectedSOPath = so != null ? AssetDatabase.GetAssetPath(so) : "";
                ResetFocus();
                Event.current.Use();
                Repaint();
            }
        }

        // ========== 显示的属性区块 ==========
        private void DrawShowAttrSection(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            var titleRect = new Rect(rect.x + 4, rect.y + 2, rect.width - 8, 20);
            EditorGUI.LabelField(titleRect, "■ 显示的属性", _titleStyle);

            var contentRect = new Rect(rect.x + 4, titleRect.yMax + 2, rect.width - 8, rect.height - 24);
            var showAttrProp = _serializedWeapon.FindProperty("showAttr");
            if (showAttrProp == null) return;

            // 创建或更新 ReorderableList（用缓存 key 判断是否需要重建）
            var cacheKey = showAttrProp.serializedObject.targetObject.GetInstanceID() + "_" + showAttrProp.propertyPath;
            if (_showAttrList == null || _showAttrListCacheKey != cacheKey)
            {
                _showAttrListCacheKey = cacheKey;
                _showAttrList = new ReorderableList(showAttrProp.serializedObject, showAttrProp,
                    true, false, true, true)
                {
                    draggable = true,
                    elementHeight = EditorGUIUtility.singleLineHeight,
                    drawHeaderCallback = r => { /* 已有标题，留空 */ },
                    drawElementCallback = (r, index, active, focused) =>
                    {
                        if (index >= showAttrProp.arraySize) return;
                        var elem = showAttrProp.GetArrayElementAtIndex(index);
                        var rawValue = elem.intValue;
                        var attrType = (WeaponAttrType)rawValue;
                        var displayName = GetWeaponAttrDisplayName(attrType);
                        EditorGUI.LabelField(r, displayName);
                    },
                    onAddDropdownCallback = (r, list) =>
                    {
                        var menu = new GenericMenu();
                        foreach (WeaponAttrType val in System.Enum.GetValues(typeof(WeaponAttrType)))
                        {
                            if (val == WeaponAttrType.Special) continue;
                            var name = GetWeaponAttrDisplayName(val);
                            menu.AddItem(new GUIContent(name), false, () =>
                            {
                                var idx = showAttrProp.arraySize;
                                showAttrProp.arraySize++;
                                showAttrProp.GetArrayElementAtIndex(idx).intValue = (int)val;
                                _serializedWeapon.ApplyModifiedProperties();
                            });
                        }
                        menu.ShowAsContext();
                    },
                };
            }

            GUILayout.BeginArea(contentRect);
            _showAttrScroll = EditorGUILayout.BeginScrollView(_showAttrScroll);
            _showAttrList.DoLayoutList();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string GetWeaponAttrDisplayName(WeaponAttrType type)
        {
            var field = typeof(WeaponAttrType).GetField(type.ToString());
            if (field != null)
            {
                var attr = System.Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute))
                    as InspectorNameAttribute;
                if (attr != null) return attr.displayName;
            }
            return type.ToString();
        }

        // ========== 模组可视化区块 ==========
        private void DrawModuleSection(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            // 标题栏
            var titleRect = new Rect(rect.x + 4, rect.y + 2, rect.width - 8, 20);
            EditorGUI.LabelField(titleRect, "■ 模组 (Modules)", _titleStyle);

            // 内容区
            var contentRect = new Rect(rect.x + 4, titleRect.yMax + 2, rect.width - 8, rect.height - 26);

            var modulesProp = _serializedWeapon.FindProperty("Modules");
            var activeModuleProp = _serializedWeapon.FindProperty("ActiveModule");
            if (modulesProp == null || activeModuleProp == null) return;

            var moduleCount = modulesProp.arraySize;
            const float cardWidth = 160f;
            const float cardHeight = 80f;
            var totalWidth = (moduleCount + 1) * (cardWidth + 6);

            var viewRect = new Rect(0, 0, Mathf.Max(totalWidth, contentRect.width - 16), contentRect.height - 4);
            _moduleScroll = GUI.BeginScrollView(contentRect, _moduleScroll, viewRect);

            for (var i = 0; i < moduleCount; i++)
            {
                var cardX = i * (cardWidth + 6) + 4;
                var cardRect = new Rect(cardX, 4, cardWidth, cardHeight);

                var moduleElem = modulesProp.GetArrayElementAtIndex(i);
                var so = moduleElem.objectReferenceValue as WeaponModuleData_SO;
                var isActive = so != null && so == (activeModuleProp.objectReferenceValue as WeaponModuleData_SO);
                var isSelected = _selectedModuleIndex == i;

                DrawModuleCard(cardRect, so, i, isActive, isSelected);
            }

            // 添加模组按钮（打开自定义选择器）
            var addX2 = moduleCount * (cardWidth + 6) + 8;
            var addRect = new Rect(addX2, 12, cardWidth - 16, 26);
            if (GUI.Button(addRect, "+ 添加模组"))
            {
                ShowModulePicker();
            }

            // 新建模组按钮
            var newRect = new Rect(addX2, 42, cardWidth - 16, 22);
            if (GUI.Button(newRect, "新建模组..."))
            {
                CreateNewModule();
            }

            GUI.EndScrollView();

            // 拖拽区域
            var dropFullRect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentRect.height);
            HandleModuleDragDrop(dropFullRect, modulesProp);
        }

        private void DrawModuleCard(Rect rect, WeaponModuleData_SO so, int index, bool isActive, bool isSelected)
        {
            // 选择合适的样式
            var bgStyle = isActive ? _activeCardStyle : (isSelected ? _cardSelectedStyle : _cardStyle);

            if (Event.current.type == EventType.Repaint)
            {
                bgStyle.Draw(rect, false, false, false, false);
            }

            // 激活标记
            if (isActive)
            {
                var activeRect = new Rect(rect.x + 2, rect.y + 2, 16, 16);
                GUI.Label(activeRect, "★", new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.yellow }, fontSize = 14 });
            }

            // 图标 + 边框
            var frameRect = new Rect(rect.x + 1, rect.y + 14, 52, 52);
            var iconRect = new Rect(frameRect.x + 9, frameRect.y + 9, 34, 34);

            if (so != null)
            {
                // 边框图片（根据模块类型染色）
                var framePath = $"Assets/Resources/Images/Icon/Frame_Module{so.type}.png";
                var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                if (frameSprite != null)
                {
                    GUI.DrawTexture(frameRect, frameSprite.texture, ScaleMode.ScaleToFit, true, 0, so.color, 0, 0);
                }

                // 图标
                if (so.icon != null)
                {
                    GUI.DrawTexture(iconRect, so.icon.texture, ScaleMode.ScaleToFit);
                }
                else if (frameSprite == null)
                {
                    EditorGUI.DrawRect(frameRect, so.color);
                    GUI.Label(frameRect, "图标", EditorStyles.centeredGreyMiniLabel);
                }

                // 名称
                var nameRect = new Rect(rect.x + 56, rect.y + 18, rect.width - 62, 18);
                EditorGUI.LabelField(nameRect, so.name, EditorStyles.boldLabel);

                // 类型（带颜色）
                var typeRect = new Rect(rect.x + 56, rect.y + 38, rect.width - 62, 16);
                var oldColor = GUI.contentColor;
                GUI.contentColor = so.color;
                EditorGUI.LabelField(typeRect, so.typeName, EditorStyles.miniBoldLabel);
                GUI.contentColor = oldColor;
            }
            else
            {
                EditorGUI.DrawRect(frameRect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(frameRect, "空", EditorStyles.centeredGreyMiniLabel);
                var emptyRect = new Rect(rect.x + 56, rect.y + 28, rect.width - 62, 20);
                EditorGUI.LabelField(emptyRect, "拖入模组", EditorStyles.miniLabel);
            }

            // 删除按钮
            var delRect = new Rect(rect.xMax - 20, rect.y + 2, 16, 16);
            GUI.contentColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUI.Button(delRect, "×", EditorStyles.miniLabel))
            {
                RemoveModule(index);
            }
            GUI.contentColor = Color.white;

            // 点击操作
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                // 左键选择 / 右键菜单
                if (Event.current.button == 0)
                {
                    // 如果按住 Ctrl，设为激活
                    if (Event.current.control)
                    {
                        SetActiveModule(index);
                    }
                    else
                    {
                        _selectedModuleIndex = index;
                        _selectedLevelIndex = -1;
                        _selectedUpgradeIndex = -1;
                        _selectedSOPath = so != null ? AssetDatabase.GetAssetPath(so) : "";
                        ResetFocus();
                    }
                    Event.current.Use();
                    Repaint();
                }
                else if (Event.current.button == 1)
                {
                    ShowModuleContextMenu(index, so);
                    Event.current.Use();
                }
            }
        }

        private void HandleModuleDragDrop(Rect dropRect, SerializedProperty modulesProp)
        {
            var evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                {
                    var hasValid = DragAndDrop.objectReferences.OfType<WeaponModuleData_SO>().Any();
                    DragAndDrop.visualMode = hasValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    evt.Use();
                    break;
                }
                case EventType.DragPerform:
                {
                    var soList = DragAndDrop.objectReferences.OfType<WeaponModuleData_SO>().ToList();
                    if (soList.Count > 0)
                    {
                        foreach (var so in soList)
                        {
                            var idx = modulesProp.arraySize;
                            modulesProp.arraySize++;
                            modulesProp.GetArrayElementAtIndex(idx).objectReferenceValue = so;
                        }
                        _serializedWeapon.ApplyModifiedProperties();
                        DragAndDrop.AcceptDrag();
                        evt.Use();
                        Repaint();
                    }
                    break;
                }
            }
        }

        private void SetActiveModule(int index)
        {
            var modulesProp = _serializedWeapon.FindProperty("Modules");
            var activeProp = _serializedWeapon.FindProperty("ActiveModule");
            if (index >= 0 && index < modulesProp.arraySize)
            {
                var so = modulesProp.GetArrayElementAtIndex(index).objectReferenceValue;
                activeProp.objectReferenceValue = so;
                _serializedWeapon.ApplyModifiedProperties();
                _selectedModuleIndex = index;
                ResetFocus();
            }
        }

        private void RemoveModule(int index)
        {
            var modulesProp = _serializedWeapon.FindProperty("Modules");
            var activeProp = _serializedWeapon.FindProperty("ActiveModule");

            if (index < 0 || index >= modulesProp.arraySize) return;

            var removedSo = modulesProp.GetArrayElementAtIndex(index).objectReferenceValue;
            var removedName = removedSo != null ? ((WeaponModuleData_SO)removedSo).name : "空";

            if (!EditorUtility.DisplayDialog("删除模组", $"确认删除模组 \"{removedName}\"？", "删除", "取消"))
                return;

            // 如果删除的是激活模组，清除 ActiveModule
            if (removedSo == activeProp.objectReferenceValue)
            {
                activeProp.objectReferenceValue = null;
            }

            modulesProp.DeleteArrayElementAtIndex(index);
            _serializedWeapon.ApplyModifiedProperties();

            if (_selectedModuleIndex == index)
            {
                _selectedModuleIndex = -1;
                ResetFocus();
            }
            if (_selectedModuleIndex > index) _selectedModuleIndex--;
            Repaint();
        }

        private void ShowModuleContextMenu(int index, WeaponModuleData_SO so)
        {
            _selectedModuleIndex = index;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("设为激活模组"), false, () => SetActiveModule(index));
            menu.AddSeparator("");
            if (so != null)
            {
                menu.AddItem(new GUIContent("在 Project 中定位"), false, () =>
                {
                    EditorGUIUtility.PingObject(so);
                });
                menu.AddItem(new GUIContent("在 Inspector 中查看"), false, () =>
                {
                    Selection.activeObject = so;
                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("删除此模组"), false, () => RemoveModule(index));
            menu.ShowAsContext();
        }

        // ========== 底部 SO 检查器 ==========
        private void DrawInspectorSection(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            var titleRect = new Rect(rect.x + 4, rect.y + 2, rect.width - 8, 20);
            EditorGUI.LabelField(titleRect, "■ SO 详情", _titleStyle);

            var contentRect = new Rect(rect.x + 4, titleRect.yMax + 2, rect.width - 8, rect.height - 24);

            ScriptableObject selectedSO = GetSelectedSO();
            if (selectedSO != null)
            {
                _selectedSOPath = AssetDatabase.GetAssetPath(selectedSO);
            }

            // 选中项变化时重建缓存的 editor
            if (_cachedSOTarget != selectedSO)
            {
                DestroyCachedSOEditor();
                _cachedSOTarget = selectedSO;
                if (selectedSO != null)
                {
                    _cachedSOEditor = UnityEditor.Editor.CreateEditor(selectedSO);
                }
            }

            if (selectedSO == null || _cachedSOEditor == null)
            {
                GUI.Label(new Rect(contentRect.x, contentRect.y + 20, contentRect.width, 24),
                    "点击升级选项或模组卡片以查看详情", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            GUILayout.BeginArea(contentRect);
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

            // 图标和名称横排
            using (new EditorGUILayout.HorizontalScope())
            {
                var iconProp = _cachedSOEditor.serializedObject.FindProperty("icon");
                if (iconProp != null && iconProp.objectReferenceValue != null)
                {
                    var tex = ((Sprite)iconProp.objectReferenceValue).texture;
                    GUILayout.Label(tex, GUILayout.Width(48), GUILayout.Height(48));
                }
                else
                {
                    GUILayout.Box("", GUILayout.Width(48), GUILayout.Height(48));
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    var nameProp = _cachedSOEditor.serializedObject.FindProperty("name");
                    if (nameProp != null)
                    {
                        EditorGUILayout.LabelField(nameProp.stringValue, EditorStyles.boldLabel);
                    }

                    if (selectedSO is WeaponUpgradeData_SO upgradeSO)
                    {
                        EditorGUILayout.LabelField(upgradeSO.type, EditorStyles.miniBoldLabel);
                    }
                    else if (selectedSO is WeaponModuleData_SO moduleSO)
                    {
                        var oldColor = GUI.contentColor;
                        GUI.contentColor = moduleSO.color;
                        EditorGUILayout.LabelField(moduleSO.typeName, EditorStyles.miniBoldLabel);
                        GUI.contentColor = oldColor;
                    }

                    EditorGUILayout.LabelField(_selectedSOPath, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(4);

            // 使用缓存的 Editor 绘制完整 Inspector（保持折叠状态）
            _cachedSOEditor.serializedObject.Update();
            _cachedSOEditor.OnInspectorGUI();
            if (_cachedSOEditor.serializedObject.hasModifiedProperties)
            {
                _cachedSOEditor.serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DestroyCachedSOEditor()
        {
            if (_cachedSOEditor != null)
            {
                DestroyImmediate(_cachedSOEditor);
                _cachedSOEditor = null;
            }
            _cachedSOTarget = null;
        }

        private ScriptableObject GetSelectedSO()
        {
            if (_currentController == null) return null;

            // 优先返回选中的升级选项
            if (_selectedLevelIndex >= 0 && _selectedUpgradeIndex >= 0)
            {
                var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
                if (upgradeProp != null && _selectedLevelIndex < upgradeProp.arraySize)
                {
                    var levelElem = upgradeProp.GetArrayElementAtIndex(_selectedLevelIndex);
                    var valueProp = levelElem.FindPropertyRelative("Value");
                    if (valueProp != null && _selectedUpgradeIndex < valueProp.arraySize)
                    {
                        return valueProp.GetArrayElementAtIndex(_selectedUpgradeIndex).objectReferenceValue
                            as ScriptableObject;
                    }
                }
            }

            // 其次返回选中的模组
            if (_selectedModuleIndex >= 0)
            {
                var modulesProp = _serializedWeapon.FindProperty("Modules");
                if (_selectedModuleIndex < modulesProp.arraySize)
                {
                    return modulesProp.GetArrayElementAtIndex(_selectedModuleIndex).objectReferenceValue
                        as ScriptableObject;
                }
            }

            return null;
        }

        // ========== 升级操作 ==========
        private void AddUpgradeLevel()
        {
            var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
            if (upgradeProp == null) return;

            var newLevel = upgradeProp.arraySize;

            upgradeProp.arraySize++;
            var newElem = upgradeProp.GetArrayElementAtIndex(newLevel);
            var keyProp = newElem.FindPropertyRelative("Key");
            if (keyProp == null) return;
            keyProp.intValue = newLevel;
            var valueProp = newElem.FindPropertyRelative("Value");
            if (valueProp == null) return;
            valueProp.arraySize = 0;

            _serializedWeapon.ApplyModifiedProperties();
            Repaint();
        }

        private void RemoveUpgradeOption(int levelIdx, int optIdx)
        {
            var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
            if (upgradeProp == null || levelIdx >= upgradeProp.arraySize) return;

            var levelElem = upgradeProp.GetArrayElementAtIndex(levelIdx);
            var valueProp = levelElem.FindPropertyRelative("Value");
            if (valueProp == null || optIdx >= valueProp.arraySize) return;

            valueProp.DeleteArrayElementAtIndex(optIdx);
            _serializedWeapon.ApplyModifiedProperties();

            if (_selectedLevelIndex == levelIdx && _selectedUpgradeIndex == optIdx)
            {
                _selectedLevelIndex = -1;
                _selectedUpgradeIndex = -1;
                ResetFocus();
            }
            Repaint();
        }

        private void CreateNewUpgradeOption(int levelIdx)
        {
            var defaultPath = "Assets/Resources/GameData/WeaponUpgrade/";
            var path = EditorUtility.SaveFilePanelInProject("新建武器升级数据", "WUD_New", "asset",
                "选择保存路径", defaultPath);
            if (string.IsNullOrEmpty(path)) return;

            var so = CreateInstance<WeaponUpgradeData_SO>();
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();

            // 添加到对应等级
            var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
            if (upgradeProp != null && levelIdx < upgradeProp.arraySize)
            {
                var levelElem = upgradeProp.GetArrayElementAtIndex(levelIdx);
                var valueProp = levelElem.FindPropertyRelative("Value");
                if (valueProp == null) return;
                var idx = valueProp.arraySize;
                valueProp.arraySize++;
                valueProp.GetArrayElementAtIndex(idx).objectReferenceValue = so;
                _serializedWeapon.ApplyModifiedProperties();
            }

            EditorGUIUtility.PingObject(so);
            Repaint();
        }

        private void CreateNewModule()
        {
            var defaultPath = "Assets/Resources/GameData/WeaponModule/";
            var path = EditorUtility.SaveFilePanelInProject("新建武器模组数据", "WMD_New", "asset",
                "选择保存路径", defaultPath);
            if (string.IsNullOrEmpty(path)) return;

            var so = CreateInstance<WeaponModuleData_SO>();
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();

            var modulesProp = _serializedWeapon.FindProperty("Modules");
            var idx = modulesProp.arraySize;
            modulesProp.arraySize++;
            modulesProp.GetArrayElementAtIndex(idx).objectReferenceValue = so;
            _serializedWeapon.ApplyModifiedProperties();

            EditorGUIUtility.PingObject(so);
            Repaint();
        }

        // ========== 自定义选择器 ==========
        private void ShowUpgradePicker(int levelIdx)
        {
            var allSO = CollectAllSO<WeaponUpgradeData_SO>("t:WeaponUpgradeData_SO");
            var activatorRect = GUILayoutUtility.GetLastRect();
            PopupWindow.Show(activatorRect, new SOPickerPopup<WeaponUpgradeData_SO>(
                allSO,
                so => { AddUpgradeSOToLevel(levelIdx, so); },
                so => so.icon,
                so => so.name,
                so => so.type));
        }

        private void ShowModulePicker()
        {
            var allSO = CollectAllSO<WeaponModuleData_SO>("t:WeaponModuleData_SO");
            var activatorRect = GUILayoutUtility.GetLastRect();
            PopupWindow.Show(activatorRect, new SOPickerPopup<WeaponModuleData_SO>(
                allSO,
                so => { AddModuleSO(so); },
                so => so.icon,
                so => so.name,
                so => so.typeName,
                so => so.color,
                so => ($"Assets/Resources/Images/Icon/Frame_Module{so.type}.png", so.color)));
        }

        private static List<T> CollectAllSO<T>(string filter) where T : ScriptableObject
        {
            var list = new List<T>();
            var guids = AssetDatabase.FindAssets(filter);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<T>(path);
                if (so != null) list.Add(so);
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            return list;
        }

        private void AddUpgradeSOToLevel(int levelIdx, WeaponUpgradeData_SO so)
        {
            if (so == null) return;
            var upgradeProp = _serializedWeapon.FindProperty("Upgrade");
            if (upgradeProp == null || levelIdx >= upgradeProp.arraySize) return;
            var levelElem = upgradeProp.GetArrayElementAtIndex(levelIdx);
            var valueProp = levelElem.FindPropertyRelative("Value");
            if (valueProp == null || valueProp.arraySize >= 3) return;
            var idx = valueProp.arraySize;
            valueProp.arraySize++;
            valueProp.GetArrayElementAtIndex(idx).objectReferenceValue = so;
            _serializedWeapon.ApplyModifiedProperties();
            Repaint();
        }

        private void AddModuleSO(WeaponModuleData_SO so)
        {
            if (so == null) return;
            var modulesProp = _serializedWeapon.FindProperty("Modules");
            var idx = modulesProp.arraySize;
            modulesProp.arraySize++;
            modulesProp.GetArrayElementAtIndex(idx).objectReferenceValue = so;
            _serializedWeapon.ApplyModifiedProperties();
            Repaint();
        }

        // ========== 样式构建 ==========
        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _cardStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(1, 1, 1, 1),
                normal = { background = MakeTex(1, 1, new Color(0.22f, 0.22f, 0.24f, 0.9f)) },
            };

            _cardSelectedStyle = new GUIStyle(_cardStyle)
            {
                normal = { background = MakeTex(1, 1, new Color(0.25f, 0.45f, 0.75f, 0.8f)) },
            };

            _activeCardStyle = new GUIStyle(_cardStyle)
            {
                normal = { background = MakeTex(1, 1, new Color(0.3f, 0.55f, 0.28f, 0.8f)) },
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.75f, 0.85f) },
            };

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.7f) },
            };
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ========== 内部数据结构 ==========
        private class WeaponAssetEntry
        {
            public GameObject Prefab;
            public WeaponPlayerController Controller;
            public string DisplayName;
            public Sprite Icon;
            public string TypeName;
            public int TypeEnum;
        }
    }
}
