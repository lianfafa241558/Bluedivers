using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Dependencies;
using MCPForUnity.Editor.Dependencies.Models;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Windows.Components.Branding;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows
{
    /// <summary>
    /// Setup window for checking and guiding dependency installation
    /// </summary>
    public class MCPSetupWindow : EditorWindow
    {
        // UI Elements
        private VisualElement pythonIndicator;
        private Label pythonVersion;
        private Label pythonDetails;
        private VisualElement uvIndicator;
        private Label uvVersion;
        private Label uvDetails;
        private VisualElement gitIndicator;
        private Label gitVersion;
        private Label gitDetails;
        private Label statusMessage;
        private VisualElement installationSection;
        private Label installationInstructions;
        private Button openPythonLinkButton;
        private Button openUvLinkButton;
        private Button installUvButton;
        private Button refreshButton;
        private Button doneButton;

        // Tracks an in-flight uv install so completion is handled on the main thread.
        private Task<UvInstaller.UvInstallResult> _uvInstallTask;

        // Step 2 (Configure Clients) UI elements
        private VisualElement stepDeps;
        private VisualElement stepClients;
        private VisualElement clientsList;
        private Button skipClientsButton;
        private Button configureSelectedButton;
        private readonly List<(IMcpClientConfigurator client, Toggle toggle)> clientToggles = new();

        private DependencyCheckResult _dependencyResult;

        public static void ShowWindow(DependencyCheckResult dependencyResult = null)
        {
            var window = GetWindow<MCPSetupWindow>("MCP 安装向导");
            window.minSize = new Vector2(480, 320);
            window._dependencyResult = dependencyResult ?? DependencyManager.CheckAllDependencies();
            window.Show();
        }

        public void CreateGUI()
        {
            string basePath = AssetPathUtility.GetMcpPackageRootPath();

            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                $"{basePath}/Editor/Windows/MCPSetupWindow.uxml"
            );

            if (visualTree == null)
            {
                McpLog.Error($"Failed to load UXML at: {basePath}/Editor/Windows/MCPSetupWindow.uxml");
                return;
            }

            visualTree.CloneTree(rootVisualElement);

            // Embed the Ocean brand mark beside the title
            var setupHeader = rootVisualElement.Q<VisualElement>("setup-header");
            if (setupHeader != null && setupHeader.Q<OceanMark>() == null)
            {
                var logo = new OceanMark { name = "setup-logo" };
                logo.AddToClassList("setup-logo");
                setupHeader.Insert(0, logo);
            }

            // Cache UI elements
            pythonIndicator = rootVisualElement.Q<VisualElement>("python-indicator");
            pythonVersion = rootVisualElement.Q<Label>("python-version");
            pythonDetails = rootVisualElement.Q<Label>("python-details");
            uvIndicator = rootVisualElement.Q<VisualElement>("uv-indicator");
            uvVersion = rootVisualElement.Q<Label>("uv-version");
            uvDetails = rootVisualElement.Q<Label>("uv-details");
            gitIndicator = rootVisualElement.Q<VisualElement>("git-indicator");
            gitVersion = rootVisualElement.Q<Label>("git-version");
            gitDetails = rootVisualElement.Q<Label>("git-details");
            statusMessage = rootVisualElement.Q<Label>("status-message");
            installationSection = rootVisualElement.Q<VisualElement>("installation-section");
            installationInstructions = rootVisualElement.Q<Label>("installation-instructions");
            openPythonLinkButton = rootVisualElement.Q<Button>("open-python-link-button");
            openUvLinkButton = rootVisualElement.Q<Button>("open-uv-link-button");
            installUvButton = rootVisualElement.Q<Button>("install-uv-button");
            refreshButton = rootVisualElement.Q<Button>("refresh-button");
            doneButton = rootVisualElement.Q<Button>("done-button");
            stepDeps = rootVisualElement.Q<VisualElement>("step-deps");
            stepClients = rootVisualElement.Q<VisualElement>("step-clients");
            clientsList = rootVisualElement.Q<VisualElement>("clients-list");
            skipClientsButton = rootVisualElement.Q<Button>("skip-clients-button");
            configureSelectedButton = rootVisualElement.Q<Button>("configure-selected-button");

            // Register callbacks
            refreshButton.clicked += OnRefreshClicked;
            doneButton.clicked += OnDoneClicked;
            openPythonLinkButton.clicked += OnOpenPythonInstallClicked;
            openUvLinkButton.clicked += OnOpenUvInstallClicked;
            if (installUvButton != null) installUvButton.clicked += OnInstallUvClicked;
            skipClientsButton.clicked += OnSkipClientsClicked;
            configureSelectedButton.clicked += OnConfigureSelectedClicked;

            // Initial update
            UpdateUI();
        }

        private void OnEnable()
        {
            if (_dependencyResult == null)
            {
                _dependencyResult = DependencyManager.CheckAllDependencies();
            }
            // Resume polling if a uv install was still in flight when the window was last disabled,
            // so its completion is still processed (button reset, dependencies re-checked).
            if (_uvInstallTask != null)
            {
                EditorApplication.update -= PollUvInstall;
                EditorApplication.update += PollUvInstall;
            }
        }

        private void OnRefreshClicked()
        {
            _dependencyResult = DependencyManager.CheckAllDependencies();
            UpdateUI();
        }

        private void OnDoneClicked()
        {
            if (_dependencyResult != null && _dependencyResult.IsSystemReady)
            {
                ShowClientsStep();
            }
            else
            {
                Setup.SetupWindowService.MarkSetupDismissed();
                Close();
            }
        }

        private void ShowClientsStep()
        {
            stepDeps.style.display = DisplayStyle.None;
            stepClients.style.display = DisplayStyle.Flex;
            PopulateClientsList();
        }

        private void PopulateClientsList()
        {
            clientsList.Clear();
            clientToggles.Clear();
            foreach (var c in McpClientRegistry.All)
            {
                if (!c.IsInstalled) continue;
                var toggle = new Toggle(c.DisplayName)
                {
                    value = true,
                    tooltip = c.GetConfigPath()
                };
                clientToggles.Add((c, toggle));
                clientsList.Add(toggle);
            }
            if (clientToggles.Count == 0)
            {
                clientsList.Add(new Label("本机未检测到受支持的 MCP 客户端。你可以稍后在「工具 → MCP for Unity」中配置客户端。"));
                configureSelectedButton.SetEnabled(false);
            }
        }

        private void OnSkipClientsClicked()
        {
            Setup.SetupWindowService.MarkSetupCompleted();
            Close();
        }

        private void OnConfigureSelectedClicked()
        {
            int success = 0, failure = 0;
            var failures = new List<string>();
            foreach (var (c, toggle) in clientToggles)
            {
                if (!toggle.value) continue;
                try
                {
                    MCPServiceLocator.Client.ConfigureClient(c);
                    success++;
                }
                catch (System.Exception ex)
                {
                    failure++;
                    failures.Add($"⚠ {c.DisplayName}: {ex.Message}");
                }
            }
            if (success == 0 && failure == 0)
            {
                EditorUtility.DisplayDialog(
                    "客户端配置",
                    "未选择任何客户端。请至少勾选一个客户端以继续，或关闭窗口跳过安装。",
                    "确定");
                return;
            }
            // Keep the summary short: a count, only the failures (if any), and the next step —
            // no need to enumerate every successfully-configured client.
            string failureList = failures.Count > 0 ? "\n\n" + string.Join("\n", failures) : "";
            string nextStep = (failure == 0 && success > 0)
                ? "\n\n一切就绪。请让你的 AI 助手在当前场景中创建一个 GameObject 以确认连接。"
                : "";
            EditorUtility.DisplayDialog(
                "客户端配置",
                $"已配置 {success} 个，失败 {failure} 个。{failureList}{nextStep}",
                "确定");
            Setup.SetupWindowService.MarkSetupCompleted();
            Close();
        }

        private void OnOpenPythonInstallClicked()
        {
            var (pythonUrl, _) = DependencyManager.GetInstallationUrls();
            Application.OpenURL(pythonUrl);
        }

        private void OnOpenUvInstallClicked()
        {
            var (_, uvUrl) = DependencyManager.GetInstallationUrls();
            Application.OpenURL(uvUrl);
        }

        private void OnInstallUvClicked()
        {
            if (_uvInstallTask != null) return; // already running

            bool proceed = EditorUtility.DisplayDialog(
                "安装 UV",
                "将下载并运行官方 uv 安装程序：\n\n" +
                UvInstaller.DescribeCommand() +
                "\n\n是否继续？",
                "安装",
                "取消");
            if (!proceed) return;

            installUvButton.SetEnabled(false);
            installUvButton.text = "正在安装 UV…";
            statusMessage.text = "正在安装 uv… 可能需要一点时间。";
            statusMessage.style.color = new StyleColor(new Color(1f, 0.6f, 0f));

            _uvInstallTask = Task.Run(() => UvInstaller.Run());
            EditorApplication.update += PollUvInstall;
        }

        private void PollUvInstall()
        {
            // The window/UI may have been torn down while the task ran — stop polling and drop it
            // (guards against dereferencing UI fields after teardown).
            if (installUvButton == null || installUvButton.panel == null)
            {
                EditorApplication.update -= PollUvInstall;
                return;
            }
            if (_uvInstallTask == null || !_uvInstallTask.IsCompleted) return;

            EditorApplication.update -= PollUvInstall;
            var task = _uvInstallTask;
            _uvInstallTask = null;

            installUvButton.SetEnabled(true);
            installUvButton.text = "自动安装 UV";

            // UvInstaller.Run catches its own exceptions, so the task always completes with a result.
            UvInstaller.UvInstallResult result = task.Result;

            if (result.Success)
            {
                _dependencyResult = DependencyManager.CheckAllDependencies();
                UpdateUI();
                if (!_dependencyResult.IsSystemReady)
                {
                    EditorUtility.DisplayDialog(
                        "安装 UV",
                        "uv 已安装，但尚未在 PATH 中生效。请重启 Unity（或终端）以加载新的 PATH，然后点击「刷新」。\n\n" +
                        result.Output,
                        "确定");
                }
            }
            else
            {
                // Reset the status label off the "Installing…" state before reporting the failure.
                UpdateUI();
                EditorUtility.DisplayDialog(
                    "安装 UV 失败",
                    "安装程序未能成功完成。你可以通过「打开 UV 安装页面」手动安装 uv。\n\n" +
                    result.Output,
                    "确定");
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollUvInstall;
        }

        private void UpdateUI()
        {
            if (_dependencyResult == null)
                return;

            // Update Python status
            var pythonDep = _dependencyResult.Dependencies.Find(d => d.Name == "Python");
            if (pythonDep != null)
            {
                UpdateDependencyStatus(pythonIndicator, pythonVersion, pythonDetails, pythonDep);
            }

            // Update uv status
            var uvDep = _dependencyResult.Dependencies.Find(d => d.Name == "uv Package Manager");
            if (uvDep != null)
            {
                UpdateDependencyStatus(uvIndicator, uvVersion, uvDetails, uvDep);
            }

            // Update git status (optional dependency: never blocks readiness)
            var gitDep = _dependencyResult.Dependencies.Find(d => d.Name == "Git");
            if (gitDep != null)
            {
                UpdateDependencyStatus(gitIndicator, gitVersion, gitDetails, gitDep);
            }

            // Offer the one-click uv installer only when uv is actually missing
            bool uvMissing = uvDep != null && !uvDep.IsAvailable;
            if (installUvButton != null)
            {
                bool showInstall = uvMissing && UvInstaller.IsSupported && _uvInstallTask == null;
                installUvButton.style.display = showInstall ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Update overall status
            if (_dependencyResult.IsSystemReady)
            {
                statusMessage.text = "✓ 已满足全部要求！MCP for Unity 可以使用了。";
                statusMessage.style.color = new StyleColor(Color.green);
                installationSection.style.display = DisplayStyle.None;
            }
            else
            {
                statusMessage.text = "⚠ 缺少依赖项。MCP for Unity 需要全部依赖才能正常工作。";
                statusMessage.style.color = new StyleColor(new Color(1f, 0.6f, 0f)); // Orange
                installationSection.style.display = DisplayStyle.Flex;
                installationInstructions.text = DependencyManager.GetInstallationRecommendations();
            }
        }

        internal static void UpdateDependencyStatus(VisualElement indicator, Label versionLabel, Label detailsLabel, DependencyStatus dep)
        {
            if (dep.IsAvailable)
            {
                indicator.RemoveFromClassList("invalid");
                indicator.AddToClassList("valid");
                versionLabel.text = $"v{dep.Version}";
                detailsLabel.text = dep.Details ?? "可用";
                detailsLabel.style.color = new StyleColor(Color.gray);
            }
            else if (dep.IsRequired)
            {
                indicator.RemoveFromClassList("valid");
                indicator.AddToClassList("invalid");
                versionLabel.text = "未找到";
                detailsLabel.text = dep.ErrorMessage ?? "不可用";
                detailsLabel.style.color = new StyleColor(Color.red);
            }
            else
            {
                // A missing optional dependency is information, not a blocker. Drop both state
                // classes so the dot keeps the neutral grey of .status-indicator-small instead of
                // the red .invalid reserved for required ones, and say what it is for.
                indicator.RemoveFromClassList("valid");
                indicator.RemoveFromClassList("invalid");
                versionLabel.text = "未找到";
                detailsLabel.text = dep.Details ?? dep.ErrorMessage ?? "不可用";
                detailsLabel.style.color = new StyleColor(Color.gray);
            }
        }
    }
}
