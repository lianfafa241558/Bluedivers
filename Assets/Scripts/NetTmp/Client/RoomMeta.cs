using System;
using KCPNet;

namespace FPSGame.Net
{
    /// <summary>
    /// 【房间扩展元数据适配层】——库缺字段期间的**唯一适配点**。
    ///
    /// <para>▍为什么需要它：KCPNet 的 <see cref="LanRoomInfo"/> 目前只有 9 个字段
    /// （<c>RoomName / HostIp / HostPort / PlayerCount / MaxPlayers / MapName /
    /// PasswordProtected / Version / PlayerNames</c>），**难度 / 任务类型 / 是否已开局 / 房间来源**
    /// 这四样广播里都没有。UI（房间列表面板）却要显示它们 ⇒ 先把"取值"收敛到本类：
    /// UI 只调 <see cref="Difficulty"/> / <see cref="TaskType"/> / <see cref="InGame"/> /
    /// <see cref="Source"/> / <see cref="MapName"/> / <see cref="RoomName"/>（剥后缀的干净房间名），
    /// 不直接碰 <c>MapName</c>/<c>RoomName</c> 的字符串约定、也不自己按 IP 猜来源。</para>
    ///
    /// <para>▍两条**临时字符串约定**（2026-10-07，库缺字段期间的权宜之计）：
    /// <list type="bullet">
    ///   <item>难度 → 拼在 <c>MapName</c> 后面：<c>"地图#难度int"</c>（<see cref="ComposeMapName"/>）；</item>
    ///   <item>任务类型 → 拼在 <c>RoomName</c> 后面：<c>"房间名#T=枚举int|任务类型名"</c>（<see cref="ComposeRoomName"/>）。
    ///         ⚠ 标记取 <c>#T=</c> 而不是单 <c>#</c>：房间名是玩家自己的名字拼的，越不可能是合法人名越安全；
    ///         解析固定取**最后一次**出现（<c>LastIndexOf</c>）⇒ 房主名字里真带 <c>#T=</c> 也不会误切。</item>
    /// </list></para>
    ///
    /// <para>▍为什么**既要枚举又要名字**（2026-10-07 追加）：房间列表那行的图标/颜色只有本地
    /// <c>MissionMainData_SO</c> 才有，而**任务类型名不唯一** —— <c>Resources/GameData/Mission/Main</c> 里
    /// 「进攻任务」有 3 份、「歼灭任务」「渗透任务」各 2 份，**且它们的 color 各不相同**
    /// ⇒ 只靠名字反查会给错色。枚举是两端一致的整数 ⇒ 能精确定位；名字留着做**显示**与**旧版房主兼容**
    /// （旧版只带名字，此时 <see cref="TaskEnum"/> 返回 <see cref="UnknownTaskEnum"/>）。</para>
    ///
    /// <para>⚠ <c>MissionEnum</c> 定义在 <c>01_GameContract</c>，而本类所在 <c>02_Net</c> 的 asmdef
    /// <c>references</c> 为空 ⇒ 本类**只处理 <c>int</c>**，由 UI 侧转成 <c>MissionEnum</c> 再查表。</para>
    ///
    /// <para>▍⚠⚠ 库改造点（**暂缓**，用户之后统一改 KCPNet 源码并重出 DLL）：
    /// 给 <c>LanRoomInfo</c> 加 <c>public int Difficulty = -1; public string TaskType; public bool InGame; public int Source;</c>
    /// 并在 <c>ToJson()</c> / <c>FromJson()</c> 里带上（缺 key 给默认值）之后，
    /// **只需要改本文件里带 <c>TODO(库)</c> 的方法体**，UI 一行都不用动：
    /// <list type="number">
    ///   <item><see cref="Difficulty"/> ⇒ <c>return room.Difficulty;</c>（删掉 <c>#</c> 约定解析）</item>
    ///   <item><see cref="TaskType"/> ⇒ <c>return room.TaskType;</c> + <see cref="RoomName"/> 改成 <c>return room.RoomName;</c>
    ///         （删掉 <c>#T=</c> 解析）</item>
    ///   <item><see cref="InGame"/> ⇒ <c>return room.InGame;</c></item>
    ///   <item><see cref="Source"/> ⇒ <c>return (SourceEnum)room.Source;</c>（删掉按地址猜）</item>
    /// </list>
    /// 开房侧同步改 <see cref="NetHostSvc.StartHost(HostRoomOptions)"/>（那里也留了 TODO）。
    /// 详见 <c>.codebuddy/plans/联机_房间列表接入与KCPNet扩展_计划.md</c> §1。</para>
    /// </summary>
    public static class RoomMeta
    {
        /// <summary>难度未知（旧版房主 / 没按约定拼 <c>#</c> 后缀）。</summary>
        public const int UnknownDifficulty = -1;

        /// <summary>任务类型未知（旧版房主只带名字 / 还没选任务 / 没按约定拼）。</summary>
        public const int UnknownTaskEnum = -1;

