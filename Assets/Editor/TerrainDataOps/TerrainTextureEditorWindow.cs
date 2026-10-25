using UnityEngine;
using UnityEditor;

public enum TerrainTarget
{
    AlphamapOnly,
    HeightmapOnly,
    Both
}

public class TerrainTextureEditorWindow : EditorWindow
{
    private Terrain targetTerrain;
    private int moveStep = 1;
    private bool wrapEdges = true;
    private TerrainTarget applyTarget = TerrainTarget.Both;
    private bool moveHeightWhileMoveAlpha = true;
    private static readonly string[] ApplyTargetLabels = { "仅纹理混合 (Alphamap)", "仅高度图 (Heightmap)", "两者" };

    [MenuItem("Tools/Terrain/地形操作器")]
    public static void Open()
    {
        var w = GetWindow<TerrainTextureEditorWindow>("地形贴图编辑器");
        w.minSize = new Vector2(360, 440);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("目标地形", EditorStyles.boldLabel);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField(
            "地形", targetTerrain, typeof(Terrain), true);
        if (targetTerrain == null && Selection.activeGameObject != null)
        {
            var t = Selection.activeGameObject.GetComponent<Terrain>();
            if (t != null) targetTerrain = t;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);
        applyTarget = (TerrainTarget)EditorGUILayout.Popup("应用范围", (int)applyTarget, ApplyTargetLabels);
        wrapEdges = EditorGUILayout.Toggle("边缘环绕", wrapEdges);
        moveStep = EditorGUILayout.IntSlider("移动步长（格）", moveStep, 1, 64);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("翻转", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("水平翻转", GUILayout.Height(30)))
                Execute(data => DoFlipHorizontal(data, applyTarget));
            if (GUILayout.Button("垂直翻转", GUILayout.Height(30)))
                Execute(data => DoFlipVertical(data, applyTarget));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("平移（按步长）", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("↑ 上移", GUILayout.Height(30)))
                Execute(data => DoMove(data, 0, -moveStep, applyTarget));
            if (GUILayout.Button("↓ 下移", GUILayout.Height(30)))
                Execute(data => DoMove(data, 0, moveStep, applyTarget));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("← 左移", GUILayout.Height(30)))
                Execute(data => DoMove(data, -moveStep, 0, applyTarget));
            if (GUILayout.Button("→ 右移", GUILayout.Height(30)))
                Execute(data => DoMove(data, moveStep, 0, applyTarget));
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "应用范围控制作用于纹理混合(Alphamap)、高度图(Heightmap)或两者。\n" +
            "边缘环绕 = 移出的像素从另一侧回来（周期平铺）。\n" +
            "不勾选则移出的像素丢弃，空出区域填 0。\n" +
            "所有操作支持 Ctrl+Z 撤销。",
            MessageType.Info);
    }

    // ----------------- 分发 -----------------

    private void DoFlipHorizontal(TerrainData data, TerrainTarget t)
    {
        if (t == TerrainTarget.AlphamapOnly || t == TerrainTarget.Both)
            TerrainDataOps.FlipHorizontal_Alpha(data);
        if (t == TerrainTarget.HeightmapOnly || t == TerrainTarget.Both)
            TerrainDataOps.FlipHorizontal_Height(data);
    }

    private void DoFlipVertical(TerrainData data, TerrainTarget t)
    {
        if (t == TerrainTarget.AlphamapOnly || t == TerrainTarget.Both)
            TerrainDataOps.FlipVertical_Alpha(data);
        if (t == TerrainTarget.HeightmapOnly || t == TerrainTarget.Both)
            TerrainDataOps.FlipVertical_Height(data);
    }

    private void DoMove(TerrainData data, int dx, int dy, TerrainTarget t)
    {
        if (t == TerrainTarget.AlphamapOnly || t == TerrainTarget.Both)
            TerrainDataOps.MoveAlpha(data, dx, dy, wrapEdges);
        if (t == TerrainTarget.HeightmapOnly || t == TerrainTarget.Both)
            TerrainDataOps.MoveHeight(data, dx, dy, wrapEdges);
    }

    private void Execute(System.Action<TerrainData> op)
    {
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            EditorUtility.DisplayDialog("错误", "请先指定一个带 TerrainData 的地形。", "确定");
            return;
        }

        var data = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "地形数据操作");

        op(data);

        data.SetBaseMapDirty();
        EditorUtility.SetDirty(data);
        targetTerrain.Flush();
        SceneView.RepaintAll();
    }
}