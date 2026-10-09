using UnityEngine;

namespace FPSGame.Utils
{
    /// <summary>
    /// 联机/战斗同步的诊断日志开关（2026-10-09）。**打包端默认开、编辑器默认关**：
    /// 编辑器里我用 <c>execute_code</c> 随时开（Console 直接读）；打包端没有 execute_code ⇒ 它的日志只能落进自己的
    /// <c>Player.log</c> 再回读（启动时会打印路径）。
    /// <para>三路分管：<c>Bullet</c> / <c>Ai</c> / <c>Sync</c>；关总开关 <c>Enabled=false</c> 即全静默。
    /// 想强制开关：命令行 <c>-netsynclog</c> / <c>-nonetsynclog</c>、环境变量 <c>NETSYNC_LOG=1</c>、<c>PlayerPrefs["NetSyncLog"]</c>。</para>
    /// <para>放 00_Utils 是因为 05/06/09/10 都引用它；<c>07_NetGame</c> 只引用 02_Net（看不见本类）⇒ 传输层日志点放在 09 的桥里。</para>
    /// </summary>
    public static class NetSyncLog
    {
        /// <summary>总开关（关掉时判断只在调用点 ⇒ 开销 = 一次静态字段读取）。</summary>
        public static bool Enabled;

        /// <summary>子弹/弹道：表现弹的生成、终点、命中、消散。</summary>
        public static bool Bullet = true;

        /// <summary>AI/寻路：敌人与场景 NPC 的决策、下发、落地、驱动开关。</summary>
        public static bool Ai = true;

        /// <summary>同步时序：NetId 分配、离场收尾、HUD 行数等。</summary>
        public static bool Sync = true;

        public static void BulletLog(string tag, string msg) { if (!Enabled || !Bullet) return; Debug.Log($"[子弹/{tag}] {msg}"); }
        public static void AiLog(string tag, string msg) { if (!Enabled || !Ai) return; Debug.Log($"[AI/{tag}] {msg}"); }
        public static void SyncLog(string tag, string msg) { if (!Enabled || !Sync) return; Debug.Log($"[同步/{tag}] {msg}"); }

        /// <summary>丢消息 / 找不到对象 / 决策作废这类"不正常"，用 Warning 便于在 Console 里筛。</summary>
        public static void Warn(string tag, string msg) { if (!Enabled) return; Debug.LogWarning($"[异常/{tag}] {msg}"); }

        /// <summary>启动时定默认值：**两端都默认关**（这些是诊断日志，常年开会把 Player.log 刷爆）。
        /// 要查就显式开：编辑器用 execute_code，打包端用启动参数 <c>-netsynclog</c> / 环境变量 <c>NETSYNC_LOG=1</c>。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitFromStartup()
        {
            Enabled = false;

            if (System.Environment.GetEnvironmentVariable("NETSYNC_LOG") == "1") Enabled = true;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == "-netsynclog") Enabled = true;
                else if (args[i] == "-nonetsynclog") Enabled = false;
            }

            if (PlayerPrefs.GetInt("NetSyncLog", 0) == 1) Enabled = true;

            if (Enabled) Debug.Log($"[同步] NetSyncLog 已启用（打包端默认开）。本进程日志文件：{Application.consoleLogPath}");
        }
    }
}
