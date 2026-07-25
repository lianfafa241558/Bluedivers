using System;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

public sealed class RoleTabModule : DataTabModule<RoleData_SO>
{
    public RoleTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Role;
    public override string DisplayName => "角色数据";

    protected override string RootPath => "Assets/Resources/GameData/Role";
    protected override string TypeName => "t:RoleData_SO";
    protected override Comparison<RoleData_SO> SortComparison => (a, b) => a.ID.CompareTo(b.ID);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个角色数据";
    protected override string GetSelectedTitle() => HasSelection ? Selected.ID : "";

    protected override List<RoleData_SO> FilterItems(List<RoleData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => i.ID.Contains(SearchFilter) || i.name.Contains(SearchFilter)).ToList();

    protected override void DrawListItemContent(RoleData_SO data, bool isSelected)
    {
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(data.ID, DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, Color.white));
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
}
