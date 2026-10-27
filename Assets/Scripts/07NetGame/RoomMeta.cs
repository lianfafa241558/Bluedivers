using KCPNet;

namespace FPSGame.Net
{
    /// <summary>
    /// 【房间字段适配层】房间列表 UI 与库的 <see cref="LanRoomInfo"/> 之间的**唯一取值点**。
    ///
    /// <para>▍职责：把"缺省值归一 + 空引用保护"收在一处。UI 只调
    /// 用本地任务配置反查（<c>TaskManager.FindMainMission</c>）。</para>
    /// </summary>
    public static class RoomMeta
    {


        /// <summary>房间来源。⚠ 取值与库里的 <c>int Source</c> 字段约定一致（0/1）。</summary>
        public enum SourceEnum
        {
            /// <summary>局域网 UDP 广播发现的房间。</summary>
            LanBroadcast = 0,
            /// <summary>专用服务器下发的房间列表。</summary>
            ServerList = 1,
        }

        /// <summary>房间难度（不关心来源时用）。</summary>
        public static int Difficulty(LanRoomInfo room)
        {
            return room!=null ? room.Difficulty : 3;
        }

        /// <summary>房间是否**已经开局**（人还没满也可能已开）。</summary>
        public static bool InGame(LanRoomInfo room)
        {
            return room != null && room.InGame;
        }

        /// <summary>房间来源（旧版房主缺该字段 ⇒ 默认按局域网算，与旧行为一致）。</summary>
        public static SourceEnum Source(LanRoomInfo room)
        {
            if (room == null) return SourceEnum.LanBroadcast;
            return room.Source == (int)SourceEnum.ServerList ? SourceEnum.ServerList : SourceEnum.LanBroadcast;
        }

        /// <summary>
        /// 房间的**主任务类型枚举值**（<c>MissionEnum</c> 的 int
        /// </summary>
        public static int TaskEnum(LanRoomInfo room)
        {
            return room != null ? room.TaskMain : 1;
        }

        /// <summary>房间名（显示 / 搜索 / 排序 / 密码弹窗都用它）。</summary>
        public static string RoomName(LanRoomInfo room)
        {
            return room != null && !string.IsNullOrEmpty(room.RoomName) ? room.RoomName : string.Empty;
        }

        /// <summary>房间的地图名（筛选 / 查 <c>MapData_SO</c> 用）。</summary>
        public static string MapName(LanRoomInfo room)
        {
            return room != null && !string.IsNullOrEmpty(room.MapName) ? room.MapName : string.Empty;
        }

        // ==================== 2026-10-13 库新增三字段的读取口 ====================

        /// <summary>房主名（库新增的 <c>LanRoomInfo.HostName</c>）。
        /// ⚠ 老版本房主没这个字段 ⇒ 返回 null，调用方**退回**"从 <c>PlayerNames</c> 末尾找非 Host_ 前缀项"的老启发式。</summary>
        public static string HostName(LanRoomInfo room)
        {
            return room != null && !string.IsNullOrEmpty(room.HostName) ? room.HostName : null;
        }

        /// <summary>房间的**稳定身份**（库新增的 <c>LanRoomInfo.RoomId</c>，房主开房时生成的 GUID）。
        /// <para>▍比"合成房主名 + 端口"稳：与网卡 / 端口 / 时间戳都无关 ⇒ 列表去重、"这房是不是我开的"都首选它。
        /// 老版本房主 ⇒ 返回 null，调用方回退。</para></summary>
        public static string RoomId(LanRoomInfo room)
        {
            return room != null && !string.IsNullOrEmpty(room.RoomId) ? room.RoomId : null;
        }

        /// <summary>本局**任务名**（库的 <c>LanRoomInfo.TaskName</c>，房主开房/确认任务时写入；空 = 还没选任务）。
        /// <para>▍与 <see cref="TaskEnum"/> 分工：枚举 → 图标/颜色（**类型**级，如「歼灭」）；
        /// 这里 → 标题（**实例**级，如「深渊回响」）。空时调用方退回按枚举反查类型名。</para></summary>
        public static string TaskName(LanRoomInfo room)
        {
            return room != null && !string.IsNullOrEmpty(room.TaskName) ? room.TaskName : null;
        }

        // ==================== 版本门槛 ====================

