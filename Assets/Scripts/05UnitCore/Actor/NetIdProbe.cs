using System.Collections.Generic;
using System.Text;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// NetId 落地探针（只诊断、不参与游戏逻辑）：给每个号记一本"出生 / 换号 / 销毁"账，
    /// 对端消息落地失败时自动比对账本与本端快照，直接区分【消息迟到】/【两端号不一致】/【本端没复刻出来】。
    /// <para>▍为什么要它：<c>FindActor</c> 失败只有一句"找不到 netId"，这三种根因要靠人去问"你当时在干什么"。</para>
    /// <para>▍落地明细同时追加到 <c>persistentDataPath/netid_probe.log</c>，Console 被清也能回读。</para>
    /// </summary>
    public static class NetIdProbe
    {
        const int SpecNetIdBase = 1000000;
        const int HostSpawnNetIdBase = 0x2000_0000;
        const int DerivedNetIdBase = 0x4000_0000;
        const int MaxLedger = 8192;
        const long MaxFileBytes = 512 * 1024;

        sealed class Entry
        {
            public string Id;
            public float BornAt;
            public float DeadAt = -1f;   // 阵亡并被 UnRegisterUnit 摘表（对象可能还活着，等销毁）
            public float DiedAt = -1f;   // 对象真正销毁（Actor.OnDestroy）
        }

        static readonly Dictionary<int, Entry> _ledger = new Dictionary<int, Entry>();
        static readonly HashSet<int> _detailed = new HashSet<int>();   // 同一个号只出一次详情，防刷屏
        static readonly StringBuilder _sb = new StringBuilder(512);
        static bool _fileBroken;

        /// <summary>总开关（关掉后连账都不记）。</summary>
        public static bool Enabled = true;

        public static int MissDamage;
        public static int MissDeath;
        public static int Retried;

        static float Now => Time.unscaledTime;

        /// <summary>号被赋给某个单位（含 4051 复刻后覆盖）。同号仍被另一个 Id 占用 ⇒ 直接告警"两端号串了"。</summary>
        public static void Born(int netId, string id)
        {
            if (!Enabled || netId == 0) return;

            if (_ledger.TryGetValue(netId, out var e))
            {
                if (e.DiedAt < 0f && e.Id != id)
                {
                    NetSyncLog.Warn("NetId冲突",
                        $"号 {netId}({Segment(netId)}) 仍被 Id={e.Id} 占用，现在又赋给 Id={id} ⇒ 两端号空间已串");
                }
                e.Id = id;
                e.BornAt = Now;
                e.DeadAt = -1f;
                e.DiedAt = -1f;
                return;
            }
            if (_ledger.Count >= MaxLedger) return;
            _ledger[netId] = new Entry { Id = id, BornAt = Now };
        }

        /// <summary>号随单位作废（<c>Actor.OnDestroy</c> / 运行时换号时调用）。</summary>
        public static void Died(int netId, string id)
        {
            if (!Enabled || netId == 0) return;

            if (!_ledger.TryGetValue(netId, out var e))
            {
                if (_ledger.Count >= MaxLedger) return;
                e = new Entry { Id = id, BornAt = Now };
                _ledger[netId] = e;
            }
            e.DiedAt = Now;
            if (string.IsNullOrEmpty(e.Id)) e.Id = id;
        }

        /// <summary>阵亡并已从 <c>Actors</c> 摘表（<c>ActorsManager.UnRegisterUnit</c>）：对象可能还在场等销毁 ⇒
        /// 之后到达的伤害/死亡消息是"打尸体"，落地失败属正常，不该当缺陷报。</summary>
        public static void Dead(int netId, string id)
        {
            if (!Enabled || netId == 0) return;

            if (!_ledger.TryGetValue(netId, out var e))
            {
                if (_ledger.Count >= MaxLedger) return;
                e = new Entry { Id = id, BornAt = Now };
                _ledger[netId] = e;
            }
            if (e.DeadAt < 0f) e.DeadAt = Now;
            if (string.IsNullOrEmpty(e.Id)) e.Id = id;
        }

        /// <summary>这次的落地失败是否属于"正常损耗"（目标已阵亡或已销毁）——是则别用 Warning 刷屏。</summary>
        public static bool IsNormalMiss(int netId)
        {
            if (!Enabled || !_ledger.TryGetValue(netId, out var e)) return false;
            return e.DiedAt >= 0f || e.DeadAt >= 0f;
        }

        public static void CountRetried() => Retried++;

        /// <summary>对端消息落地失败时调用：累计计数 + 自动比对，返回可直接拼进日志的结论。</summary>
        public static string Explain(int netId, int key, string kind)
        {
            if (!Enabled) return string.Empty;
            if (kind == "伤害") MissDamage++; else MissDeath++;

            _sb.Clear();
            _sb.Append("探针 netId=").Append(netId).Append('(').Append(Segment(netId)).Append(')');
            if (key != 0) _sb.Append(" key=").Append(key);

            if (_ledger.TryGetValue(netId, out var e))
            {
                if (e.DiedAt >= 0f)
                {
                    _sb.Append(" | 账本: Id=").Append(e.Id)
                       .Append(" 存活 ").Append((e.DiedAt - e.BornAt).ToString("F1"))
                       .Append("s，已于 ").Append((Now - e.DiedAt).ToString("F1"))
                       .Append("s 前销毁 ⇒ 消息迟到，号本身没错");
                }
                else if (e.DeadAt >= 0f)
                {
                    _sb.Append(" | 账本: Id=").Append(e.Id)
                       .Append(" 已于 ").Append((Now - e.DeadAt).ToString("F1"))
                       .Append("s 前阵亡并从 Actors 摘表（对象还在场等销毁）⇒ 这一发打在尸体上，属正常");
                }
                else
                {
                    _sb.Append(" | 账本: Id=").Append(e.Id)
                       .Append(" 记账为存活、Actors 里却查不到 ⇒ 被摘表却既没阵亡也没销毁（真缺陷）");
                }
            }
            else
            {
                _sb.Append(" | 账本: 本端从未登记过该号 ⇒ ").Append(Diag(netId));
            }

            if (_detailed.Add(netId))
            {
                _sb.Append(" | 本端快照: ").Append(Snapshot());
                ToFile(kind + " " + _sb.ToString());
            }
            return _sb.ToString();
        }

        /// <summary>本端号分布摘要（一行，供与另一端的日志对照）。</summary>
        public static string Snapshot()
        {
            int enemy = 0, spec = 0, host = 0, derived = 0, none = 0, maxEnemy = 0, maxSpec = 0;
            var list = ActorsManager.Actors;
            for (int i = 0; i < list.Count; ++i)
            {
                if (!(list[i] is Actor a) || a == null) continue;
                int id = a.NetId;
                if (id <= 0) { none++; continue; }
                if (id >= DerivedNetIdBase) { derived++; continue; }
                if (id >= HostSpawnNetIdBase) { host++; continue; }
                if (id >= SpecNetIdBase) { spec++; if (id > maxSpec) maxSpec = id; continue; }
                enemy++; if (id > maxEnemy) maxEnemy = id;
            }
            return $"units={list.Count} 敌={enemy}(max{maxEnemy}) 特殊={spec}(max{maxSpec}) 生成={host} 派生={derived} 无号={none}";
        }

        /// <summary>一行汇总：累计失败次数 + 当前快照（随时可读，用于"两端各读一次再对照"）。</summary>
        public static string Dump()
        {
            int alive = 0, slain = 0, gone = 0;
            foreach (var e in _ledger.Values)
            {
                if (e.DiedAt >= 0f) gone++;
                else if (e.DeadAt >= 0f) slain++;
                else alive++;
            }
            return $"[NetIdProbe] 账本={_ledger.Count}(活{alive}/阵亡{slain}/已销毁{gone}) 失败:伤害{MissDamage}/死亡{MissDeath} 迟到重试{Retried} | {Snapshot()}";
        }

        public static string Segment(int netId)
        {
            if (netId <= 0) return "无号";
            if (netId >= DerivedNetIdBase) return "派生";
            if (netId >= HostSpawnNetIdBase) return "房主生成";
            if (netId >= SpecNetIdBase) return "特殊单位";
            return "敌人自增";
        }

        public static void Reset()
        {
            _ledger.Clear();
            _detailed.Clear();
            MissDamage = MissDeath = Retried = 0;
        }

        static string Diag(int netId)
        {
            if (netId >= DerivedNetIdBase) return "派生号两端算出的值不同（同 Id 的位置/序号有偏差）";
            if (netId >= HostSpawnNetIdBase) return "房主生成段：本端没收到 / 没复刻出 4051 那一只";
            if (netId >= SpecNetIdBase) return "特殊单位段：本端少建了一个 SpecUnit";
            return "敌人段：本端少建了一只（波次 / 巢穴投放数量两端不一致）";
        }

        static void ToFile(string line)
        {
            if (_fileBroken) return;
            try
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, "netid_probe.log");
                var fi = new System.IO.FileInfo(path);
                if (fi.Exists && fi.Length > MaxFileBytes) fi.Delete();
                System.IO.File.AppendAllText(path, System.DateTime.Now.ToString("MM-dd HH:mm:ss.fff ") + line + "\n");
            }
            catch { _fileBroken = true; }
        }
    }
}
