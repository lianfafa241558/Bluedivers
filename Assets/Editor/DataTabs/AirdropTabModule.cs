using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AirdropTabModule : DataTabModule<AirdropData_SO>
{
    public AirdropTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Airdrop;
    public override string DisplayName => "战备数据";

    protected override string RootPath => "Assets/Resources/GameData/Airdrop";
    protected override string TypeName => "t:AirdropData_SO";
    protected override Comparison<AirdropData_SO> SortComparison => (a, b) => a.ID.CompareTo(b.ID);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个战备数据";
    protected override string GetSelectedTitle() => HasSelection ? $"{Selected.showName}[{Selected.ID}]" : "";

    protected override GUIStyle GetSelectedTitleStyle()
        => HasSelection ? DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, Selected.Color) : EditorStyles.boldLabel;

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<AirdropData_SO> FilterItems(List<AirdropData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.showName.Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(AirdropData_SO data, bool isSelected)
    {
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        else
            GUI.Box(iconRect, "");

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label($"{data.showName}[{data.ID}]", DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, data.Color));

            string opterStr = "";
            if (data.opter != null && data.opter.Length > 0)
            {
                opterStr = string.Join("", data.opter.Select(o => o switch
                {
                    DirectionEnum.Left => "←",
                    DirectionEnum.Up => "↑",
                    DirectionEnum.Right => "→",
                    DirectionEnum.Down => "↓",
                    _ => "?",
                }));
            }
            string deliveryStr = DataEditorWindow.GetInspectorName(data.deliveryType);
            string infoText = string.IsNullOrEmpty(opterStr) ? deliveryStr : $"{opterStr} · {deliveryStr}";
            GUILayout.Label(infoText, new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
}