        /// <summary>房间名里「任务类型」后缀的标记（见 <see cref="ComposeRoomName"/>）。</summary>
        private const string TaskMark = "#T=";

        /// <summary>后缀里「枚举」与「名字」的分隔符（<c>#T=3|收集任务</c>）。</summary>
        private const char EnumSep = '|';

        /// <summary>房间来源。⚠ 取值与库里的 <c>int Source</c> 字段约定一致（0/1）。</summary>
        public enum SourceEnum
        {
            /// <summary>局域网 UDP 广播发现的房间。</summary>
            LanBroadcast = 0,
            /// <summary>专用服务器下发的房间列表。</summary>
            ServerList = 1,
        }

        /// <summary>
        /// 房间难度。<paramref name="fromBroadcast"/> = 这个值是否**真的来自广播**。
        ///
        /// <para>TODO(库)：<c>LanRoomInfo</c> 加上 <c>int Difficulty</c> 后，本方法整个改成
        /// <c>fromBroadcast = room != null &amp;&amp; room.Difficulty &gt;= 0; return room.Difficulty;</c></para>
        /// </summary>
        public static int Difficulty(LanRoomInfo room, out bool fromBroadcast)
        {
            fromBroadcast = false;

            //TODO(库·临时约定)：房主把难度拼在 MapName 后面（"地图#难度int"）。库加字段后删掉这段。
            if (room != null && !string.IsNullOrEmpty(room.MapName))
            {
                int at = room.MapName.IndexOf('#');
                if (at >= 0 && at + 1 < room.MapName.Length)
                {
                    int value;
                    if (int.TryParse(room.MapName.Substring(at + 1), out value) && value >= 0)
                    {
                        fromBroadcast = true;
                        return value;
                    }
                }
            }
            return UnknownDifficulty;
        }

        /// <summary>房间难度（不关心来源时用）。</summary>
        public static int Difficulty(LanRoomInfo room)
        {
            return Difficulty(room, out _);
        }

        /// <summary>
        /// 房间是否**已经开局**（人还没满也可能已开）。
        /// <para>TODO(库)：<c>LanRoomInfo</c> 加上 <c>bool InGame</c> 后改成 <c>return room != null &amp;&amp; room.InGame;</c></para>
        /// </summary>
        public static bool InGame(LanRoomInfo room)
        {
            return false;
        }

        /// <summary>
        /// 房间来源。
        /// <para>TODO(库)：<c>LanRoomInfo</c> 加上 <c>int Source</c> 后改成
        /// <c>return room == null ? SourceEnum.LanBroadcast : (SourceEnum)room.Source;</c>；
        /// 在此之前只能按主机地址猜（内网/环回 ⇒ 局域网，其余 ⇒ 服务器）。</para>
        /// </summary>
        public static SourceEnum Source(LanRoomInfo room)
        {
            return IsLanAddress(room != null ? room.HostIp : null)
                ? SourceEnum.LanBroadcast
                : SourceEnum.ServerList;
        }

        /// <summary>
        /// 房间的**任务类型名**（如「歼灭」/「护送」），房主开房/确认任务时拼进房间名。
        /// 返回 null = 这个房间没带（旧版房主 / 还没选任务 / 没按约定拼）。
        ///
        /// <para>TODO(库)：<c>LanRoomInfo</c> 加上 <c>string TaskType</c> 后改成 <c>return room != null ? room.TaskType : null;</c>，
        /// 并删掉 <see cref="ComposeRoomName"/> 与 <see cref="RoomName"/> 里的剥后缀（开房侧同步改）。</para>
        /// </summary>
        public static string TaskType(LanRoomInfo room)
        {
            string payload = TaskPayload(room);
            if (string.IsNullOrEmpty(payload)) return null;

            // 新约定 "#T=枚举|名字"：名字在分隔符之后；旧约定 "#T=名字"：整段就是名字
            int sep = payload.IndexOf(EnumSep);
            string type = (sep >= 0 ? payload.Substring(sep + 1) : payload).Trim();
            return type.Length > 0 ? type : null;
        }

        /// <summary>
        /// 房间的**主任务类型枚举值**（<c>MissionEnum</c> 的 int；<see cref="UnknownTaskEnum"/> = 没带）。
        ///
        /// <para>▍为什么要有它：任务类型**名字不唯一**（3 份「进攻任务」颜色各不相同）⇒ 房间列表要精确取
        /// 图标/颜色就只能靠枚举。<b>这是本约定存在的首要理由</b>，名字只是顺带的显示值。</para>
        ///
        /// <para>⚠ 返回值是 <c>int</c> 而不是 <c>MissionEnum</c>：本类在 <c>02_Net</c>，看不见 <c>01_GameContract</c>
        /// 的枚举（asmdef references 为空）⇒ 由 UI 侧强转，见 <c>ServerListPanel.RoomTaskMission</c>。</para>
        ///
        /// <para>TODO(库)：<c>LanRoomInfo</c> 加上 <c>int TaskMain</c> 后改成
        /// <c>return room != null ? room.TaskMain : UnknownTaskEnum;</c> 并删掉 <c>#T=</c> 解析。</para>
        /// </summary>
        public static int TaskEnum(LanRoomInfo room)
        {
            string payload = TaskPayload(room);
            if (string.IsNullOrEmpty(payload)) return UnknownTaskEnum;

            int sep = payload.IndexOf(EnumSep);
            if (sep <= 0) return UnknownTaskEnum;   // 旧版房主：只有名字，没有枚举

            int value;
            return int.TryParse(payload.Substring(0, sep).Trim(), out value) && value >= 0
                ? value
                : UnknownTaskEnum;
        }

