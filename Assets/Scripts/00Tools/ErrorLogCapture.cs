using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace FPSGame.Utils
{
    /// <summary>
    /// 打包端错误落盘（2026-11-12）：把 Error / Exception / Assert 追加到
    /// <c>&lt;游戏根目录&gt;/Error_yyyyMMdd_HHmmss.log</c>，文件名取本次**启动**时刻。
    /// <para>▍为什么用 Threaded 版：网络/加载等子线程抛的异常只出现在这一路回调里；代价是回调不在主线程。</para>
    /// <para>▍抓不到什么：native crash（C++ 层）与订阅之前打出的日志都不进这条链，前者只能看 Player.log。</para>
    /// </summary>
    public static class ErrorLogCapture
    {
        /// <summary>总开关：编辑器默认关（不然每 Play 一次项目根就多一个文件），打包端默认开。</summary>
        public static bool Enabled;

        /// <summary>本次启动时刻，同时决定文件名；SubsystemRegistration 时定一次，之后不再变。</summary>
        private static DateTime _bootTime;

        /// <summary>游戏根目录（打包后 = exe 同级）。**主线程算好**，回调里不碰 Application。</summary>
        private static string _rootDir;

        /// <summary>文件头（版本/平台），同样在主线程拼好，供子线程直接写。</summary>
        private static string _header;

        private static StreamWriter _writer;

        /// <summary>写盘失败后置位：避免每条错误都重试 IO、把 Console 刷满。</summary>
        private static bool _dead;

        /// <summary>StreamWriter 非线程安全，而回调可能来自任意线程。</summary>
        private static readonly object _lock = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            // 关闭域重载时静态字段会留着上一轮的文件句柄 ⇒ 先收尾再重开
            Close();
            _dead = false;

            _bootTime = DateTime.Now;
            _rootDir = Path.GetDirectoryName(Application.dataPath);
            _header = $"# 错误日志  启动 {_bootTime:yyyy-MM-dd HH:mm:ss}  {Application.productName} {Application.version}"
                      + $"  Unity {Application.unityVersion}  {Application.platform}";
            // 把当前堆栈级别写进文件头：文件里堆栈为空/太短时，一眼能判断是被设置关掉了而不是没抓到
            _header += Environment.NewLine + $"# 堆栈级别 Error={Application.GetStackTraceLogType(LogType.Error)}"
                      + $" Exception={Application.GetStackTraceLogType(LogType.Exception)}"
                      + $" Log={Application.GetStackTraceLogType(LogType.Log)}";
            Enabled = !Application.isEditor;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == "-errorlog") Enabled = true;
                else if (args[i] == "-noerrorlog") Enabled = false;
            }

            if (!Enabled) return;

            // 先 -= 再 += ：关闭域重载时静态字段不清空，重复注册会让每条错误写两遍
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            Application.quitting -= Close;
            Application.quitting += Close;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_dead) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            // ⚠ 回调不一定在主线程：这里只做字符串拼装与文件写入，不许碰 UnityEngine 对象
            StringBuilder sb = new StringBuilder(512);
            sb.Append('[').Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("][").Append(type).Append("] ").AppendLine(condition);
            if (!string.IsNullOrEmpty(stackTrace)) sb.AppendLine(stackTrace);
            sb.AppendLine("────────────────────────────────────────");

            lock (_lock)
            {
                if (_dead) return;
                try
                {
                    if (_writer == null) OpenWriter();
                    _writer.Write(sb.ToString());
                    _writer.Flush();
                }
                catch (Exception e)
                {
                    // ⚠ 必须**先**置 _dead 再打日志：否则这条 LogError 又回调进来，无限递归
                    _dead = true;
                    Debug.LogError($"[ErrorLog] 写日志失败，本次运行后续错误不再落盘：{e.Message}");
                }
            }
        }

        private static void OpenWriter()
        {
            // 首次出现错误才建文件：没报错的局不留空文件（文件名仍是启动时刻）
            string path = Path.Combine(_rootDir, $"Error_{_bootTime:yyyyMMdd_HHmmss}.log");
            // FileShare.ReadWrite：允许边玩边用文本编辑器打开看
            _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                                       new UTF8Encoding(false));
            _writer.AutoFlush = true;
            _writer.WriteLine(_header);
            Debug.Log($"[ErrorLog] 本次运行的错误将追加到：{path}");
        }

        private static void Close()
        {
            lock (_lock)
            {
                if (_writer == null) return;
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
        }
    }
}
