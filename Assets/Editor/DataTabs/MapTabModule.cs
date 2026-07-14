using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class MapTabModule : DataTabModule<MapData_SO>
{
    public MapTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Map;
    public override string DisplayName => "地图数据";

    protected override string RootPath => "Assets/Resources/GameData/Map";
    protected override string TypeName => "t:MapData_SO";
    protected override Comparison<MapData_SO> SortComparison => (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个地图数据";
    protected override string GetSelectedTitle() => HasSelection ? Selected.AreaName : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<MapData_SO> FilterItems(List<MapData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.AreaName.Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(MapData_SO data, bool isSelected)
    {
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
    }
}
