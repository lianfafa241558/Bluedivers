using System;
using System.Collections.Generic;
using System.Reflection;
using FPSGame.AI;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 单位数值编辑器：左侧按 敌人/其他/战备 三类浏览指定目录下带 Actor 的预制体，
/// 列表显示 Portrait + ShowName；右侧可编辑 名称/ID/头像/基础血量（直接写回预制体资产），
/// 并显示不同难度 x ExtraDifficulty(0~3) 的血量表格（参照 HealthEnemy.Awake 公式），
/// 以及每个武器每个 DamageData 的伤害表格（参照 FpsHelper.DiffDamageScale 公式，不含玩家人数项）。
/// 修改通过 Undo 记录，点击工具栏"保存资产"落盘。
/// </summary>
public class EnemyStatEditorWindow : EditorWindow
{
    [MenuItem("Tools/怪物编辑器")]
    private static void Open()
    {
        var wnd = GetWindow<EnemyStatEditorWindow>("怪物编辑器");
        wnd.minSize = new Vector2(900, 600);
    }

    /// <summary>左侧列表分类</summary>
    private enum ListCategory
    {
        Enemy = 0,
        Other = 1,
        Battle = 2,
    }

    /// <summary>列表分类定义：按钮文案（Name）+ 统计用短名（ShortName）+ 扫描目录（Folders）</summary>
    private sealed class ListCategoryDef
    {
        public readonly string Name;
        public readonly string ShortName;
        public readonly string[] Folders;

        public ListCategoryDef(string name, string shortName, string[] folders)
        {
            Name = name;
            ShortName = shortName;
            Folders = folders;
        }
    }

    /// <summary>敌人/其他/战备 三类列表目录（AssetDatabase.FindAssets 会在目录内递归查找）</summary>
    private static readonly ListCategoryDef[] ListCategories =
    {
        new ListCategoryDef("敌人列表", "敌人", new[] { "Assets/Resources/Prefabs/Enemy" }),
        new ListCategoryDef("其他列表", "其他", new[] { "Assets/Art/Prefabs", "Assets/Resources/Prefabs/GameEvent" }),
        new ListCategoryDef("战备列表", "战备", new[] { "Assets/Resources/Prefabs/Airdrop", "Assets/Resources/Prefabs/BattleBase" }),
    };

    /// <summary>分类切换按钮文案（与 ListCategories 顺序一致）</summary>
    private static readonly string[] ListCategoryNames =
    {
        ListCategories[0].Name,
        ListCategories[1].Name,
        ListCategories[2].Name,
    };

    /// <summary>ExtraDifficulty 行范围固定为 0~3</summary>
    private const int MaxExtraDiff = 3;

    /// <summary>难度系数表：与 DifficultyEnum 枚举顺序一一对应（血量参照 HealthEnemy.Awake，伤害参照 FpsHelper.DiffDamageScale）</summary>
    private static readonly float[] HealthDiffScale = { 0.5f, 0.6f, 0.7f, 0.85f, 1f, 1.15f, 1.2f, 1.35f };
    private static readonly float[] DamageDiffScale = { 0.6f, 0.75f, 0.9f, 1f, 1.5f, 2f, 2.5f, 3f };

