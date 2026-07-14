using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class UpdateTabModule : DataTabModule<UpdateData_SO>
{
    public UpdateTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Update;
    public override string DisplayName => "更新说明";

    protected override string RootPath => "Assets/Resources/GameData/Update";
    protected override string TypeName => "t:UpdateData_SO";
    protected override Comparison<UpdateData_SO> SortComparison => (a, b) =>
    {
        int c = string.Compare(a.time, b.time, StringComparison.Ordinal);
        return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.Ordinal);
    };

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个更新说明";
    protected override string GetSelectedTitle() => HasSelection ? Selected.title : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<UpdateData_SO> FilterItems(List<UpdateData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.title.Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(UpdateData_SO data, bool isSelected)
    {
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
    }
}
