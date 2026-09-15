using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// Qwen Code MCP client configurator.
    /// Qwen Code uses a JSON-based configuration file with mcpServers section.
    /// Config path: ~/.qwen/settings.json
    ///
    /// Qwen Code supports both stdio (uvx) and HTTP transport modes.
    /// Default: stdio mode (works without Unity Editor for basic operations)
    /// HTTP mode: requires Unity Editor running with MCP HTTP server started
    /// </summary>
    public class QwenCodeConfigurator : JsonFileMcpConfigurator
    {
        public QwenCodeConfigurator() : base(new McpClient
        {
            name = "Qwen Code",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen", "settings.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen", "settings.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen", "settings.json"),
            SupportsHttpTransport = true,
            // Default to stdio transport for Qwen Code (like Cursor)
            // User can switch to HTTP in Unity: Window > MCP for Unity > Settings
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "确保已安装 Qwen Code（npm install -g @qwen-code/qwen-code，或从 https://github.com/QwenLM/qwen-code 下载）",
            "打开 Qwen Code",
            "点击「Auto Configure」自动将 UnityMCP 添加到 settings.json",
            "或点击「Manual Setup」复制配置 JSON",
            "打开 ~/.qwen/settings.json 并粘贴配置",
            "保存并重启 Qwen Code",
            "在 Qwen Code 中使用 /mcp 命令验证 Unity MCP 已连接",
            "注意：如需完整功能，请打开 Unity 编辑器并启动 HTTP 服务器"
        };
    }
}
