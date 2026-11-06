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
    /// 改装树 / 显示属性 / 模组 / 底部 SO 检查器 / 增删选项。
    /// </summary>
    public partial class WeaponUpgradeEditorWindow
    {
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
                // 卡片点击即把对应 SO 同步选中到 Project 窗口，省掉手动去目录里翻
                SelectAssetInProject(so);
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
                        // 卡片点击即把对应 SO 同步选中到 Project 窗口
                        SelectAssetInProject(so);
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
                    SelectAssetInProject(so);
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
                so => so.type,
                // 不能直接传方法组：GetEffectSummary 多了个可选参数，委托只认无默认值的精确签名
                getExtra: so => GetEffectSummary(so),
                getExtraColor: GetEffectColor));
        }

        /// <summary>
        /// 把 modifys 压成一行数值摘要：Base/Extra 直出，Factor 转百分比（0.3 ⇒ +30%）。
        /// 只显示数值不显示属性名 —— 选择器一行放不下，属性名在下方 SO 详情里能看到。
        /// </summary>
        private static string GetEffectSummary(WeaponUpgradeData_SO so, int maxParts = 3)
        {
            if (so == null || so.modifys == null || so.modifys.Count == 0) return "";

            var parts = new List<string>(so.modifys.Count);
            for (var i = 0; i < so.modifys.Count; i++)
            {
                if (i >= maxParts)
                {
                    parts.Add("…");
                    break;
                }

                var item = so.modifys[i];
                // ModifierType 是 [Flags]，All 也含 Factor ⇒ 用位与判断而不是相等
                var isFactor = (item.modifier & ModifierType.Factor) != 0;
                var value = isFactor ? item.value * 100f : item.value;
                var sign = value >= 0f ? "+" : "";
                parts.Add($"{sign}{value:0.##}{(isFactor ? "%" : "")}");
            }

            return string.Join("  ", parts);
        }

        /// <summary>减益（负值）用暖色，增益用绿色，和右侧卡片配色保持一致</summary>
        private static Color GetEffectColor(WeaponUpgradeData_SO so)
        {
            var negative = so != null && so.modifys != null && so.modifys.Any(m => m.value < 0f);
            return negative ? new Color(0.95f, 0.68f, 0.42f) : new Color(0.6f, 0.9f, 0.6f);
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
    }
}
