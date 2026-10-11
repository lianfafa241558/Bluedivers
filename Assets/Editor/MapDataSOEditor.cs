using FPSGame.Data;
using Pixeye.Unity;
using ProjectEditors;      // TerrainDataBaker（本检视器是全局命名空间，与 Assets/Editor 其余编辑器一致）
using UnityEditor;
using UnityEngine;

/// <summary>
/// `MapData_SO` 检视器：**字段绘制完全委托给全局 fallback `EditorOverride`**（保住 `[InspectorName]` /
/// `[Compare]` / `[Divider]` / `[SpritePreview]` / `[Singleline]` 等既有观感），只在其后追加一块
/// 「地形资产」区：显示本图资产是否与地图数据同步，并提供一键烘焙按钮。
///
/// <para>▍为什么需要它（方案A 的收尾，2026-10-10）：打包端**改不了** `TerrainData.treePrototypes` /
/// `detailPrototypes`（Editor-only）⇒ "本图用哪些石块/树/草"只能烘在**该图自己的** TerrainData 里；
/// 改了 `MapData_SO` 的原型数组后必须重烘一次，否则打包端还是旧原型。放在这里是为了"改完顺手点一下"。</para>
/// </summary>
[CustomEditor(typeof(MapData_SO))]
public class MapDataSOEditor : Editor
{
    private Editor _fallback;           // EditorOverride 实例（字段绘制用它，观感与没有本检视器时一致）
    private bool _fold = true;

    private string _status;
    private bool _statusOk;
    private double _nextStatusTime;

    private static GUIStyle _okStyle, _badStyle;

    private void OnDisable()
    {
        if (_fallback != null) DestroyImmediate(_fallback);
        _fallback = null;
    }

    public override void OnInspectorGUI()
    {
        DrawFields();

        var map = (MapData_SO)target;
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _fold = EditorGUILayout.Foldout(_fold, "地形资产（方案A：一图一份 TerrainData）", true);
        DrawStatus(map);
        if (_fold) DrawActions(map);
        EditorGUILayout.EndVertical();
    }

    /// <summary>字段本体：优先委托 `EditorOverride`，拿不到才退回 `DrawDefaultInspector`。</summary>
    private void DrawFields()
    {
        if (_fallback == null)
        {
            try { _fallback = CreateEditor(target, typeof(EditorOverride)); }
            catch { _fallback = null; }
        }

        if (_fallback == null)
        {
            DrawDefaultInspector();
            return;
        }

        _fallback.serializedObject.Update();
        _fallback.OnInspectorGUI();
        if (_fallback.serializedObject.hasModifiedProperties)
            _fallback.serializedObject.ApplyModifiedProperties();
    }

    /// <summary>状态行。⚠ IMGUI 每帧都跑，而读 `treePrototypes` 会整表 marshal ⇒ 节流 0.5s。</summary>
    private void DrawStatus(MapData_SO map)
    {
        if (_status == null || EditorApplication.timeSinceStartup >= _nextStatusTime)
        {
            _nextStatusTime = EditorApplication.timeSinceStartup + 0.5;
            _status = TerrainDataBaker.StatusOf(map, out _statusOk);
        }

        EnsureStyles();
        EditorGUILayout.LabelField(_status, _statusOk ? _okStyle : _badStyle);
    }

    private void DrawActions(MapData_SO map)
    {
        int stone = map.stonePrototypes != null ? map.stonePrototypes.Length : 0;
        int tree = map.treePrototypes != null ? map.treePrototypes.Length : 0;
        int detail = map.detailPrototypes != null ? map.detailPrototypes.Length : 0;

        EditorGUILayout.LabelField($"石块 {stone} + 树 {tree} ⇒ 树原型 {stone + tree}；草的细节原型 {detail} 个",
            EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(stone + tree + detail == 0))
        {
            if (GUILayout.Button("烘焙到本图地形资产（把上面这些原型写进 TerrainData）"))
            {
                string report = TerrainDataBaker.BakeOne(map);
                Debug.Log($"[烘焙] {map.name}：{report}");
                _status = null;                 // 强制下帧重算状态
                serializedObject.Update();      // 引用可能刚被写入（terrainAsset）
                Repaint();
            }
        }

        using (new EditorGUI.DisabledScope(map.terrainAsset == null))
        {
            if (GUILayout.Button("选中本图地形资产"))
                Selection.activeObject = map.terrainAsset;
        }

        EditorGUILayout.LabelField(
            "⚠ 打包端改不了原型数组（Editor-only）⇒ 改了上面的原型后要点一次；" +
            "资产只存「原型清单 + 草的分辨率」，形状/位置/密度仍是进战斗时现场生成。",
            EditorStyles.wordWrappedMiniLabel);
    }

    private static void EnsureStyles()
    {
        if (_okStyle != null) return;
        _okStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = new Color(0.45f, 0.85f, 0.45f) } };
        _badStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = new Color(1f, 0.65f, 0.35f) } };
    }
}