        /// <summary><c>#T=</c> 之后那段原始内容（未按 <see cref="EnumSep"/> 切分；没有标记 ⇒ null）。</summary>
        private static string TaskPayload(LanRoomInfo room)
        {
            string raw = room != null ? room.RoomName : null;
            if (string.IsNullOrEmpty(raw)) return null;

            int at = raw.LastIndexOf(TaskMark, StringComparison.Ordinal);
            if (at < 0) return null;

            string payload = raw.Substring(at + TaskMark.Length).Trim();
            return payload.Length > 0 ? payload : null;
        }

        /// <summary>房间的**干净房间名**（剥掉 <c>#T=任务类型</c> 后缀；没有后缀时原样返回）。显示 / 搜索 / 排序都用它。</summary>
        public static string RoomName(LanRoomInfo room)
        {
            string raw = room != null ? room.RoomName : null;
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            int at = raw.LastIndexOf(TaskMark, StringComparison.Ordinal);
            return at >= 0 ? raw.Substring(0, at) : raw;
        }

        /// <summary>
        /// 按约定把任务类型拼进房间名（供**房主开房 / 确认任务**时用）：
        /// <c>"房间名#T=枚举int|任务类型名"</c>（<paramref name="taskMain"/> &lt; 0 时退化成只拼名字，兼容旧格式）。
        /// 两者都空 = 不拼（保持原样）；重复调用幂等（先剥旧后缀，房主改选任务时能直接覆盖）。
        /// <para>TODO(库)：<c>LanRoomInfo</c> 有 <c>TaskType</c>/<c>TaskMain</c> 字段后，这个方法与它的调用点一起删。</para>
        /// </summary>
        /// <param name="taskMain"><c>MissionEnum</c> 的 int（-1 = 未知/旧版）——**精确取图标/颜色的唯一依据**。</param>
        public static string ComposeRoomName(string roomName, string taskType, int taskMain = UnknownTaskEnum)
        {
            string clean = StripTaskMark(roomName ?? string.Empty);
            string name = taskType != null ? taskType.Trim() : string.Empty;
            if (taskMain < 0 && name.Length == 0) return clean;

            return clean + TaskMark + (taskMain >= 0 ? taskMain + EnumSep.ToString() : string.Empty) + name;
        }

        /// <summary>剥掉串里的 <c>#T=…</c> 后缀（<see cref="ComposeRoomName"/> 的幂等基础，也接受"只有标记没内容"）。</summary>
        private static string StripTaskMark(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            int at = raw.LastIndexOf(TaskMark, StringComparison.Ordinal);
            return at >= 0 ? raw.Substring(0, at) : raw;
        }

        /// <summary>房间的地图名（剥掉 <c>#难度</c> 后缀；没有后缀时原样返回）。</summary>
        public static string MapName(LanRoomInfo room)
        {
            string raw = room != null ? room.MapName : null;
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            int at = raw.IndexOf('#');
            return at >= 0 ? raw.Substring(0, at) : raw;
        }

        /// <summary>是否内网 / 环回地址（10/8、172.16/12、192.168/16、169.254/16、127/8、localhost）。</summary>
        public static bool IsLanAddress(string ip)
        {
            // 地址不明时按局域网算：当前唯一的房间来源就是局域网广播
            if (string.IsNullOrEmpty(ip)) return true;
            if (ip == "localhost" || ip == "::1") return true;

            string[] parts = ip.Split('.');
            if (parts.Length != 4) return false;

            int a, b;
            if (!int.TryParse(parts[0], out a) || !int.TryParse(parts[1], out b)) return false;

            if (a == 10 || a == 127) return true;
            if (a == 172 && b >= 16 && b <= 31) return true;
            if (a == 192 && b == 168) return true;
            if (a == 169 && b == 254) return true;
            return false;
        }

        /// <summary>
        /// 按约定把难度拼进地图名（供**房主开房**时用）。
        /// <para>TODO(库)：<c>LanRoomInfo</c> 有 <c>Difficulty</c> 字段后，这个方法与它的调用点一起删。</para>
        /// </summary>
        public static string ComposeMapName(string mapName, int difficulty)
        {
            if (difficulty < 0) return mapName ?? string.Empty;
            return (mapName ?? string.Empty) + "#" + difficulty;
        }
    }
}
