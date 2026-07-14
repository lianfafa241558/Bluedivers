using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

public sealed class WeaponUpgradeTabModule : DataTabModule<WeaponUpgradeData_SO>
{
    public WeaponUpgradeTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.WeaponUpgrade;
    public override string DisplayName => "武器升级";

    protected override string RootPath => "Assets/Resources/GameData/WeaponUpgrade";
    protected override string TypeName => "t:WeaponUpgradeData_SO";
    protected override bool IncludeSubDirs => true;
    protected override Comparison<WeaponUpgradeData_SO> SortComparison
        => (a, b) => string.Compare(GetSortKey(a), GetSortKey(b), StringComparison.Ordinal);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个武器升级";
    protected override string GetSelectedTitle() => HasSelection ? Selected.name : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<WeaponUpgradeData_SO> FilterItems(List<WeaponUpgradeData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.name.Contains(SearchFilter)).ToList();

    private static string GetSortKey(WeaponUpgradeData_SO data)
    {
        string path = AssetDatabase.GetAssetPath(data);
        string folderName = Path.GetFileName(Path.GetDirectoryName(path));
        return string.IsNullOrEmpty(folderName) || folderName == "WeaponUpgrade" ? data.name : $"{folderName}/{data.name}";
    }

    protected override void DrawListItemContent(WeaponUpgradeData_SO data, bool isSelected)
    {
        EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(40));

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        else
            GUI.Box(iconRect, "");

        // Get parent folder name from asset path
        string assetPath = AssetDatabase.GetAssetPath(data);
        string folderName = Path.GetFileName(Path.GetDirectoryName(assetPath));

        EditorGUILayout.BeginVertical();
        {
            string displayName = string.IsNullOrEmpty(folderName) || folderName == "WeaponUpgrade"
                ? data.name
                : $"{folderName}/{data.name}";
            GUILayout.Label(displayName, EditorStyles.boldLabel);
            GUILayout.Label(data.type, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
}
