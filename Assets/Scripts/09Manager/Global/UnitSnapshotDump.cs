using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FPSGame.Core.Interface;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机诊断：每 20 秒把"每个玩家 50m 范围内的单位"写进**工程根目录**的 txt（每次生成后立刻落盘）。
    ///
    /// <para>▍用途：两端"单位位置不一致"的对照 —— 两台机器各出一份，按 <c>netId</c> 逐行比坐标，
    /// 一眼能看出是"某个单位整体漂了"还是"某类单位压根没同步"。</para>
    ///
    /// <para>▍为什么不需要挂场景：<c>[RuntimeInitializeOnLoadMethod]</c> 自举 + <c>DontDestroyOnLoad</c>，
    /// 与 <c>NetPlayerStateBridge</c> / <c>SceneUnitMoveSink</c> 同款常驻套路。</para>
    ///
    /// <para>▍为什么用 <see cref="WaitForSecondsRealtime"/>：时停 / 慢动作时 <c>WaitForSeconds</c> 会一起变慢，
    /// 而对照两端位置恰恰最需要"真实时间同刻"的两份表。</para>
    /// </summary>
    public class UnitSnapshotDump : MonoBehaviour
    {
        /// <summary>扫描周期(秒)。</summary>
        private const float IntervalSeconds = 20f;

        /// <summary>扫描半径(米)。</summary>
        private const float Radius = 50f;

        private static bool s_spawned;

        /// <summary>复用缓冲：每拍只清空不重建。</summary>
        private readonly List<IActor> _buffer = new();
        /*
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_spawned) return;
            s_spawned = true;

            var go = new GameObject("[诊断]单位快照");
            DontDestroyOnLoad(go);
            go.AddComponent<UnitSnapshotDump>();
        }*/

        private void Start()
        {
            StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(IntervalSeconds);
            while (true)
            {
                yield return wait;
                Dump();
            }
        }

        /// <summary>本机写盘路径：工程根目录（<c>Application.dataPath</c> 的上一级）/ 按天分文件。</summary>
        private static string FilePath
        {
            get
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                string root = parent != null ? parent.FullName : Application.persistentDataPath;
                return Path.Combine(root, "UnitSnapshot_" + System.DateTime.Now.ToString("yyyyMMdd") + ".txt");
            }
        }

        private void Dump()
        {
            var sb = new StringBuilder(2048);
            var flow = NetRoomFlow.Instance;
            string side = flow == null ? "单机" : (flow.IsHost ? "房主" : "成员");
            sb.Append("===== ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
              .Append("  机器=").Append(System.Environment.MachineName)
              .Append("  端=").Append(side)
              .Append(" =====").Append('\n');

            var players = ActorsManager.Players;
            for (int i = 0; i < players.Count; ++i)
            {
                IActor player = players[i];
                if (!player.IsValidMono()) continue;

                Vector3 playerPos = player.transform.position;
                _buffer.Clear();
                var actors = ActorsManager.Actors;
                for (int j = 0; j < actors.Count; ++j)
                {
                    IActor unit = actors[j];
                    if (!unit.IsValidMono()) continue;
                    if ((unit.transform.position - playerPos).sqrMagnitude > Radius * Radius) continue;
                    _buffer.Add(unit);
                }

                // 按 netId 升序：两端同一单位的行会落在同一位置，肉眼/工具都容易对齐
                _buffer.Sort((a, b) => a.NetId.CompareTo(b.NetId));

                sb.Append("--- 玩家 ").Append(player.ShowName).Append(" netId=").Append(player.NetId)
                  .Append(" pos=").Append(Fmt(playerPos))
                  .Append(" 半径").Append(Radius).Append("m 命中 ").Append(_buffer.Count).Append(" 个单位").Append('\n');

                for (int k = 0; k < _buffer.Count; ++k)
                {
                    IActor unit = _buffer[k];
                    Vector3 pos = unit.transform.position;
                    float dist = Vector3.Distance(pos, playerPos);
                    sb.Append("  ").Append(unit.Type.ToString().PadRight(10))
                      .Append(" ").Append((string.IsNullOrEmpty(unit.ShowName) ? "<无名>" : unit.ShowName).PadRight(18))
                      .Append(" netId=").Append(unit.NetId.ToString().PadRight(7))
                      .Append(" pos=").Append(Fmt(pos))
                      .Append(" 距=").Append(dist.ToString("F1")).Append("m").Append('\n');
                }
            }

            try
            {
                // 每次生成立刻落盘（不攒缓冲）：崩了 / 断线了也能拿到最后几拍
                File.AppendAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[诊断]单位快照写不进去：{FilePath}\n{e.Message}");
                enabled = false;   // 目录不可写时别每 20 秒刷同一条错
            }
        }

        private static string Fmt(Vector3 p) => $"({p.x:F2},{p.y:F2},{p.z:F2})";
    }
}
