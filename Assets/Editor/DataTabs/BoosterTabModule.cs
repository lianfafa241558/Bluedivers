using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class BoosterTabModule : DataTabModule<Booster_SO>
{
    public BoosterTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Booster; 
    public override string DisplayName => "全队强化";

    protected override string RootPath => "Assets/Resources/GameData/Booster";
    protected override string TypeName => "t:Booster_SO";
    protected override Comparison<Booster_SO> SortComparison => (a, b) => a.ID.CompareTo(b.ID);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个全队强化数据";
    protected override string GetSelectedTitle() => HasSelection ? $"{Selected.showName}[{Selected.ID}]" : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<Booster_SO> FilterItems(List<Booster_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.showName.Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(Booster_SO data, bool isSelected)
    {
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        else
            GUI.Box(iconRect, "");

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label($"{data.showName}[{data.ID}]", EditorStyles.boldLabel);
            string typeStr = DataEditorWindow.GetInspectorName(data.type);
            GUILayout.Label(typeStr, new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
}
