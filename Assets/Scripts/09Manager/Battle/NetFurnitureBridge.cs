using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · 家具交互同步桥（09）：**一处收口**所有共享家具的交互。
    ///
    /// <para>▍为什么收口在 <c>GlobalEventBus.FurnitureOperate</c>：家具交互（雷达站 / 密码锁 / 炮台授权 /
    /// 欧帕兹拾取 / 凯伊提交…）是任务推进的关键触发点；每条链路单开通道会散成一堆。收口后
    /// <c>OnOOPartCollect</c>/<c>OnSubmitOOPart</c>/<c>OnKeiSubmit</c> **都不需要单独同步** ——
    /// 远端重放会跑同一套本地逻辑（各自写自己的共享状态）⇒ 两端 <c>TaskState</c> 自然一致。</para>
    ///
    /// <para>▍键 = <c>Furniture_Attached.SyncId</c>（<c>FNV1a(Id + 位置量化)</c>，创建时算一次并缓存）。
    /// ⚠ 不能只用 <c>Id</c>（同类家具多实例）、不能用 <c>NumberID</c>（各端静态自增）。</para>
    ///
    /// <para>▍消息走向（一条 4036 双向）：
    /// ① 本端 <c>FurnitureOperate</c> → 成员**上报房主**、房主**广播全体**；
    /// ② 房主收到成员上报 ⇒ 转发全体 + 本地应用；成员收到 ⇒ **按 Sid 丢掉自己那条**（本地已执行过）再应用。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> Install/Uninstall（同 <see cref="EnemyNetBridge"/> 模式）。</para>
    /// </summary>
    public static class NetFurnitureBridge
    {
        static bool installed;

        /// <summary>回环门：true = 正在"重放远端交互"。此时家具 <c>Operate()</c> 会再派发一次
        /// <c>FurnitureOperate</c> ⇒ 必须挡住它被当成"本机操作"再上报一次。</summary>
        static bool _applyingRemote;

        /// <summary>sid → 盟友实例 的查询来源（常驻 GameRoot 上），惰性解析并缓存。</summary>
        static NetFriendBridge _friendBridge;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _applyingRemote = false;
            _friendBridge = null;

            GlobalEventBus.OnFurnitureOperate += OnLocalOperate;
            NetRoomFlow.OnFurnitureOperate += OnRemoteOperate;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            GlobalEventBus.OnFurnitureOperate -= OnLocalOperate;
            NetRoomFlow.OnFurnitureOperate -= OnRemoteOperate;
            _friendBridge = null;
        }

        #region 本端 -> 网络

        static void OnLocalOperate(GameObject user, IFurniture furn)
        {
            if (_applyingRemote) return;              // 远端重放触发的，别再上报

            var f = furn as Furniture_Attached;
            if (f == null || f.SyncId == 0) return;   // 没有稳定键的家具不参与同步

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            // 本端已经真执行过一次了 ⇒ 这里只负责把"发生了什么"传出去
            flow.SendFurnitureOperate(f.SyncId, flow.IsHost ? 0u : flow.SelfSid);
        }

        #endregion

        #region 网络 -> 本端

        static void OnRemoteOperate(FurnitureOperateMsg m)
        {
            if (m == null) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost)
            {
                // 房主：转发给全体（含发起者；发起者按 Sid 丢掉自己那条）—— 权威 Sid 原样带上
                flow.SendFurnitureOperate(m.SyncId, m.Sid);
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                               // 自己发的那条绕回来了：本地已经执行过
            }

            var furn = Furniture_Attached.FindBySyncId(m.SyncId);
            if (furn == null) return;                 // 本端没有这个家具（没生成出来）⇒ 忽略

            var remoteUser = ResolveRemoteUser(m.Sid);

            _applyingRemote = true;
            try
            {
                furn.ApplyRemoteOperate(remoteUser);
            }
            finally
            {
                _applyingRemote = false;
            }
        }

        /// <summary>
        /// 把权威 <c>Sid</c> 解析成本端的 GameObject：<c>0</c> = 房主，其余 = 对应盟友实例。
        /// <para>⚠ **绝不能**退化成"本机玩家"：<c>PlayerInputHandler/PlayerWeaponsManager</c> 的
        /// <c>OnOperation</c> 靠 <c>user == gameObject</c> 判定"是不是我在操作"，传本机玩家会误动本机。</para>
        /// <para>解析不到就给 null（订阅方同样会早退，只是没有对应的表现落点）。</para>
        /// </summary>
        static GameObject ResolveRemoteUser(uint sid)
        {
            // NetFriendBridge 现在带静态 Instance（它在 Init 里自登记；见其字段注释）⇒ 优先直连，
            // 兜底才 FindObjectOfType（例如桥还没来得及 Init 的极端时序）。
            if (_friendBridge == null) _friendBridge = NetFriendBridge.Instance;
            if (_friendBridge == null) _friendBridge = UnityEngine.Object.FindObjectOfType<NetFriendBridge>();
            if (_friendBridge != null && _friendBridge.TryGetFriendObject(sid, out GameObject go)) return go;
            return null;
        }

        #endregion
    }
}
