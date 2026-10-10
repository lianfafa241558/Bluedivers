using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 出生点 / 集合点的"每人一位"摊算（2026-10-10）。
    ///
    /// <para>▍口径：把**一个点**摊成**以它为中心、左右对称的一排**（沿 +X）：
    /// 队伍 N 人、第 i 号 ⇒ 偏移 <c>(i - (N-1)/2) × 间距</c>，即
    /// 2 人 ⇒ 0 号 <c>(-0.25,0,0)</c>、1 号 <c>(+0.25,0,0)</c>；
    /// 3 人 ⇒ 0 号 <c>(-0.5,0,0)</c>、1 号 <c>(0,0,0)</c>、2 号 <c>(+0.5,0,0)</c>（间距 0.5）。</para>
    ///
    /// <para>▍为什么要有这个类：这些场合必须用**同一套**摊算，否则各端算出来的"我该站哪"不一致 ——
    /// 大厅**开局落地**（<c>BridgeRoleManager.GetStartPoint</c>，各端同一帧各自刷自己的人）、
    /// 大厅**集合点**（<c>MoveSelfToReadyPoint</c>，手动按 <c>O</c>）、战斗**出生点**
    /// （<c>BattleRoleManager.GetStartPoint</c>）、盟友**初始落点**（<c>NetFriendBridge.NextSpawnPos</c>）。
    /// 口径：间距 0.5、按队伍以中心对称排开。</para>
    /// <para>▍**例外（故意不用它）**：入房时给新人让位（<c>BridgeRoleManager.FindFreeSpotNear</c>）——
    /// 那一步只挪新人、要跟"别人已经站好的位置"对齐，所以读别人的**实际坐标**沿 +X 找空位。</para>
    ///
    /// <para>▍序号口径：**房主视角下标**（房主 = 0；成员 = 自己在房主名单里的下标），见 <see cref="LocalOrdinal"/>。
    /// ⚠ 不能用 <c>TeamManager.SelfIndex</c>：本地名单把"自己"固定放在第一位，它恒为 0，谁都错不开位。</para>
    ///
    /// <para>▍各端只挪**自己**那一台（序号由"自己在房主名单里的下标"独立算出，天然一致），
    /// 其余玩家靠 20Hz 位姿同步跟过来 ⇒ 不需要额外的网络消息。</para>
    /// </summary>
    public static class SpawnSlots
    {
        /// <summary>本机玩家在**房主视角名单**里的序号（房主 = 0；单机 / 名单未知 = 0）。</summary>
        public static int LocalOrdinal
        {
            get
            {
                var flow = NetRoomFlow.Instance;
                int index = flow != null ? flow.SelfHostIndex : -1;
                return index > 0 ? index : 0;
            }
        }

        /// <summary>当前队伍人数（至少 1）。<c>TeamManager</c> 还没就绪 ⇒ 按 1 算（偏移 0，与原行为一致）。</summary>
        public static int TeamCount
        {
            get
            {
                var team = TeamManager.Instance;
                if (team == null || team.players == null) return 1;

                int n = 0;
                for (int i = 0; i < team.players.Count; ++i)
                {
                    if (team.players[i] != null) ++n;   // 只数有效项（空槽不算人）
                }
                return n > 0 ? n : 1;
            }
        }

        /// <summary>第 <paramref name="index"/> 号玩家相对中心点的偏移量（沿 +X，以中心对称）。
        /// <para>⚠ <paramref name="index"/> 是**房主视角下标**（0 起），不是"第几个"（1 起）。</para></summary>
        public static float OffsetX(int index, float spacing)
            => (index - (TeamCount - 1) * 0.5f) * spacing;

        /// <summary>把"一个点"摊成"第 <paramref name="index"/> 号该站的那一位"。</summary>
        public static Vector3 Spread(Vector3 center, int index, float spacing)
            => center + new Vector3(OffsetX(index, spacing), 0f, 0f);
    }
}
