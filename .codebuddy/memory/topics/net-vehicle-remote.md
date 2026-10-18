# 主题 · 联机 · 载具远端侧（外观 / 血量权威 / 开火 4054 / 断线）

> 由 `net-battle.md` §7.3 拆出（2026-10-18），承接 `net-vehicle.md`（号一致 / 上下机 / 位姿）。
> 含：骑手随车销毁、运输机投放、房主强关、皮肤被表现系统盖掉、P3 血量权威、P4/P5 收尾。

- **"房主强关"客机怎么发现（2026-10-14 修）**：⭐ **只信 `NetSvc.IsConnected` 是不够的** ——
  它等于 KCP 库 `clientSession.IsConnected()`，而房主进程被**强杀**时 **UDP 没有 FIN**，库不一定会（及时）翻成断开
  ⇒ 客机侧 `NetRoomFlow.DetectHostLost` 永不触发 ⇒ **房主的盟友模型 + `PlayerWnd` 队友行会一直挂着**
  （两个症状同时出现 = `OnHostLost` 没触发，别再分别去查模型/UI）。
  修法 = **心跳静默看门狗**（`NetClient.IsHostSilent`：发 Ping 时记 deadline、收到任何 Pong 清掉，默认 5s/心跳 2s）
  ⇒ `DetectHostLost` 判"活着"改成 `IsConnected && !IsHostSilent`，且静默时先 `svc.Disconnect()` 再走清理。
  ⚠ 队友 HUD 行的数据源 = **`ActorsManager.Players`**（`PlayerWnd.UpdatePlayerStates` 每帧遍历 ⇒ 实例一销毁就自愈），
  但 **`TeamManager.players`**（战备/结算界面按它排行 + `IdToIndex`）**不会自己收缩** ⇒
  `TeamNetBridge` 也必须订阅 `OnHostLost` 收敛成"仅本机"（否则回舰桥会看到幽灵行）。

- **载具外观（皮肤/武器）的联机口径与"会被盖掉"的坑（2026-10-14 实测）**：
  外观由 `BattleApplyVehicleData` 按**归属 sid**（`VFXAirdropEffect.StampCaller` 用 `CallerSid` 打标）
  查 `VehicleCustomState.TryGet(sid, vehicleName)`；⚠ `TryGet` 明确"该 sid 有同步表却缺这辆车 ⇒ **不回退**"，
  所以**任何地方都不许再兜一层本机存档**（否则就是"用我的配置冒充别人的"，实战表现 = 两端皮肤都变成本机那个）。
  ⭐ **皮肤写在 `MaterialPropertyBlock` 上（`MpbController`，`SetPropertyBlock` 整块替换语义）**
  ⇒ 会被别的表现系统整块盖掉：受击闪白/溶解（`FxControllerBase` 的 `RendererSlot`，按材质下标写块）
  与**诞生材质**（`EnemyControllerFX`：`BirthDuration` 内把 `sharedMaterials` 换成 `BirthMaterial`，
  到点 `RestoreMat()` 整块写回）。空投 Medivac 的"落地卸载"（`UpdateMedivac`）正好落在诞生材质还原的时间点附近
  ⇒ 实测"天上挂运输机时皮肤是对的、落地后变回默认"。**对策 = 表现切换点强制重贴**
  （`BattleApplyVehicleData.ForceApply`，空投落地那刻由 `VFXAirdropEffect` 调；`Apply()` 有"无变化跳过"，救不回来）。
  排查这类问题时用 `BattleApplyVehicleData.LogAppearance(tag)` 打"渲染器上真正生效的材质 + MPB 里的 `_BaseMap`"。
  ⭐ **修法（2026-10-14 定死）**：皮肤必须写进**与受击/溶解同一个共享槽位块** ——
  `FxControllerBase.GetSharedSlotBlock(r,index)` 拿到块 → 写 `_BaseMap`/`_BlendingMap`/`_BlendingScale` →
  `MarkSlotDirty`。理由：`SetPropertyBlock(block)`（渲染器级）与 `SetPropertyBlock(block,index)`（按下标）
  是**两套存储**，`RendererSlot.Flush()` 用按下标那份 ⇒ 一挨打就把渲染器级那份**永久顶掉**
  （实测：空投落地摔一下触发闪白，机甲皮肤从此变回默认）。⚠ 槽位表在 `FxControllerBase.Start` 才建，
  而载具 `Awake` 就贴外观 ⇒ 必须在 `Update` 里低频补盖（`_slotStampRetry`）。
  ⚠ 判"皮肤不对"前先分清是**配置查错**（应用日志里的 `皮肤=N` 与舰桥设置不符）还是**被盖掉**
  （应用日志对、但探针的 `逐下标[idxX=无(受击槽)]`）；探针只列**本体**渲染器（粒子/拖尾要跳过，
  否则日志会被 `VFX_HitSparks` 占满）。
  ⚠⚠ **"改装配置里没有武器"≠"该清掉武器"（2026-10-15 修，实测坑）**：`ApplyCustom` 原来在 `rebuildWeapons` 时
  **无条件** `_manager.ClearWeapons()` 再按 `data.weaponRights/Lefts` 重建；而炮台/运输机/补给仓/撤离信标这些的
  `VehicleData_SO`（`Vehicle_Turret`＝"雷酱" 等）**两个数组本来就是空**（整个工程只有 Exosuit 是 4/4）
  ⇒ 清完一把都不加 ⇒ 载具变"无武器"，连锁反应很唬人：`VehicleWeaponsManager.HaveWeapon()==false` ⇒
  ① 左右键的射击/瞄准分支（`else if (HaveWeapon())`）整段进不去 ② `OnSetOwner` 里走 `OnStateChange(false)`
  ⇒ **载具 UI 与准星根本不出现**（实测：空投 `Turret_Manual_AT` 上机后无 UI/无准星/LMB/RMB 全无反应，而机甲完全正常）。
  这些车的武器是**预制体预置**的（`Turret_Manual_AT` 自带 `Weapon_Rocket`），且它们**没配** `weaponPointR/L`
  （`Instantiate(go, null)` 会挂到场景根）⇒ 修法 = **配置无武器时跳过清空/重建**（`cfgHasWeapon = R>0||L>0`），
  **不是**往数据资产里塞武器。判据速查：探针看 `VehicleWeaponsManager.m_WeaponSlots.Length` + `BattleApplyVehicleData._applied`。