    /// <summary>DamageData 的私有伤害字段（编辑器工具用反射读取，仅编辑器使用）</summary>
    private static readonly FieldInfo DamageDirectField =
        typeof(DamageData).GetField("DamageDirect", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo DamageExplosionField =
        typeof(DamageData).GetField("DamageExplosion", BindingFlags.Instance | BindingFlags.NonPublic);

    private class EnemyEntry
    {
        public GameObject prefab;
        public Actor actor;
        public string path;
    }

    /// <summary>当前列表分类</summary>
    private ListCategory _category = ListCategory.Enemy;

    /// <summary>当前分类的定义（目录/文案）</summary>
    private ListCategoryDef CurDef => ListCategories[(int)_category];

    private readonly List<EnemyEntry> _entries = new List<EnemyEntry>();
    private Vector2 _listScroll;
    private Vector2 _detailScroll;
    private int _selectedIndex = -1;

    private int _healthValue = 100;
    private int _healthLoadedFor = -1;

    private void OnEnable()
    {
        ReloadPrefabs();
    }

    private void OnGUI()
    {
        // 顶部工具栏
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("刷新列表", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                ReloadPrefabs();
            }
            if (GUILayout.Button("保存资产", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            GUILayout.Label($"共 {_entries.Count} 个{CurDef.ShortName}预制体", EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
        }

        HandleArrowKeyNavigation();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftList();
            DrawRightDetail();
        }
    }

    /// <summary>列表项高度（与 DrawListEntry 中 GetControlRect 高度一致）</summary>
    private const float ListItemHeight = 32f;

    /// <summary>左侧列表滚动区域的实际矩形（用于键盘导航时自动滚动）</summary>
    private Rect _listViewRect;

    /// <summary>处理 上/下箭头键 切换选中项（正在编辑文本框时不响应，避免抢占光标移动）</summary>
    private void HandleArrowKeyNavigation()
    {
        if (Event.current.type != EventType.KeyDown) return;
        if (Event.current.keyCode != KeyCode.DownArrow && Event.current.keyCode != KeyCode.UpArrow) return;
        // 文本框正在编辑时放行，让箭头键正常移动光标
        if (EditorGUIUtility.editingTextField) return;
        if (_entries.Count == 0) return;

        int dir = Event.current.keyCode == KeyCode.DownArrow ? 1 : -1;
        int newIndex = Mathf.Clamp(_selectedIndex + dir, 0, _entries.Count - 1);
        Event.current.Use();

        if (newIndex == _selectedIndex) return;
        _selectedIndex = newIndex;

        // 自动滚动让选中项可见
        float itemTop = _selectedIndex * ListItemHeight;
        float itemBottom = itemTop + ListItemHeight;
        if (itemTop < _listScroll.y)
        {
            _listScroll.y = itemTop;
        }
        else if (itemBottom > _listScroll.y + _listViewRect.height)
        {
            _listScroll.y = itemBottom - _listViewRect.height;
        }
        Repaint();
    }

    /// <summary>扫描当前分类目录下所有带 Actor 的预制体</summary>
    private void ReloadPrefabs()
    {
        _entries.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", CurDef.Folders);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Actor actor = prefab.GetComponent<Actor>();
            if (actor == null) actor = prefab.GetComponentInChildren<Actor>(true);
            if (actor == null) continue;

            _entries.Add(new EnemyEntry { prefab = prefab, actor = actor, path = path });
        }
        _entries.Sort((a, b) => string.Compare(a.actor.ShowName, b.actor.ShowName, StringComparison.Ordinal));
        _selectedIndex = Mathf.Clamp(_selectedIndex, -1, _entries.Count - 1);
    }

    // ---------------------------------------------------------------- 左侧列表

    private void DrawLeftList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(280)))
        {
            // 分类切换按钮：敌人列表 / 其他列表 / 战备列表
            ListCategory newCategory = (ListCategory)GUILayout.Toolbar((int)_category, ListCategoryNames, EditorStyles.toolbarButton);
            if (newCategory != _category)
            {
                _category = newCategory;
                _selectedIndex = -1;
                _healthLoadedFor = -1;
                _listScroll = Vector2.zero;
                ReloadPrefabs();
            }

            EditorGUILayout.Space(4);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _entries.Count; i++)
            {
                DrawListEntry(i);
            }
            EditorGUILayout.EndScrollView();
            // 滚动区域整体矩形（键盘导航自动滚动需要）
            _listViewRect = GUILayoutUtility.GetLastRect();
        }
    }

    private void DrawListEntry(int index)
    {
        EnemyEntry entry = _entries[index];
        Rect rowRect = EditorGUILayout.GetControlRect(GUILayout.Height(32));
        bool selected = index == _selectedIndex;

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rowRect, selected ? new Color(0.24f, 0.48f, 0.9f, 0.35f)
                                                 : (index % 2 == 0 ? new Color(1, 1, 1, 0.03f) : Color.clear));
        }

        // 头像
        Rect iconRect = new Rect(rowRect.x + 4, rowRect.y + 4, 24, 24);
        DrawSprite(iconRect, entry.actor.Portrait);

        // 名称
        Rect nameRect = new Rect(iconRect.xMax + 8, rowRect.y + 2, rowRect.width - 40, rowRect.height - 4);
        GUI.Label(nameRect, entry.actor.ShowName, EditorStyles.label);

        // 处理点击选中
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            _selectedIndex = index;
            Event.current.Use();
        }
    }

    /// <summary>绘制 Sprite 区域（按 textureRect 从图集中裁剪）</summary>
    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;
        Rect tr = sprite.textureRect;
        Rect coords = new Rect(
            tr.x / sprite.texture.width,
            tr.y / sprite.texture.height,
            tr.width / sprite.texture.width,
            tr.height / sprite.texture.height);
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, coords, true);
    }

    // ---------------------------------------------------------------- 右侧详情

    private void DrawRightDetail()
    {
        using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll, GUILayout.ExpandWidth(true)))
        {
            _detailScroll = scroll.scrollPosition;

            if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            {
                EditorGUILayout.HelpBox($"请从左侧选择一个{CurDef.ShortName}单位", MessageType.Info);
                return;
            }

            EnemyEntry entry = _entries[_selectedIndex];
            Actor actor = entry.actor;

            // 基本信息（可直接编辑并写回预制体）
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSprite(GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48)), actor.Portrait);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(actor.ShowName, EditorStyles.boldLabel);
                        // 先用 LabelField 占布局位，再取其矩形做点击检测（手动 GUI.Label 不占布局，会与名称重叠）
                        EditorGUILayout.LabelField("路径: " + entry.path, EditorStyles.miniLabel);
                        Rect pathRect = GUILayoutUtility.GetLastRect();
                        EditorGUIUtility.AddCursorRect(pathRect, MouseCursor.Link);
                        if (Event.current.type == EventType.MouseDown && pathRect.Contains(Event.current.mousePosition))
                        {
                            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(entry.path));
                            Event.current.Use();
                        }
                    }
                }

                EditorGUI.BeginChangeCheck();
                string showName = EditorGUILayout.TextField("名称 (ShowName)", actor.ShowName);
                string id = EditorGUILayout.TextField("ID", actor.Id);
                Sprite portrait = (Sprite)EditorGUILayout.ObjectField("头像 (Portrait)", actor.Portrait, typeof(Sprite), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(actor, "修改敌人信息");
                    actor.ShowName = showName;
                    actor.Id = id;
                    actor.Portrait = portrait;
                    EditorUtility.SetDirty(actor);
                }
            }

            EditorGUILayout.Space(6);

            // 特效组件
            DrawEnemyFx(entry.prefab);

            EditorGUILayout.Space(6);

            // 血量
            Health health = entry.prefab.GetComponent<Health>();
            if (health == null) health = entry.prefab.GetComponentInChildren<Health>(true);
            if (_healthLoadedFor != _selectedIndex && health != null)
            {
                _healthValue = health.MaxHealth;
                _healthLoadedFor = _selectedIndex;
            }

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                EditorGUILayout.LabelField("血量", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _healthValue = EditorGUILayout.IntField("基础血量 (Health.MaxHealth)", _healthValue);
                if (EditorGUI.EndChangeCheck() && health != null)
                {
                    // 写回预制体：MaxHealth 与 showHealth 保持一致（Health.Awake 中两者同步初始化）
                    Undo.RecordObject(health, "修改敌人血量");
                    health.MaxHealth = Mathf.Max(1, _healthValue);
                    health.showHealth = health.MaxHealth;
                    EditorUtility.SetDirty(health);
                }

                if (health == null)
                {
                    EditorGUILayout.HelpBox("该预制体没有 Health 组件", MessageType.Warning);
                }
                else if (health is HealthEnemy)
                {
                    // 仅 HealthEnemy 在 Awake 用 Extra[3] × 难度系数重算血量（参照 HealthEnemy.Awake）
                    EditorGUILayout.LabelField("最终血量 = 基础血量 × (1 + Extra[3] × 0.1) × 难度系数", EditorStyles.miniLabel);
                    DrawScaleTable("Extra[3]", (rowExtra, colDiff) =>
                        Mathf.RoundToInt(_healthValue * (1 + rowExtra * 0.1f) * HealthDiffScale[colDiff]).ToString());
                }
                else
                {
                    // 其它 Health 类型（Player/Other/SpecUnit 等）不参与难度缩放，仅维护基础值
                    EditorGUILayout.LabelField($"{health.GetType().Name}：不参与 Extra/难度 血量缩放，仅维护基础值", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(6);

            // 武器
            DrawWeapons(entry.prefab);
        }
    }

    /// <summary>
    /// 绘制选中敌人的 EnemyControllerFX（含子类）序列化字段，修改直接写回预制体
    /// </summary>
    private void DrawEnemyFx(GameObject prefab)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EnemyControllerFX fx = prefab.GetComponent<EnemyControllerFX>();
            if (fx == null)
            {
                fx = prefab.GetComponentInChildren<EnemyControllerFX>(true);
            }

            if (fx == null)
            {
                EditorGUILayout.LabelField("特效组件", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("该预制体没有 EnemyControllerFX 组件", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"特效组件 ({fx.GetType().Name})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("编辑该组件序列化字段，改动点\"保存资产\"落盘", EditorStyles.miniLabel);

            using (var so = new SerializedObject(fx))
            {
                so.Update();
                EditorGUI.BeginChangeCheck();

                SerializedProperty prop = so.GetIterator();
                if (prop.NextVisible(true))
                {
                    do
                    {
                        // 跳过组件自身的 m_Script 与开关项，仅展示配置字段
                        if (prop.name == "m_Script" || prop.name == "m_Enabled") continue;
                        EditorGUILayout.PropertyField(prop, true);
                    }
                    while (prop.NextVisible(false));
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(fx, "修改特效组件");
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(fx);
                }
            }
        }
    }

    /// <summary>
    /// 读取预制体上的所有武器并绘制伤害表格
    /// </summary>
    private void DrawWeapons(GameObject prefab)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("武器", EditorStyles.boldLabel);

            WeaponBaseController[] weapons = prefab.GetComponentsInChildren<WeaponBaseController>(true);
            if (weapons.Length == 0)
            {
                EditorGUILayout.HelpBox("该预制体没有武器", MessageType.Info);
                return;
            }

            for (int w = 0; w < weapons.Length; w++)
            {
                WeaponBaseController weapon = weapons[w];
                bool useDiffScale = weapon.BulletFlag.HasFlag(BulletFlag.EnemyIntensify);

                EditorGUILayout.Space(4);
                string flagTip = useDiffScale ? "（EnemyIntensify：随难度/ExtraDifficulty 增幅）"
                                              : "（不随难度增幅，数值恒定）";
                EditorGUILayout.LabelField($"[{weapon.gameObject.name}] {flagTip}", EditorStyles.boldLabel);

                if (weapon.Damages == null || weapon.Damages.Count == 0)
                {
                    EditorGUILayout.LabelField("  无 DamageData");
                    continue;
                }

                for (int d = 0; d < weapon.Damages.Count; d++)
                {
                    DamageData data = weapon.Damages[d];
                    if (data == null) continue;

                    float direct = DamageDirectField != null ? Convert.ToSingle(DamageDirectField.GetValue(data)) : 0f;
                    float explosion = DamageExplosionField != null ? Convert.ToSingle(DamageExplosionField.GetValue(data)) : 0f;

                    if (direct <= 0 && explosion <= 0)
                    {
                        EditorGUILayout.LabelField($"  DamageData[{d}]：无伤害数值");
                        continue;
                    }

                    EditorGUILayout.LabelField($"  DamageData[{d}]  直击 {FormatNum(direct)}" +
                        (explosion > 0 ? $"  爆炸 {FormatNum(explosion)}" : ""));

                    // 伤害公式参照 FpsHelper.DiffDamageScale（不含玩家人数项）：
                    // 伤害 = 基础伤害 × (1 + Extra[0] × 0.15) × 难度系数
                    DrawScaleTable("Extra[0]", (rowExtra, colDiff) =>
                    {
                        float scale = useDiffScale ? (1 + rowExtra * 0.15f) * DamageDiffScale[colDiff] : 1f;
                        string text = FormatNum(direct * scale);
                        if (explosion > 0) text += " / " + FormatNum(explosion * scale);
                        return text;
                    });
                }
            }
        }
    }

    /// <summary>
    /// 绘制 数值表：行 = ExtraDifficulty(0~3)，列 = 8 个难度（DifficultyEnum 顺序）
    /// </summary>
    /// <param name="rowLabelPrefix">行标签前缀</param>
    /// <param name="getCell">(行 ExtraDifficulty 值, 列难度枚举值) → 单元格文本</param>
    private void DrawScaleTable(string rowLabelPrefix, Func<int, int, string> getCell)
    {
        const int difficultyCount = 8;
        const float labelWidth = 56f;
        const float cellHeight = 20f;

        // 表头行：整行按实际布局宽均分，绝不产生超出可视区的内容 → 不会触发水平滚动条
        Rect headerRow = EditorGUILayout.GetControlRect(GUILayout.Height(cellHeight));
        GUI.Label(new Rect(headerRow.x, headerRow.y, labelWidth, headerRow.height),
            rowLabelPrefix + "\\难度", EditorStyles.miniBoldLabel);
        DrawScaleRowCells(headerRow, labelWidth, difficultyCount, (cellRect, col) =>
        {
            EditorGUI.DrawRect(cellRect, new Color(1, 1, 1, 0.08f));
            GUI.Label(cellRect, ((DifficultyEnum)col).ToString(), EditorStyles.miniBoldLabel);
        });

        // 数据行
        for (int row = 0; row <= MaxExtraDiff; row++)
        {
            int rowExtra = row;
            Rect rowRect = EditorGUILayout.GetControlRect(GUILayout.Height(cellHeight));
            GUI.Label(new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height), $"= {rowExtra}", EditorStyles.miniLabel);
            DrawScaleRowCells(rowRect, labelWidth, difficultyCount, (cellRect, col) =>
            {
                EditorGUI.DrawRect(cellRect, rowExtra % 2 == 0 ? new Color(1, 1, 1, 0.03f) : Color.clear);
                GUI.Label(cellRect, getCell(rowExtra, col), EditorStyles.miniLabel);
            });
        }
    }

    /// <summary>
    /// 将一行矩形去掉标签列后剩余宽度平均分成 count 列，逐列回调绘制
    /// </summary>
    private static void DrawScaleRowCells(Rect rowRect, float labelWidth, int count, Action<Rect, int> drawCell)
    {
        float cellWidth = Mathf.Max(1f, (rowRect.width - labelWidth) / count);
        for (int c = 0; c < count; c++)
        {
            Rect cellRect = new Rect(rowRect.x + labelWidth + c * cellWidth, rowRect.y, cellWidth, rowRect.height);
            drawCell(cellRect, c);
        }
    }

    /// <summary>数值格式化：最多 1 位小数</summary>
    private static string FormatNum(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) < 0.05f
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.#");
    }
}
