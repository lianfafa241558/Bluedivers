using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class AirdropTabModule : DataTabModule<AirdropData_SO>
{
    /// <summary>存在 ID 重复 / 操作序列重复 的战备集合（用于列表标红）</summary>
    private readonly HashSet<AirdropData_SO> _conflictSet = new HashSet<AirdropData_SO>();

    public AirdropTabModule(DataEditorWindow host) : base(host) { }

    /// <summary>刷新时清理遮罩纹理缓存</summary>
    protected override void RefreshData()
    {
        ClearMaskCache();
        base.RefreshData();
        RecomputeConflicts();
    }


    /// <summary>全量两两比较：同 ID 或同操作序列（均非空）视为冲突</summary>
    private void RecomputeConflicts()
    {
        _conflictSet.Clear();
        for (int i = 0; i < Items.Count; i++)
        {
            for (int j = i + 1; j < Items.Count; j++)
            {
                AirdropData_SO a = Items[i];
                AirdropData_SO b = Items[j];
                bool idDup = a.ID == b.ID;
                bool opDup = a.opter != null && b.opter != null
                             && a.opter.Length > 0 && b.opter.Length > 0
                             && a.opter.SequenceEqual(b.opter);
                if (idDup || opDup)
                {
                    _conflictSet.Add(a);
                    _conflictSet.Add(b);
                }
            }
        }
    }

    private bool HasConflict(AirdropData_SO data) => _conflictSet.Contains(data);

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
        {
            Texture2D maskTex = GetOrCreateMaskTexture(data.icon.texture, data.IconColor);
            if (maskTex != null)
                GUI.DrawTexture(iconRect, maskTex, ScaleMode.ScaleToFit);
            else
                GUI.DrawTexture(iconRect, data.icon.texture, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.Box(iconRect, "");
        }

        EditorGUILayout.BeginVertical();
        {
            bool conflict = HasConflict(data);
            GUILayout.Label($"{data.showName}[{data.ID}]", DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, conflict ? Color.red : data.Color));

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
            if (conflict)
            {
                GUILayout.Label($"{infoText}   ⚠ ID/操作冲突",
                    new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = Color.red } });
            }
            else
            {
                GUILayout.Label(infoText, new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold });
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private static readonly Dictionary<(int, Color), Texture2D> _maskCache = new Dictionary<(int, Color), Texture2D>();

    private static Texture2D GetOrCreateMaskTexture(Texture2D icon, Color color)
    {
        if (icon == null)
        {
            return null;
        }

        var key = (icon.GetInstanceID(), color);
        if (_maskCache.TryGetValue(key, out Texture2D cached))
        {
            return cached;
        }

        // 通过 RenderTexture 读取像素
        RenderTexture rt = RenderTexture.GetTemporary(icon.width, icon.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(icon, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        // 应用遮罩规则：R * color + G * white
        Color[] pixels = readable.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            if (c.a < 0.01f)
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                pixels[i] = color*c.r+Color.white*c.g;
            }
        }

        Texture2D result = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false);
        result.SetPixels(pixels);
        result.Apply();

        //DestroyImmediate(readable);
        _maskCache[key] = result;
        return result;
    }


    private static void ClearMaskCache()
    {
        foreach (var tex in _maskCache.Values)
        {
            //Object.DestroyImmediate(tex);
        }
        _maskCache.Clear();
    }
}
