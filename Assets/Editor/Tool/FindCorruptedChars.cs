using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class FindCorruptedChars : EditorWindow
{
    private string _result = "";
    private Vector2 _scrollPos;
    private static readonly char CorruptedChar = '\uFFFD';

    [MenuItem("Tools/查找损坏字符")]
    private static void Open()
    {
        GetWindow<FindCorruptedChars>("查找损坏字符");
    }

    [MenuItem("Tools/查找损坏字符", true)]
    private static bool ValidateOpen()
    {
        return true;
    }

    private void OnGUI()
    {
        if (GUILayout.Button("扫描所有 .cs 文件", GUILayout.Height(40)))
        {
            Scan();
        }

        if (GUILayout.Button("一键修复全部损坏字符", GUILayout.Height(40)))
        {
            FixAll();
        }

        EditorGUILayout.Space();

        if (!string.IsNullOrEmpty(_result))
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.TextArea(_result, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("复制结果"))
            {
                EditorGUIUtility.systemCopyBuffer = _result;
                Debug.Log("已复制到剪贴板");
            }
        }
    }

    private static void Scan()
    {
        var root = Application.dataPath;
        var allCsFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

        var found = new List<string>();

        foreach (var file in allCsFiles)
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var reader = new StreamReader(file, Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf(CorruptedChar) >= 0)
                    {
                        found.Add(file);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"跳过文件 {file}：{ex.Message}");
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"扫描完成，共检查 {allCsFiles.Length} 个 .cs 文件");
        sb.AppendLine($"发现 {found.Count} 个包含损坏字符的文件：");
        sb.AppendLine();

        foreach (var f in found)
        {
            var relative = "Assets" + f.Substring(root.Length);
            sb.AppendLine(relative);
        }

        var result = sb.ToString();
        Debug.Log(result);

        var window = GetWindow<FindCorruptedChars>();
        if (window != null)
        {
            window._result = result;
            window.Repaint();
        }
    }

    [MenuItem("Tools/一键修复所有损坏字符")]
    private static void FixAll()
    {
        var root = Application.dataPath;
        var allCsFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

        int fixedCount = 0;
        int totalFiles = 0;

        foreach (var file in allCsFiles)
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                if (content.IndexOf(CorruptedChar) < 0)
                    continue;

                totalFiles++;
                var newContent = content.Replace(CorruptedChar.ToString(), "");
                if (newContent != content)
                {
                    File.WriteAllText(file, newContent, new UTF8Encoding(false));
                    fixedCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"跳过文件 {file}：{ex.Message}");
            }
        }

        AssetDatabase.Refresh();
        var msg = $"修复完成，共检查 {totalFiles} 个损坏文件，已修复 {fixedCount} 个文件（移除了所有 U+FFFD 损坏字符）";
        Debug.Log(msg);

        var window = GetWindow<FindCorruptedChars>();
        if (window != null)
        {
            window._result = msg;
            window.Repaint();
        }
    }
}
