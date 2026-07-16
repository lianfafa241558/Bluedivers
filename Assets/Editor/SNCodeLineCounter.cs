using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SN.CodeStatistics {
/// <summary>
/// 一个 Unity 编辑器工具，用于分析和统计项目中的代码行数。
/// 提供各种文件类型的代码行、注释行和空行的统计信息。
/// </summary>
public class SNCodeLineCounter : EditorWindow
{
    [Serializable]
    public class FileStatistics
    {
        /// <summary>文件在项目中的相对路径。</summary>
        public string filePath;

        /// <summary>带扩展名的文件名。</summary>
        public string fileName;

        /// <summary>文件扩展名（例如 ".cs"、".js"）。</summary>
        public string extension;

        /// <summary>文件总行数。</summary>
        public int totalLines;

        /// <summary>代码行数（非注释、非空行）。</summary>
        public int codeLines;

        /// <summary>注释行数。</summary>
        public int commentLines;

        /// <summary>空行数。</summary>
        public int emptyLines;
    }

    // 设置
    private string rootFolder = "Assets/Scripts";
    private List<string> extensionsToCheck = new List<string> { ".cs" };
    private bool excludeThirdParty = true;

    // UI 状态
    private bool showSettings = false;
    private bool isAnalyzing = false;

    // 分析结果
    private List<FileStatistics> fileStatsList = new List<FileStatistics>();
    private int totalFiles = 0;
    private int totalCodeLines = 0;
    private int totalCommentLines = 0;
    private int totalEmptyLines = 0;

    // 编辑器偏好设置
    private const string EditorPrefRootFolder = "SN_CodeLineCounter_RootFolder";
    private const string EditorPrefExcludeThirdParty = "SN_CodeLineCounter_ExcludeThirdParty";
    private GUIStyle headerStyle;


    /// <summary>
    /// 添加一个菜单项，用于打开 SN Code Line Counter 窗口。
    /// </summary>
    [MenuItem("Tools/统计代码行数")]
    public static void ShowWindow()
    {
        var window = GetWindow<SNCodeLineCounter>("统计代码行数");
        window.minSize = new Vector2(500, 600);
        window.maxSize = new Vector2(500, 600);
    }


    /// <summary>
    /// 窗口启用时进行初始化。
    /// </summary>
    private void OnEnable()
    {
        // 加载已保存的设置
        rootFolder = EditorPrefs.GetString(EditorPrefRootFolder, "Assets/Scripts");
        excludeThirdParty = EditorPrefs.GetBool(EditorPrefExcludeThirdParty, true);

        // 准备 UI 样式
        headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
    }

    /// <summary>
    /// 绘制编辑器 GUI。
    /// </summary>
    private void OnGUI()
    {
        DrawHeader();

        // 如果正在分析中，不允许交互
        EditorGUI.BeginDisabledGroup(isAnalyzing);

        DrawFolderSelection();
        DrawSettings();

        GUILayout.Space(10);
        if (GUILayout.Button("分析", GUILayout.Height(28)))
        {
            StartAnalysis();
        }

        EditorGUI.EndDisabledGroup();

        if (fileStatsList.Count > 0)
        {
            DrawResults();
        }

        // 如有需要，显示正在分析的消息
        if (isAnalyzing)
        {
            EditorGUILayout.HelpBox("正在分析文件... 请稍候。", MessageType.Info);
        }
    }


    /// <summary>
    /// 绘制窗口的标题部分。
    /// </summary>
    private void DrawHeader()
    {
        GUILayout.Label("Unity Code Statistics Tool", headerStyle);
        GUILayout.Space(5);
        EditorGUILayout.HelpBox("该工具用于分析指定文件夹中代码文件的行数统计。", MessageType.Info);
        GUILayout.Space(10);
    }

    /// <summary>
    /// 绘制文件夹选择控件。
    /// </summary>
    private void DrawFolderSelection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("根文件夹:");

        // 文件夹路径输入框，并保存设置
        EditorGUI.BeginChangeCheck();
        rootFolder = EditorGUILayout.TextField(rootFolder);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(EditorPrefRootFolder, rootFolder);
        }

        // 浏览按钮
        if (GUILayout.Button("浏览", GUILayout.Width(70)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择根文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 直接使用所选路径——在分析阶段会进行验证
                rootFolder = selectedPath;
                EditorPrefs.SetString(EditorPrefRootFolder, rootFolder);
            }
        }

        // 项目根目录按钮
        if (GUILayout.Button("项目根目录", GUILayout.Width(90)))
        {
            rootFolder = Path.GetDirectoryName(Application.dataPath);
            EditorPrefs.SetString(EditorPrefRootFolder, rootFolder);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制窗口的设置部分。
    /// </summary>
    private void DrawSettings()
    {
        showSettings = EditorGUILayout.Foldout(showSettings, "设置", true);
        if (showSettings)
        {
            EditorGUI.indentLevel++;

            // 第三方代码排除选项
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排除第三方代码", GUILayout.Width(165));
            excludeThirdParty = EditorGUILayout.Toggle("", excludeThirdParty);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(EditorPrefExcludeThirdParty, excludeThirdParty);
            }

            EditorGUILayout.LabelField("包含的文件扩展名:", GUILayout.Width(170));
            for (int i = 0; i < extensionsToCheck.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                extensionsToCheck[i] = EditorGUILayout.TextField(extensionsToCheck[i]);

                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button("移除", GUILayout.Width(60)))
                {
                    extensionsToCheck.RemoveAt(i);
                    GUIUtility.ExitGUI(); // 防止 UI 更新问题
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("添加扩展名", GUILayout.Width(120)))
            {
                extensionsToCheck.Add(".txt");
            }
            GUI.backgroundColor = Color.white;

            EditorGUI.indentLevel--;
        }
    }


    /// <summary>
    /// 绘制窗口的结果部分。
    /// </summary>
    private void DrawResults()
    {
        GUILayout.Space(15);
        GUILayout.Label("结果", headerStyle);

        // 汇总
        EditorGUILayout.BeginVertical("box");
        int totalAllLines = totalCodeLines + totalCommentLines + totalEmptyLines;

        if (totalAllLines > 0)
        {
            DrawResultRow("文件总数:", totalFiles.ToString());
            DrawResultRow("总行数:", totalAllLines.ToString());
            DrawResultRow("代码行:", $"{totalCodeLines} ({(float)totalCodeLines / totalAllLines:P1})");
            DrawResultRow("注释行:", $"{totalCommentLines} ({(float)totalCommentLines / totalAllLines:P1})");
            DrawResultRow("空行:", $"{totalEmptyLines} ({(float)totalEmptyLines / totalAllLines:P1})");
        }
        else
        {
            DrawResultRow("文件总数:", totalFiles.ToString());
            DrawResultRow("总行数:", "0");
            DrawResultRow("代码行:", "0");
            DrawResultRow("注释行:", "0");
            DrawResultRow("空行:", "0");
        }

        EditorGUILayout.EndVertical();

        // 导出选项
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        if (GUILayout.Button("导出为 CSV"))
        {
            string exportPath = EditorUtility.SaveFilePanel("保存统计信息", "", "CodeStatistics", "csv");
            if (!string.IsNullOrEmpty(exportPath))
            {
                ExportCSV(exportPath);
            }
        }
        GUI.backgroundColor = Color.white;
    }


    /// <summary>
    /// 绘制结果汇总中的一行。
    /// </summary>
    /// <param name="label">行的标签。</param>
    /// <param name="value">要显示的值。</param>
    private void DrawResultRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(120));
        EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }


    /// <summary>
    /// 启动分析过程。
    /// </summary>
    private void StartAnalysis()
    {
        // 重置之前的数据
        fileStatsList.Clear();
        totalFiles = 0;
        totalCodeLines = 0;
        totalCommentLines = 0;
        totalEmptyLines = 0;
        isAnalyzing = true;

        // 检查文件夹路径
        if (!ValidateFolder())
        {
            isAnalyzing = false;
            return;
        }

        // 开始异步分析
        EditorApplication.update += AnalysisUpdateLoop;
        analysisState = new AnalysisState();
    }


    /// <summary>
    /// 保存正在进行的分析过程的状态。
    /// </summary>
    private class AnalysisState
    {
        public string[] allFiles;
        public int currentIndex = 0;
        public int processedFiles = 0;
        public int totalFilesToProcess = 0;
        public bool initialized = false;
    }

    private AnalysisState analysisState;
    private const int FILES_PER_FRAME = 10; // 每帧处理这么多文件


    /// <summary>
    /// 用于增量文件分析的编辑器更新循环。
    /// 防止分析期间编辑器无响应。
    /// </summary>
    private void AnalysisUpdateLoop()
    {
        if (!isAnalyzing || analysisState == null)
        {
            EditorApplication.update -= AnalysisUpdateLoop;
            return;
        }

        try
        {
            if (!analysisState.initialized)
            {
                InitializeAnalysis();
                analysisState.initialized = true;
            }

            if (analysisState.currentIndex >= analysisState.allFiles.Length)
            {
                FinishAnalysis();
                return;
            }

            // 处理一批文件
            int filesToProcess = Math.Min(FILES_PER_FRAME, analysisState.allFiles.Length - analysisState.currentIndex);
            for (int i = 0; i < filesToProcess; i++)
            {
                string filePath = analysisState.allFiles[analysisState.currentIndex++];
                FileStatistics stats = AnalyzeFile(filePath);
                fileStatsList.Add(stats);

                totalFiles++;
                totalCodeLines += stats.codeLines;
                totalCommentLines += stats.commentLines;
                totalEmptyLines += stats.emptyLines;

                analysisState.processedFiles++;
            }

            // 更新进度条
            float progress = (float)analysisState.processedFiles / analysisState.totalFilesToProcess;
            bool canceled = EditorUtility.DisplayCancelableProgressBar("正在分析文件",
                $"已分析 {analysisState.processedFiles} / {analysisState.totalFilesToProcess} 个文件...", progress);

            if (canceled)
            {
                FinishAnalysis();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"分析文件时出错：{ex.Message}");
            EditorUtility.DisplayDialog("错误", $"分析文件时出错：{ex.Message}", "确定");
            FinishAnalysis();
        }
    }

    /// <summary>
    /// 通过查找所有待分析文件来初始化分析过程。
    /// </summary>
    private void InitializeAnalysis()
    {
        // 查找所有匹配扩展名的文件
        try
        {
            var allFilesQuery = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                .Where(file => extensionsToCheck.Contains(Path.GetExtension(file).ToLower()));

            if (excludeThirdParty)
            {
                allFilesQuery = allFilesQuery.Where(file => !ShouldExcludeFile(file));
            }

            analysisState.allFiles = allFilesQuery.ToArray();
            analysisState.totalFilesToProcess = analysisState.allFiles.Length;

            if (analysisState.totalFilesToProcess == 0)
            {
                EditorUtility.DisplayDialog("提示", "在所选文件夹中未找到匹配的文件。", "确定");
                FinishAnalysis();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"查找文件时出错：{ex.Message}");
            EditorUtility.DisplayDialog("错误", $"查找文件时出错：{ex.Message}", "确定");
            FinishAnalysis();
        }
    }


    /// <summary>
    /// 完成分析过程并进行清理。
    /// </summary>
    private void FinishAnalysis()
    {
        // 按代码行数排序
        fileStatsList = fileStatsList.OrderByDescending(f => f.codeLines).ToList();

        EditorUtility.ClearProgressBar();
        isAnalyzing = false;
        EditorApplication.update -= AnalysisUpdateLoop;
        Repaint(); // 更新 UI
    }


    /// <summary>
    /// 判断文件是否应被排除在分析之外。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <returns>如果文件应被排除则返回 true，否则返回 false。</returns>
    private bool ShouldExcludeFile(string filePath)
    {
        // 常见的第三方目录
        string[] thirdPartyDirs = new string[]
        {
            "ThirdParty", "Plugins", "External", "Vendor",
            "Packages", "Library", "node_modules"
        };

        foreach (var dir in thirdPartyDirs)
        {
            if (filePath.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar) ||
                filePath.StartsWith(dir + Path.DirectorySeparatorChar))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// 验证所选文件夹是否存在且在项目内。
    /// </summary>
    /// <returns>如果文件夹有效则返回 true，否则返回 false。</returns>
    private bool ValidateFolder()
    {
        // 直接检查目录是否存在
        if (Directory.Exists(rootFolder))
        {
            // 检查路径是否在项目内
            string projectPath = Path.GetDirectoryName(Application.dataPath);

            // 规范化路径以确保一致的比较
            string normalizedRootFolder = Path.GetFullPath(rootFolder).TrimEnd('/', '\\');
            string normalizedProjectPath = Path.GetFullPath(projectPath).TrimEnd('/', '\\');

            // 检查规范化的根文件夹是否以规范化的项目路径开头
            if (!normalizedRootFolder.StartsWith(normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"所选文件夹在当前项目之外：{rootFolder}");
                EditorUtility.DisplayDialog("错误", "只能分析当前项目内的文件夹。", "确定");
                rootFolder = projectPath; // 重置为项目根目录
                return false;
            }

            return true;
        }

        // 项目根路径
        string fullProjectPath = Path.GetDirectoryName(Application.dataPath);

        // 尝试作为项目相对路径
        string fullPath = Path.Combine(fullProjectPath, rootFolder);
        if (Directory.Exists(fullPath))
        {
            rootFolder = fullPath;
            return true;
        }

        // 尝试在 Assets 目录下查找
        if (Directory.Exists(Path.Combine(Application.dataPath, rootFolder)))
        {
            rootFolder = Path.Combine(Application.dataPath, rootFolder);
            return true;
        }

        Debug.LogError($"未找到目录：{rootFolder}");
        EditorUtility.DisplayDialog("错误", $"未找到目录：{rootFolder}", "确定");
        return false;
    }

    /// <summary>
    /// 分析单个文件，统计其代码行数、注释行数和空行数。
    /// </summary>
    /// <param name="filePath">要分析的文件路径。</param>
    /// <returns>包含分析结果的 FileStatistics 对象。</returns>
    private FileStatistics AnalyzeFile(string filePath)
    {
        FileStatistics stats = new FileStatistics
        {
            filePath = GetProjectRelativePath(filePath),
            fileName = Path.GetFileName(filePath),
            extension = Path.GetExtension(filePath).ToLower()
        };

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            stats.totalLines = lines.Length;

            bool inBlockComment = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // 检查空行
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    stats.emptyLines++;
                    continue;
                }

                // 处理多行注释
                if (inBlockComment)
                {
                    stats.commentLines++;
                    if (trimmedLine.Contains("*/"))
                    {
                        inBlockComment = false;
                    }
                    continue;
                }

                // 注释开始
                if (trimmedLine.StartsWith("/*"))
                {
                    stats.commentLines++;
                    if (!trimmedLine.Contains("*/"))
                    {
                        inBlockComment = true;
                    }
                    continue;
                }

                // 单行注释
                if (trimmedLine.StartsWith("//"))
                {
                    stats.commentLines++;
                    continue;
                }

                // 带注释的代码行
                if (trimmedLine.Contains("//"))
                {
                    stats.codeLines++;
                    continue;
                }

                // 普通代码行
                stats.codeLines++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"分析文件时出错：{filePath} - {ex.Message}");
            stats.totalLines = 0;
        }

        return stats;
    }


    /// <summary>
    /// 将分析结果导出为 CSV 文件。
    /// </summary>
    /// <param name="path">CSV 文件的保存路径。</param>
    private void ExportCSV(string path)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                // 写入标题行
                writer.WriteLine("File,Extension,Total Lines,Code Lines,Comment Lines,Empty Lines");

                // 写入汇总行
                writer.WriteLine($"TOTAL,{totalFiles} Files,{totalCodeLines + totalCommentLines + totalEmptyLines},{totalCodeLines},{totalCommentLines},{totalEmptyLines}");

                // 写入文件详情
                foreach (var stats in fileStatsList)
                {
                    writer.WriteLine($"\"{stats.fileName}\",{stats.extension},{stats.totalLines},{stats.codeLines},{stats.commentLines},{stats.emptyLines}");
                }
            }

            Debug.Log($"统计信息已导出至：{path}");

            // 使用默认应用程序打开
            Application.OpenURL("file://" + path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"导出 CSV 时出错：{ex.Message}");
            EditorUtility.DisplayDialog("导出错误", ex.Message, "确定");
        }
    }


    /// <summary>
    /// 将绝对文件路径转换为项目相对路径。
    /// </summary>
    /// <param name="absolutePath">要转换的绝对路径。</param>
    /// <returns>项目相对路径。</returns>
    private string GetProjectRelativePath(string absolutePath)
    {
        if (absolutePath.StartsWith(Application.dataPath))
        {
            return "Assets" + absolutePath.Substring(Application.dataPath.Length);
        }

        // 查找项目根目录
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (absolutePath.StartsWith(projectRoot))
        {
            return absolutePath.Substring(projectRoot.Length + 1); // +1 表示分隔符
        }

        return absolutePath;
    }
}
}