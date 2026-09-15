using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class CursorConfigurator : JsonFileMcpConfigurator
    {
        public CursorConfigurator() : base(new McpClient
        {
            name = "Cursor",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor", "mcp.json")
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "打开 Cursor",
            "进入 文件 > 首选项 > Cursor 设置 > MCP > Add new global MCP server\n或直接打开上述配置文件",
            "粘贴配置 JSON",
            "保存并重启 Cursor"
        };
    }
}
