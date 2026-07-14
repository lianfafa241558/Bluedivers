using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class CampTabModule : DataTabModule<CampData_SO>
{
    public CampTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Camp;
    public override string DisplayName => "阵营数据";

    protected override string RootPath => "Assets/Resources/GameData/Camp";
    protected override string TypeName => "t:CampData_SO";
    protected override Comparison<CampData_SO> SortComparison => (a, b) => ((int)a.enemyVarietyType).CompareTo((int)b.enemyVarietyType);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个阵营数据";
    protected override string GetSelectedTitle() => HasSelection ? Selected.ShowName : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<CampData_SO> FilterItems(List<CampData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.ShowName.Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(CampData_SO data, bool isSelected)
    {
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
    }
}
