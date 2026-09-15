using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// Kimi Code CLI MCP client configurator.
    /// Kimi Code uses a JSON-based configuration file with mcpServers section.
    /// Config path: ~/.kimi/mcp.json
    ///
    /// Kimi Code supports both stdio (uvx) and HTTP transport modes.
    /// Default: stdio mode (works without Unity Editor for basic operations)
    /// HTTP mode: requires Unity Editor running with MCP HTTP server started
    /// </summary>
    public class KimiCodeConfigurator : JsonFileMcpConfigurator
    {
        public KimiCodeConfigurator() : base(new McpClient
        {
            name = "Kimi Code",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi", "mcp.json"),
            SupportsHttpTransport = true,
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "确保已安装 Kimi Code CLI（pip install kimi-cli，或参见 https://github.com/MoonshotAI/kimi-cli）",
            "点击「Auto Configure」自动将 UnityMCP 添加到 ~/.kimi/mcp.json",
            "或点击「Manual Setup」复制配置 JSON",
            "打开 ~/.kimi/mcp.json 并粘贴配置",
            "保存并重启 Kimi Code CLI",
            "使用 'kimi mcp list' 验证 Unity MCP 已连接",
            "注意：如需完整功能，请打开 Unity 编辑器并启动 HTTP 服务器"
        };
    }
}
