using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class KiroConfigurator : JsonFileMcpConfigurator
    {
        public KiroConfigurator() : base(new McpClient
        {
            name = "Kiro",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kiro", "settings", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kiro", "settings", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kiro", "settings", "mcp.json"),
            EnsureEnvObject = true,
            DefaultUnityFields = { { "disabled", false } }
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "打开 Kiro",
            "进入 文件 > 设置 > 设置 > 搜索 \"MCP\" > Open Workspace MCP Config\n或直接打开上述配置文件",
            "粘贴配置 JSON",
            "保存并重启 Kiro"
        };
    }
}
