using System;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

public sealed class WeaponModuleTabModule : DataTabModule<WeaponModuleData_SO>
{
    public WeaponModuleTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.WeaponModule;
    public override string DisplayName => "武器模组";

    protected override string RootPath => "Assets/Resources/GameData/WeaponModule";
    protected override string TypeName => "t:WeaponModuleData_SO";
    protected override bool IncludeSubDirs => true;
    protected override Comparison<WeaponModuleData_SO> SortComparison => (a, b) =>
    {
        int c = ((int)a.type).CompareTo((int)b.type);
        return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.Ordinal);
    };

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个武器模组";
    protected override string GetSelectedTitle() => HasSelection ? Selected.name : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<WeaponModuleData_SO> FilterItems(List<WeaponModuleData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(WeaponModuleData_SO data, bool isSelected)
    {
        EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(60));

        // Frame + icon (frame larger, icon smaller)
        var frameRect = EditorGUILayout.GetControlRect(GUILayout.Width(60), GUILayout.Height(60));
        var iconRect = new Rect(frameRect.x + 10, frameRect.y + 10, 40, 40);

        string framePath = $"Assets/Resources/Images/Icon/Frame_Module{data.type}.png";
        Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);

        if (frameSprite != null)
            GUI.DrawTexture(frameRect, frameSprite.texture, ScaleMode.ScaleToFit, true, 0, data.color, 0, 0);

        if (data.icon != null)
            GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        else if (frameSprite == null)
            EditorGUI.DrawRect(frameRect, data.color);

        // Info
        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(data.name, DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, data.color));

            if (data.desc != null)
            {
                Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Arrow_Down.png");

                foreach (var entry in data.desc)
                {
                    EditorGUILayout.BeginHorizontal();

                    var arrowRect = EditorGUILayout.GetControlRect(GUILayout.Width(12), GUILayout.Height(12));
                    if (arrowSprite != null)
                    {
                        var prevColor = GUI.color;
                        GUI.color = entry.Key ? Color.green : Color.red;
                        GUIUtility.RotateAroundPivot(entry.Key ? 180f : 0f, arrowRect.center);
                        GUI.DrawTexture(arrowRect, arrowSprite.texture, ScaleMode.ScaleToFit);
                        GUI.matrix = Matrix4x4.identity;
                        GUI.color = prevColor;
                    }
                    else
                    {
                        GUILayout.Label(entry.Key ? "↑" : "↓",
                            new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = entry.Key ? Color.green : Color.red } },
                            GUILayout.Width(12));
                    }

                    GUILayout.Label(entry.Value, new GUIStyle(GUI.skin.label) { fontSize = 10 });
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
}
