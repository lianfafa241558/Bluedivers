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
    }
}
