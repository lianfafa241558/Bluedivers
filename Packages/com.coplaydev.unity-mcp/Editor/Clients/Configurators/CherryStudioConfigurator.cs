using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;
using UnityEditor;

namespace MCPForUnity.Editor.Clients.Configurators
{
    public class CherryStudioConfigurator : JsonFileMcpConfigurator
    {
        public const string ClientName = "Cherry Studio";

        public CherryStudioConfigurator() : base(new McpClient
        {
            name = ClientName,
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cherry Studio", "config"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Cherry Studio", "config"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Cherry Studio", "config"),
            SupportsHttpTransport = false
        })
        { }

        public override bool SupportsAutoConfigure => false;

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "打开 Cherry Studio",
            "进入 设置（⚙️）→ MCP Server",
            "点击「Add Server」按钮",
            "STDIO 模式（推荐）：",
            "  - 名称：unity-mcp",
            "  - 类型：STDIO",
            "  - 命令：uvx",
            "  - 参数：复制下方「手动配置」中的 JSON 值",
            "点击保存并重启 Cherry Studio",
            "",
            "注意：Cherry Studio 使用界面化配置。",
            "请参照下方手动配置片段填写各项取值。"
        };

        public override McpStatus CheckStatus(bool attemptAutoRewrite = true)
        {
            client.SetStatus(McpStatus.NotConfigured, "Cherry Studio 需要在界面中手动配置");
            return client.status;
        }

        public override void Configure()
        {
            throw new InvalidOperationException(
                "Cherry Studio 使用界面化配置。" +
                "请使用「手动配置」片段与「安装步骤」手动配置。"
            );
        }

        public override string GetManualSnippet()
        {
            bool useHttp = EditorConfigurationCache.Instance.UseHttpTransport;

            if (useHttp)
            {
                return "# Cherry Studio 不支持 WebSocket 传输。\n" +
                       "# Cherry Studio 支持 STDIO 与 SSE 传输。\n" +
                       "# \n" +
                       "# 使用 Cherry Studio 的方法：\n" +
                       "# 1. 在下方「高级设置」中将传输方式切换为 'Stdio'\n" +
                       "# 2. 返回此配置界面\n" +
                       "# 3. 复制随后出现的 STDIO 配置片段\n" +
                       "# \n" +
                       "# 选项 2：SSE 模式（未来支持）\n" +
                       "# 注意：Unity MCP 目前没有 SSE 端点。\n" +
                       "# 该功能可能在后续更新中加入。";
            }

            return base.GetManualSnippet() + "\n\n" +
                   "# Cherry Studio 配置说明：\n" +
                   "# Cherry Studio 使用界面化配置，而非 JSON 文件。\n" +
                   "# \n" +
                   "# 配置方法：\n" +
                   "# 1. 打开 Cherry Studio\n" +
                   "# 2. 进入 设置（⚙️）→ MCP Server\n" +
                   "# 3. 点击 'Add Server'\n" +
                   "# 4. 按上方 JSON 填写以下取值：\n" +
                   "#    - 名称：unity-mcp\n" +
                   "#    - 类型：STDIO\n" +
                   "#    - 命令：（复制 JSON 中的 'command' 值）\n" +
                   "#    - 参数：（复制 'args' 数组的值，以空格分隔或逐项填写）\n" +
                   "#    - 启用：true\n" +
                   "# 5. 点击保存\n" +
                   "# 6. 重启 Cherry Studio";
        }
    }
}
