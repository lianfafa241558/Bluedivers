using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 「本局玩家单位」的身份接口 —— <b>本机玩家（<c>PlayerController</c>）与联机盟友实例（<c>FriendController</c>）都实现它</b>。
    ///
    /// <para>▍解决什么：很多地方手里是一个"可能是玩家、也可能是盟友实例"的物体
    /// （伤害来源/起源、击杀归属、统计、HUD），而两者在本端是**两个不同的组件类型**
    /// ⇒ 只认 <c>PlayerController</c> 的代码会把"队友（或他召唤的炮台）做的事"算到别人头上
    /// （2026-10-12 查实：房主端"队友的炮台击杀"被记到房主）。有了本接口，这类换算一律走
    /// <see cref="PlayerUnitUtil.TryGetPlayerIndex(GameObject, out int)"/>。</para>
    ///
    /// <para>▍口径：<see cref="PlayerIndex"/> = <b>本局玩家序号</b>（<c>TeamManager.players</c> 的下标；
    /// <c>ArmamentWnd</c>/<c>GameEndWnd</c>/<c>TaskData.BattleData</c> 全按它索引，房主通常是 0）。</para>
    ///
    /// <para>▍谁写：本机玩家由 <c>PlayerController.Init(PlayerIndex)</c>；盟友实例由 09 的
    /// <c>NetFriendBridge</c> 在建实例时按 sid→下标写入（那张表只有 09 有，06/10 看不见）。</para>
    /// </summary>
    public interface IPlayerUnit
    {
        /// <summary>本局玩家序号（= <c>TeamManager.players</c> 下标）；<c>&lt; 0</c> = 未就位 / 认不出。</summary>
        int PlayerIndex { get; }

        /// <summary>播报 / 名牌用的**玩家名**（= 联机昵称）；空 ⇒ 调用方回落身份名（<c>IEntity.ShowName</c>，角色名）。
        ///
        /// <para>▍口径（两端同源）：本机玩家 = 存档昵称 <c>ArchivesData_SO.Current.playerName</c>
        /// （入房 / 开房上报的就是它）；盟友实例 = 名单资料里带来的玩家名（<c>FriendController.PlayerName</c>）。</para>
        ///
        /// <para>▍⚠ 别与 <c>BaseSelfController.PlayerName</c>（= <c>Actor.ShowName</c>，**角色名**）混：
        /// 那个答的是"演的是谁"，这个答的是"谁"。消息窗播报一律用本属性。</para></summary>
        string PlayerName { get; }
    }

    /// <summary>
    /// <see cref="IPlayerUnit"/> 的统一取用。⚠ 不要自己写 <c>GetComponent&lt;PlayerController&gt;()</c> 去判
    /// "这是哪个玩家" —— 那对盟友实例一定失败（见 <see cref="IPlayerUnit"/>）。
    /// </summary>
    public static class PlayerUnitUtil
    {
        /// <summary>
        /// 把"可能是玩家 / 盟友实例"的物体换算成**本局玩家序号**；认不出 ⇒ <c>false</c> 且 <paramref name="index"/> = -1。
        ///
        /// <para>▍会往上找一层：传进来的常常是**子物体**（武器 / 挂点 / 召唤物），而组件挂在单位根上。</para>
        /// </summary>
        public static bool TryGetPlayerIndex(GameObject go, out int index)
        {
            index = -1;
            if (go == null) return false;

            var unit = go.GetComponent<IPlayerUnit>();
            if (unit == null) unit = go.GetComponentInParent<IPlayerUnit>();
            if (unit == null || unit.PlayerIndex < 0) return false;

            index = unit.PlayerIndex;
            return true;
        }
    }
}
