using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class CopilotCliConfigurator : JsonFileMcpConfigurator
    {
        public CopilotCliConfigurator() : base(new McpClient
        {
            name = "GitHub Copilot CLI",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "mcp-config.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "mcp-config.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "mcp-config.json")
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "安装 GitHub Copilot CLI（https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli）",
            "在上述路径创建或打开 mcp-config.json",
            "粘贴配置 JSON（或在 CLI 中使用 /mcp add）",
            "重启 Copilot CLI 会话"
        };
    }
}
