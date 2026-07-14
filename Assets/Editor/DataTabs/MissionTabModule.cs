using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class MissionTabModule : DataTabModule<MissionData_SO>
{
    private GUIStyle _mainTaskStyle;
    private GUIStyle _subTaskStyle;

    public MissionTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Mission;
    public override string DisplayName => "任务数据";

    protected override string RootPath => "Assets/Resources/GameData/Mission";
    protected override string TypeName => "t:MissionData_SO";
    protected override bool IncludeSubDirs => true;
    protected override Comparison<MissionData_SO> SortComparison => (a, b) => ((int)a.type).CompareTo((int)b.type);

    protected override void OnItemLoaded(MissionData_SO data)
        => LabelCache[data] = DataEditorWindow.GetEnumLabel(data.type);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个任务数据";
    protected override string GetSelectedTitle() => HasSelection ? LabelCache[Selected] : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(IsMissionMain(Selected) ? "(主线任务)" : "(支线任务)", EditorStyles.miniLabel);
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<MissionData_SO> FilterItems(List<MissionData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => LabelCache[i].Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(MissionData_SO data, bool isSelected)
    {
        EnsureStyles();
        bool isMain = IsMissionMain(data);
        string label = LabelCache[data];

        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.sprite != null)
        {
            Color tint = Color.white;
            if (isMain && data is MissionMainData_SO mainData)
                tint = mainData.color;
            GUI.DrawTexture(iconRect, data.sprite.texture, ScaleMode.ScaleToFit, true, 0, tint, 0, 0);
        }
        else
        {
            GUI.Box(iconRect, "");
        }

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(label, isMain ? _mainTaskStyle : _subTaskStyle);
            GUILayout.Label(string.IsNullOrEmpty(data.desc) ? data.name : data.desc, new GUIStyle(GUI.skin.label) { fontSize = 10 });

            if (isMain && data is MissionMainData_SO missionMain && missionMain.subType != null && missionMain.subType.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                foreach (var sub in missionMain.subType)
                {
                    if (GUILayout.Button(DataEditorWindow.GetEnumLabel(sub), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        SelectMissionByType(sub);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void SelectMissionByType(MissionEnum type)
    {
        var found = Items.FirstOrDefault(i => i.type == type);
        if (found != null)
            OnSelect(found);
        else
            Debug.LogWarning($"未找到类型为 {DataEditorWindow.GetEnumLabel(type)} 的任务数据");
    }

    /// <summary>判断是否为主线任务数据</summary>
    private static bool IsMissionMain(MissionData_SO data) => data is MissionMainData_SO;

    private void EnsureStyles()
    {
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
    }
}
