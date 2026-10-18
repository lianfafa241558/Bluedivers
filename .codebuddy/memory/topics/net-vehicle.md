# 主题 · 联机 · 载具（号一致 / 上下机 4053 / 炮位 / P2 位姿）

> 由 `net-battle.md` §7.3 拆出（2026-10-18），本篇 = 号一致 + 上下机 + 炮位 + 位姿推导。
> 远端表现 / 外观 / 血量权威 / 开火 4054 / 断线 → `net-vehicle-remote.md`。
> 计划原文：`plans/联机_载具系统同步_执行计划.md`。

### 7.3 🚗 载具（2026-10-14 P0/P1 落地 · 计划 `plans/联机_载具系统同步_执行计划.md`）
- **事实更正（计划行号是错的）**：`ADSO_O_Exosuit.deliveryType = 3` = **Medivac 运输机**
  （`AirdropDeliveryEnum`：Pod0/Bomb1/Jet2/Medivac3）⇒ 机甲在 `VFXAirdropEffect.StartMedivac` 生成；
  `Furniture_Vehicle` 挂在 **Exosuit 的子物体**上（`relatedTrans` = 座位、`relatedTrans2` = 载具根）。`Turret_Manual_AT` 也用它。
- **P0 号一致**：4027/4028 追加 `[Key(6)] int VehicleNetId`（只许追加）；房主分配，号段
  `ActorsManager.VehicleNetIdBase = 0x3000_0000`；⚠ **07 看不见 05** ⇒ `NetHostSvc.VehicleNetIdAllocator` 由 09 的桥**委托注入**。
  `VFXAirdropEffect.SpawnCreatObject` 把 **5 处** `Instantiate(creatObect)`（373/455/515/551/595）统一收口：
  `ActorsManager.Replicating` 包住（**不领自增号**）→ 生成后盖章根 `SpecUnit Actor`；`_vehicleNetId` 三态（-1 不托管 / 0 待房主定号 / >0 已定）。
  成员自己那发先 0，收到 4028 按 FIFO（`NetFriendBridge._pendingVehicleBeacons`）补号。
- **P1 上下机**：**4053 单号双向**（同 4036 走法）。⚠ **载具必须从 4036 摘出** —— 4036 的远端重放会走
  `Furniture_Vehicle.Operate()` ⇒ `VehicleController.SetOwener(盟友实例)`，而那条路硬要 `PlayerController` ⇒ **NRE**。
  远端改走 `Furniture_Vehicle.ApplyRemoteMount`（挂点 + 摘父级，**刻意不置 `inOperate`**）+ `FriendController.SetVehicleRide`
  （停 `NetTransformView` 免得被位姿拽回地面 + 压停移动动画 免得在舱里狂奔）。上报点 = `SetOwener` 抛
  `BattleEventBus.OnVehicleOwnerChanged`（06 看不见 09）；桥 = `09Manager/Battle/NetVehicleBridge`（自举常驻）。
  ⚠ **远端挂点必须落"座位终点"而不是"入场点"（2026-10-14 修）**：本地 `Furniture_Vehicle.Operate()` 先把驾驶者放到
  入场点 `transform.TransformPoint(0,0,1)`，随后 `InOperateUpdate()` 在 1 秒内 Slerp/Lerp 贴到 `relatedTrans.position/rotation`
  ⇒ 远端 `ApplyRemoteMount` 应**直接落 `relatedTrans`**（Exosuit 实测：座位点相对载具根 `(0,2.638,-0.030)` vs
  入场点 `(0,3.430,-1.045)`，**差 1.287 米**；错了就是"客机上骑手站在机甲顶上偏后"）。
  ⚠ `relatedTrans`/`relatedTrans2` 都在**骨骼**层级下（FV 挂在 `bone_Pelvis/Back`）⇒ 挂点坐标随动画动，
  远端只在"上车那一刻采样一次并烘焙到载具根下"（本地同）。**自检口径**：`载具/上机·收` 的 `局部偏移=` 应≈`(0,2.64,-0.03)`。
  ⚠ **驾驶者本人死亡也必须下车（2026-10-14 修）**：原来 `VehicleController` 只订阅**车自己**的 `Health.OnDie`
  ⇒ 人被打死时 `_owner` 还非空、死人还能接着开。修 = 上车时**额外**订阅驾驶者的 `IHealth.OnDie`，仍走唯一收口
  `SetOwener(null)`（于是 4053 自动上报"下机"，**不需要新消息**）；但**不能**直接复用那套还原 ——
  它会把 `ActorState` 刷回 `Normal`（把倒地的人拽出死亡态）、重开武器相机（死亡镜头上叠武器画面）⇒
  用"只在状态是 `Hide` 时还原 `ActorState`" + `_ownerDead` 守卫解决。同时 `Furniture_Vehicle` 侧也要清
  `inOperate/owner`（否则死后按 F 会走 `Operate()` 把人搬到"入场点"）。
  ⭐ **交互总闸（2026-10-14）**：`PlayerOperationController.CanInteract()` —— `IsDead` 或
  `ActorState ∈ {Dead, Hide}` 时**一帧都不给交互**（早退还要清 `target/Press/音频/InOperation/ForceAim/第三人称距离`）。
  ⚠ 这条**不能**写进 `PlayerInputHandler.CanProcessInput`：倒地呼叫（跳键）也走它，会被一起掐掉。
  全工程只有 `PlayerOperationController` 消费玩家的 `GetOperate*`（载具/炮位的 `TryExit` 用它们**自己**的
  InputHandler）⇒ 一处即总闸。⚠ `EmplacementController`（炮位）与 `VehicleController` 同病：**都没接驾驶者死亡**。

