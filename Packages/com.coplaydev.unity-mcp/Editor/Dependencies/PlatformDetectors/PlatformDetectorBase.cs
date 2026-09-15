using System;
using MCPForUnity.Editor.Dependencies.Models;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;

namespace MCPForUnity.Editor.Dependencies.PlatformDetectors
{
    /// <summary>
    /// Base class for platform-specific dependency detection
    /// </summary>
    public abstract class PlatformDetectorBase : IPlatformDetector
    {
        public abstract string PlatformName { get; }
        public abstract bool CanDetect { get; }

        public abstract DependencyStatus DetectPython();
        public abstract string GetPythonInstallUrl();
        public abstract string GetUvInstallUrl();
        public abstract string GetInstallationRecommendations();

        public virtual DependencyStatus DetectUv()
        {
            var status = new DependencyStatus("uv Package Manager", isRequired: true)
            {
                InstallationHint = GetUvInstallUrl()
            };

            try
            {
                // Get uv path from PathResolverService (respects override)
                string uvxPath = MCPServiceLocator.Paths.GetUvxPath();

                // Verify uv executable and get version
                if (MCPServiceLocator.Paths.TryValidateUvxExecutable(uvxPath, out string version))
                {
                    status.IsAvailable = true;
                    status.Version = version;
                    status.Path = uvxPath;

                    // Check if we used fallback from override to system path
                    if (MCPServiceLocator.Paths.HasUvxPathFallback)
                    {
                        status.Details = $"已找到 uv {version}（回退到系统路径）";
                        status.ErrorMessage = "未找到覆盖路径，改用系统路径";
                    }
                    else
                    {
                        status.Details = MCPServiceLocator.Paths.HasUvxPathOverride
                            ? $"已找到 uv {version}（覆盖路径）"
                            : $"已在系统路径中找到 uv {version}";
                    }
                    return status;
                }

                status.ErrorMessage = "未找到 uvx";
                status.Details = "请安装 uv 包管理器，或在「高级设置」中配置路径覆盖。";
            }
            catch (Exception ex)
            {
                status.ErrorMessage = $"检测 uvx 时出错：{ex.Message}";
            }

            return status;
        }


        // Git is not needed to run the bridge, only to add or update the package from a Git URL
        // in the Package Manager, which is the install path most users take (issue #1216). It is
        // reported as optional so a missing git never blocks setup, but the row tells the user why
        // "Error when executing git command" appeared and how to clear it.
        public const string GitInstallUrl = "https://git-scm.com/downloads";

        public virtual DependencyStatus DetectGit()
        {
            var status = new DependencyStatus("Git", isRequired: false)
            {
                InstallationHint = GitInstallUrl
            };

            try
            {
                if (!TryFindInPath("git", out string gitPath))
                {
                    status.ErrorMessage = "未找到 git";
                    status.Details = "仅在通过包管理器的 Git URL 安装或更新 MCP for Unity 时才需要。";
                    return status;
                }

                if (ExecPath.TryRun(gitPath, "--version", null, out string stdout, out string stderr, 5000)
                    && TryParseGitVersion(string.IsNullOrWhiteSpace(stdout) ? stderr : stdout, out string version))
                {
                    status.IsAvailable = true;
                    status.Version = version;
                    status.Path = gitPath;
                    status.Details = "如果包管理器仍提示 'not in a git directory'，说明 git 拒绝访问属于其他用户的文件夹："
                        + "请运行 git config --global --add safe.directory \"<你的 Unity 项目文件夹>\"";
                    return status;
                }

                status.ErrorMessage = "已找到 git，但未能获取版本";
                status.Path = gitPath;
            }
            catch (Exception ex)
            {
                status.ErrorMessage = $"检测 git 时出错：{ex.Message}";
            }

            return status;
        }

        /// <summary>Parses "git version 2.45.1.windows.1" or "git version 2.39.5 (Apple Git-154)" into "2.45.1.windows.1" / "2.39.5".</summary>
        internal static bool TryParseGitVersion(string output, out string version)
        {
            version = null;
            string line = (output ?? string.Empty).Trim();
            const string prefix = "git version ";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rest = line.Substring(prefix.Length).Trim();
            int end = rest.IndexOfAny(new[] { ' ', '\r', '\n' });
            version = end >= 0 ? rest.Substring(0, end) : rest;
            return version.Length > 0 && char.IsDigit(version[0]);
        }

        protected bool TryParseVersion(string version, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            try
            {
                var parts = version.Split('.');
                if (parts.Length >= 2)
                {
                    return int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor);
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return false;
        }
        // In PlatformDetectorBase.cs
        protected bool TryValidateUvWithPath(string command, string augmentedPath, out string version, out string fullPath)
        {
            version = null;
            fullPath = null;

            try
            {
                string commandToRun = command;
                if (TryFindInPath(command, out string resolvedPath))
                {
                    commandToRun = resolvedPath;
                }

                if (!ExecPath.TryRun(commandToRun, "--version", null, out string stdout, out string stderr,
                    5000, augmentedPath))
                    return false;

                string output = string.IsNullOrWhiteSpace(stdout) ? stderr.Trim() : stdout.Trim();

                if (output.StartsWith("uvx ") || output.StartsWith("uv "))
                {
                    int spaceIndex = output.IndexOf(' ');
                    if (spaceIndex >= 0)
                    {
                        var remainder = output.Substring(spaceIndex + 1).Trim();
                        int nextSpace = remainder.IndexOf(' ');
                        int parenIndex = remainder.IndexOf('(');
                        int endIndex = Math.Min(
                            nextSpace >= 0 ? nextSpace : int.MaxValue,
                            parenIndex >= 0 ? parenIndex : int.MaxValue
                        );
                        version = endIndex < int.MaxValue ? remainder.Substring(0, endIndex).Trim() : remainder;
                        fullPath = commandToRun;
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore validation errors
            }

            return false;
        }
        

        // Add abstract method for subclasses to implement
        protected abstract bool TryFindInPath(string executable, out string fullPath);
    }
}
