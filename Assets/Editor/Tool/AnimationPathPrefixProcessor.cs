using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FPSGame.EditorExt
{

/// <summary>
/// 批量去掉 .anim 节点路径的前缀。只改 `path:` 行，不用 AnimationUtility 重写曲线——
/// 重建 clip 会丢曲线/事件/绑定参数，文本级替换保得住其余内容；BOM 与换行原样保留。
/// </summary>
public static class AnimationPathPrefixProcessor
{
    private const string PathKeyword = "path:";

    /// <summary>工程根目录（Assets 的上一层）。</summary>
    private static string ProjectRoot
    {
        get { return Path.GetDirectoryName(Application.dataPath); }
    }

    /// <summary>单个动画文件的扫描结果。</summary>
    public sealed class FileResult
    {
        public string AssetPath;
        public string FileName;
        public int PathLineCount;      // 全部 path 行数
        public int PrefixHitCount;     // path 行里以该前缀开头的条数（会被改）
        public int OtherLineHitCount;  // 非 path 行出现该前缀（只提示，不会动）
        public string SampleBefore;
        public string SampleAfter;
    }

    /// <summary>自动检测出来的候选前缀。</summary>
    public sealed class PrefixCandidate
    {
        public string Prefix;
        public int HitCount;
        public int PathLineCount;
    }

    /// <summary>把用户输入规范成 "xxx/" 形式：去掉首尾空白与多余的首个 '/'，缺尾斜杠自动补。</summary>
    public static string NormalizePrefix(string prefix)
    {
        prefix = (prefix ?? string.Empty).Trim();
        while (prefix.StartsWith("/", System.StringComparison.Ordinal))
            prefix = prefix.Substring(1);
        if (prefix.Length == 0) return string.Empty;
        return prefix.EndsWith("/", System.StringComparison.Ordinal) ? prefix : prefix + "/";
    }

    /// <summary>目录下的全部 .anim，返回 "Assets/..." 资产路径。</summary>
    public static List<string> CollectAnimFiles(string folderAssetPath, bool recursive)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(folderAssetPath) || !AssetDatabase.IsValidFolder(folderAssetPath)) return list;

        string full = ToFullPath(folderAssetPath);
        if (!Directory.Exists(full)) return list;

        string[] files = Directory.GetFiles(full, "*.anim",
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string asset = ToAssetPath(files[i]);
            if (!string.IsNullOrEmpty(asset)) list.Add(asset);
        }
        list.Sort(System.StringComparer.Ordinal);
        return list;
    }

    /// <summary>按 path 首段的出现次数给出候选前缀（纯数字首段是 fileID 形式，跳过）。</summary>
    public static List<PrefixCandidate> DetectCandidatePrefixes(string folderAssetPath, bool recursive, int top)
    {
        List<string> files = CollectAnimFiles(folderAssetPath, recursive);
        var counts = new Dictionary<string, int>();
        int pathLines = 0;

        for (int i = 0; i < files.Count; i++)
        {
            string[] lines = ReadText(ToFullPath(files[i])).Split('\n');
            for (int j = 0; j < lines.Length; j++)
            {
                if (!TryGetPathValue(lines[j], out string value) || value.Length == 0) continue;
                pathLines++;

                // 只统计"后面还有子路径"的首段，命中数才和真正会改的 path 行对得上
                int slash = value.IndexOf('/');
                if (slash <= 0) continue;
                string head = value.Substring(0, slash);
                if (IsAllDigits(head)) continue;

                counts.TryGetValue(head, out int n);
                counts[head] = n + 1;
            }
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, System.StringComparer.Ordinal)
            .Take(Mathf.Max(1, top))
            .Select(kv => new PrefixCandidate { Prefix = kv.Key + "/", HitCount = kv.Value, PathLineCount = pathLines })
            .ToList();
    }

    /// <summary>扫描一批动画，把结果填进 results（调用方负责清空）。</summary>
    public static void Scan(List<string> animAssets, string prefix, List<FileResult> results)
    {
        prefix = NormalizePrefix(prefix);
        for (int i = 0; i < animAssets.Count; i++)
        {
            string full = ToFullPath(animAssets[i]);
            if (!File.Exists(full)) continue;

            var file = new FileResult
            {
                AssetPath = animAssets[i],
                FileName = Path.GetFileName(animAssets[i]),
            };

            string[] lines = ReadText(full).Split('\n');
            for (int j = 0; j < lines.Length; j++)
            {
                string line = lines[j];
                if (TryGetPathValue(line, out string value))
                {
                    file.PathLineCount++;
                    if (prefix.Length > 0 && value.StartsWith(prefix, System.StringComparison.Ordinal))
                    {
                        file.PrefixHitCount++;
                        if (file.SampleBefore == null)
                        {
                            file.SampleBefore = value;
                            file.SampleAfter = value.Substring(prefix.Length);
                        }
                    }
                }
                else if (prefix.Length > 0 && line.IndexOf(prefix, System.StringComparison.Ordinal) >= 0)
                {
                    file.OtherLineHitCount++;
                }
            }

            results.Add(file);
        }
    }

    /// <summary>执行去前缀，只处理 path 行；返回实际写入的处数。校验不过的文件跳过并记进 failures。</summary>
    public static int RemovePrefix(List<FileResult> files, string prefix, List<string> failures)
    {
        prefix = NormalizePrefix(prefix);
        if (prefix.Length == 0 || files.Count == 0) return 0;

        var regex = new Regex(@"^([ \t]*" + PathKeyword + @"[ \t]*)" + Regex.Escape(prefix),
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        var changed = new List<string>();
        int total = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                string asset = files[i].AssetPath;
                string full = ToFullPath(asset);
                if (!File.Exists(full)) continue;

                string text = ReadText(full);
                int hits = regex.Matches(text).Count;
                if (hits == 0) continue;

                string newText = regex.Replace(text, "$1");

                // 每处只允许少一个前缀长度、且行结构不变；对不上说明匹配跑歪，宁可不动
                if (CountNewlines(newText) != CountNewlines(text) ||
                    text.Length - newText.Length != hits * prefix.Length)
                {
                    failures.Add(asset);
                    Debug.LogError($"[动画路径前缀] 校验失败已跳过：{asset}");
                    continue;
                }

                WriteText(full, newText);
                changed.Add(asset);
                total += hits;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        for (int i = 0; i < changed.Count; i++)
            AssetDatabase.ImportAsset(changed[i], ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        return total;
    }

    private static bool TryGetPathValue(string line, out string value)
    {
        value = null;
        string trimmed = line.TrimStart();
        if (trimmed.Length < PathKeyword.Length) return false;
        if (string.CompareOrdinal(trimmed, 0, PathKeyword, 0, PathKeyword.Length) != 0) return false;
        value = trimmed.Substring(PathKeyword.Length).Trim();
        return true;
    }

    private static bool IsAllDigits(string s)
    {
        if (s.Length == 0) return false;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] < '0' || s[i] > '9') return false;
        }
        return true;
    }

    private static int CountNewlines(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\n') n++;
        }
        return n;
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ToAssetPath(string fullPath)
    {
        string root = ProjectRoot.Replace('\\', '/').TrimEnd('/');
        string full = fullPath.Replace('\\', '/');
        if (!full.StartsWith(root, System.StringComparison.OrdinalIgnoreCase)) return null;
        return full.Substring(root.Length + 1);
    }

    private static string ReadText(string fullPath)
    {
        byte[] raw = File.ReadAllBytes(fullPath);
        int offset = HasBom(raw) ? 3 : 0;
        return new UTF8Encoding(false).GetString(raw, offset, raw.Length - offset);
    }

    private static void WriteText(string fullPath, string text)
    {
        // 按原文件有无 BOM 回写，别让 Unity 把整个 clip 当成换了编码
        bool bom = HasBom(File.ReadAllBytes(fullPath));
        File.WriteAllText(fullPath, text, new UTF8Encoding(bom));
    }

    private static bool HasBom(byte[] raw)
    {
        return raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
    }
}

}