- ⭐ **炮位 = 载具的同一套结构（2026-10-14 落地）**：`Turret_Manual_AT.prefab`（空投手动炮台）与 Exosuit 同型 ——
  根 = `Actor(**SpecUnit**)` + `EmplacementController` + `VehicleWeaponsManager`，`Furniture_Vehicle` 在 `Back`，
  `relatedTrans=Point` / `relatedTrans2=`根 ⇒ **上下机复用 4053、开火复用 4054**，只要：
  ① `EmplacementController.SetOwener` 末尾抛 **`BattleEventBus.VehicleOwnerChanged`**（原来漏了 ⇒ 炮位完全不上报）；
  ② 桥从"只认机甲"放宽：`FindVehicle`/`RideInfo.Vehicle` 用共同基类 **`BaseSelfController`**；
  ③ 炮台是 `BaseSelfController`（**只转不移动**）⇒ `RideInfo.FixedPos = true`，P2 推导位**只推 yaw、位置原样**
  （拿驾驶者位姿推位置会把炮台抖起来）。
  ⚠ 4036 不冲突：`NetFurnitureBridge.OnLocalOperate` 早按 `furn is Furniture_Vehicle` 摘出（炮台共用同一组件）。
  ⚠ 新增可驾驶物（`IDrivable`）的**检查清单**：① 在 `SetOwener` 收口里抛 `VehicleOwnerChanged`；
  ② 桥的 `FindVehicle`/`RideInfo` 能否认它（共同基类）；③ 它是"移动"还是"只转"（`FixedPos`）；
  ④ 驾驶者死亡要下车（订阅 `IHealth.OnDie` → `SetOwener(null)` + 死亡守卫：`ActorState` 只在 `Hide` 时还原、
     别开武器相机）；⑤ 它有 `VehicleWeaponsManager` 才有 4054。
