using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// Claude Code configurator using the CLI-based registration (claude mcp add/remove).
    /// This integrates with Claude Code's native MCP management.
    /// </summary>
    public class ClaudeCodeConfigurator : ClaudeCliMcpConfigurator
    {
        public ClaudeCodeConfigurator() : base(new McpClient
        {
            name = "Claude Code",
            SupportsHttpTransport = true,
        })
        { }

        public override bool SupportsSkills => true;

        public override string GetSkillInstallPath()
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".claude", "skills", "unity-mcp-skill");
        }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "确保已安装 Claude CLI（随 Claude Code 提供）",
            "点击「配置」通过 'claude mcp add' 添加 UnityMCP",
            "服务器将自动在 Claude Code 中可用",
            "使用「注销」通过 'claude mcp remove' 移除"
        };
    }
}
