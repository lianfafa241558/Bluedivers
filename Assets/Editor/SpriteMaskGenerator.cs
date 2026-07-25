using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 空投图标遮罩生成器：将 Airdrop 下的图片按颜色规则转换为遮罩纹理，
/// 输出到 Airdrop/Test 目录。
/// 规则：非白色（红黄绿橙等）→ R=1，白色 → G=1，B=0，透明 → 保持透明。
/// </summary>
public class SpriteMaskGenerator : EditorWindow
{
    private const string SourceDir = "Assets/Resources/GameData/Airdrop";
    private const string OutputDir = "Assets/Resources/GameData/Airdrop/Test";

    private readonly List<string> _pngPaths = new List<string>();
    private readonly List<string> _pngNames = new List<string>();
    private int _selectedIndex = -1;
    private Vector2 _scrollPos;
    private bool _isProcessing;

    [MenuItem("Tools/Sprite遮罩生成器")]
    private static void Open()
    {
        var window = GetWindow<SpriteMaskGenerator>();
        window.titleContent = new GUIContent("Sprite遮罩生成器");
        window.minSize = new Vector2(350, 400);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshPngList();
    }

    private void RefreshPngList()
    {
        _pngPaths.Clear();
        _pngNames.Clear();

        if (!Directory.Exists(SourceDir))
        {
            return;
        }

        string[] files = Directory.GetFiles(SourceDir, "*.png", SearchOption.TopDirectoryOnly);
        foreach (string file in files)
        {
            string assetPath = file.Replace("\\", "/");
            _pngPaths.Add(assetPath);
            _pngNames.Add(Path.GetFileNameWithoutExtension(file));
        }

        if (_selectedIndex >= _pngNames.Count)
        {
            _selectedIndex = _pngNames.Count > 0 ? 0 : -1;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("刷新列表", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            RefreshPngList();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (_pngNames.Count == 0)
        {
            EditorGUILayout.HelpBox($"未在 {SourceDir} 下找到 PNG 图片文件。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"可选图片 ({_pngNames.Count} 张)：", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("选中图片：", GUILayout.Width(70));
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField(_selectedIndex >= 0 ? _pngNames[_selectedIndex] : "（未选择）");
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // 列表区域
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
        for (int i = 0; i < _pngNames.Count; i++)
        {
            Rect rowRect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 高亮选中行
            if (i == _selectedIndex)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.5f, 1f, 0.2f));
            }

            EditorGUILayout.LabelField(_pngNames[i], GUILayout.ExpandWidth(true));

            // 预览小图
            Texture2D preview = AssetDatabase.LoadAssetAtPath<Texture2D>(_pngPaths[i]);
            if (preview != null)
            {
                GUILayout.Label(preview, GUILayout.Width(32), GUILayout.Height(32));
            }

            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                _selectedIndex = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // 操作区域
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = _selectedIndex >= 0 && !_isProcessing;

        if (GUILayout.Button("转换选中图片", GUILayout.Height(40)))
        {
            ConvertSelected();
        }

        GUI.enabled = _pngNames.Count > 0 && !_isProcessing;

        if (GUILayout.Button("批量转换全部", GUILayout.Height(40)))
        {
            ConvertAll();
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (_isProcessing)
        {
            EditorGUILayout.HelpBox("正在处理中，请稍候...", MessageType.Info);
        }
    }

    private void ConvertSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _pngPaths.Count)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            ConvertSingle(_pngPaths[_selectedIndex]);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("完成", $"已生成：{_pngNames[_selectedIndex]}_Mask.png", "确定");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void ConvertAll()
    {
        _isProcessing = true;
        try
        {
            int successCount = 0;
            for (int i = 0; i < _pngPaths.Count; i++)
            {
                try
                {
                    ConvertSingle(_pngPaths[i]);
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"转换 {_pngNames[i]} 失败：{ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("完成", $"批量转换完成，成功 {successCount}/{_pngPaths.Count} 张。", "确定");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 转换单张图片：非白色 → R=1，白色 → G=1，B=0，透明保持透明。
    /// </summary>
    private void ConvertSingle(string sourceAssetPath)
    {
        string sourceFileName = Path.GetFileNameWithoutExtension(sourceAssetPath);
        string outputAssetPath = $"{OutputDir}/{sourceFileName}_Mask.png";

        // 读取源纹理（需要设置为可读）
        Texture2D sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);
        if (sourceTex == null)
        {
            Debug.LogError($"无法加载纹理：{sourceAssetPath}");
            return;
        }

        Texture2D readableTex = GetReadableTexture(sourceTex);

        int width = readableTex.width;
        int height = readableTex.height;
        Color[] pixels = readableTex.GetPixels();
        Color[] outputPixels = new Color[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];

            // 透明像素保持透明
            if (c.a < 0.01f)
            {
                outputPixels[i] = new Color(0f, 0f, 0f, 0f);
                continue;
            }

            // 判断是否为白色（RGB 都接近 1）
            bool isWhite = IsWhitePixel(c);

            if (isWhite)
            {
                // 白色 → G=1, R=0, B=0
                outputPixels[i] = new Color(0f, 1f, 0f, 1f);
            }
            else
            {
                // 非白色（红黄绿橙等）→ R=1, G=0, B=0
                outputPixels[i] = new Color(1f, 0f, 0f, 1f);
            }
        }

        // 创建输出纹理
        Texture2D outputTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        outputTex.SetPixels(outputPixels);
        outputTex.Apply();

        // 确保输出目录存在
        string outputDir = Path.GetDirectoryName(outputAssetPath);
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 编码为 PNG 并写入文件
        byte[] pngData = outputTex.EncodeToPNG();
        File.WriteAllBytes(outputAssetPath, pngData);

        // 清理临时纹理
        DestroyImmediate(readableTex);
        DestroyImmediate(outputTex);

        Debug.Log($"已生成遮罩：{outputAssetPath}");
    }

    /// <summary>
    /// 判断像素是否为白色（RGB 通道都接近 1.0）。
    /// </summary>
    private static bool IsWhitePixel(Color c)
    {
        const float Threshold = 0.9f;
        return c.r > Threshold && c.g > Threshold && c.b > Threshold;
    }

    /// <summary>
    /// 获取可读取像素的纹理副本。
    /// 通过 RenderTexture 方式获取，避免源纹理 Read/Write 设置问题。
    /// </summary>
    private static Texture2D GetReadableTexture(Texture2D source)
    {
        RenderTexture renderTex = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(source, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);

        return readable;
    }
}
