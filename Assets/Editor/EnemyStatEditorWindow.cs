using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 敌人/怪物数值编辑器：读取 Assets/Resources/Prefabs/Enemy 下带 Actor 的预制体，
/// 左侧列表显示 Portrait + ShowName，右侧可编辑 名称/ID/头像/基础血量（直接写回预制体资产），
/// 并显示不同难度 x ExtraDifficulty(0~3) 的血量表格（参照 HealthEnemy.Awake 公式），
/// 以及每个武器每个 DamageData 的伤害表格（参照 FpsHelper.DiffDamageScale 公式，不含玩家人数项）。
/// 修改通过 Undo 记录，点击工具栏"保存资产"落盘。
/// </summary>
public class EnemyStatEditorWindow : EditorWindow
{
    [MenuItem("Tools/敌人/怪物编辑器")]
    private static void Open()
    {
        var wnd = GetWindow<EnemyStatEditorWindow>("怪物编辑器");
        wnd.minSize = new Vector2(900, 600);
    }

    /// <summary>敌人预制体目录</summary>
    private const string EnemyPrefabFolder = "Assets/Resources/Prefabs/Enemy";

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
            GUILayout.Label($"共 {_entries.Count} 个敌人预制体", EditorStyles.toolbarButton);
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

    /// <summary>扫描目录下所有带 Actor 的预制体</summary>
    private void ReloadPrefabs()
    {
        _entries.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
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
            EditorGUILayout.LabelField("敌人列表", EditorStyles.boldLabel);
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
                EditorGUILayout.HelpBox("请从左侧选择一个敌人", MessageType.Info);
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
                else
                {
                    EditorGUILayout.LabelField("最终血量 = 基础血量 × (1 + Extra[3] × 0.1) × 难度系数", EditorStyles.miniLabel);
                    // 血量公式参照 HealthEnemy.Awake
                    DrawScaleTable("Extra[3]", (rowExtra, colDiff) =>
                        Mathf.RoundToInt(_healthValue * (1 + rowExtra * 0.1f) * HealthDiffScale[colDiff]).ToString());
                }
            }

            EditorGUILayout.Space(6);

            // 武器
            DrawWeapons(entry.prefab);
        }
    }

    /// <summary>读取预制体上的所有武器并绘制伤害表格</summary>
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
        const float labelWidth = 70f;
        const float cellWidth = 86f;
        const float cellHeight = 20f;

        // 表头
        using (new EditorGUILayout.HorizontalScope())
        {
            Rect headerLabelRect = EditorGUILayout.GetControlRect(GUILayout.Width(labelWidth), GUILayout.Height(cellHeight));
            GUI.Label(headerLabelRect, rowLabelPrefix + "\\难度", EditorStyles.miniBoldLabel);
            for (int c = 0; c < 8; c++)
            {
                Rect cellRect = EditorGUILayout.GetControlRect(GUILayout.Width(cellWidth), GUILayout.Height(cellHeight));
                EditorGUI.DrawRect(cellRect, new Color(1, 1, 1, 0.08f));
                GUI.Label(cellRect, ((DifficultyEnum)c).ToString(), EditorStyles.miniBoldLabel);
            }
        }

        // 数据行
        for (int row = 0; row <= MaxExtraDiff; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect rowLabelRect = EditorGUILayout.GetControlRect(GUILayout.Width(labelWidth), GUILayout.Height(cellHeight));
                GUI.Label(rowLabelRect, $"= {row}", EditorStyles.miniLabel);

                for (int c = 0; c < 8; c++)
                {
                    Rect cellRect = EditorGUILayout.GetControlRect(GUILayout.Width(cellWidth), GUILayout.Height(cellHeight));
                    EditorGUI.DrawRect(cellRect, row % 2 == 0 ? new Color(1, 1, 1, 0.03f) : Color.clear);
                    GUI.Label(cellRect, getCell(row, c), EditorStyles.miniLabel);
                }
            }
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
