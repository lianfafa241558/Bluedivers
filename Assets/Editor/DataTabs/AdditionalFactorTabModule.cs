using System;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Data;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{

/// <summary>
/// 「附加因素」数据页（<c>Resources/GameData/AdditionalFactor</c>）。
/// <para>▍列表行按 SO 的 <c>Color</c> 给图标染色、并标出枚举名与模板条数 —— 与运行时选图界面的观感对齐，
/// 一眼能看出"哪份资产还没配图/没配色/模板是空的"。</para>
/// <para>▍右面板不另写绘制：直接用资产自己的 Inspector（走全局 <c>EditorOverride</c>，
/// 因此 <c>[InspectorName]</c> / <c>[Foldout]</c> / <c>[SpritePreview]</c> 照常生效）。</para>
/// </summary>
public sealed class AdditionalFactorTabModule : DataTabModule<AdditionalFactorData_SO>
{
    public AdditionalFactorTabModule(DataEditorWindow host) : base(host) { }

    public override TabType TabType => TabType.AdditionalFactor;
    public override string DisplayName => "附加因素";

    protected override string RootPath => "Assets/Resources/GameData/AdditionalFactor";
    protected override string TypeName => "t:AdditionalFactorData_SO";
    protected override Comparison<AdditionalFactorData_SO> SortComparison =>
        (a, b) => ((int)a.type).CompareTo((int)b.type);

    protected override string GetEmptyMessage() => "请从左侧列表中选择一个附加因素数据";
    protected override string GetSelectedTitle()
        => HasSelection ? $"{Selected.ShowName}[{DataEditorWindow.GetInspectorName(Selected.type)}]" : "";

    protected override void DrawRightPanelExtra()
    {
        GUILayout.Label(AssetDatabase.GetAssetPath(Selected),
            new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
    }

    protected override List<AdditionalFactorData_SO> FilterItems(List<AdditionalFactorData_SO> items)
        => string.IsNullOrEmpty(SearchFilter)
            ? items
            : items.Where(i => (!string.IsNullOrEmpty(i.ShowName) && i.ShowName.Contains(SearchFilter))
                            || i.name.Contains(SearchFilter)
                            || (!string.IsNullOrEmpty(i.Desc) && i.Desc.Contains(SearchFilter))).ToList();

    protected override void DrawListItemContent(AdditionalFactorData_SO data, bool isSelected)
    {
        EditorGUILayout.BeginHorizontal(GUI.skin.box);

        var iconRect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
        if (data.Sprite != null)
            GUI.DrawTexture(iconRect, data.Sprite.texture, ScaleMode.ScaleToFit, true, 0, data.Color, 0, 0);
        else
            EditorGUI.DrawRect(iconRect, data.Color);   // 没配图也留一块本色，好认出类型色

        EditorGUILayout.BeginVertical();
        {
            GUILayout.Label(string.IsNullOrEmpty(data.ShowName) ? data.name : data.ShowName, EditorStyles.boldLabel);
            string sub = DataEditorWindow.GetInspectorName(data.type);
            if (data.template != null) sub += $" · 模板 {data.template.Count} 条";
            GUILayout.Label(sub, new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold });
            if (!string.IsNullOrEmpty(data.Desc))
                GUILayout.Label(data.Desc, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
}
}
