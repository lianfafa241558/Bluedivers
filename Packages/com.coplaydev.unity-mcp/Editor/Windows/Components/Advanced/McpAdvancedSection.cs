using System;
using System.IO;
using System.Runtime.InteropServices;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Components.Advanced
{
    /// <summary>
    /// Controller for the Advanced Settings section.
    /// Handles path overrides, server source configuration, dev mode, and package deployment.
    /// </summary>
    public class McpAdvancedSection
    {
        // UI Elements
        private TextField uvxPathOverride;
        private Button browseUvxButton;
        private Button clearUvxButton;
        private VisualElement uvxPathStatus;
        private TextField gitUrlOverride;
        private Button browseGitUrlButton;
        private Button clearGitUrlButton;
        private Toggle autoStartOnLoadToggle;
        private Toggle debugLogsToggle;
        private Toggle logRecordToggle;
        private Toggle devModeForceRefreshToggle;
        private Toggle allowLanHttpBindToggle;
        private Toggle allowInsecureRemoteHttpToggle;
        private TextField screenshotsFolderOverride;
        private Button browseScreenshotsFolderButton;
        private Button clearScreenshotsFolderButton;
        private TextField deploySourcePath;
        private Button browseDeploySourceButton;
        private Button clearDeploySourceButton;
        private Button deployButton;
        private Button deployRestoreButton;
        private Label deployTargetLabel;
        private Label deployBackupLabel;
        private Label deployStatusLabel;
        private VisualElement healthIndicator;
        private Label healthStatus;
        private Button testConnectionButton;

        // Events
        public event Action OnGitUrlChanged;
        public event Action OnHttpServerCommandUpdateRequested;
        public event Action OnTestConnectionRequested;
        public event Action OnPackageDeployed;

        public VisualElement Root { get; private set; }

        public McpAdvancedSection(VisualElement root)
        {
            Root = root;
            CacheUIElements();
            InitializeUI();
            RegisterCallbacks();
        }

        private void CacheUIElements()
        {
            uvxPathOverride = Root.Q<TextField>("uv-path-override");
            browseUvxButton = Root.Q<Button>("browse-uv-button");
            clearUvxButton = Root.Q<Button>("clear-uv-button");
            uvxPathStatus = Root.Q<VisualElement>("uv-path-status");
            gitUrlOverride = Root.Q<TextField>("git-url-override");
            browseGitUrlButton = Root.Q<Button>("browse-git-url-button");
            clearGitUrlButton = Root.Q<Button>("clear-git-url-button");
            autoStartOnLoadToggle = Root.Q<Toggle>("auto-start-on-load-toggle");
            debugLogsToggle = Root.Q<Toggle>("debug-logs-toggle");
            logRecordToggle = Root.Q<Toggle>("log-record-toggle");
            devModeForceRefreshToggle = Root.Q<Toggle>("dev-mode-force-refresh-toggle");
            allowLanHttpBindToggle = Root.Q<Toggle>("allow-lan-http-bind-toggle");
            allowInsecureRemoteHttpToggle = Root.Q<Toggle>("allow-insecure-remote-http-toggle");
            screenshotsFolderOverride = Root.Q<TextField>("screenshots-folder-override");
            browseScreenshotsFolderButton = Root.Q<Button>("browse-screenshots-folder-button");
            clearScreenshotsFolderButton = Root.Q<Button>("clear-screenshots-folder-button");
            deploySourcePath = Root.Q<TextField>("deploy-source-path");
            browseDeploySourceButton = Root.Q<Button>("browse-deploy-source-button");
            clearDeploySourceButton = Root.Q<Button>("clear-deploy-source-button");
            deployButton = Root.Q<Button>("deploy-button");
            deployRestoreButton = Root.Q<Button>("deploy-restore-button");
            deployTargetLabel = Root.Q<Label>("deploy-target-label");
            deployBackupLabel = Root.Q<Label>("deploy-backup-label");
            deployStatusLabel = Root.Q<Label>("deploy-status-label");
            healthIndicator = Root.Q<VisualElement>("health-indicator");
            healthStatus = Root.Q<Label>("health-status");
            testConnectionButton = Root.Q<Button>("test-connection-button");
        }

        private void InitializeUI()
        {
            // Set tooltips for fields
            if (uvxPathOverride != null)
                uvxPathOverride.tooltip = "覆盖 uvx 可执行文件路径。留空则自动检测。";
            if (gitUrlOverride != null)
                gitUrlOverride.tooltip = "覆盖 uvx --from 使用的服务器来源。留空则使用默认 PyPI 包。本地开发示例：/path/to/unity-mcp/Server";
            if (debugLogsToggle != null)
            {
                debugLogsToggle.tooltip = "向 Unity 控制台输出详细调试日志。";
                var debugLabel = debugLogsToggle?.parent?.Q<Label>();
                if (debugLabel != null)
                    debugLabel.tooltip = debugLogsToggle.tooltip;
            }
            if (logRecordToggle != null)
            {
                logRecordToggle.tooltip = "将每次 MCP 工具执行（工具、动作、状态、耗时）记录到 Assets/UnityMCP/Log/mcp.log。";
                var logRecordLabel = logRecordToggle?.parent?.Q<Label>();
                if (logRecordLabel != null)
                    logRecordLabel.tooltip = logRecordToggle.tooltip;
            }
            if (devModeForceRefreshToggle != null)
            {
                devModeForceRefreshToggle.tooltip = "启用后，生成的 uvx 命令会在启动前加上 '--no-cache --refresh'（启动较慢，但可避免调试服务器时使用过期的缓存构建）。";
                var forceRefreshLabel = devModeForceRefreshToggle?.parent?.Q<Label>();
                if (forceRefreshLabel != null)
                    forceRefreshLabel.tooltip = devModeForceRefreshToggle.tooltip;
            }
            if (allowLanHttpBindToggle != null)
            {
                allowLanHttpBindToggle.tooltip = "允许 HTTP 本地绑定所有网卡（0.0.0.0 / ::）。默认禁用，因为局域网内的设备可能访问到 MCP 工具。";
                var lanBindLabel = allowLanHttpBindToggle?.parent?.Q<Label>();
                if (lanBindLabel != null)
                    lanBindLabel.tooltip = allowLanHttpBindToggle.tooltip;
            }
            if (allowInsecureRemoteHttpToggle != null)
            {
                allowInsecureRemoteHttpToggle.tooltip = "允许通过明文 http/ws 使用 HTTP 远程。默认禁用，以要求 HTTPS/WSS。";
                var insecureRemoteLabel = allowInsecureRemoteHttpToggle?.parent?.Q<Label>();
                if (insecureRemoteLabel != null)
                    insecureRemoteLabel.tooltip = allowInsecureRemoteHttpToggle.tooltip;
            }
            if (testConnectionButton != null)
                testConnectionButton.tooltip = "测试 Unity 与 MCP 服务器之间的连接。";
            if (screenshotsFolderOverride != null)
            {
                screenshotsFolderOverride.tooltip = "manage_camera / manage_ui 截图默认保存目录。" +
                    "使用项目相对路径（例如 'Assets/Screenshots' 或 'Captures'）。留空 = 内置默认（Assets/Screenshots）。" +
                    "每次调用中的 'output_folder' 参数始终优先。";
                screenshotsFolderOverride.SetValueWithoutNotify(ScreenshotPreferences.DefaultFolder);
            }
            if (browseScreenshotsFolderButton != null)
                browseScreenshotsFolderButton.tooltip = "选择项目内的文件夹；路径将以项目相对路径保存。";
            if (clearScreenshotsFolderButton != null)
                clearScreenshotsFolderButton.tooltip = "清除覆盖并恢复内置默认（Assets/Screenshots）。";
            if (deploySourcePath != null)
                deploySourcePath.tooltip = "将 MCPForUnity 文件夹复制到本项目的包目录。";

            // Set tooltips for buttons
            if (browseUvxButton != null)
                browseUvxButton.tooltip = "浏览 uvx 可执行文件";
            if (clearUvxButton != null)
                clearUvxButton.tooltip = "清除覆盖并使用自动检测";
            if (browseGitUrlButton != null)
                browseGitUrlButton.tooltip = "选择本地服务器来源文件夹";
            if (clearGitUrlButton != null)
                clearGitUrlButton.tooltip = "清除覆盖并使用默认 PyPI 包";
            if (browseDeploySourceButton != null)
                browseDeploySourceButton.tooltip = "选择 MCPForUnity 来源文件夹";
            if (clearDeploySourceButton != null)
                clearDeploySourceButton.tooltip = "清除部署来源路径";
            if (deployButton != null)
                deployButton.tooltip = "将 MCPForUnity 复制到本项目的包目录";
            if (deployRestoreButton != null)
                deployRestoreButton.tooltip = "还原部署前的最近一次备份";

            if (autoStartOnLoadToggle != null)
            {
                autoStartOnLoadToggle.tooltip = "Unity 编辑器打开时自动启动本地 HTTP 服务器并连接 MCP 桥接。仅适用于 HTTP 传输（stdio 始终自动启动）。";
                var autoStartLabel = autoStartOnLoadToggle.parent?.Q<Label>();
                if (autoStartLabel != null)
                    autoStartLabel.tooltip = autoStartOnLoadToggle.tooltip;
                autoStartOnLoadToggle.SetValueWithoutNotify(EditorPrefs.GetBool(EditorPrefKeys.AutoStartOnLoad, false));
            }

            gitUrlOverride.value = EditorPrefs.GetString(EditorPrefKeys.GitUrlOverride, "");

            bool debugEnabled = EditorPrefs.GetBool(EditorPrefKeys.DebugLogs, false);
            debugLogsToggle.value = debugEnabled;
            McpLog.SetDebugLoggingEnabled(debugEnabled);

            if (logRecordToggle != null)
                logRecordToggle.value = McpLogRecord.IsEnabled;

            devModeForceRefreshToggle.value = EditorPrefs.GetBool(EditorPrefKeys.DevModeForceServerRefresh, false);
            if (allowLanHttpBindToggle != null)
            {
                allowLanHttpBindToggle.SetValueWithoutNotify(EditorPrefs.GetBool(EditorPrefKeys.AllowLanHttpBind, false));
            }
            if (allowInsecureRemoteHttpToggle != null)
            {
                allowInsecureRemoteHttpToggle.SetValueWithoutNotify(EditorPrefs.GetBool(EditorPrefKeys.AllowInsecureRemoteHttp, false));
            }
            UpdatePathOverrides();
            UpdateDeploymentSection();
        }

        private void RegisterCallbacks()
        {
            browseUvxButton.clicked += OnBrowseUvxClicked;
            clearUvxButton.clicked += OnClearUvxClicked;
            browseGitUrlButton.clicked += OnBrowseGitUrlClicked;

            gitUrlOverride.RegisterValueChangedCallback(evt =>
            {
                string url = evt.newValue?.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    EditorPrefs.DeleteKey(EditorPrefKeys.GitUrlOverride);
                }
                else
                {
                    url = ResolveServerPath(url);
                    // Update the text field if the path was auto-corrected, without re-triggering the callback
                    if (url != evt.newValue?.Trim())
                    {
                        gitUrlOverride.SetValueWithoutNotify(url);
                    }
                    EditorPrefs.SetString(EditorPrefKeys.GitUrlOverride, url);
                }
                OnGitUrlChanged?.Invoke();
                OnHttpServerCommandUpdateRequested?.Invoke();
            });

            clearGitUrlButton.clicked += () =>
            {
                gitUrlOverride.value = string.Empty;
                EditorPrefs.DeleteKey(EditorPrefKeys.GitUrlOverride);
                OnGitUrlChanged?.Invoke();
                OnHttpServerCommandUpdateRequested?.Invoke();
            };

            debugLogsToggle.RegisterValueChangedCallback(evt =>
            {
                McpLog.SetDebugLoggingEnabled(evt.newValue);
            });

            if (logRecordToggle != null)
            {
                logRecordToggle.RegisterValueChangedCallback(evt =>
                {
                    McpLogRecord.IsEnabled = evt.newValue;
                });
            }

            if (autoStartOnLoadToggle != null)
            {
                autoStartOnLoadToggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(EditorPrefKeys.AutoStartOnLoad, evt.newValue);
                });
            }

            devModeForceRefreshToggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(EditorPrefKeys.DevModeForceServerRefresh, evt.newValue);
                OnHttpServerCommandUpdateRequested?.Invoke();
            });

            if (allowLanHttpBindToggle != null)
            {
                allowLanHttpBindToggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(EditorPrefKeys.AllowLanHttpBind, evt.newValue);
                    OnHttpServerCommandUpdateRequested?.Invoke();
                });
            }

            if (allowInsecureRemoteHttpToggle != null)
            {
                allowInsecureRemoteHttpToggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool(EditorPrefKeys.AllowInsecureRemoteHttp, evt.newValue);
                    OnHttpServerCommandUpdateRequested?.Invoke();
                });
            }

            deploySourcePath.RegisterValueChangedCallback(evt =>
            {
                string path = evt.newValue?.Trim();
                if (string.IsNullOrEmpty(path) || path == "Not set")
                {
                    return;
                }

                try
                {
                    MCPServiceLocator.Deployment.SetStoredSourcePath(path);
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("来源无效", ex.Message, "确定");
                    UpdateDeploymentSection();
                }
            });

            if (screenshotsFolderOverride != null)
            {
                screenshotsFolderOverride.RegisterValueChangedCallback(evt =>
                {
                    ScreenshotPreferences.DefaultFolder = evt.newValue;
                });
            }
            if (browseScreenshotsFolderButton != null)
            {
                browseScreenshotsFolderButton.clicked += OnBrowseScreenshotsFolderClicked;
            }
            if (clearScreenshotsFolderButton != null)
            {
                clearScreenshotsFolderButton.clicked += () =>
                {
                    ScreenshotPreferences.DefaultFolder = string.Empty;
                    screenshotsFolderOverride?.SetValueWithoutNotify(string.Empty);
                };
            }

            browseDeploySourceButton.clicked += OnBrowseDeploySourceClicked;
            clearDeploySourceButton.clicked += OnClearDeploySourceClicked;
            deployButton.clicked += OnDeployClicked;
            deployRestoreButton.clicked += OnRestoreBackupClicked;
            testConnectionButton.clicked += () => OnTestConnectionRequested?.Invoke();
        }

        public void UpdatePathOverrides()
        {
            var pathService = MCPServiceLocator.Paths;

            bool hasOverride = pathService.HasUvxPathOverride;
            bool hasFallback = pathService.HasUvxPathFallback;
            string uvxPath = hasOverride ? pathService.GetUvxPath() : null;

            // Determine display text based on override and fallback status
            if (hasOverride)
            {
                if (hasFallback)
                {
                    // Override path invalid, using system fallback
                    string overridePath = EditorPrefs.GetString(EditorPrefKeys.UvxPathOverride, string.Empty);
                    uvxPathOverride.value = $"无效的覆盖路径：{overridePath}（回退到 uvx 路径）{uvxPath}";
                }
                else if (!string.IsNullOrEmpty(uvxPath))
                {
                    // Override path valid
                    uvxPathOverride.value = uvxPath;
                }
                else
                {
                    // Override set but invalid, no fallback available
                    string overridePath = EditorPrefs.GetString(EditorPrefKeys.UvxPathOverride, string.Empty);
                    uvxPathOverride.value = $"无效的覆盖路径：{overridePath}，未找到 uv";
                }
            }
            else
            {
                uvxPathOverride.value = "uvx（使用 PATH）";
            }

            uvxPathStatus.RemoveFromClassList("valid");
            uvxPathStatus.RemoveFromClassList("invalid");
            uvxPathStatus.RemoveFromClassList("warning");

            if (hasOverride)
            {
                if (hasFallback)
                {
                    // Using fallback - show as warning (yellow)
                    uvxPathStatus.AddToClassList("warning");
                }
                else
                {
                    // Override mode: validate the override path
                    string overridePath = EditorPrefs.GetString(EditorPrefKeys.UvxPathOverride, string.Empty);
                    if (pathService.TryValidateUvxExecutable(overridePath, out _))
                    {
                        uvxPathStatus.AddToClassList("valid");
                    }
                    else
                    {
                        uvxPathStatus.AddToClassList("invalid");
                    }
                }
            }
            else
            {
                // PATH mode: validate system uvx
                string systemUvxPath = pathService.GetUvxPath();
                if (!string.IsNullOrEmpty(systemUvxPath) && pathService.TryValidateUvxExecutable(systemUvxPath, out _))
                {
                    uvxPathStatus.AddToClassList("valid");
                }
                else
                {
                    uvxPathStatus.AddToClassList("invalid");
                }
            }

            gitUrlOverride.value = EditorPrefs.GetString(EditorPrefKeys.GitUrlOverride, "");
            if (autoStartOnLoadToggle != null)
                autoStartOnLoadToggle.value = EditorPrefs.GetBool(EditorPrefKeys.AutoStartOnLoad, false);
            debugLogsToggle.value = EditorPrefs.GetBool(EditorPrefKeys.DebugLogs, false);
            if (logRecordToggle != null)
                logRecordToggle.value = McpLogRecord.IsEnabled;
            devModeForceRefreshToggle.value = EditorPrefs.GetBool(EditorPrefKeys.DevModeForceServerRefresh, false);
            if (allowLanHttpBindToggle != null)
            {
                allowLanHttpBindToggle.value = EditorPrefs.GetBool(EditorPrefKeys.AllowLanHttpBind, false);
            }
            if (allowInsecureRemoteHttpToggle != null)
            {
                allowInsecureRemoteHttpToggle.value = EditorPrefs.GetBool(EditorPrefKeys.AllowInsecureRemoteHttp, false);
            }
            UpdateDeploymentSection();
        }

        private void OnBrowseUvxClicked()
        {
            string suggested = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "/opt/homebrew/bin"
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string picked = EditorUtility.OpenFilePanel("选择 uv 可执行文件", suggested, "");
            if (!string.IsNullOrEmpty(picked))
            {
                try
                {
                    MCPServiceLocator.Paths.SetUvxPathOverride(picked);
                    UpdatePathOverrides();
                    McpLog.Info($"uv path override set to: {picked}");
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("路径无效", ex.Message, "确定");
                }
            }
        }

        private void OnClearUvxClicked()
        {
            MCPServiceLocator.Paths.ClearUvxPathOverride();
            UpdatePathOverrides();
            McpLog.Info("uv path override cleared");
        }

        private void OnBrowseGitUrlClicked()
        {
            string picked = EditorUtility.OpenFolderPanel("选择服务器文件夹（包含 pyproject.toml）", string.Empty, string.Empty);
            if (!string.IsNullOrEmpty(picked))
            {
                picked = ResolveServerPath(picked);
                gitUrlOverride.value = picked;
                EditorPrefs.SetString(EditorPrefKeys.GitUrlOverride, picked);
                OnGitUrlChanged?.Invoke();
                OnHttpServerCommandUpdateRequested?.Invoke();
                McpLog.Info($"Server source override set to: {picked}");
            }
        }

        /// <summary>
        /// Validates and auto-corrects a local server path to ensure it points to the directory
        /// containing pyproject.toml (the Python package root). If the user selects a parent
        /// directory (e.g. the repo root), this checks for a "Server" subdirectory with
        /// pyproject.toml and returns that instead.
        /// </summary>
        private static string ResolveServerPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // If path is not a local filesystem path, return as-is (git URLs, PyPI refs, etc.)
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("git+", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            // Strip file:// prefix for filesystem checks, but preserve it for the return value
            string checkPath = path;
            string prefix = string.Empty;
            if (checkPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "file://";
                checkPath = checkPath.Substring(7);
            }

            // Already points to a directory with pyproject.toml — correct path
            if (File.Exists(Path.Combine(checkPath, "pyproject.toml")))
            {
                return path;
            }

            // Check if "Server" subdirectory contains pyproject.toml (common repo structure)
            string serverSubDir = Path.Combine(checkPath, "Server");
            if (File.Exists(Path.Combine(serverSubDir, "pyproject.toml")))
            {
                string corrected = prefix + serverSubDir;
                McpLog.Info($"Auto-corrected server path to 'Server' subdirectory: {corrected}");
                return corrected;
            }

            // Return as-is; uvx will report the error if the path is invalid
            return path;
        }

        private void UpdateDeploymentSection()
        {
            var deployService = MCPServiceLocator.Deployment;

            string sourcePath = deployService.GetStoredSourcePath();
            deploySourcePath.value = sourcePath ?? string.Empty;

            deployTargetLabel.text = $"目标：{deployService.GetTargetDisplayPath()}";

            string backupPath = deployService.GetLastBackupPath();
            if (deployService.HasBackup())
            {
                // Use forward slashes to avoid backslash escape sequence issues in UI text
                deployBackupLabel.text = $"上次备份：{backupPath?.Replace('\\', '/')}";
            }
            else
            {
                deployBackupLabel.text = "上次备份：无";
            }

            deployRestoreButton?.SetEnabled(deployService.HasBackup());
        }

        private void OnBrowseScreenshotsFolderClicked()
        {
            // Start the picker at the project's Assets/ since that's the most common target.
            string startDir = UnityEngine.Application.dataPath;
            string picked = EditorUtility.OpenFolderPanel("选择截图文件夹（位于本项目内）", startDir, string.Empty);
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")).Replace('\\', '/');
            string normalizedRoot = projectRoot.EndsWith("/") ? projectRoot : projectRoot + "/";
            string normalizedPicked = picked.Replace('\\', '/');

            if (normalizedPicked.Equals(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                // Storing "" would wipe the EditorPrefs key (= "unset"), so reject the project
                // root rather than silently revert the override the user just chose.
                EditorUtility.DisplayDialog(
                    "请选择子文件夹",
                    "请选择项目内的子文件夹（例如 'Assets/Screenshots' 或 'Captures'）。" +
                    "选择项目根目录会把截图混入项目文件中。",
                    "确定");
                return;
            }

            if (!normalizedPicked.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "文件夹在项目之外",
                    $"所选文件夹位于 Unity 项目根目录之外。\n\n已选：{normalizedPicked}\n项目：{projectRoot}\n\n请选择项目内的文件夹。",
                    "确定");
                return;
            }

            string projectRelative = normalizedPicked.Substring(normalizedRoot.Length);

            ScreenshotPreferences.DefaultFolder = projectRelative;
            screenshotsFolderOverride?.SetValueWithoutNotify(projectRelative);
            McpLog.Info($"Default screenshots folder set to '{projectRelative}'.");
        }

        private void OnBrowseDeploySourceClicked()
        {
            string picked = EditorUtility.OpenFolderPanel("选择 MCPForUnity 文件夹", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            try
            {
                MCPServiceLocator.Deployment.SetStoredSourcePath(picked);
                SetDeployStatus($"已设置来源：{picked}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("来源无效", ex.Message, "确定");
                SetDeployStatus("来源选择失败");
            }

            UpdateDeploymentSection();
        }

        private void OnClearDeploySourceClicked()
        {
            MCPServiceLocator.Deployment.ClearStoredSourcePath();
            UpdateDeploymentSection();
            SetDeployStatus("已清除来源");
        }

        private void OnDeployClicked()
        {
            var result = MCPServiceLocator.Deployment.DeployFromStoredSource();
            SetDeployStatus(result.Message, !result.Success);

            if (!result.Success)
            {
                EditorUtility.DisplayDialog("部署失败", result.Message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("部署完成", result.Message + (string.IsNullOrEmpty(result.BackupPath) ? string.Empty : $"\n备份：{result.BackupPath}"), "确定");
                OnPackageDeployed?.Invoke();
            }

            UpdateDeploymentSection();
        }

        private void OnRestoreBackupClicked()
        {
            var result = MCPServiceLocator.Deployment.RestoreLastBackup();
            SetDeployStatus(result.Message, !result.Success);

            if (!result.Success)
            {
                EditorUtility.DisplayDialog("还原失败", result.Message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("还原完成", result.Message, "确定");
                OnPackageDeployed?.Invoke();
            }

            UpdateDeploymentSection();
        }

        private void SetDeployStatus(string message, bool isError = false)
        {
            if (deployStatusLabel == null)
            {
                return;
            }

            deployStatusLabel.text = message;
            deployStatusLabel.style.color = isError
                ? new StyleColor(new Color(0.85f, 0.2f, 0.2f))
                : StyleKeyword.Null;
        }

        public void UpdateHealthStatus(bool isHealthy, string statusText)
        {
            if (healthStatus != null)
            {
                healthStatus.text = statusText;
            }

            if (healthIndicator != null)
            {
                healthIndicator.RemoveFromClassList("healthy");
                healthIndicator.RemoveFromClassList("disconnected");
                healthIndicator.RemoveFromClassList("unknown");

                if (isHealthy)
                {
                    healthIndicator.AddToClassList("healthy");
                }
                else if (statusText == HealthStatus.Unknown)
                {
                    healthIndicator.AddToClassList("unknown");
                }
                else
                {
                    healthIndicator.AddToClassList("disconnected");
                }
            }
        }
    }
}
