using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AboStateTabModule : DataTabModule<AboStateData_SO>
{
    public AboStateTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.AboState;
    public override string DisplayName => "异常状态";

    protected override string RootPath => "Assets/Resources/GameData/AboState";
    protected override string TypeName => "t:AboStateData_SO";
    protected override Comparison<AboStateData_SO> SortComparison => (a, b) => ((int)a.typeEnum).CompareTo((int)b.typeEnum);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个异常状态数据";
    protected override string GetSelectedTitle() => HasSelection ? DataEditorWindow.GetInspectorName(Selected.typeEnum) : "";

    protected override GUIStyle GetSelectedTitleStyle()
        => HasSelection ? DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, Selected.color) : EditorStyles.boldLabel;

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<AboStateData_SO> FilterItems(List<AboStateData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => DataEditorWindow.GetInspectorName(i.typeEnum).Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(AboStateData_SO data, bool isSelected)
    {
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit, true, 0, data.color, 0, 0);
        else
            EditorGUI.DrawRect(iconRect, data.color);

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(DataEditorWindow.GetInspectorName(data.typeEnum), DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, data.color));
            GUILayout.Label(data.name, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
}
