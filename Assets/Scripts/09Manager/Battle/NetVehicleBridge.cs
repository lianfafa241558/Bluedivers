using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **载具**桥（09）：上下机（原 4053，2026-10-27 并入 **4036** 的 NetId 分支）—— 全工程唯一同时看得见载具/盟友实例与网络层的地方。
    ///
    /// <para>▍键 = 载具 <c>Actor.NetId</c>（房主随 4028 分配、两端一致，见 <c>ActorsManager.VehicleNetIdBase</c>）；
    /// 值 = 驾驶者 sid（房主 = 0）⇒ 各端按 sid 解析"本端那个实体"（本机玩家 / 盟友实例）。</para>
    ///
    /// <para>▍ ⚠ 远端**不能**走家具重放：4036 的 <c>ApplyRemoteOperate</c> 会调 <c>Furniture_Vehicle.Operate()</c>
    /// ⇒ <c>VehicleController.SetOwener(远端实例)</c>，而其中一批开关（<c>ActorState.Hide</c> / 相机 / 本机输入）
    /// 本质是"本机玩家专属"。故 4036 只当**传输**（NetId 分支），应用一律走
    /// <see cref="Furniture_Vehicle.ApplyRemoteMount"/>（只做"挂到挂点 + 停位姿/停动画"）。</para>
    ///
    /// <para>▍自举：<c>[RuntimeInitializeOnLoadMethod]</c>（与 <c>NetPlayerStateBridge</c> / <c>SceneUnitMoveSink</c>
    /// 同款）⇒ 不需要宿主，也不需要任何 Install/Uninstall 调用点。</para>
    /// </summary>
    public static class NetVehicleBridge
    {
        static bool installed;

        /// <summary>上车后把"推导位"过渡到位的时长（秒）。
        /// <para>▍本地那份上车有 1s 的 <c>Slerp/Lerp</c> 贴合（<c>Furniture_Vehicle.InOperateUpdate</c>），远端没有
        /// ⇒ 不补这一段就会"啪"地跳一下；0.35s 够盖住这点残差，又不至于让机甲肉眼可见地滑过去。</para></summary>
        const float MountBlendSeconds = 0.35f;

        /// <summary>本机**正在驾驶**的载具 <c>NetId</c>。
        /// <para>▍为什么要有它：<c>SetOwener(null)</c>（下机 / 被打爆）时**已经拿不到驾驶者是谁**了，
        /// 只能靠这张"我开过哪些车"的留痕表判"这一下该不该上报"（否则远端载具死亡会在每台机器上都触发一次误报）。</para></summary>
        static readonly HashSet<int> _selfDriven = new HashSet<int>();

        /// <summary>远端驾驶者 sid → 本端那份骑手实例 + 它的载具座位（下车/清理时要用座位还原父级）。</summary>
        static readonly Dictionary<uint, RideInfo> _remoteRides = new Dictionary<uint, RideInfo>();

        /// <summary>【一车一人】每辆载具**当前是谁在开**（载具 <c>NetId</c> → 驾驶者 sid；房主 = 0）。
        ///
        /// <para>▍为什么要它：本机自己开的那台**不在** <see cref="_remoteRides"/> 里（那是"远端骑手"表），
        /// 所以原来两端各判各的 ⇒ "a 上了之后 b 也能上，同一台机甲里坐了两个人"（2026-10-15 实测）。</para>
        ///
        /// <para>▍与 <see cref="_selfDriven"/> 的分工：后者是"**我**开过哪些车"的留痕（只有本机驾驶者那台有），
        /// 本表是**全端都维护**的当前占用（本机 / 远端都在里面），用来判"这辆有人了吗"。</para>
        ///
        /// <para>▍裁决口径：**房主是唯一裁决者，先到先得，成员永远让**（成员没有全局视野，自己判必然分叉）。
        /// 请求者要上而占用者在案 ⇒ 房主**拒绝**它（不应用），并把"占用者在上机"重播回去 ⇒
        /// 发起者那台收到后会**先把自己放出来**再挂占用者（见 <see cref="OnVehicleMountMsg"/>）。</para></summary>
        static readonly Dictionary<int, uint> _drivers = new Dictionary<int, uint>();

        private sealed class RideInfo
        {
            public GameObject Rider;
            public Furniture_Vehicle Seat;

            // ---- P2 位姿推导用（见 TryDriveVehiclePose）----

            /// <summary>载具号（"同一辆车换骑手"/"本机自己抢开同一辆"时要按它反查）。</summary>
            public int NetId;
            /// <summary>远端那台的**控制组件**（机甲 = <c>VehicleController</c> / 炮台 = <c>EmplacementController</c>）
            /// —— 驾驶中要冻结它：机甲是怕 <c>GroundCheck</c> + 重力 <c>Move</c> 每帧把 <c>transform</c> 往下拽、
            /// 和推导位打架（表现 = 一路下沉 / 抖）；炮台本来就不动（<c>BaseSelfController</c>），冻掉也无害。
            /// <para>▍类型取二者的共同基类：<c>VehicleController : BaseSelfMoveableController : BaseSelfController</c>，
            /// <c>EmplacementController : BaseSelfController</c>。</para></summary>
            public BaseSelfController Vehicle;
            /// <summary>**位置固定**的驾驶物（炮台：只转不移动）⇒ P2 推导位**只推 yaw、不动位置**。
            /// <para>▍判据 = 它不是 <c>BaseSelfMoveableController</c>（没有走路那套）。</para></summary>
            public bool FixedPos;
            /// <summary>载具根 = **<c>Actor</c>/<c>VehicleController</c> 所在那个物体**（CC 动的就是它 ⇒ 它才是"载具世界位姿"的载体）。
            /// <para>⚠ 刻意不取 <c>Furniture_Vehicle.relatedTrans2</c>：Exosuit 上两者确实是同一个（已核对 <c>relatedTrans2.isRoot=true</c>），
            /// 但没有理由假定所有载具都这样（炮台的 <c>relatedTrans2</c> 可能是可转子件）。</para></summary>
            public Transform Root;
            /// <summary>骑手是否真的挂在载具根**之下**（挂点没配 / 配到了根外面 ⇒ 动根带不动他 ⇒ 位姿推导必须让位给直写）。</summary>
            public bool Seated;
            /// <summary>骑手在载具根下的**局部位置**（挂点给的常量偏移）⇒ 载具位置 = 驾驶者位置 − 载具朝向 × 它。</summary>
            public Vector3 SeatLocalPos;
            /// <summary>骑手相对载具根**绕 Y 的角度偏移**（度）。
            /// <para>▍为什么只留 yaw、不反解整份旋转：载具根本地就是 **yaw-only**（<c>HandleRotation</c> 只绕世界 Y 转），
            /// 而座位（<c>relatedTrans</c>）常常带俯仰/翻滚（Exosuit 的 <c>Point</c> 是 180° 绕斜轴）——
            /// 把整份旋转反解到载具根上会把机甲**整台拧歪**；实测两端只差一个 yaw。</para></summary>
            public float SeatYawOffset;
            /// <summary>上车时刻（见 <see cref="MountBlendSeconds"/>）。</summary>
            public float MountTime;
            /// <summary>运动组件是否已关（下车 / 被本机抢开时要还原）。</summary>
            public bool LocomotionOff;

            // ---- P2 表现层（驾驶期间挂上并启用、下车关掉）----

            /// <summary>载具根上的**位姿插值**。⚠ 网络位姿**不能"收到就写"**：20Hz 直写会退化成"每个包跳一下"
            /// （表现 = 高频短距离位移，正是 <c>NetTransformView</c> 类注释里记的那个坑）。</summary>
            public NetTransformView View;
            /// <summary>载具根上的**动画驱动**：运动栈被冻结后，走路动画得有人替它算（见 <c>NetVehicleRemoteMotion</c>）。</summary>
            public NetVehicleRemoteMotion Motion;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (installed) return;
            installed = true;

            BattleEventBus.OnVehicleOwnerChanged += OnLocalOwnerChanged;   // 本机（驾驶者那台）的上下机 ⇒ 上报
            UnitEventBus.OnUnitDeath += OnUnitDeath;                       // 载具死亡（含本端副本被远端判死）⇒ 先摘骑手
            NetRoomFlow.OnVehicleMount += OnVehicleMountMsg;               // 收到"某人上/下机" ⇒ 转发 + 应用
            NetRoomFlow.OnVehicleShoot += OnVehicleShootMsg;               // 收到"某台载具开火" ⇒ 转发 + 放表现弹
            NetRoomFlow.OnPlayerLeft += OnPlayerLeft;                      // 驾驶者离场 ⇒ 摘掉他骑的那辆
            NetRoomFlow.OnHostLost += OnHostLost;                          // 房主掉了（本端退回单机）⇒ 清绑定
            GlobalEventBus.OnSceneChange += OnSceneChanged;                // 换场景：旧载具/骑手都没了
        }

        // ==================== 本机 -> 网络 ====================

        /// <summary>本机（驾驶者那台）的上下机 ⇒ 上报。
        /// <para>▍只认"我开的"：进入时判 <c>owner == ActorsManager.Player</c>；退出时 <c>owner == null</c>
        /// （拿不到驾驶者是谁）⇒ 查 <see cref="_selfDriven"/>。</para>
        /// </summary>
        static void OnLocalOwnerChanged(GameObject vehicleGo, GameObject owner)
        {
            if (NetApplyGuard.Applying) return;      // 远端应用触发的 ⇒ 别回声（本桥应用不调 SetOwener，这是防御）
            if (vehicleGo == null) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;   // 单机 / 不在房：两端本来就一致，也别领任何东西

            if (!vehicleGo.TryGetComponent(out Actor actor)) return;
            int netId = actor.NetId;
            if (netId == 0)
            {
                FPSGame.Utils.NetSyncLog.Warn("载具/上机·发",
                    $"载具 {vehicleGo.name} 没有 NetId ⇒ 上下机同步不了（这一发空投的 4028 没带上载具号？见 P0）");
                return;
            }

            bool mounted = owner != null;
            uint sid = flow.IsHost ? 0u : flow.SelfSid;

            if (owner != null)
            {
                if (ActorsManager.Player == null || owner != ActorsManager.Player.gameObject) return;  // 不是本机玩家在开

                // ★【一车一人 · 兜底】(2026-10-15) 这辆已经被**别人**占着（消息比我的动作先到）⇒ 我**让**：
                //   把自己从他车里放出来（本地下车），**不上报**（我没真开成；报了远端会把占用者一起摘掉）。
                //   ▍挡在入口的那条是 Furniture_Vehicle.CanOperate（_remoteOccupied，提示都会收起来），
                //     这里只是"消息在路上时仍挤进来了"的兜底 —— 那时占用者已经在案，我先让必然收敛。
                if (_drivers.TryGetValue(netId, out uint occupier) && occupier != sid)
                {
                    FPSGame.Utils.NetSyncLog.Warn("载具/上机·发",
                        $"netId={netId} 已被 sid={occupier} 占着 ⇒ 本机让车（不进舱）");
                    if (FindVehicle(netId, out Furniture_Vehicle blockedSeat) != null) blockedSeat.ForceLocalExit();
                    return;
                }

                _selfDriven.Add(netId);
                _drivers[netId] = sid;
                EvictRideOfVehicle(netId);   // 本机自己开上了这辆 ⇒ 把远端那份骑手摘掉（连它的运动栈一起还原）
            }
            else if (!_selfDriven.Remove(netId))
            {
                // 我没开过这辆 ⇒ 只是"载具被打爆"这类连带 SetOwener(null)（每台机器都会走）⇒ 不报
                // ★ 但要先把挂在它下面的远端骑手摘出来（见 RescueRiders：骑手是子物体，会被连带销毁）
                RescueRiders(netId, "连带 SetOwener(null)");
                return;
            }
            else
            {
                // 我开过 ⇒ 这一次是我下机：占用表跟着清（判据带上 sid，别把"别人换我"的情况误清）
                if (_drivers.TryGetValue(netId, out uint cur) && cur == sid) _drivers.Remove(netId);
            }

            FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·发",
                $"netId={netId} sid={sid} mounted={mounted}（{(mounted ? owner.name : "下机/被打爆")}）");
            flow.SendVehicleMount(netId, sid, mounted);

            // ★ P4：跟着上下机绑定 / 解绑"这辆车的武器开火上报"（我只报**自己开的那台**的开火）
            if (mounted) BindLocalDrivenWeapons(vehicleGo, netId);
            else UnbindLocalDrivenWeapons();
        }

        // ==================== P4 载具开火表现（4054） ====================

        /// <summary>【本机驾驶的那台】的载具武器管理器（订阅它的 <c>OnWeaponShoot</c>；下机 / 换车时退订）。</summary>
        static VehicleWeaponsManager _localDrivenWeapons;

        /// <summary>上面那台的载具号 —— 开火消息的键（不是 sid：键 = 载具号，见 <c>VehicleShootMsg</c>）。</summary>
        static int _localDrivenNetId;

        /// <summary>准心射程（取落点时的最大距离；口径同 <c>NetFriendBridge.AimRayLength</c>）。</summary>
        const float AimRayLength = 300f;

        /// <summary>把"本机正在开的这台车"的武器接进开火上报（换车/重上车都先退订再订 ⇒ 不会双订）。</summary>
        static void BindLocalDrivenWeapons(GameObject vehicleGo, int netId)
        {
            UnbindLocalDrivenWeapons();
            if (vehicleGo == null || netId == 0) return;

            var wm = vehicleGo.GetComponentInChildren<VehicleWeaponsManager>(true);
            if (wm == null)
            {
                // ⚠ 静默会让"我开火、别人看不见"完全无从下手 ⇒ 必须留痕
                FPSGame.Utils.NetSyncLog.Warn("载具/开火·发",
                    $"{vehicleGo.name}(netId={netId}) 上没有 VehicleWeaponsManager ⇒ 本机开火**不会上报**（对端不会有枪口焰/弹道）");
                return;
            }

            wm.OnWeaponShoot += OnLocalVehicleShoot;
            _localDrivenWeapons = wm;
            _localDrivenNetId = netId;
            FPSGame.Utils.NetSyncLog.SyncLog("载具/开火·发",
                $"已订阅本机驾驶载具的开火：netId={netId} 武器管理器={wm.name}（武器会被按配置重建，订阅由它内部跟着走）");
        }

        /// <summary>退订（下机 / 换车 / 房主掉线 / 换场景）。</summary>
        static void UnbindLocalDrivenWeapons()
        {
            if (_localDrivenWeapons != null) _localDrivenWeapons.OnWeaponShoot -= OnLocalVehicleShoot;
            _localDrivenWeapons = null;
            _localDrivenNetId = 0;
        }

        /// <summary>【上行】本机驾驶的载具开了一次火 ⇒ 上报表现（房主广播全体 / 成员上报房主）。
        /// <para>▍方向与落点口径**照抄玩家那条**（<c>NetFriendBridge.OnLocalShoot</c>）：
        /// 方向 = 该武器枪口朝向 + 本武器散布（盟友模型只同步了 yaw、没有俯仰，不带方向就只能水平打）；
        /// 落点 = 驾驶者准心实指那点（相机中心射线）—— 收端靠这两样让弹道**穿过同一个点**。</para>
        /// <para>⚠ 制导类车辆弹**不在这里**逐帧同步：收端本地重算（<c>FpsHelper.TryGetAimPoint</c>），
        /// 见 <c>ProjectilePlayerHoming</c> 的表现弹分支。</para></summary>
        static void OnLocalVehicleShoot(FPSGame.Weapon.WeaponBaseController weapon)
        {
            if (_localDrivenWeapons == null || weapon == null || _localDrivenNetId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;   // 单机 / 不在房：没有对端要收

            int weaponIndex = _localDrivenWeapons.IndexOfWeapon(weapon);
            UnityEngine.Vector3 dir = UnityEngine.Vector3.zero;
            var muzzle = weapon.GetMuzzle(0);
            if (muzzle != null) dir = weapon.GetShotDirectionWithinSpread(muzzle);

            UnityEngine.Vector3 aim = LocalDrivenAimPoint();

            // ★【弹量随发】(2026-10-15) 载具武器每端各一份实例，弹量本来会分叉（实测：a 打完下车、
            //   b 上车看到满弹）⇒ 让**驾驶者那台当权威**，每次开火顺手把"这一发打完之后的弹量"推给其余端。
            //   ⚠ 必须是"扣完之后"的值：OnShoot 是在 HandleShoot() 里抛的，UseMagazine 还在它后面。
            _localDrivenWeapons.TryGetAmmoAfterShot(weaponIndex, out int magLeft, out int ammoLeft);

            FPSGame.Utils.NetSyncLog.BulletLog("载具/开火·发",
                $"netId={_localDrivenNetId} 武器={weapon.name} 下标={weaponIndex} 档位={weapon.UseDamageIndex} " +
                $"发射点={weapon.LastMuzzleIndex} 方向={dir:F2} 落点={(aim.sqrMagnitude > 0.0001f ? aim.ToString("F2") : "<无>")} " +
                $"弹匣余={magLeft} 备弹余={ammoLeft}");

            flow.SendVehicleShoot(_localDrivenNetId, weaponIndex, dir, aim, weapon.UseDamageIndex,
                weapon.LastMuzzleIndex, magLeft, ammoLeft);
        }

        /// <summary>驾驶者准心实指那一点（相机中心射线；取不到相机 ⇒ 零向量 = 未知，收端退回"只用方向"）。</summary>
        static UnityEngine.Vector3 LocalDrivenAimPoint()
        {
            var self = ActorsManager.Player;
            if (self == null || self.transform == null) return UnityEngine.Vector3.zero;

            var pc = self.transform.GetComponent<PlayerController>();
            var cam = pc != null ? pc.PlayerCamera : null;
            if (cam == null) return UnityEngine.Vector3.zero;

            var ray = cam.ViewportPointToRay(new UnityEngine.Vector3(0.5f, 0.5f));
            UnityEngine.RaycastHit hit;
            return UnityEngine.Physics.Raycast(ray, out hit, AimRayLength) ? hit.point : ray.GetPoint(AimRayLength);
        }

        // ==================== 网络 -> 本机 ====================

        /// <summary>收到"某人上/下机"。走法同 4036：**房主先转发全体、再本地应用**；发起方按 sid 丢掉自己那条。
        /// <para>⚠ 顺序不能反：<c>SendVehicleMount</c> 的房主分支只 <c>SendToAll</c>（**不自派发**）⇒ 不会递归回到这里。</para>
        /// <para>▍【一车一人 · 2026-10-15】**房主是唯一裁决者，先到先得，成员永远让**（见 <see cref="_drivers"/>）：
        /// ① 请求者要上、而这辆已有**别人**在案 ⇒ 房主**不应用**它，并把"占用者在上机"重播回去
        /// （发起者那台收到后会先把自己放出来，再挂占用者 —— 下面那条成员分支）；
        /// ② **非占用者**报的下机（典型 = 被顶掉那位在本地退出时的回声）⇒ 只清本端那条绑定，
        /// 不转发、不动占用 —— 否则一次回声就把占用清了，第三个人又能上。</para></summary>
        static void OnVehicleMountMsg(int netId, uint sid, bool mounted)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (sid == flow.SelfSid) return;   // 自己那条（成员绕回来 / 房主本地那条）：本机已经真跑过一次 SetOwener

            bool rejected = false;

            if (flow.IsHost)
            {
                if (_drivers.TryGetValue(netId, out uint occupier) && occupier != sid)
                {
                    rejected = true;

                    if (mounted)
                    {
                        FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收",
                            $"netId={netId} 已被 sid={occupier} 开着 ⇒ 拒绝 sid={sid} 上机（先到先得），并重播占用者");
                        flow.SendVehicleMount(netId, occupier, true);   // 重播"谁在开" ⇒ 发起者那台会自己下车
                    }
                    else
                    {
                        FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收",
                            $"netId={netId} sid={sid} 不是占用者({occupier}) ⇒ 只清本端绑定（不转发、不动占用）");
                        ApplyUnmount(sid);
                    }
                }
                else
                {
                    flow.SendVehicleMount(netId, sid, mounted);   // 成员上报 ⇒ 房主广播全体（含发起者，他自己会丢）
                }
            }
            else if (mounted && _selfDriven.Contains(netId))
            {
                // ★【成员让车】房主把"这辆归别人"发回来了，而**本机自己正开着它**（我抢在前面上了）⇒
                //   先把我自己从车里放出来（走唯一收口；它回出来的那条 4053 会被房主按"非占用者"忽略掉），
                //   再把占用者挂上去 —— 这样两端最终是同一份状态，而不是各开各的。
                FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收",
                    $"netId={netId} 房主裁决归 sid={sid} ⇒ 本机让车（先下车再挂他）");
                if (FindVehicle(netId, out Furniture_Vehicle mySeat) != null) mySeat.ForceLocalExit();
            }

            if (rejected) return;

            if (mounted)
            {
                // 重放（房主的"占用者重播" / 同一条重复到）⇒ 本端已经是他的座就别重挂：
                // ApplyMount 内部会先 ApplyUnmount 再挂，重复应用会闪一下 + 抖一帧运动栈。
                if (_remoteRides.TryGetValue(sid, out RideInfo exist) && exist != null && exist.NetId == netId) return;
                ApplyMount(netId, sid);
            }
            else ApplyUnmount(sid);
        }

        /// <summary>收到"某台载具开了一次火"。走法同 4053：**房主先转发全体、再本地应用**；发起方丢自己那条。
        /// <para>⚠ 丢自己那条**按"这辆车是不是我开的"**判（键是载具号 ⇒ 没有 sid 可用）：
        /// 我开的那台本机已经真放过一发，再放一遍就是双份枪口焰。</para>
        /// <para>▍<paramref name="magazine"/> / <paramref name="ammo"/> = 开枪者那台**扣完之后**的弹量
        /// （<c>&lt;0</c> = 未带/无限 ⇒ 收端不动），见 <see cref="ApplyVehicleShoot"/>。</para></summary>
        static void OnVehicleShootMsg(int netId, int weaponIndex, UnityEngine.Vector3 dir, UnityEngine.Vector3 hit,
            int damageIndex, int muzzleIndex, int magazine, int ammo)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null || netId == 0) return;

            if (_selfDriven.Contains(netId)) return;   // 我开的那台：本机已经真放过一发（含房主转回来的自己那条）

            if (flow.IsHost)
            {
                // 成员上报 ⇒ 房主广播全体（含发起者，他自己会按上一条丢）
                // ⚠ SendVehicleShoot 的房主分支只 SendToAll、**不自派发** ⇒ 不会递归回到这里；房主自己要应用 → 落到下面
                flow.SendVehicleShoot(netId, weaponIndex, dir, hit, damageIndex, muzzleIndex, magazine, ammo);
            }

            ApplyVehicleShoot(netId, weaponIndex, dir, hit, damageIndex, muzzleIndex, magazine, ammo);
        }

        /// <summary>在本端那台载具的对应武器上放"只表现"的一发（枪口焰 + 弹道 + 命中特效，不结算伤害）。
        /// <para>▍为什么直接调 <c>WeaponBaseController.SpawnVisualSalvo</c>：它就是把 <c>FpsHelper.Hit</c> 摘掉、
        /// 只留弹道与命中特效的那条既有通路（盟友开火用的也是它）⇒ 载具复用同一套，别另写一份。</para>
        /// <para>▍武器的 <c>Damages</c> 来自预制体、<c>LogicBehaviour.Awake</c> 已跑过 <c>LogicInit</c>
        /// ⇒ 即使本端没人驾驶（<c>VehicleWeaponsManager.enabled == false</c>），表现弹也能放出来。</para></summary>
        static void ApplyVehicleShoot(int netId, int weaponIndex, UnityEngine.Vector3 dir, UnityEngine.Vector3 hit,
            int damageIndex, int muzzleIndex, int magazine, int ammo)
        {
            var vehicle = FindVehicle(netId, out _);
            if (vehicle == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("载具/开火·收", $"netId={netId} **本端找不到这辆载具** ⇒ 这一发没有任何表现");
                DumpVehicles();
                return;
            }

            var wm = vehicle.GetComponentInChildren<VehicleWeaponsManager>(true);
            var weapon = wm != null ? wm.GetWeaponAtSlotIndex(weaponIndex) : null;
            if (weapon == null)
            {
                // ⚠ 最常见成因：本端这台车的外观/武器还没按持有者配置重建（配置没到）⇒ 槽位对不上
                FPSGame.Utils.NetSyncLog.Warn("载具/开火·收",
                    $"netId={netId} 武器下标={weaponIndex} 在本端取不到武器（武器管理器={(wm != null ? wm.name : "<无>")}" +
                    $" 主手={(wm != null ? (wm.GetWeaponAtSlotIndex(0) != null).ToString() : "-")}）⇒ 这一发被丢弃");
                return;
            }

            // ★ 收端必须按**开枪者**的档位取命中特效/表现弹（同 PlayerShoot.DamageIndex 的理由：一档一个效果）
            int maxIdx = weapon.Damages != null ? weapon.Damages.Count - 1 : 0;
            weapon.UseDamageIndex = Mathf.Clamp(damageIndex, 0, Mathf.Max(0, maxIdx));

            var muzzle = weapon.GetMuzzle(0);
            weapon.SpawnVisualSalvo(muzzle, dir, hit, muzzleIndex);

            // ★【弹量随发】(2026-10-15) 把开枪者那台的权威弹量写进**本端这一份**武器实例：
            //   两端各有一份实例（按持有者配置重建），不写就永远停在"满弹"——实测 a 打完 10 发下车、
            //   b 上车看到的还是满弹（"载具的子弹没有真扣除"）。
            if (wm != null) wm.ApplyRemoteAmmo(weaponIndex, magazine, ammo);

            FPSGame.Utils.NetSyncLog.BulletLog("载具/开火·收",
                $"netId={netId} 武器={weapon.name} 下标={weaponIndex} 档位={weapon.UseDamageIndex} 发射点={muzzleIndex} " +
                $"方向={dir:F2} 落点={(hit.sqrMagnitude > 0.0001f ? hit.ToString("F2") : "<无>")} " +
                $"弹匣余={magazine} 备弹余={ammo}");
        }

        /// <summary>应用"某人上了某辆载具"：把本端那个实体挂到载具挂点上，并停掉它的位姿/动画。</summary>
        static void ApplyMount(int netId, uint sid)
        {
            var vehicle = FindVehicle(netId, out Furniture_Vehicle seat);
            if (vehicle == null || seat == null)
            {
                // ⚠ 静默 return 会让"怎么都不同步"完全无从下手 ⇒ 必须留痕 + 打整张本端载具表
                FPSGame.Utils.NetSyncLog.Warn("载具/上机·收",
                    $"netId={netId}（驾驶者 sid={sid}）**本端找不到这辆载具** ⇒ 这一下被丢弃");
                DumpVehicles();
                return;
            }

            // ★【一车一人】占用在案 + 对**本地入口**关闸（Furniture_Vehicle.CanOperate 会因为 _remoteOccupied
            //   变 false ⇒ 本机玩家连"按 F 进舱"的提示都看不到）。刻意放在"解析骑手实例"**之前**：
            //   就算他的实例还没建出来（名单没到），这台车也确实归他，本端不该让别人挤进来。
            _drivers[netId] = sid;
            seat.SetRemoteOccupied(true);

            if (!TryResolveRider(sid, out GameObject rider))
            {
                FPSGame.Utils.NetSyncLog.Warn("载具/上机·收",
                    $"netId={netId} 解析不到驾驶者 sid={sid} 的本端实例（盟友实例还没建出来 / 已离场？）⇒ 丢弃");
                return;
            }

            ApplyUnmount(sid);          // 换车 / 重放：先把他**上一次**骑的那辆摘掉
            EvictRideOfVehicle(netId);  // 同一辆车只能有一个远端骑手（换人时把旧的摘掉）

            var info = new RideInfo { Rider = rider, Seat = seat, Vehicle = vehicle, NetId = netId };
            _remoteRides[sid] = info;

            NetApplyGuard.Begin();
            try
            {
                // ① 先合门（停位姿 + 停移动动画）：否则这一帧网格还会被网络位姿拽回地面
                var fc = rider.GetComponent<FriendController>();
                if (fc != null) fc.SetVehicleRide(true);

                // ② 再挂到载具挂点（与本地 Furniture_Vehicle.Operate() 同一挂点）
                seat.ApplyRemoteMount(rider, true);

                // ③ 记下"座位在**载具根**下的局部位置 + 绕 Y 偏移" —— P2 靠它反推载具位姿（见 TryDriveVehiclePose）
                info.Root = vehicle.transform;
                info.Seated = rider.transform.parent != null && rider.transform.IsChildOf(info.Root);
                info.SeatLocalPos = info.Root.InverseTransformPoint(rider.transform.position);
                info.SeatYawOffset = Mathf.DeltaAngle(info.Root.eulerAngles.y, rider.transform.eulerAngles.y);
                info.MountTime = Time.time;
                // 炮台这类"只转不移动"的（不是 BaseSelfMoveableController）⇒ P2 只推 yaw（见 TryDriveVehiclePose）
                info.FixedPos = !(vehicle is BaseSelfMoveableController);

                // ④ 表现层：位姿插值 + 动画驱动（挂点生效时才有意义）
                if (info.Seated && info.View == null) info.View = info.Root.GetComponent<NetTransformView>();
                if (info.Seated && info.View == null) info.View = info.Root.gameObject.AddComponent<NetTransformView>();
                if (info.Seated && info.Motion == null) info.Motion = info.Root.GetComponent<NetVehicleRemoteMotion>();
                if (info.Seated && info.Motion == null) info.Motion = info.Root.gameObject.AddComponent<NetVehicleRemoteMotion>();
                if (info.View != null) { info.View.ResetPose(); info.View.enabled = true; }
                if (info.Motion != null) info.Motion.enabled = true;

                // ⑤ 冻结远端那份的运动栈（否则它的 GroundCheck + 重力每帧把 transform 往下拽，与推导位打架）
                SetRemoteLocomotion(info, false);
            }
            finally { NetApplyGuard.End(); }

            FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收",
                $"netId={netId} 驾驶者 sid={sid}（{rider.name}）已进舱（座位={seat.name}" +
                (info.Seated
                    ? $"，局部偏移={info.SeatLocalPos:F2} yaw偏移={info.SeatYawOffset:F1}" +
                      $"；固定位置={info.FixedPos} 插值={(info.View != null)} 动画={(info.Motion != null)}"
                    : "；⚠ 骑手没挂在载具根下 ⇒ 位姿推导不可用") + "）");
        }

        /// <summary>应用"某人下了车"（也用于"换车/离场/清场/本机抢开"的摘除）：还运动栈 + 还原父级 + 恢复吃位姿。</summary>
        static void ApplyUnmount(uint sid)
        {
            if (!_remoteRides.TryGetValue(sid, out RideInfo info)) return;
            _remoteRides.Remove(sid);
            if (info == null) return;

            // ★【一车一人】占用表与本端入口闸门一起还回去（判据带上 sid：换人时别把**新占用者**清掉）
            if (info.NetId != 0 && _drivers.TryGetValue(info.NetId, out uint cur) && cur == sid) _drivers.Remove(info.NetId);
            if (info.Seat != null) info.Seat.SetRemoteOccupied(false);

            // ⚠ 运动栈要**先**还 —— 它不依赖骑手还在不在（骑手可能已被销毁）
            SetRemoteLocomotion(info, true);

            // 表现层也一起停：插值组件若继续跑，会把载具**钉在最后一帧的位姿上**
            // （下车后载具自己的运动栈回来了 ⇒ 一个要写、一个不许动，必然打架）
            if (info.View != null) info.View.enabled = false;
            if (info.Motion != null) info.Motion.enabled = false;

            if (info.Rider == null) return;   // 骑手已销毁（离场/换场景）⇒ 只剩表要清

            NetApplyGuard.Begin();
            try
            {
                if (info.Seat != null) info.Seat.ApplyRemoteMount(info.Rider, false);
                else info.Rider.transform.SetParent(null, true);   // 座位没了（载具被销毁）⇒ 先落地再说

                var fc = info.Rider.GetComponent<FriendController>();
                if (fc != null) fc.SetVehicleRide(false);          // 恢复"网络位姿 → transform"
            }
            finally { NetApplyGuard.End(); }

            FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收", $"驾驶者 sid={sid} 已下机（挂点已还原）");
        }

        /// <summary>远端载具的**运动栈**开关（<c>VehicleController</c>）。
        /// <para>▍为什么驾驶中必须关：它的 <c>Update</c> 会跑 <c>GroundCheck</c> + 重力 <c>Move</c>，
        /// 每帧把载具 <c>transform</c> 往下拽 ⇒ 与 P2 的"推导位"打架（表现 = 机甲一路下沉 / 抖）。
        /// 下车（或被本机玩家抢开）时还原，让它照原样贴地。</para></summary>
        static void SetRemoteLocomotion(RideInfo info, bool running)
        {
            if (info == null || info.Vehicle == null) return;
            info.LocomotionOff = !running;
            if (info.Vehicle.enabled != running) info.Vehicle.enabled = running;
        }

        /// <summary>同一辆车只能有一个"远端骑手"：把绑在同一辆上的**其它 sid** 摘掉（换人 / 重放 / 本机抢开）。</summary>
        static void EvictRideOfVehicle(int netId, uint exceptSid = uint.MaxValue)
        {
            uint found = 0u;
            bool has = false;
            foreach (var kv in _remoteRides)
            {
                if (kv.Key == exceptSid) continue;
                if (kv.Value == null || kv.Value.NetId != netId) continue;
                found = kv.Key;
                has = true;
                break;
            }
            if (has) ApplyUnmount(found);
        }

        /// <summary>【盟友实例被意外重建后】把他的载具绑定接到新实例上：车还在 ⇒ 重新进舱；车没了 ⇒ 只清脏绑定。
        /// <para>▍为什么需要：旧绑定留着的话，<see cref="TryDriveVehiclePose"/> 会把位姿继续喂给**载具根**
        /// （新实例没人喂位姿 ⇒ 表现 = 车在动、人钉在原地）；而载具已销毁时那条绑定是脏的，必须清掉让新实例吃位姿。</para>
        /// </summary>
        public static void RebindRide(uint sid)
        {
            if (!_remoteRides.TryGetValue(sid, out RideInfo info) || info == null) return;

            int netId = info.NetId;
            bool vehicleAlive = info.Root != null && info.Seat != null;
            ApplyUnmount(sid);                    // 旧实例多半已销毁 ⇒ 内部走"只剩表要清"那条（安全）
            if (vehicleAlive) ApplyMount(netId, sid);
        }

        // ==================== 位姿推导（P2；零新消息） ====================

        /// <summary>【P2】某 sid 正在驾驶 ⇒ 把**载具根**摆到"驾驶者位姿 ⊗ 座位局部偏移⁻¹"（**只推 yaw**）。
        ///
        /// <para>▍为什么不用新消息：本地结构就是"驾驶者 <c>transform.parent = 载具根</c>、座位局部偏移恒定"
        /// ⇒ 载具世界位姿完全由驾驶者位姿决定，而驾驶者位姿已经在 5001/5002 里传了（含 <c>Pitch</c>）。</para>
        ///
        /// <para>▍⚠ **不能在这里直接写 <c>transform</c>**：位姿是 20Hz 来的，直写就是"每个包跳一下"
        /// （用户实测 = "高频短距离位移、不连贯"）⇒ 必须交给载具根上的 <c>NetTransformView</c> 做缓冲插值
        /// （它也正是盟友实例那套平滑方案）。</para>
        ///
        /// <para>▍⚠ 只有"本端不是驾驶者"才会走到这里（本机驾驶那台是**真跑**的，别去盖它）；
        /// 也只在挂点配好（<c>relatedTrans2</c> 非空）时才接管，否则交回调用方直接写盟友实例。</para>
        /// </summary>
        /// <returns>true = 这条位姿已被"载具推导"消费（调用方**不要**再把它写进盟友实例）。</returns>
        internal static bool TryDriveVehiclePose(uint sid, Vector3 pos, float yaw, bool teleport)
        {
            if (!_remoteRides.TryGetValue(sid, out RideInfo info) || info == null) return false;
            if (info.Root == null || !info.Seated) return false;   // 挂点没生效 ⇒ 推不动，交回"直写盟友实例"

            // ⚠ 载具根是 **yaw-only**（本地 HandleRotation 只绕世界 Y 转）⇒ 这里也只推 yaw：
            //   座位那份旋转（含俯仰/翻滚）留在骑手的**局部**旋转里（与本地父子结构一致），不反解到载具根上。
            float vehYaw = Mathf.DeltaAngle(0f, yaw - info.SeatYawOffset);
            Quaternion vehRot = Quaternion.Euler(0f, vehYaw, 0f);

            // 位置：机甲按"座位局部偏移的逆"推；**炮台位置固定**（EmplacementController 只转不移动）
            // ⇒ 绝不能拿驾驶者位姿去推——他那点位置抖动会把整台炮台抖起来。
            Vector3 vehPos = info.FixedPos ? info.Root.position : pos - vehRot * info.SeatLocalPos;

            // 上车头 MountBlendSeconds 秒内插值（本地有 1s 贴合、远端没有 ⇒ 不补就会"啪"地跳一下）
            if (!teleport)
            {
                float age = Time.time - info.MountTime;
                if (age < MountBlendSeconds)
                {
                    float prog = Mathf.Clamp01(age / MountBlendSeconds);
                    if (!info.FixedPos)
                    {
                        vehPos = Vector3.Lerp(info.Root.position, vehPos, prog);   // Root.position 是**插值后**的渲染位姿
                    }
                    vehYaw = Mathf.LerpAngle(info.Root.eulerAngles.y, vehYaw, prog);
                }
            }

            // ★ 交给插值组件落地（见方法注释：直写就是"每包跳一下"）
            if (info.View != null) info.View.ApplyPose(vehPos, vehYaw, 0f, teleport);
            else info.Root.SetPositionAndRotation(vehPos, vehRot);   // 兜底：插值组件挂不上时至少位置/朝向对

            return true;
        }

        /// <summary>把 sid 解析成**本端那个实体**：本机 sid ⇒ 本机玩家；其余 ⇒ 对应盟友实例。</summary>
        static bool TryResolveRider(uint sid, out GameObject rider)
        {
            rider = null;

            var flow = NetRoomFlow.Instance;
            if (flow != null && sid == flow.SelfSid)
            {
                // 自己的那条本该在 OnVehicleMountMsg 里被丢掉；这里只作兜底（本机玩家身上没有 FriendController）
                var self = ActorsManager.Player as Component;
                rider = self != null ? self.gameObject : null;
                return rider != null;
            }

            var fb = NetFriendBridge.Instance;
            if (fb != null && fb.TryGetFriendObject(sid, out rider) && rider != null) return true;

            rider = null;
            return false;
        }

        /// <summary>按 <c>NetId</c> 找本端**可驾驶物**（统一在 <c>ActorsManager.Actors</c> 全表里找；
        /// 号段不相交 ⇒ 一个号只指一台）。机甲与炮台是同一套结构：根上就是 <c>Actor</c>(SpecUnit) +
        /// 一个 <c>IDrivable</c>（<c>VehicleController</c> / <c>EmplacementController</c>），
        /// <c>Furniture_Vehicle</c> 在 `Back` 子物体上（已核对 <c>Turret_Manual_AT.prefab</c> 与 Exosuit 同型）。</summary>
        static BaseSelfController FindVehicle(int netId, out Furniture_Vehicle seat)
        {
            seat = null;
            if (netId == 0) return null;

            var list = ActorsManager.Actors;
            for (int i = 0; i < list.Count; ++i)
            {
                if (list[i] is Actor a && a != null && a.NetId == netId)
                {
                    seat = a.GetComponentInChildren<Furniture_Vehicle>(true);
                    return a.GetComponent<BaseSelfController>();   // 机甲 / 炮台都在根上
                }
            }
            return null;
        }

        /// <summary>找不到载具时把本端"载具表"整张打出来（判"键不对"还是"本端压根没这辆"）。</summary>
        static void DumpVehicles()
        {
            var sb = new System.Text.StringBuilder();
            int n = 0;
            var list = ActorsManager.Actors;
            for (int i = 0; i < list.Count; ++i)
            {
                if (list[i] is Actor a && a != null && a.GetComponent<BaseSelfController>() != null)
                {
                    if (n++ > 0) sb.Append(", ");
                    sb.Append(a.name).Append(':').Append(a.NetId);
                }
            }
            FPSGame.Utils.NetSyncLog.Warn("载具/键表", $"本端载具共 {n} 辆（名字:NetId）：{sb}");
        }

        // ==================== 骑手抢救（载具要被销毁时） ====================

        /// <summary>
        /// 【载具将死】把挂在这辆车下的**远端骑手**先摘出来（<c>SetParent(null)</c> + 还运动栈 + 恢复吃位姿）。
        ///
        /// <para>▍为什么必须有：远端骑手是 <see cref="Furniture_Vehicle.ApplyRemoteMount"/> 挂到
        /// <c>relatedTrans2</c>（载具根）下的**子物体**，而载具 prefab 的 <c>Health</c> 勾了 `AutoDestroy`
        /// （<c>Health.HandleDeath</c> 末句 = <c>Destroy(gameObject)</c>）⇒ **骑手被连带销毁**；
        /// 而 <c>Actor.OnDestroy</c> 只会让 <c>NetFriendBridge</c> 把他的实例从字典里摘掉，重建时机
        /// （名单同步 / 换场景）在战斗期**都不会来** ⇒ 客机上房主**永久消失**
        /// （2026-10-14 实测："房主看得见客机、客机看不见房主"的单方向断联）。</para>
        ///
        /// <para>▍时机安全：死亡链是"先 <c>OnDie?.Invoke</c>（本方法在其内）**再** <c>Destroy()</c>"，
        /// 而 <c>Destroy</c> 是帧末延迟执行 ⇒ 摘骑手必然早于真正的销毁。</para>
        /// </summary>
        static void RescueRiders(int netId, string why)
        {
            if (netId == 0 || _remoteRides.Count == 0) return;

            uint found = 0u;
            bool has = false;
            foreach (var kv in _remoteRides)
            {
                if (kv.Value == null || kv.Value.NetId != netId) continue;
                found = kv.Key;
                has = true;
                break;
            }
            if (!has) return;   // 车上没有远端骑手（本地自己开的 / 空车）⇒ 无事可做

            FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收",
                $"载具 netId={netId} 即将销毁（{why}）⇒ 先摘下骑手 sid={found}" +
                "（他是载具的子物体，不摘就会跟着一起没 ⇒ 客机上房主彻底消失）");
            ApplyUnmount(found);
        }

        /// <summary>载具**死亡** ⇒ 抢救骑手。两端都会走：房主那条是权威死亡，本端副本那条来自远端判死
        /// （<c>EnemyNetBridge.ApplyRemoteDeath → IHealth.Kill</c>）。⚠ 远端副本在本端**从来没有 owner**
        /// ⇒ <c>VehicleController.OnDie</c> 根本没订阅过 ⇒ 只能靠这条事件拿到时机。</summary>
        static void OnUnitDeath(Actor actor)
        {
            if (actor == null) return;
            RescueRiders(actor.NetId, "单位死亡");
            EvictRiderOfActor(actor);
        }

        /// <summary>【骑手自己死了 ⇒ 也要把他摘出来】(2026-10-15)。
        ///
        /// <para>▍驾驶者那台：死亡收口在 <c>VehicleController.OnDriverDie</c> / <c>Furniture_Vehicle.OnRiderDie</c>
        /// （唯一收口 = <c>SetOwener(null)</c>，回包就是 4053 <c>mounted=false</c>）。</para>
        ///
        /// <para>▍**远端**：盟友实例上没有任何人会走那条路（他的 Health / 输入都在**他自己的**机器上）⇒
        /// 尸体一直挂在机甲里、机甲还照着他的网络位姿被"推导"着跑（实测：玩家死在机甲上没被移出载具）。
        /// 这里只清**本端那条绑定**：人已经死了，运动栈/父级该还原；占用表跟着清（车不能被尸体钉住）。</para>
        ///
        /// <para>▍本机玩家死亡不走这里（他没在 <see cref="_remoteRides"/> 表里）：那次由
        /// <c>VehicleController</c> 的死亡收口负责，并且**会**上报 4053。</para>
        /// </summary>
        static void EvictRiderOfActor(Actor actor)
        {
            if (_remoteRides.Count == 0) return;

            var go = actor.gameObject;
            uint found = 0u;
            bool has = false;
            foreach (var kv in _remoteRides)
            {
                if (kv.Value == null || kv.Value.Rider != go) continue;
                found = kv.Key;
                has = true;
                break;
            }
            if (!has) return;

            FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收",
                $"骑手 sid={found}（{go.name}）已死亡 ⇒ 从载具里摘出来（远端不会走本地的死亡收口）");
            ApplyUnmount(found);
        }

        // ==================== 边缘 / 清理 ====================

        static void OnPlayerLeft(uint sid)
        {
            if (sid == 0u) return;
            ApplyUnmount(sid);   // 驾驶者离场：他那辆上不该再挂着人（实例随后会被 NetFriendBridge 销毁）
        }

        static void OnHostLost()
        {
            int n = _remoteRides.Count;
            ClearRides();
            UnbindLocalDrivenWeapons();   // P4：房主没了 ⇒ 退订开火上报（本端退回单机，没人要收）
            FPSGame.Utils.NetSyncLog.SyncLog("载具/上机·收", $"与房主断开 ⇒ 清空 {n} 条驾驶绑定（本端退回单机）");
        }

        static void OnSceneChanged(string sceneName)
        {
            ClearRides();
            _selfDriven.Clear();   // 旧场景的载具全没了，留痕表只会让"下机"误报
            UnbindLocalDrivenWeapons();   // P4：旧场景那台车的武器管理器也要退订（换场景物体全销毁）
        }

        static void ClearRides()
        {
            if (_remoteRides.Count == 0)
            {
                _drivers.Clear();   // 占用表也要清（本机开的那台 / 已失联的占用者）
                return;
            }

            // 逐个摘（遍历时不能改字典）
            var sids = new List<uint>(_remoteRides.Keys);
            for (int i = 0; i < sids.Count; ++i) ApplyUnmount(sids[i]);
            _remoteRides.Clear();
            _drivers.Clear();
        }
    }
}
