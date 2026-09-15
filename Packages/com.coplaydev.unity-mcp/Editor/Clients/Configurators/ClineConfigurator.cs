using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class ClineConfigurator : JsonFileMcpConfigurator
    {
        public ClineConfigurator() : base(new McpClient
        {
            name = "Cline",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            HttpTypeValue = "streamableHttp",
            DefaultUnityFields = { { "disabled", false }, { "autoApprove", new object[] { } } }
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "在 VS Code 中打开 Cline",
            "点击 Cline 面板中的 MCP Servers 图标",
            "进入 Configure 选项卡并点击 'Configure MCP Servers'\n或直接打开上述配置文件",
            "将配置 JSON 粘贴到 mcpServers 对象中",
            "保存并重启 VS Code"
        };
    }
}
