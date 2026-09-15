using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Models;
using UnityEditor;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class GeminiCliConfigurator : JsonFileMcpConfigurator
    {
        public GeminiCliConfigurator() : base(new McpClient
        {
            name = "Gemini CLI",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "settings.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "settings.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "settings.json"),
            HttpUrlProperty = "httpUrl",
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "确保已安装 Gemini CLI（参见 https://geminicli.com/docs/get-started/installation/）",
            "点击「注册」通过 'gemini mcp add' 添加 UnityMCP",
            "服务器将自动在 Gemini CLI 中可用",
            "使用「注销」通过 'gemini mcp remove' 移除"
        };
    }
}
