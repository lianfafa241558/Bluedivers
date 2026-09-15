using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class WindsurfConfigurator : JsonFileMcpConfigurator
    {
        public WindsurfConfigurator() : base(new McpClient
        {
            name = "Windsurf",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codeium", "windsurf", "mcp_config.json"),
            HttpUrlProperty = "serverUrl",
            DefaultUnityFields = { { "disabled", false } },
            StripEnvWhenNotRequired = true
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "打开 Windsurf",
            "进入 文件 > 首选项 > Windsurf 设置 > MCP > Manage MCPs > View raw config\n或直接打开上述配置文件",
            "粘贴配置 JSON",
            "保存并重启 Windsurf"
        };
    }
}