- **P2 位姿（零新消息，2026-10-14 落地）**：载具位姿 = 驾驶者位姿 ⊗ **座位在载具根下的局部偏移⁻¹**
  （`NetVehicleBridge.TryDriveVehiclePose`，由 `NetFriendBridge.HandlePoses` 每条先问，true 就不写盟友实例）。
  载具根取 **`vehicle.transform`（Actor/VehicleController 所在物体）**，不取 `relatedTrans2`（Exosuit 上二者相同，
  但炮台未必）；上车头 0.35s 插值贴合。
  ⭐ **远端必须冻结 `VehicleController`**（它 `Update` 里跑 `GroundCheck` + 重力 `Move`，会和推导位打架 ⇒ 抖动/下沉）；
  下车或被本机抢开时还原（`EvictRideOfVehicle`）。
  ⚠ **三处实测修正（2026-10-14 用户双端验收后）**：
  ① **绝不能直写 `transform`**：20Hz 直写 = "每个包跳一下"（用户原话"高频短距离位移"）⇒ 载具根挂 **`NetTransformView`**
  接收推导位做缓冲插值（下车**必须关掉**，否则它把载具钉在最后一帧位姿上、与回来的运动栈打架）；
  ② 由此**只推 yaw**：座位（`relatedTrans`）常带俯仰/翻滚（Exosuit 的 `Point` 是 180° 绕斜轴）⇒
  `RideInfo` 存 `SeatYawOffset`（**不是**整份四元数，整份反解会把机甲拧歪，本地载具根恒 yaw-only）；
  ③ 冻结运动栈会**连带掐掉走路动画**（动画就挂在 `BaseSelfMoveableController.LateUpdate` 里读 `CharacterVelocity`）
  ⇒ 载具根另挂 `NetVehicleRemoteMotion`（按每帧位移差分驱动 `IsMove`/`Speed`，口径与本地逐字一致）。
- ⭐ **骑手是载具的"子物体" ⇒ 载具一销毁就把他带走（2026-10-14 实测"客机看不见房主"的根因）**：
  载具 prefab 的 `Health` 勾了 `AutoDestroy`（`HandleDeath` 末句 `Destroy(gameObject)`；本端副本由 4040 判死那条
  `EnemyNetBridge.ApplyRemoteDeath → IHealth.Kill()` 触发）⇒ 挂在其下的盟友实例被**连带销毁**，
  而 `Actor.OnDestroy` 只让 `NetFriendBridge.HandleFriendLeave` **静默**摘字典 ⇒ 单方向永久消失
  （重建时机只有"名单同步 / 换场景"，战斗期名单冻结 ⇒ 都不来）。
  ⚠ 别指望 `VehicleController.OnDie → SetOwener(null)`：它只在 `SetOwener` 的 **owner 分支**里订阅，
  而远端副本在本端**从来没有 owner** ⇒ 那条路不触发（本地那侧靠它自己摘父级，所以只有客机坏）。
  修（两层）：① `NetVehicleBridge` 订阅 `UnitEventBus.OnUnitDeath` → `RescueRiders`（载具将死先摘骑手；
  时机安全：`OnDie?.Invoke` 在前、`Destroy` 帧末延迟执行）② `NetFriendBridge` 每 0.5s `ReconcileFriends` 自愈
  + `RebindRide` 重接载具绑定（对付任何意外销毁，不只载具）。
- ⭐ **运输机投放：机甲必须"卸下"才不会丢（2026-10-14 实测"客机第一台机甲没落下来"）**：
  机甲生成即挂成运输机（`NeoNimbus_Vehicle`）的**子物体**，`UpdateMedivac` 在 `data.State == Sustain` 时 `parent = null`；
  ⚠ 本机自己叫的那发 `data` 就是**战备槽位那份共享 `AirdropData`**（`SetOrigin` 里直接赋值）⇒ **连续呼叫同一战备**时
  第二发把它重置回 `Arrive`，第一发永远等不到 `Sustain` ⇒ 机甲一直挂在运输机下、随运输机回池一起消失（"没落下来/不存在"）。
  远端重放是**独立副本**（`TmpAirdrop → new AirdropData` + `isTmp` 自走）⇒ 只有**呼叫端**会丢 ⇒ 两端机甲数不一致（房主 3 / 客机 2）。
  修：卸载判据加 `showTime >= arriveTime` 兜底 + `Init` 复位 `showTime` + `EndMedivac` 就地卸下兜底。
