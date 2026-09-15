using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class RiderConfigurator : JsonFileMcpConfigurator
    {
        public RiderConfigurator() : base(new McpClient
        {
            name = "Rider GitHub Copilot",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "github-copilot", "intellij", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "github-copilot", "intellij", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "github-copilot", "intellij", "mcp.json"),
            IsVsCodeLayout = true
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "在 Rider 中安装 GitHub Copilot 插件",
            "在上述路径创建或打开 mcp.json",
            "粘贴配置 JSON",
            "保存并重启 Rider"
        };
    }
}

