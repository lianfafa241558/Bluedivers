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


        /// <summary>sid → 盟友实例 的查询来源（常驻 GameRoot 上），惰性解析并缓存。</summary>
        static NetFriendBridge _friendBridge;

        // ⚠ 回环门用的是**跨桥共享**的 NetApplyGuard（不是本类私有字段）：应用终端阶段时也会走到
        //   furn.Operate() ⇒ 会派发本类订阅的 FurnitureOperate ⇒ 各持一道门挡不住那条回环。

        /// <summary>常驻安装（**不是**每局一份）。
        /// <para>⚠ 原来只在 <c>WaveManager</c> 里 Install/Uninstall ⇒ 舰桥（<c>Utnapishitim</c> 场景）里
        /// 这条桥根本没挂：而舰桥才有 <c>Furniture_NPCChat</c> / <c>Furniture_Door</c> / <c>Furniture_General</c>
        /// ⇒ "和 NPC 对话，队友那边毫无反应"（2026-10-22 用户报）。桥本身无每局状态（<c>_friendBridge</c> 惰性解析）
        /// ⇒ 全程常驻、随场景切换自然续用。</para></summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            Install();
        }

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _friendBridge = null;

            GlobalEventBus.OnFurnitureOperate += OnLocalOperate;
            Furniture_Attached.OnStateChanged += OnLocalStateChanged;   // 家具"状态"变更（无交互驱动的那种）
            Furniture_OOPartDepositBase.OnSubmitReported += OnLocalSubmitReported;   // 容器类家具的一次提交
            NetRoomFlow.OnFurnitureOperate += OnRemoteOperate;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            GlobalEventBus.OnFurnitureOperate -= OnLocalOperate;
            Furniture_Attached.OnStateChanged -= OnLocalStateChanged;
            Furniture_OOPartDepositBase.OnSubmitReported -= OnLocalSubmitReported;
            NetRoomFlow.OnFurnitureOperate -= OnRemoteOperate;
            _friendBridge = null;
        }

        #region 本端 -> 网络

        static void OnLocalOperate(GameObject user, IFurniture furn)
        {
            if (NetApplyGuard.Applying)
            {
                // 远端重放触发的（重放时家具又派发了一次事件）⇒ 本地已经执行过，别再上报
                FPSGame.Utils.NetSyncLog.SyncLog("家具/重放", $"{Describe(furn as Furniture_Attached, furn)} 由远端重放触发 ⇒ 不再上报");
                return;
            }

            // ★ 盟友实例身上的家具（典型：倒地救援的 Furniture_PlayerDown）：它的键含"位置量化"，
            //   而盟友出生点与本人出生点差 0.2m(Y) ⇒ 跨端必然对不上，广播过去只会让对方刷一条"找不到这个家具"。
            //   这类交互走 NetPlayerStateBridge 的 **sid** 通道（4050 扶起队友）。
            var host = furn as Component;
            if (host != null && host.GetComponent<FriendController>() != null) return;

            // ★ 载具（Furniture_Vehicle）：走**它自己的 4053**（NetVehicleBridge）。
            //   本条重放会调 Furniture_Vehicle.Operate() ⇒ controller.SetOwener(远端实例)，而那条路硬要
            //   PlayerController（盟友实例只有 FriendController）⇒ NRE；且"本机玩家专属开关"也不该在远端执行。
            if (furn is Furniture_Vehicle) return;

            var f = furn as Furniture_Attached;
            if (f == null || f.SyncId == 0)
            {
                // ⚠ 这里原来是静默 return：不是 Furniture_Attached 子类 / 没有稳定键 ⇒ 远端**永远收不到**，
                //   而现象就是"这个家具怎么操作都不同步"，所以必须留下痕迹。
                FPSGame.Utils.NetSyncLog.Warn("家具/操作·发", $"{Describe(f, furn)} **不参与同步**（{(f == null ? "不是 Furniture_Attached 子类" : "SyncId=0")}）");
                return;
            }

            var flow = NetRoomFlow.Instance;
            if (flow == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("家具/操作·发", $"{Describe(f, furn)} 发不出去：没有 NetRoomFlow");
                return;
            }

            // 本端已经真执行过一次了 ⇒ 这里只负责把"发生了什么"传出去
            uint sid = flow.IsHost ? 0u : flow.SelfSid;
            FPSGame.Utils.NetSyncLog.SyncLog("家具/操作·发", $"{Describe(f, furn)} sid={sid} 操作者={(user != null ? user.name : "<null>")}");
            flow.SendFurnitureOperate(f.SyncId, sid);
        }

        /// <summary>家具"状态"变更（子类调 <c>Furniture_Attached.ReportState</c>）：走同一条 4036 上行。
        /// <para>▍与交互路径的区别：那条重放 <c>Operate()</c>；这条只带状态（<c>StateKey</c> 非空）⇒ 接收端直接置位。</para></summary>
        static void OnLocalStateChanged(Furniture_Attached f, int code, string key)
        {
            if (NetApplyGuard.Applying) return;          // 远端应用触发的（ApplyRemoteState 里又改了状态）：别再上报
            if (f == null || f.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;    // 单机 / 不在房里：两端本来就一致

            FPSGame.Utils.NetSyncLog.SyncLog("同步/家具/状态·发",
                $"{Describe(f, f)} code={code} key={key}");
            flow.SendFurnitureOperate(f.SyncId, flow.IsHost ? 0u : flow.SelfSid, code, key);
        }

        /// <summary>容器类家具提交完成（子类调 <c>OnSubmitReported</c>）：上报"提交了几个 + 主类型"。
        /// <para>▍远端收到后会<b>真跑一遍</b> BeginPress/StepPress（见 <see cref="OnRemoteSubmit"/>）——
        /// 因为这类家具的效果（<c>held</c> / <c>OnSubmit</c>）只在按压过程里产生，<c>Operate()</c> 重放不出来。</para></summary>
        static void OnLocalSubmitReported(Furniture_OOPartDepositBase f, int count, int type)
        {
            if (NetApplyGuard.Applying) return;          // 远端重放里又触发了一次：别再上报
            if (f == null || f.SyncId == 0 || count <= 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;    // 单机 / 不在房里：两端本来就一致

            FPSGame.Utils.NetSyncLog.SyncLog("同步/容器·发",
                $"{Describe(f, f)} 提交 {count} 个（type={type}）");
            flow.SendOOPartSubmit(f.SyncId, flow.IsHost ? 0u : flow.SelfSid, count, type);
        }

        /// <summary>日志用的一句话描述（Id / SyncId / 物体名）。</summary>
        static string Describe(Furniture_Attached f, IFurniture raw)
        {
            if (f != null) return $"id={f.Id} sync={f.SyncId} go={f.name}";
            var comp = raw as Component;
            return $"go={(comp != null ? comp.name : "<非组件>")}";
        }

        /// <summary>
        /// 把本端**同步键表**打一条（只在"收不到对应家具"时调用）。
        /// <para>▍为什么整表都打：键 = <c>FNV1a(Id + 位置量化)</c> —— 两端只要位置/身份有一点点不同，键就不一样。
        /// 发起端日志里有"发的是 id/sync"，接收端这份表一贴出来，是"键不一致"还是"本端压根没这个家具"一眼可判。</para>
        /// </summary>
        static void LogSyncTable(string tag)
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var kv in Furniture_Attached.listBySyncId)
            {
                if (kv.Value == null) continue;
                if (n++ > 0) sb.Append(", ");
                sb.Append(kv.Value.Id).Append(':').Append(kv.Key);
            }
            FPSGame.Utils.NetSyncLog.Warn(tag, $"本端键表共 {n} 条（Id:SyncId）：{sb}");
        }

        #endregion

        #region 网络 -> 本端

        static void OnRemoteOperate(FurnitureOperateMsg m)
        {
            if (m == null) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            FPSGame.Utils.NetSyncLog.SyncLog("家具/操作·收", $"sync={m.SyncId} 发起 sid={m.Sid} 我是{(flow.IsHost ? "房主" : "成员")}");

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
            if (furn == null)
            {
                // ⚠ 这里原来是静默 return —— "交互没同步"最常见的真凶就藏在这一行：
                //   要么两端 SyncId 不一致（键 = Id + 位置量化），要么本端压根没生成这个家具。
                //   把本端整张键表打出来，和发起端那行"操作·发"对照即可定性。
                FPSGame.Utils.NetSyncLog.Warn("家具/操作·收", $"sync={m.SyncId}（发起 sid={m.Sid}）**本端找不到这个家具** ⇒ 这一下被丢弃");
                LogSyncTable("家具/键表");
                return;
            }

            // ★ 状态同步（StateKey 非空）：直接应用状态，**不重放 Operate()**（那是交互路径的事）
            if (!string.IsNullOrEmpty(m.StateKey))
            {
                FPSGame.Utils.NetSyncLog.SyncLog("家具/状态·收", $"{Describe(furn, furn)} → {m.StateKey}（发起 sid={m.Sid}）");
                NetApplyGuard.Begin();
                try { furn.ApplyRemoteState(m.State, m.StateKey); }
                finally { NetApplyGuard.End(); }
                return;
            }

            var remoteUser = ResolveRemoteUser(m.Sid);

            FPSGame.Utils.NetSyncLog.SyncLog("家具/操作·收", $"{Describe(furn, furn)} 远端操作者={(remoteUser != null ? remoteUser.name : "<null>")}" +
                (remoteUser == null ? " ⚠ 操作者为空 ⇒ 依赖 owner 的效果（如 KeyScreen.SetOwener）会打不出来" : ""));

            NetApplyGuard.Begin();
            try
            {
                furn.ApplyRemoteOperate(remoteUser);
            }
            finally
            {
                NetApplyGuard.End();
            }
        }

        /// <summary>容器类家具的一次提交（远端）：把个数注入盟友的**幽灵背包**，再跑同一套按压逻辑。
        /// <para>▍这样 <c>held</c> 与 <c>OnSubmit</c>（炮弹架要转成炮弹）在远端自然成立，
        /// 不需要再单开一条"结果状态"通道；<c>User</c> 必须是**远端单位**（同 4036 的口径）。</para></summary>
        static void OnRemoteSubmit(OOPartSubmitMsg m)
        {
            if (m == null || m.SyncId == 0 || m.Count <= 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost)
            {
                flow.SendOOPartSubmit(m.SyncId, m.Sid, m.Count, m.Type);   // 房主：转发全体
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                                                    // 自己发的那条绕回来了
            }

            var furn = Furniture_Attached.FindBySyncId(m.SyncId) as Furniture_OOPartDepositBase;
            if (furn == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("容器/收", $"sync={m.SyncId} **本端找不到这个容器**（或类型不符）⇒ 丢弃");
                return;
            }

            var user = ResolveRemoteUser(m.Sid);
            if (user == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("容器/收", $"{Describe(furn, furn)} 解析不到远端操作者 sid={m.Sid} ⇒ 丢弃");
                return;
            }

            var bag = user.GetComponent<FriendOOPartInventory>();
            if (bag == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("容器/收", $"{user.name} 上没有 FriendOOPartInventory ⇒ 无法重放（预制体应带）");
                return;
            }

            bag.Inject((FPSGame.GameContract.OOPartEnum)m.Type, m.Count);

            var step = (FPSGame.GameContract.IStepPress)furn;
            FPSGame.Utils.NetSyncLog.SyncLog("容器/收",
                $"{Describe(furn, furn)} 重放提交 {m.Count} 个（type={m.Type}）");

            NetApplyGuard.Begin();
            try
            {
                if (!step.BeginPress(user))
                {
                    FPSGame.Utils.NetSyncLog.Warn("容器/收", $"{Describe(furn, furn)} BeginPress 被拒 ⇒ 本次重放作废");
                    return;
                }

                // 给足节拍：StepPress 内部按 SubmitPerSecond 的节拍逐个扣（见 Furniture_OOPartDepositBase）
                step.StepPress(m.Count * (1f / Furniture_OOPartDepositBase.SubmitPerSecond) + 0.05f);
                furn.Handle(user);      // 收尾（同一条 Operate ⇒ held 已加、OnSubmit 已触发）
            }
            finally
            {
                NetApplyGuard.End();
                bag.Clear();            // 幽灵背包用完即弃
            }
        }

        /// <summary>把权威 <c>Sid</c> 解析成本端的 GameObject：<c>0</c> = 房主，其余 = 对应盟友实例。</summary>
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
