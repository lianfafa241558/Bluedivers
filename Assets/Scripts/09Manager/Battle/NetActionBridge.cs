using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · 玩家世界动作同步桥（09）：**标记点位（4037） / 呼叫凯伊（4038）**。
    ///
    /// <para>▍这两条都是"玩家发起、别人该看见"的动作，且都**带动玩法副作用**，所以不能只同步一个视觉：
    /// 标记会驱动盟友的"敌情"喊话与任务点暴露；呼叫凯伊会让**凯伊单位真的走过去 + 放信标 + 救人**。
    /// 因此远端重放走的是同一条事件链（<c>GlobalEventBus.Mark</c> / <c>BattleEventBus.CallKai</c>）。</para>
    ///
    /// <para>▍消息走向（与 <see cref="NetFurnitureBridge"/> 同款）：本端发生 → 成员**上报房主**、房主**广播全体**；
    /// 房主收到上报 ⇒ **转发全体 + 本地应用**；成员收到 ⇒ **按 Sid 丢掉自己那条**（本地已发生）再应用。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> Install/Uninstall。</para>
    /// </summary>
    public static class NetActionBridge
    {
        static bool installed;

        /// <summary>回环门：true = 正在应用远端消息 ⇒ 本地事件不许再上行（防回环）。</summary>
        static bool _applyingRemote;

        /// <summary>sid → 盟友实例 的查询来源（常驻 GameRoot 上），惰性解析并缓存。</summary>
        static NetFriendBridge _friendBridge;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _applyingRemote = false;
            _friendBridge = null;

            GlobalEventBus.OnMark += OnLocalMark;
            BattleEventBus.OnCallKai += OnLocalCallKai;

            NetRoomFlow.OnMark += OnRemoteMark;
            NetRoomFlow.OnCallKai += OnRemoteCallKai;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            GlobalEventBus.OnMark -= OnLocalMark;
            BattleEventBus.OnCallKai -= OnLocalCallKai;

            NetRoomFlow.OnMark -= OnRemoteMark;
            NetRoomFlow.OnCallKai -= OnRemoteCallKai;
            _friendBridge = null;
        }

        #region 本端 -> 网络
        // 这两个事件的发布点都只有"本机玩家"（Mark ← VFXHaloEffect / CallKai ← PlayerController 的输入）
        // ⇒ 不需要再比对身份，按角色取 sid 即可。

        static void OnLocalMark(GameObject owner, GameObject target, Vector3 point)
        {
            if (_applyingRemote) return;
            var flow = NetRoomFlow.Instance;
            if (flow == null) return;
            flow.SendMark(flow.IsHost ? 0u : flow.SelfSid, point.x, point.y, point.z);
        }

        static void OnLocalCallKai(GameObject source, Vector3 point)
        {
            if (_applyingRemote) return;
            var flow = NetRoomFlow.Instance;
            if (flow == null) return;
            flow.SendCallKai(flow.IsHost ? 0u : flow.SelfSid, point.x, point.y, point.z);
        }
        #endregion

        #region 网络 -> 本端

        static void OnRemoteMark(MarkMsg m)
        {
            if (m == null) return;
            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost) flow.SendMark(m.Sid, m.X, m.Y, m.Z);   // 房主：转发全体
            else if (m.Sid == flow.SelfSid) return;                 // 成员：自己那条绕回来了

            // ⚠ target 传 null：目标实体是**引用**不能过网。标记的表现落点（字幕/光环）用 owner + point 就够；
            //   任务点的"被发现"由任务同步（4035）负责，不依赖这条。
            var owner = ResolveRemoteUser(m.Sid);

            _applyingRemote = true;
            try
            {
                GlobalEventBus.Mark(owner, null, new Vector3(m.X, m.Y, m.Z));
            }
            finally
            {
                _applyingRemote = false;
            }
        }

        static void OnRemoteCallKai(CallKaiMsg m)
        {
            if (m == null) return;
            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost) flow.SendCallKai(m.Sid, m.X, m.Y, m.Z);  // 房主：转发全体
            else if (m.Sid == flow.SelfSid) return;                   // 成员：自己那条绕回来了

            // 凯伊的 OnCall 只用 point（不按 source 过滤）⇒ 本机凯伊会走到同一点、放信标、就近救人
            var source = ResolveRemoteUser(m.Sid);

            _applyingRemote = true;
            try
            {
                BattleEventBus.CallKai(source, new Vector3(m.X, m.Y, m.Z));
            }
            finally
            {
                _applyingRemote = false;
            }
        }

        /// <summary>把权威 <c>Sid</c> 解析成本端的盟友实例（<c>0</c> = 房主）。解析不到给 null。</summary>
        static GameObject ResolveRemoteUser(uint sid)
        {
            // ⚠ NetFriendBridge 没有静态 Instance（挂在常驻 GameRoot 上的 I_GlobaManager）⇒ 惰性解析一次并缓存
            if (_friendBridge == null) _friendBridge = UnityEngine.Object.FindObjectOfType<NetFriendBridge>();
            if (_friendBridge != null && _friendBridge.TryGetFriendObject(sid, out GameObject go)) return go;
            return null;
        }

        #endregion
    }
}