        /// <summary>本端与房间的版本关系（联机门槛用）。</summary>
        public enum VersionRelation
        {
            /// <summary>判不出来（对面没带版本 / 版本号不是纯数字）——**放行**：
            /// 老版本房主与"手动直连"构造的房间都落这里，别把它们当"不兼容"。</summary>
            Unknown = 0,
            /// <summary>完全一致。</summary>
            Same = 1,
            /// <summary>对面更旧（本端更新）⇒ 对面该更新，本端**无法联机**。</summary>
            PeerOlder = 2,
            /// <summary>对面更新（本端更旧）⇒ **本端该更新**。</summary>
            PeerNewer = 3,
        }

        /// <summary>本端（<c>Application.version</c>）与房间版本的关系。</summary>
        public static VersionRelation VersionRelationOf(LanRoomInfo room)
        {
            return CompareVersion(UnityEngine.Application.version, room != null ? room.Version : null);
        }

        /// <summary>版本是否**可联机**（判不出来 = 放行）。</summary>
        public static bool IsVersionCompatible(LanRoomInfo room)
        {
            var rel = VersionRelationOf(room);
            return rel == VersionRelation.Same || rel == VersionRelation.Unknown;
        }

        /// <summary>
        /// 版本不兼容时给玩家的提示；可联机 / 判不出来 ⇒ <c>null</c>（调用方据此决定要不要拦）。
        /// <para>▍两句文案是用户口径：对方更旧 →「对方版本不同无法联机」；自己更旧 →「请更新版本」。</para>
        /// </summary>
        public static string VersionBlockTip(LanRoomInfo room)
        {
            var rel = VersionRelationOf(room);
            return VersionBlockTip(rel, UnityEngine.Application.version, room != null ? room.Version : null);
        }

        /// <summary>提示文案（把版本号一起打出来，内测现场好对账）。</summary>
        public static string VersionBlockTip(VersionRelation rel, string mine, string peer)
        {
            switch (rel)
            {
                case VersionRelation.PeerOlder:
                    return $"对方版本不同，无法联机（房主 v{peer}，本端 v{mine}）—— 需要双方版本一致。";
                case VersionRelation.PeerNewer:
                    return $"房主版本更新，请更新版本后再联机（房主 v{peer}，本端 v{mine}）。";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 版本串比较：按 <c>'.'</c> 切段逐段比数值，短的一方缺段补 0 ⇒ <c>"1.0"</c> 等于 <c>"1.0.0"</c>。
        /// <para>▍为什么不用 <c>System.Version</c>：Unity 的 <c>Application.version</c> 可能是
        /// <c>"1.0.3b"</c> / <c>"0.0.1-beta"</c> 这类带后缀的串，<c>Version.TryParse</c> 直接失败。
        /// 而这里是**联机门槛** ⇒ 判不出来一律 <see cref="VersionRelation.Unknown"/> 放行（宁可放行也别误拦）。</para>
        /// </summary>
        public static VersionRelation CompareVersion(string mine, string theirs)
        {
            if (!TryParseVersion(mine, out var a) || !TryParseVersion(theirs, out var b))
                return VersionRelation.Unknown;

            int len = System.Math.Max(a.Length, b.Length);
            for (int i = 0; i < len; ++i)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x == y) continue;
                return x > y ? VersionRelation.PeerOlder : VersionRelation.PeerNewer;
            }
            return VersionRelation.Same;
        }

        /// <summary>把 <c>"v1.2.3"</c> 这类串切成纯数字段；任一段不是纯数字 ⇒ false（交给 Unknown 放行）。</summary>
        private static bool TryParseVersion(string version, out int[] parts)
        {
            parts = null;
            if (string.IsNullOrEmpty(version)) return false;

            string s = version.Trim();
            if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);
            if (s.Length == 0) return false;

            string[] segs = s.Split('.');
            var nums = new int[segs.Length];
            for (int i = 0; i < segs.Length; ++i)
            {
                string seg = segs[i].Trim();
                if (seg.Length == 0) return false;
                for (int k = 0; k < seg.Length; ++k)
                {
                    if (seg[k] < '0' || seg[k] > '9') return false;
                }
                if (!int.TryParse(seg, out nums[i])) return false;
            }

            parts = nums;
            return nums.Length > 0;
        }
    }
}