- **P3 血量/死亡（房主权威，2026-10-14 落地）**：**没有新消息**（复用 4025/4040）。
  ⭐ 关键查证：`EnemyNetBridge` 的应用路径按 `NetId` 在 `Actors` **全表找、不按类型过滤** ⇒ 载具天然吃得下 4040 与死亡；
  缺的只有 ① 上行（载具无 `EnemyController`）② 权威门。口径 = **AI 来源的载具伤害只有房主落地并广播；
  玩家侧来源（Player/Friend；部署物算 AI）照样落地 + 上报**；门插在 `Damageable.LocalDamageGate`（09 注入的委托，
  05 看不见 06/07/09）——返回 false ⇒ 不扣血/不死/**也不派发 `UnitDamaged`**（⇒ 自动不上报）。
  权威位 `BattleState.IsVehicleDamageAuthority`（**与 `IsTickDamageAuthority` 分两位**）；
  桥 = `09Manager/Battle/NetVehicleDamageBridge`（`WaveManager` Install/Uninstall）；上报要带**原始成分 + 部位下标**。
- **P4 开火表现（4054，2026-10-14 落地）**；**P5 三条收尾（2026-10-15）**：
  - ⭐ **一车一人**：原判据只看**本端** `inOperate`（两边各判各的）⇒ "a 上了 b 也能上、两人同车"。
    口径 = **房主唯一裁决 + 先到先得 + 成员永远让**：桥新增占用表 `_drivers`（载具号→驾驶者 sid，本地/远端都写），
    收到别人上机而占用者在案 ⇒ **房主拒绝**该 mount 并把占用者重播回去；**成员**若本机正开着这辆则
    `Furniture_Vehicle.ForceLocalExit()` 让车后再挂占用者；**非占用者报的下机**只清本端、不转发（否则回声把占用清了）。
    本地入口闸 = `Furniture_Vehicle._remoteOccupied`（`ApplyMount/ApplyUnmount` 推）→ `CanOperate` false（连交互提示都收起）。
    ⚠ 只标**远端**骑手：本机自己开时不置位，否则"退出驾驶"提示会被 `OperationWnd` 收掉。
  - ⭐ **弹量随发（4054 载荷末尾 +2 项）**：载具武器两端各一份实例 ⇒ 弹量天然分叉（实测 a 打完下车、b 上车看到满弹）。
    权威 = **驾驶者那台**；`VehicleShootMsg[Key(10/11)] Magazine/Ammo`，发端用
    `VehicleWeaponsManager.TryGetAmmoAfterShot`（⚠ `OnShoot` 是在 `HandleShoot()` 里抛的，`UseMagazine` 还在它后面 ⇒
    直接读会**永远差一发**；折算要 `WeaponBaseController.AmmoPerShot`），收端 `ApplyRemoteAmmo`。
  - ⭐ **"死了不下车"的真凶 = 共享 `OnDie` 列表被前面的订阅者打断**：驾驶者那份 Health 的 `OnDie` 上挂着
    Actor/PlayerController/玩家窗/HUD 一圈；探针实测打出**目标已销毁仍挂在表里**的 `Furniture_Vehicle.OnRiderDie@<noObj>`
    （它排在 `VehicleController.OnDriverDie` **前面**）⇒ 一旦抛异常，后面整段不执行。两层修：
    ① `VehicleController/Furniture_Vehicle` 在 `OnDisable` 退订驾驶者 `OnDie`（`OnEnable` 再挂回）；
    ② `VehicleController.LateUpdate` 加**轮询兜底** `_ownerHealth.GetHpCurrent() <= 0 ⇒ 走同一收口 SetOwener(null)`
    （⚠ 判 `IHealth` 存活要先 `as UnityEngine.Object`，接口引用不吃 Unity 假 null）。
    另：**远端骑手死亡**本机无人收口（他自己的 Health 在他那台）⇒ 桥加 `EvictRiderOfActor`（`OnUnitDeath` 里摘人 + 清占用）。
