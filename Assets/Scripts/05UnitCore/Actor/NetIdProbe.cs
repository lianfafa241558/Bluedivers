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
    /// <para>▍落地明细同时追加到 <c>persistentDataPath/netid_probe_&lt;role&gt;.log</c>，Console 被清也能回读。</para>
    /// <para>⚠ 文件名带角色（host / member / solo）：编辑器 Play 与打包端的 <c>persistentDataPath</c> 是**同一个目录**，
    /// 固定文件名会让两端互相覆盖（2026-10-20 实测：本端 69 个号里混着对端写进去的内容）。</para>
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

        /// <summary>本端角色名（<c>host</c> / <c>member</c> / <c>solo</c>）—— 只用于**文件名**：
        /// 同机双端的 <c>persistentDataPath</c> 相同，固定文件名会互相覆盖。
        /// <para>▍为什么由外部写入：05（本程序集）看不见 07 的 <c>NetRoomFlow</c>，判角色只能在 09 侧
        /// （<c>NetFriendBridge.Update</c> → <see cref="TickAutoDump"/>，每帧都会来，所以进战斗后必定已是正确的）。
        /// 未写入前（战斗初始化那几帧）落到 <c>solo</c>，内容一样能读。</para></summary>
        static string _role = "solo";

        // ---------- 号 → 身份 映射（两端对照用） ----------

        /// <summary>一个号对应的身份：<c>NetId</c> + 单位 Id + 出生坐标（派生段还带"同 Id 同坐标序号"）。</summary>
        sealed class MapEntry
        {
            public int NetId;
            public string Id;
            public int QX, QY, QZ;      // 出生坐标（0.1m 量化，与派生号哈希同口径）
            public int Ordinal = -1;    // 仅派生段有意义（同 Id 同坐标的第几个）
            public int Seg;             // 见 SegmentOf()
        }

        static readonly List<MapEntry> _map = new List<MapEntry>();
        const int MaxMap = 8192;

        /// <summary>号段归类（0=自增 1=特殊 2=房主生成/载具 3=派生）。</summary>
        static int SegmentOf(int netId)
        {
            if (netId >= DerivedNetIdBase) return 3;
            if (netId >= HostSpawnNetIdBase) return 2;
            if (netId >= SpecNetIdBase) return 1;
            return 0;
        }

        /// <summary>段的显示名（写文件时的分块标题）。</summary>
        static string SegmentName(int seg) => seg switch
        {
            3 => "派生段（场景摆放/任务实体：两端各算各的，号必须相同）",
            2 => "房主生成段（4052 复刻：号由房主下发，两端必须相同）",
            1 => "特殊单位段（自增，两端必须同序）",
            _ => "敌人自增段（波次/巢穴：**第 n 号必须是同一种单位**）",
        };

        static void AddMap(int netId, string id, Vector3 pos, int ordinal)
        {
            if (!Enabled || netId == 0) return;
            if (_map.Count >= MaxMap) return;
            _map.Add(new MapEntry
            {
                NetId = netId,
                Id = id,
                QX = Mathf.RoundToInt(pos.x * 10f),
                QY = Mathf.RoundToInt(pos.y * 10f),
                QZ = Mathf.RoundToInt(pos.z * 10f),
                Ordinal = ordinal,
                Seg = SegmentOf(netId),
            });
        }

        /// <summary>开战后多久出第一份映射（等场景单位 / 任务实体都建完）。</summary>
        const float MapDumpDelay = 8f;
        /// <summary>之后每隔多久覆盖重写一次（保证拿到的是"最近一刻"的场上状态）。</summary>
        const float MapDumpInterval = 60f;
        static float _nextMapDump = -1f;

        /// <summary>总开关（关掉后连账都不记）。</summary>
        public static bool Enabled = false;

        public static int MissDamage;
        public static int MissDeath;
        public static int MissMove;      // "移动消息落地失败"（正常损耗居多：目标已阵亡/销毁）
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
            if (kind == "伤害") MissDamage++;
            else if (kind == "移动") MissMove++;
            else MissDeath++;

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
            return $"[NetIdProbe] 账本={_ledger.Count}(活{alive}/阵亡{slain}/已销毁{gone}) 失败:伤害{MissDamage}/死亡{MissDeath}/移动{MissMove} 迟到重试{Retried} | {Snapshot()}";
        }

        // ---------- 派生号映射：记录 + 落盘 ----------

        /// <summary>记录一次派生号计算（调用点唯一：<c>ActorsManager.DerivedNetId</c>）。
        /// <para>▍为什么把**输入**也一起记：两端号对不上时，光有两个 NetId 没法判断是
        /// "Id 不同（prefab/命名两端不一致）"、"出生坐标分叉"、还是"同 Id 同坐标的序号错位"。</para></summary>
        public static void RecordDerived(string id, int qx, int qy, int qz, int ordinal, int netId)
        {
            if (!Enabled) return;
            if (_map.Count >= MaxMap) return;
            _map.Add(new MapEntry { NetId = netId, Id = id, QX = qx, QY = qy, QZ = qz, Ordinal = ordinal, Seg = 3 });
        }

        /// <summary>记录一个**非派生段**的号（调用点：<c>Actor.NetId</c> 的 setter ⇒ 自增 / 特殊 / 4052 覆盖都在内）。
        /// <para>▍为什么要它：敌人自增号的口径是"两端第几个被创建"，只要两端创建顺序/数量有一点不同，
        /// 从某个号起就整体平移（A 端的 7 号是怪、B 端的 7 号是别的）⇒ 命中/死亡消息打到错误的单位或查不到。
        /// 记录 <c>号 → Id</c> 后，两端按号对齐就能直接看出"从第几号开始错位、错成什么"。</para>
        /// <para>⚠ 派生段已在 <see cref="RecordDerived"/> 里带序号记过（setter 也会来一次），这里跳过以免重复。</para></summary>
        public static void RecordMap(int netId, string id, Vector3 pos)
        {
            if (!Enabled || netId == 0) return;
            if (netId >= DerivedNetIdBase) return;
            AddMap(netId, id, pos, -1);
        }

        /// <summary>
        /// 【诊断】派生号映射自动落盘：开战后 <see cref="MapDumpDelay"/> 秒写第一份，之后每
        /// <see cref="MapDumpInterval"/> 秒覆盖一次（拿到的永远是最近一刻的场上状态）。
        /// <para>▍由 09 侧的 <c>NetFriendBridge.Update</c> 每帧驱动并传入本端角色名（05 看不见 07 的 <c>NetRoomFlow</c>）。</para>
        /// <para>⚠ 同机双端（编辑器 Play + 打包）的 <c>persistentDataPath</c> 是**同一个目录** ⇒ 文件名必须带角色，
        /// 否则两份互相覆盖。</para>
        /// </summary>
        public static void TickAutoDump(string role)
        {
            if (!Enabled) return;
            if (!string.IsNullOrEmpty(role)) _role = role;   // 落地明细的文件名靠它区分两端（见 _role）
            if (!FPSGame.Data.BattleState.IsStartBattle)
            {
                _nextMapDump = -1f;   // 还没开战 / 已经结算：下一局重新计时
                return;
            }
            float now = Time.unscaledTime;
            if (_nextMapDump < 0f)
            {
                _nextMapDump = now + MapDumpDelay;
                return;
            }
            if (now < _nextMapDump) return;
            _nextMapDump = now + MapDumpInterval;
            DumpMap(role);
        }

        /// <summary>把 <see cref="RecordMap"/> / <see cref="RecordDerived"/> 记下的映射写成
        /// <c>netid_map_&lt;role&gt;.log</c>（覆盖写，按号段分块）。
        /// <para>▍两段比较口径不同：<b>自增 / 特殊 / 房主生成段按 NetId 对齐</b>（同一个号是不是同一种单位）；
        /// <b>派生段按 Id+坐标+序号对齐</b>（两端各算各的号是不是同一个）。</para>
        /// <para>▍去重：同一个号被重赋（4052 覆盖）/ 同一身份被重建 ⇒ 只留**最后一条**，
        /// 否则文件的条数会被"重建次数"干扰（上一版实测 688 条里有 122 条重复）。</para></summary>
        public static void DumpMap(string role)
        {
            if (!Enabled) return;
            if (!string.IsNullOrEmpty(role)) _role = role;   // 与落地明细同名口径

            var seq = new Dictionary<int, MapEntry>();              // 非派生段：号唯一
            var derived = new Dictionary<string, MapEntry>();       // 派生段：身份唯一
            for (int i = 0; i < _map.Count; ++i)
            {
                var e = _map[i];
                if (e.Seg == 3)
                    derived[e.Id + "|" + e.QX + "|" + e.QY + "|" + e.QZ + "|" + e.Ordinal] = e;
                else
                    seq[e.NetId] = e;
            }

            var seqKeys = new List<int>(seq.Keys);
            seqKeys.Sort();
            var dv = new List<MapEntry>(derived.Values);
            dv.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Id, b.Id);
                if (c != 0) return c;
                c = a.QX.CompareTo(b.QX); if (c != 0) return c;
                c = a.QY.CompareTo(b.QY); if (c != 0) return c;
                c = a.QZ.CompareTo(b.QZ); if (c != 0) return c;
                return a.Ordinal.CompareTo(b.Ordinal);
            });

            _sb.Clear();
            _sb.Append("# netid_map role=").Append(role)
               .Append(" 时间=").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
               .Append(" 非派生=").Append(seqKeys.Count).Append(" 派生=").Append(dv.Count)
               .Append(" | ").Append(Snapshot()).Append('\n');
            _sb.Append("# 自增/特殊/房主生成段按 **NetId 对齐**（第 n 号是不是同一种单位）；派生段按 **Id+坐标+序号对齐**\n");
            _sb.Append("# 列: NetId \\t Id \\t qx,qy,qz(0.1m) [\\t #同Id同坐标序号（仅派生段）]\n");

            for (int seg = 0; seg <= 2; ++seg)
            {
                bool headerWritten = false;
                for (int i = 0; i < seqKeys.Count; ++i)
                {
                    var e = seq[seqKeys[i]];
                    if (e.Seg != seg) continue;
                    if (!headerWritten)
                    {
                        _sb.Append("# === ").Append(SegmentName(seg)).Append(" ===\n");
                        headerWritten = true;
                    }
                    _sb.Append(e.NetId).Append('\t').Append(e.Id).Append('\t')
                       .Append(e.QX).Append(',').Append(e.QY).Append(',').Append(e.QZ).Append('\n');
                }
            }

            if (dv.Count > 0) _sb.Append("# === ").Append(SegmentName(3)).Append(" ===\n");
            for (int i = 0; i < dv.Count; ++i)
            {
                var e = dv[i];
                _sb.Append(e.NetId).Append('\t').Append(e.Id).Append('\t')
                   .Append(e.QX).Append(',').Append(e.QY).Append(',').Append(e.QZ).Append('\t')
                   .Append('#').Append(e.Ordinal).Append('\n');
            }

            string path = System.IO.Path.Combine(Application.persistentDataPath, "netid_map_" + role + ".log");
            try
            {
                System.IO.File.WriteAllText(path, _sb.ToString());
                _lastMapDumpAt = Time.unscaledTime;
                Debug.Log($"[NetIdProbe] 号映射已写出 {path}（非派生 {seqKeys.Count} / 派生 {dv.Count}）");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[NetIdProbe] 写号映射失败: " + e.Message);
            }
        }

        static float _lastMapDumpAt = -999f;

        /// <summary>【诊断】"远端消息落地失败"那一刻顺手把映射刷一次（限流 5 秒）——
        /// 这样文件内容正好对应出事那一刻，而不是最近一次定时快照。</summary>
        public static void DumpMapOnMiss(string role)
        {
            if (!Enabled) return;
            if (Time.unscaledTime - _lastMapDumpAt < 5f) return;
            DumpMap(role);
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
            _map.Clear();
            _nextMapDump = -1f;
            MissDamage = MissDeath = MissMove = Retried = 0;
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
                var path = System.IO.Path.Combine(Application.persistentDataPath, "netid_probe_" + _role + ".log");
                var fi = new System.IO.FileInfo(path);
                if (fi.Exists && fi.Length > MaxFileBytes) fi.Delete();
                System.IO.File.AppendAllText(path, System.DateTime.Now.ToString("MM-dd HH:mm:ss.fff ") + line + "\n");
            }
            catch { _fileBroken = true; }
        }
    }
}
