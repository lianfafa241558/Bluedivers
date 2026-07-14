using System;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WeaponTabModule : DataTabModule<GameObject>
{
    public WeaponTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.Weapon;
    public override string DisplayName => "武器数据";

    protected override string RootPath => "Assets/Resources/Weapons";
    protected override string TypeName => "t:Prefab";
    protected override bool IncludeSubDirs => true;
    protected override Comparison<GameObject> SortComparison => (a, b) => ((int)GetWeapon(a).WeaponTypeEnum).CompareTo((int)GetWeapon(b).WeaponTypeEnum);

    protected override Object GetEditorTarget(GameObject data)
    {
        var wp = data.GetComponentInChildren<WeaponPlayerController>();
        return wp != null ? (Object)wp : data;
    }

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个武器预制体";
    protected override string GetSelectedTitle() => HasSelection ? GetWeapon(Selected).WeaponName : "";

    protected override void DrawRightPanelExtra()
    {
        var wp = GetWeapon(Selected);
        GUILayout.Label(wp.WeaponType, EditorStyles.miniLabel);
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<GameObject> FilterItems(List<GameObject> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => GetWeapon(i).WeaponName.Contains(SearchFilter)).ToList();

    protected override void RefreshData()
    {
        Items.Clear();
        if (!AssetDatabase.IsValidFolder(RootPath))
        {
            Debug.LogError($"路径不存在: {RootPath}");
            return;
        }

        string[] subDirs = AssetDatabase.GetSubFolders(RootPath);
        string[] searchPaths = new[] { RootPath }.Concat(subDirs).ToArray();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", searchPaths);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (prefab.GetComponentInChildren<WeaponPlayerController>() == null) continue;

            Items.Add(prefab);
        }

        var comparison = SortComparison;
        if (comparison != null)
            Items.Sort(comparison);
    }

    protected override void DrawListItemContent(GameObject data, bool isSelected)
    {
        var wp = GetWeapon(data);

        EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(40));

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (wp.WeaponIcon != null)
            GUI.DrawTexture(iconRect, wp.WeaponIcon.texture, ScaleMode.ScaleToFit);
        else
            GUI.Box(iconRect, "");

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(wp.WeaponName, EditorStyles.boldLabel);
            GUILayout.Label(string.IsNullOrEmpty(wp.WeaponType) ? data.name : wp.WeaponType, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>获取预制体上挂载的 WeaponPlayerController 组件</summary>
    private static WeaponPlayerController GetWeapon(GameObject prefab)
        => prefab.GetComponentInChildren<WeaponPlayerController>();
}
