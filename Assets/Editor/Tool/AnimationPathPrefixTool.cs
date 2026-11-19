using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{

/// <summary>
/// 批量去掉动画节点路径前缀的窗口：拖目录 → 自动检测/手填前缀 → 扫描预览 → 执行。
/// 只动 `path:` 行（clip 名与曲线数据不变）。
/// </summary>
public class AnimationPathPrefixTool : EditorWindow
{
    private const float ListMaxHeight = 240f;

    [SerializeField] private DefaultAsset _folder;
    [SerializeField] private bool _recursive = true;
    [SerializeField] private string _prefix = string.Empty;

    private readonly List<AnimationPathPrefixProcessor.FileResult> _results =
        new List<AnimationPathPrefixProcessor.FileResult>();
    private readonly List<AnimationPathPrefixProcessor.PrefixCandidate> _candidates =
        new List<AnimationPathPrefixProcessor.PrefixCandidate>();

    private Vector2 _scroll;

    private string FolderPath
    {
        get
        {
            if (_folder == null) return null;
            string path = AssetDatabase.GetAssetPath(_folder);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }
    }

    private int TotalHits
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _results.Count; i++) n += _results[i].PrefixHitCount;
            return n;
        }
    }

    [MenuItem("Tools/动画/批量去除节点路径前缀", false, 100)]
    [MenuItem("Assets/工具/批量去除动画路径前缀", false, 30)]
    private static void Open()
    {
        var wnd = GetWindow<AnimationPathPrefixTool>(false, "动画路径前缀");
        wnd.TryUseSelection();
        wnd.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "只改动画里的 path 行（节点路径前缀）：clip 名、曲线、事件都不动。\n" +
            "执行前请先关掉 Animation 窗口里正在编辑的 clip（Unity 会用内存值覆盖磁盘）。\n" +
            "执行不可撤销，建议先提交 git 或勾上备份。",
            MessageType.Info);

        DrawFolderRow();
        _recursive = EditorGUILayout.ToggleLeft("包含子目录", _recursive);
        DrawPrefixRow();
        DrawCandidateRow();

        EditorGUILayout.Space(6);
        DrawActionRow();
        DrawResultList();
        DrawSample();
    }

    private void DrawFolderRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            _folder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("动画目录", "把项目里的文件夹拖到这里"), _folder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck()) ClearResults();

            if (GUILayout.Button("用当前选择", GUILayout.Width(90f))) TryUseSelection();
        }

        if (_folder != null && FolderPath == null)
            EditorGUILayout.HelpBox("拖进来的不是项目文件夹", MessageType.Warning);
    }

    private void DrawPrefixRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _prefix = EditorGUILayout.TextField(new GUIContent("路径前缀", "要删掉的节点路径开头，如 Droid_Kurokame_AR/"), _prefix);

            using (new EditorGUI.DisabledScope(FolderPath == null))
            {
                if (GUILayout.Button("自动检测", GUILayout.Width(90f))) Detect();
            }
        }
    }

    private void DrawCandidateRow()
    {
        if (_candidates.Count == 0) return;

        EditorGUILayout.LabelField("候选（点击填入，右侧是命中/总 path 行）", EditorStyles.miniLabel);
        for (int i = 0; i < _candidates.Count; i++)
        {
            AnimationPathPrefixProcessor.PrefixCandidate c = _candidates[i];
            if (GUILayout.Button($"{c.Prefix}      {c.HitCount}/{c.PathLineCount}", EditorStyles.miniButton))
                _prefix = c.Prefix;
        }
    }

    private void DrawActionRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(FolderPath == null))
            {
                if (GUILayout.Button("扫描", GUILayout.Height(26f))) Rescan();
            }

            using (new EditorGUI.DisabledScope(TotalHits == 0))
            {
                if (GUILayout.Button($"执行：去掉 {TotalHits} 处前缀", GUILayout.Height(26f))) Execute();
            }
        }
    }

    private void DrawResultList()
    {
        if (_results.Count == 0) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"扫描结果：{_results.Count} 个文件 / 命中 {TotalHits} 处", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(ListMaxHeight));
        for (int i = 0; i < _results.Count; i++)
        {
            AnimationPathPrefixProcessor.FileResult r = _results[i];
            string label = $"{r.FileName}    {r.PrefixHitCount}/{r.PathLineCount}";
            if (r.OtherLineHitCount > 0) label += $"    ⚠ 非 path 行还有 {r.OtherLineHitCount} 处（不会改）";

            if (GUILayout.Button(label, EditorStyles.label))
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(r.AssetPath));
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawSample()
    {
        AnimationPathPrefixProcessor.FileResult sample = _results.FirstOrDefault(r => r.SampleBefore != null);
        if (sample == null) return;

        EditorGUILayout.LabelField("示例", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox($"{sample.SampleBefore}\n→\n{sample.SampleAfter}", MessageType.None);
    }

    private void Detect()
    {
        _candidates.Clear();
        _candidates.AddRange(AnimationPathPrefixProcessor.DetectCandidatePrefixes(FolderPath, _recursive, 4));

        if (_candidates.Count == 0)
        {
            EditorUtility.DisplayDialog("自动检测", "目录里没找到可用的 path 前缀。", "好");
            return;
        }

        if (string.IsNullOrWhiteSpace(_prefix)) _prefix = _candidates[0].Prefix;
    }

    private void Rescan()
    {
        string folder = FolderPath;
        if (folder == null) return;

        if (string.IsNullOrWhiteSpace(_prefix))
        {
            Detect();
            if (string.IsNullOrWhiteSpace(_prefix)) return;
        }

        _results.Clear();
        List<string> files = AnimationPathPrefixProcessor.CollectAnimFiles(folder, _recursive);
        AnimationPathPrefixProcessor.Scan(files, _prefix, _results);

        Debug.Log($"[动画路径前缀] 扫描 {folder}：{files.Count} 个 anim，前缀 {_prefix} 命中 {TotalHits} 处");
    }

    private void Execute()
    {
        string prefix = AnimationPathPrefixProcessor.NormalizePrefix(_prefix);
        List<AnimationPathPrefixProcessor.FileResult> targets = _results.Where(r => r.PrefixHitCount > 0).ToList();
        if (prefix.Length == 0 || targets.Count == 0) return;

        int hits = 0;
        for (int i = 0; i < targets.Count; i++) hits += targets[i].PrefixHitCount;

        bool go = EditorUtility.DisplayDialog("确认去掉路径前缀",
            $"前缀：{prefix}\n文件：{targets.Count} 个\npath 行：{hits} 处\n\n继续？", "执行", "取消");
        if (!go) return;

        var failures = new List<string>();
        int done = AnimationPathPrefixProcessor.RemovePrefix(targets, prefix, failures);

        Debug.Log($"[动画路径前缀] 已处理 {targets.Count} 个文件、{done} 处");

        Rescan();
        EditorUtility.DisplayDialog("完成",
            $"已去掉 {done} 处前缀（{targets.Count} 个文件）" +
            (failures.Count > 0 ? $"\n{failures.Count} 个文件校验失败已跳过，见 Console。" : string.Empty), "好");
    }

    private void TryUseSelection()
    {
        Object selected = Selection.activeObject;
        if (selected == null) return;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path)) return;

        if (AssetDatabase.IsValidFolder(path))
        {
            SetFolder(path);
        }
        else if (path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
        {
            SetFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        }
    }

    private void SetFolder(string folderPath)
    {
        _folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        ClearResults();
    }

    private void ClearResults()
    {
        _results.Clear();
        _candidates.Clear();
        _scroll = Vector2.zero;
    }
}

}
