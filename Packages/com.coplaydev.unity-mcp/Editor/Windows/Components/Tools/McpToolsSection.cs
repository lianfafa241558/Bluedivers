using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using MCPForUnity.Editor.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Components.Tools
{
    /// <summary>
    /// Controller for the Tools section inside the MCP for Unity editor window.
    /// Provides discovery, filtering, and per-tool enablement toggles.
    /// Tools are grouped by their Group property (core first, then alphabetical).
    /// </summary>
    public class McpToolsSection
    {
        private readonly Dictionary<string, Toggle> toolToggleMap = new();
        private Toggle projectScopedToolsToggle;
        private Label summaryLabel;
        private Label noteLabel;
        private Button enableAllButton;
        private Button disableAllButton;
        private Button rescanButton;
        private Button reconfigureButton;
        private VisualElement categoryContainer;
        private List<ToolMetadata> allTools = new();
        private readonly Dictionary<string, Toggle> groupToggleMap = new();
        private readonly List<(Foldout foldout, string title, List<ToolMetadata> tools)> foldoutEntries = new();

        /// <summary>Human-friendly names for tool groups shown in the UI.</summary>
        private static readonly Dictionary<string, string> GroupDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "core", "核心工具" },
            { "vfx", "特效与着色器" },
            { "animation", "动画" },
            { "ui", "UI 工具包" },
            { "scripting_ext", "脚本扩展" },
            { "testing", "测试" },
            { "probuilder", "ProBuilder — 实验性" },
            { "profiling", "性能分析与帧调试器" },
            { "asset_gen", "资产生成" },
        };

        public VisualElement Root { get; }

        public McpToolsSection(VisualElement root)
        {
            Root = root;
            CacheUIElements();
            RegisterCallbacks();
        }

        private void CacheUIElements()
        {
            projectScopedToolsToggle = Root.Q<Toggle>("project-scoped-tools-toggle");
            summaryLabel = Root.Q<Label>("tools-summary");
            noteLabel = Root.Q<Label>("tools-note");
            enableAllButton = Root.Q<Button>("enable-all-button");
            disableAllButton = Root.Q<Button>("disable-all-button");
            rescanButton = Root.Q<Button>("rescan-button");
            reconfigureButton = Root.Q<Button>("reconfigure-button");
            categoryContainer = Root.Q<VisualElement>("tool-category-container");
        }

        private void RegisterCallbacks()
        {
            if (projectScopedToolsToggle != null)
            {
                projectScopedToolsToggle.value = EditorPrefs.GetBool(
                    EditorPrefKeys.ProjectScopedToolsLocalHttp,
                    false
                );
                projectScopedToolsToggle.tooltip = "启用后，将以 HTTP 本地与 stdio 传输方式注册项目级工具，允许按项目自定义工具集。";
                projectScopedToolsToggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(EditorPrefKeys.ProjectScopedToolsLocalHttp, evt.newValue);
                });
            }

            if (enableAllButton != null)
            {
                enableAllButton.AddToClassList("tool-action-button");
                enableAllButton.style.marginRight = 4;
                enableAllButton.clicked += () => SetAllToolsState(true);
            }

            if (disableAllButton != null)
            {
                disableAllButton.AddToClassList("tool-action-button");
                disableAllButton.style.marginRight = 4;
                disableAllButton.clicked += () => SetAllToolsState(false);
            }

            if (rescanButton != null)
            {
                rescanButton.AddToClassList("tool-action-button");
                rescanButton.clicked += () =>
                {
                    McpLog.Info("Rescanning MCP tools from the editor window.");
                    MCPServiceLocator.ToolDiscovery.InvalidateCache();
                    Refresh();
                };
            }

            if (reconfigureButton != null)
            {
                reconfigureButton.AddToClassList("tool-action-button");
                reconfigureButton.clicked += OnReconfigureClientsClicked;
            }
        }

        /// <summary>
        /// Rebuilds the tool list and synchronises toggle states.
        /// Tools are displayed in group-based foldouts: core first, then other
        /// groups alphabetically. Custom (non-built-in) tools appear in a
        /// separate "Custom Tools" foldout at the bottom.
        /// </summary>
        public void Refresh()
        {
            toolToggleMap.Clear();
            groupToggleMap.Clear();
            foldoutEntries.Clear();
            categoryContainer?.Clear();

            var service = MCPServiceLocator.ToolDiscovery;
            allTools = service.DiscoverAllTools()
                .OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasTools = allTools.Count > 0;
            enableAllButton?.SetEnabled(hasTools);
            disableAllButton?.SetEnabled(hasTools);

            if (noteLabel != null)
            {
                noteLabel.style.display = hasTools ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasTools)
                {
                    bool isHttp = EditorConfigurationCache.Instance.UseHttpTransport;
                    noteLabel.text = isHttp
                        ? "更改在重新连接或重新注册工具后生效。"
                        : "stdio 模式：开关在启动时同步。修改开关后，请让 AI 执行 manage_tools 的 'sync' 动作以刷新。";
                }
            }

            if (!hasTools)
            {
                AddInfoLabel("未发现 MCP 工具。请为类添加 [McpForUnityTool] 特性以暴露工具。");
                UpdateSummary();
                return;
            }

            // Partition into built-in and custom
            var builtInTools = allTools.Where(IsBuiltIn).ToList();
            var customTools = allTools.Where(tool => !IsBuiltIn(tool)).ToList();

            // Group built-in tools by their Group property
            var grouped = builtInTools
                .GroupBy(t => t.Group ?? "core")
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList());

            // Render "core" first, then remaining groups alphabetically
            if (grouped.TryGetValue("core", out var coreTools))
            {
                BuildCategory(GetGroupDisplayName("core"), "group-core", coreTools);
                grouped.Remove("core");
            }

            foreach (var kvp in grouped.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                BuildCategory(GetGroupDisplayName(kvp.Key), $"group-{kvp.Key}", kvp.Value);
            }

            // Custom tools at the bottom
            if (customTools.Count > 0)
            {
                BuildCategory("自定义工具", "custom", customTools);
            }

            UpdateSummary();
        }

        private static string GetGroupDisplayName(string group)
        {
            if (GroupDisplayNames.TryGetValue(group, out var displayName))
                return displayName;
            // Fallback: capitalize first letter
            return string.IsNullOrEmpty(group)
                ? "其他"
                : char.ToUpper(group[0]) + group.Substring(1);
        }

        private void BuildCategory(string title, string prefsSuffix, IEnumerable<ToolMetadata> tools)
        {
            var toolList = tools.ToList();
            if (toolList.Count == 0)
            {
                return;
            }

            bool isExperimental = string.Equals(prefsSuffix, "group-probuilder", StringComparison.OrdinalIgnoreCase);

            int enabledCount = toolList.Count(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));

            // Default foldout state: core is open, others collapsed
            bool defaultOpen = prefsSuffix == "group-core";
            var foldout = new Foldout
            {
                text = $"{title} ({enabledCount}/{toolList.Count})",
                value = EditorPrefs.GetBool(EditorPrefKeys.ToolFoldoutStatePrefix + prefsSuffix, defaultOpen)
            };

            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != foldout) return;
                EditorPrefs.SetBool(EditorPrefKeys.ToolFoldoutStatePrefix + prefsSuffix, evt.newValue);
            });

            // Add a checkbox into the foldout header to toggle all tools in this group
            bool allEnabled = enabledCount == toolList.Count;
            var groupCheckbox = new Toggle { value = allEnabled };
            groupCheckbox.AddToClassList("group-header-checkbox");
            groupCheckbox.tooltip = $"一键启用或禁用“{title}”中的全部工具。";

            // Prevent the click from propagating to the foldout expand/collapse toggle
            groupCheckbox.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            groupCheckbox.RegisterValueChangedCallback(evt =>
            {
                evt.StopPropagation();
                SetGroupToolsState(toolList, evt.newValue, foldout, title);
            });

            // Insert the checkbox into the foldout's own header toggle element
            foldout.Q<Toggle>()?.Add(groupCheckbox);
            groupToggleMap[prefsSuffix] = groupCheckbox;

            foreach (var tool in toolList)
            {
                foldout.Add(CreateToolRow(tool));
            }

            if (isExperimental)
            {
                var warning = new HelpBox(
                    "ProBuilder 支持为实验性功能。在复杂拓扑上执行网格编辑操作可能产生 " +
                    "意外结果。执行破坏性操作前请务必保存场景。",
                    HelpBoxMessageType.Warning);
                warning.style.marginTop = 4;
                warning.style.marginBottom = 2;
                foldout.Insert(0, warning);
            }

            foldoutEntries.Add((foldout, title, toolList));
            categoryContainer?.Add(foldout);
        }

        private VisualElement CreateToolRow(ToolMetadata tool)
        {
            var row = new VisualElement();
            row.AddToClassList("tool-item");

            var header = new VisualElement();
            header.AddToClassList("tool-item-header");

            var toggle = new Toggle(tool.Name)
            {
                value = MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name)
            };
            toggle.AddToClassList("tool-item-toggle");
            toggle.tooltip = string.IsNullOrWhiteSpace(tool.Description) ? tool.Name : tool.Description;

            toggle.RegisterValueChangedCallback(evt =>
            {
                HandleToggleChange(tool, evt.newValue);
            });

            toolToggleMap[tool.Name] = toggle;
            header.Add(toggle);

            var tagsContainer = new VisualElement();
            tagsContainer.AddToClassList("tool-tags");

            bool defaultEnabled = tool.AutoRegister || tool.IsBuiltIn;
            tagsContainer.Add(CreateTag(defaultEnabled ? "默认启用" : "默认禁用"));

            tagsContainer.Add(CreateTag(tool.StructuredOutput ? "结构化输出" : "自由格式"));

            if (tool.RequiresPolling)
            {
                tagsContainer.Add(CreateTag($"轮询：{tool.PollAction}"));
            }

            header.Add(tagsContainer);
            row.Add(header);

            // Skip auto-generated placeholder descriptions like "Tool: find_gameobjects"
            if (!string.IsNullOrWhiteSpace(tool.Description)
                && !tool.Description.StartsWith("Tool: ", StringComparison.OrdinalIgnoreCase))
            {
                var description = new Label(tool.Description);
                description.AddToClassList("tool-item-description");
                row.Add(description);
            }

            if (tool.Parameters != null && tool.Parameters.Count > 0)
            {
                var paramSummary = string.Join(", ", tool.Parameters.Select(p =>
                    $"{p.Name}{(p.Required ? string.Empty : "（可选）")}: {p.Type}"));

                var parametersLabel = new Label(paramSummary);
                parametersLabel.AddToClassList("tool-parameters");
                row.Add(parametersLabel);
            }

            if (IsManageCameraTool(tool))
            {
                row.Add(CreateManageSceneActions());
            }

            if (IsBatchExecuteTool(tool))
            {
                row.Add(CreateBatchExecuteSettings());
            }

            return row;
        }

        private void HandleToggleChange(
            ToolMetadata tool,
            bool enabled,
            bool updateSummary = true,
            bool reregisterTools = true)
        {
            MCPServiceLocator.ToolDiscovery.SetToolEnabled(tool.Name, enabled);

            if (updateSummary)
            {
                UpdateSummary();
                UpdateFoldoutHeaders();
                SyncGroupToggles();
            }

            if (reregisterTools)
            {
                // Trigger tool reregistration with connected MCP server
                ReregisterToolsAsync();
            }
        }

        private void ReregisterToolsAsync()
        {
            // Fire and forget - don't block UI thread
            var transportManager = MCPServiceLocator.TransportManager;
            var client = transportManager.GetClient(TransportMode.Http);
            if (client == null || !client.IsConnected)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await client.ReregisterToolsAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"Failed to reregister tools: {ex}");
                }
            });
        }

        private void SetAllToolsState(bool enabled)
        {
            bool hasChanges = false;

            foreach (var tool in allTools)
            {
                if (!toolToggleMap.TryGetValue(tool.Name, out var toggle))
                {
                    bool currentEnabled = MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name);
                    if (currentEnabled != enabled)
                    {
                        MCPServiceLocator.ToolDiscovery.SetToolEnabled(tool.Name, enabled);
                        hasChanges = true;
                    }
                    continue;
                }

                if (toggle.value == enabled)
                {
                    continue;
                }

                toggle.SetValueWithoutNotify(enabled);
                HandleToggleChange(tool, enabled, updateSummary: false, reregisterTools: false);
                hasChanges = true;
            }

            UpdateSummary();
            UpdateFoldoutHeaders();
            SyncGroupToggles();

            if (hasChanges)
            {
                // Trigger a single reregistration after bulk change
                ReregisterToolsAsync();
            }
        }

        private void SetGroupToolsState(List<ToolMetadata> groupTools, bool enabled, Foldout foldout, string title)
        {
            bool hasChanges = false;

            foreach (var tool in groupTools)
            {
                if (toolToggleMap.TryGetValue(tool.Name, out var toggle))
                {
                    if (toggle.value != enabled)
                    {
                        toggle.SetValueWithoutNotify(enabled);
                        HandleToggleChange(tool, enabled, updateSummary: false, reregisterTools: false);
                        hasChanges = true;
                    }
                }
                else
                {
                    bool currentEnabled = MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name);
                    if (currentEnabled != enabled)
                    {
                        MCPServiceLocator.ToolDiscovery.SetToolEnabled(tool.Name, enabled);
                        hasChanges = true;
                    }
                }
            }

            // Update the foldout header count
            int enabledCount = groupTools.Count(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));
            foldout.text = $"{title} ({enabledCount}/{groupTools.Count})";

            // Sync global group toggles after group change
            SyncGroupToggles();

            UpdateSummary();

            if (hasChanges)
            {
                ReregisterToolsAsync();
            }
        }

        /// <summary>
        /// Synchronises group toggle checkmarks with actual tool states.
        /// Called after individual tool toggles change so the group toggle
        /// stays accurate.
        /// </summary>
        private void SyncGroupToggles()
        {
            // We need the grouped tool lists to check states.
            var builtInTools = allTools.Where(IsBuiltIn).ToList();
            var grouped = builtInTools
                .GroupBy(t => t.Group ?? "core")
                .ToDictionary(g => g.Key, g => g.ToList());
            var customTools = allTools.Where(t => !IsBuiltIn(t)).ToList();

            foreach (var kvp in groupToggleMap)
            {
                List<ToolMetadata> groupTools;
                if (kvp.Key == "custom")
                {
                    groupTools = customTools;
                }
                else
                {
                    string groupKey = kvp.Key.StartsWith("group-") ? kvp.Key.Substring(6) : kvp.Key;
                    if (!grouped.TryGetValue(groupKey, out groupTools))
                        continue;
                }

                bool allEnabled = groupTools.All(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));
                kvp.Value.SetValueWithoutNotify(allEnabled);
            }
        }

        private void OnReconfigureClientsClicked()
        {
            try
            {
                // Re-register tools with the server (HTTP mode)
                ReregisterToolsAsync();

                // Reconfigure all already-configured clients.
                // For CLI-based clients Configure() is a toggle (unregister if
                // configured, register if not), so we call it twice: first to
                // unregister, then to re-register with the updated tool set.
                var clients = MCPServiceLocator.Client.GetAllClients();
                int success = 0;
                int skipped = 0;
                var messages = new List<string>();

                foreach (var client in clients)
                {
                    try
                    {
                        client.CheckStatus(attemptAutoRewrite: false);

                        if (client.Status != McpStatus.Configured)
                        {
                            skipped++;
                            continue;
                        }

                        if (client is ClaudeCliMcpConfigurator)
                        {
                            // Toggle off (unregister), then toggle on (register)
                            MCPServiceLocator.Client.ConfigureClient(client);
                            MCPServiceLocator.Client.ConfigureClient(client);
                        }
                        else
                        {
                            // JSON-file clients: rewrite is idempotent
                            MCPServiceLocator.Client.ConfigureClient(client);
                        }

                        success++;
                        messages.Add($"✓ {client.DisplayName}：已重新配置");
                    }
                    catch (Exception ex)
                    {
                        messages.Add($"⚠ {client.DisplayName}: {ex.Message}");
                    }
                }

                string header = $"已重新配置 {success} 个客户端，跳过 {skipped} 个。";
                string body = messages.Count > 0
                    ? header + "\n\n" + string.Join("\n", messages)
                    : header;

                EditorUtility.DisplayDialog("重新配置客户端", body, "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("重新配置失败", ex.Message, "确定");
                McpLog.Error($"Reconfigure failed: {ex.Message}");
            }
        }

        private void UpdateSummary()
        {
            if (summaryLabel == null)
            {
                return;
            }

            if (allTools.Count == 0)
            {
                summaryLabel.text = "未发现 MCP 工具。";
                return;
            }

            int enabledCount = allTools.Count(tool => MCPServiceLocator.ToolDiscovery.IsToolEnabled(tool.Name));
            summaryLabel.text = $"将向已连接的客户端注册 {enabledCount} / {allTools.Count} 个工具。";
        }

        private void UpdateFoldoutHeaders()
        {
            foreach (var (foldout, title, tools) in foldoutEntries)
            {
                int enabledCount = tools.Count(t => MCPServiceLocator.ToolDiscovery.IsToolEnabled(t.Name));
                foldout.text = $"{title} ({enabledCount}/{tools.Count})";
            }
        }

        private void AddInfoLabel(string message)
        {
            var label = new Label(message);
            label.AddToClassList("help-text");
            categoryContainer?.Add(label);
        }

        private VisualElement CreateManageSceneActions()
        {
            var actions = new VisualElement();
            actions.AddToClassList("tool-item-actions");

            var gameViewButton = new Button(OnManageSceneScreenshotClicked)
            {
                text = "游戏视图"
            };
            gameViewButton.AddToClassList("tool-action-button");
            gameViewButton.style.marginTop = 4;
            gameViewButton.tooltip = "截取游戏相机画面。默认保存至 Assets/Screenshots（可在“高级”中配置）。";

            var sceneViewButton = new Button(OnSceneViewScreenshotClicked)
            {
                text = "场景视图"
            };
            sceneViewButton.AddToClassList("tool-action-button");
            sceneViewButton.style.marginTop = 4;
            sceneViewButton.style.marginLeft = 4;
            sceneViewButton.tooltip = "截取当前场景视图视口。默认保存至 Assets/Screenshots（可在“高级”中配置）。";

            var multiviewButton = new Button(OnManageSceneMultiviewClicked)
            {
                text = "多视角"
            };
            multiviewButton.AddToClassList("tool-action-button");
            multiviewButton.style.marginTop = 4;
            multiviewButton.style.marginLeft = 4;
            multiviewButton.tooltip = "围绕场景中心生成 6 个角度的拼图。默认保存至 Assets/Screenshots（可在“高级”中配置）。";

            var captureLabel = new Label("截图：");
            captureLabel.style.marginTop = 6;
            captureLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.Add(gameViewButton);
            row.Add(sceneViewButton);
            row.Add(multiviewButton);

            actions.Add(captureLabel);
            actions.Add(row);
            return actions;
        }

        private VisualElement CreateBatchExecuteSettings()
        {
            var container = new VisualElement();
            container.AddToClassList("tool-item-actions");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginTop = 4;

            var label = new Label("每批最大命令数：");
            label.style.marginRight = 8;
            label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            container.Add(label);

            int currentValue = EditorPrefs.GetInt(
                EditorPrefKeys.BatchExecuteMaxCommands,
                BatchExecute.DefaultMaxCommandsPerBatch
            );

            var field = new IntegerField
            {
                value = Math.Clamp(currentValue, 1, BatchExecute.AbsoluteMaxCommandsPerBatch),
                style = { width = 60 }
            };
            field.tooltip = $"每次 batch_execute 调用允许的命令数量（1–{BatchExecute.AbsoluteMaxCommandsPerBatch}）。默认：{BatchExecute.DefaultMaxCommandsPerBatch}。";

            field.RegisterValueChangedCallback(evt =>
            {
                int clamped = Math.Clamp(evt.newValue, 1, BatchExecute.AbsoluteMaxCommandsPerBatch);
                if (clamped != evt.newValue)
                {
                    field.SetValueWithoutNotify(clamped);
                }
                EditorPrefs.SetInt(EditorPrefKeys.BatchExecuteMaxCommands, clamped);
            });

            container.Add(field);

            var hint = new Label($"（上限 {BatchExecute.AbsoluteMaxCommandsPerBatch}）");
            hint.style.marginLeft = 4;
            hint.style.color = new UnityEngine.Color(0.5f, 0.5f, 0.5f);
            hint.style.fontSize = 10;
            container.Add(hint);

            return container;
        }

        private void OnManageSceneScreenshotClicked()
        {
            try
            {
                var response = ManageScene.ExecuteScreenshot();
                if (response is SuccessResponse success && !string.IsNullOrWhiteSpace(success.Message))
                {
                    McpLog.Info(success.Message);
                }
                else if (response is ErrorResponse error && !string.IsNullOrWhiteSpace(error.Error))
                {
                    McpLog.Error(error.Error);
                }
                else
                {
                    McpLog.Info("Screenshot capture requested.");
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to capture screenshot: {ex.Message}");
            }
        }

        private void OnSceneViewScreenshotClicked()
        {
            try
            {
                var response = ManageScene.ExecuteSceneViewScreenshot();
                if (response is SuccessResponse success && !string.IsNullOrWhiteSpace(success.Message))
                {
                    McpLog.Info(success.Message);
                }
                else if (response is ErrorResponse error && !string.IsNullOrWhiteSpace(error.Error))
                {
                    McpLog.Error(error.Error);
                }
                else
                {
                    McpLog.Info("Scene View screenshot capture requested.");
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to capture Scene View screenshot: {ex.Message}");
            }
        }

        private void OnManageSceneMultiviewClicked()
        {
            try
            {
                var response = ManageScene.ExecuteMultiviewScreenshot();
                if (response is SuccessResponse success)
                {
                    // The data object is an anonymous type with imageBase64 — serialize to extract it
                    var json = Newtonsoft.Json.Linq.JObject.FromObject(success.Data);
                    string base64 = json["imageBase64"]?.ToString();
                    if (!string.IsNullOrEmpty(base64))
                    {
                        string folderSpec = MCPForUnity.Editor.Helpers.ScreenshotPreferences.Resolve(null);
                        string folder;
                        try
                        {
                            folder = MCPForUnity.Runtime.Helpers.ScreenshotUtility.ResolveFolderAbsolute(folderSpec);
                        }
                        catch (System.InvalidOperationException ex)
                        {
                            McpLog.Error(ex.Message);
                            return;
                        }
                        if (!System.IO.Directory.Exists(folder))
                            System.IO.Directory.CreateDirectory(folder);

                        string fileName = $"Multiview_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string filePath = System.IO.Path.Combine(folder, fileName);
                        System.IO.File.WriteAllBytes(filePath, Convert.FromBase64String(base64));

                        // Import only when the file landed under Assets/.
                        string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..")).Replace('\\', '/');
                        string normalizedFile = filePath.Replace('\\', '/');
                        string normalizedRoot = projectRoot.EndsWith("/") ? projectRoot : projectRoot + "/";
                        string projectRelative = normalizedFile.StartsWith(normalizedRoot, System.StringComparison.OrdinalIgnoreCase)
                            ? normalizedFile.Substring(normalizedRoot.Length)
                            : normalizedFile;
                        if (MCPForUnity.Runtime.Helpers.ScreenshotUtility.IsUnderAssets(projectRelative))
                            AssetDatabase.ImportAsset(projectRelative, ImportAssetOptions.ForceSynchronousImport);

                        McpLog.Info($"Multiview contact sheet saved to {projectRelative}");
                    }
                    else
                    {
                        McpLog.Info(success.Message ?? "Multiview capture completed.");
                    }
                }
                else if (response is ErrorResponse error && !string.IsNullOrWhiteSpace(error.Error))
                {
                    McpLog.Error(error.Error);
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to capture multiview: {ex.Message}");
            }
        }

        private static Label CreateTag(string text)
        {
            var tag = new Label(text);
            tag.AddToClassList("tool-tag");
            return tag;
        }

        private static bool IsManageSceneTool(ToolMetadata tool) => string.Equals(tool?.Name, "manage_scene", StringComparison.OrdinalIgnoreCase);

        private static bool IsManageCameraTool(ToolMetadata tool) => string.Equals(tool?.Name, "manage_camera", StringComparison.OrdinalIgnoreCase);

        private static bool IsBatchExecuteTool(ToolMetadata tool) => string.Equals(tool?.Name, "batch_execute", StringComparison.OrdinalIgnoreCase);

        private static bool IsBuiltIn(ToolMetadata tool) => tool?.IsBuiltIn ?? false;
    }
}
