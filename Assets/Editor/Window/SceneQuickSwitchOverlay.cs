using System;
using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;

using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Scene 视图工具条上的「快速切场景」按钮元素：纯图标、无文字。
/// <para>点击后弹出 Unity 原生下拉菜单（<see cref="GenericDropdownMenu"/>，与 Unity 自带下拉菜单同样的
/// 深色样式：灰色分组标题 + 当前场景打勾），点选即以 Single 模式打开该场景
/// （打开前询问是否保存当前场景的修改）。</para>
/// </summary>
[EditorToolbarElement(ButtonId, typeof(SceneView))]
public class SceneQuickSwitchButton : EditorToolbarButton
{
    /// <summary>工具条元素 ID，供 <see cref="SceneQuickSwitchOverlay"/> 组装工具条时引用。</summary>
    public const string ButtonId = "RTSClient/SceneQuickSwitch/Button";

    private const string AssetsPrefix = "Assets/";

    public SceneQuickSwitchButton()
    {
        icon = GetSceneIcon();
        tooltip = "快速切场景（Build Settings 中已勾选的场景）";
        clicked += ShowSceneMenu;
    }

    /// <summary>
    /// 取与当前皮肤匹配的场景资源图标（实测 Pro 皮肤返回 <c>d_SceneAsset Icon</c>）。
    /// </summary>
    public static Texture2D GetSceneIcon()
    {
        var sceneIcon = AssetPreview.GetMiniTypeThumbnail(typeof(SceneAsset));
        if (sceneIcon == null)
        {
            var iconName = EditorGUIUtility.isProSkin ? "d_SceneAsset Icon" : "SceneAsset Icon";
            sceneIcon = EditorGUIUtility.IconContent(iconName).image as Texture2D;
        }

        return sceneIcon;
    }

    /// <summary>
    /// 弹出原生下拉菜单：按场景所在目录分组（灰色分组标题），当前场景带勾选标记。
    /// </summary>
    private void ShowSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[SceneQuickSwitch] 运行模式下不允许切换场景。");
            return;
        }

        var menu = new GenericDropdownMenu();
        var activeScenePath = EditorSceneManager.GetActiveScene().path;
        var optionCount = 0;

        var isFirstGroup = true;
        foreach (var group in CollectSceneGroups())
        {
            if (!isFirstGroup)
            {
                menu.AddSeparator(string.Empty);
            }

            isFirstGroup = false;

            // 实测：AddSeparator 传文字不会渲染出来；灰色分组标题必须用 AddDisabledItem（灰字 + 不可点）
            menu.AddDisabledItem(group.Key, false);

            foreach (var scenePath in group.Value)
            {
                var path = scenePath;
                menu.AddItem(Path.GetFileNameWithoutExtension(path), path == activeScenePath, () => SwitchScene(path));
                optionCount++;
            }
        }

        if (optionCount == 0)
        {
            menu.AddDisabledItem("Build Settings 中没有勾选的场景", false);
        }

        menu.DropDown(new Rect(worldBound.xMin, worldBound.yMax, 0f, 0f), this, false);
    }

    /// <summary>
    /// 收集 Build Settings 中已勾选的场景，按所在目录分组（组名去掉 "Assets/" 前缀，组内按名称排序）。
    /// </summary>
    private static List<KeyValuePair<string, List<string>>> CollectSceneGroups()
    {
        var orderedFolders = new List<string>();
        var scenesByFolder = new Dictionary<string, List<string>>();

        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled)
            {
                continue;
            }

            var scenePath = buildScene.path;
            if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(scenePath)))
            {
                continue;
            }

            var folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/') ?? string.Empty;
            if (!scenesByFolder.TryGetValue(folder, out var scenes))
            {
                scenes = new List<string>();
                scenesByFolder[folder] = scenes;
                orderedFolders.Add(folder);
            }

            scenes.Add(scenePath);
        }

        var groups = new List<KeyValuePair<string, List<string>>>(orderedFolders.Count);
        foreach (var folder in orderedFolders)
        {
            var scenes = scenesByFolder[folder];
            scenes.Sort();

            var title = folder.StartsWith(AssetsPrefix, StringComparison.Ordinal)
                ? folder.Substring(AssetsPrefix.Length)
                : folder;

            groups.Add(new KeyValuePair<string, List<string>>(string.IsNullOrEmpty(title) ? "项目根目录" : title, scenes));
        }

        return groups;
    }

    /// <summary>
    /// 保存当前场景修改后以 Single 模式打开目标场景；目标已是当前场景时直接忽略。
    /// </summary>
    private static void SwitchScene(string scenePath)
    {
        if (EditorSceneManager.GetActiveScene().path == scenePath)
        {
            return;
        }

        // 用户取消保存 → 中止切换
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[SceneQuickSwitch] 打开场景失败：{scenePath}");
        }
    }
}

/// <summary>
/// 承载 <see cref="SceneQuickSwitchButton"/> 的 Scene 视图工具条 Overlay。
/// <para>必须用 <see cref="ToolbarOverlay"/> 而不是普通 <see cref="Overlay"/>：普通 Overlay 停靠在
/// 工具条里会先折叠成一个「显示名按钮」，要点一次才展开内容；ToolbarOverlay 的元素是直接排在
/// 工具条上的（Unity 自带的 2D / 轴心 / 画刷等就是这么做出来的）。</para>
/// </summary>
[Overlay(typeof(SceneView), OverlayId,
    displayName = "快速切场景",
    defaultDisplay = true,
    defaultLayout = Layout.HorizontalToolbar,
    defaultDockZone = DockZone.TopToolbar)]
public class SceneQuickSwitchOverlay : ToolbarOverlay
{
    private const string OverlayId = "rts-quick-scene-switch";

    private bool _expanding;

    public SceneQuickSwitchOverlay()
        : base(SceneQuickSwitchButton.ButtonId)
    {
    }

    public override void OnCreated()
    {
        base.OnCreated();

        // 万一真的被折叠成单个条目，也显示场景图标而不是名称首字
        collapsedIcon = SceneQuickSwitchButton.GetSceneIcon();

        collapsedChanged += OnCollapsedChanged;

        // ⚠️ 折叠位（collapsed）会被编辑器布局持久化，并且是在 OnCreated 之后才恢复成 true 的
        // → 这里先立刻展开，布局恢复完成后再延迟一帧纠正一次
        Expand();
        EditorApplication.delayCall += Expand;
    }

    public override void OnWillBeDestroyed()
    {
        EditorApplication.delayCall -= Expand;
        collapsedChanged -= OnCollapsedChanged;

        base.OnWillBeDestroyed();
    }

    private void OnCollapsedChanged(bool collapsedValue)
    {
        Expand();
    }

    /// <summary>
    /// 强制展开：展开态下 <see cref="SceneQuickSwitchButton"/> 才会作为按钮条直接显示在工具条上，
    /// 而不是折叠成一个写着「快速切场景」的条目（点一次才展开）。
    /// </summary>
    private void Expand()
    {
        if (_expanding || !collapsed || containerWindow == null)
        {
            return;
        }

        _expanding = true;
        try
        {
            collapsed = false;
        }
        finally
        {
            _expanding = false;
        }
    }
}
