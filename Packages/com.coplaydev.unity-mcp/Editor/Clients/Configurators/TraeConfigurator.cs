using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class TraeConfigurator : JsonFileMcpConfigurator
    {
        public TraeConfigurator() : base(new McpClient
        {
            name = "Trae",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trae", "User", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Trae", "User", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Trae", "User", "mcp.json"),
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "打开 Trae 并进入 设置 > MCP",
            "选择 Add Server > Add Manually",
            "粘贴 JSON 或指向 mcp.json 文件\n"+
                "Windows：%AppData%\\Trae\\User\\mcp.json\n" +
                "macOS：~/Library/Application Support/Trae/User/mcp.json\n" +
                "Linux：~/.config/Trae/User/mcp.json\n",
            "保存并重启 Trae"
        };
    }
}
