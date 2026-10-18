# 主题 · 联机 · 消息号表（`07NetGame/CmdId.cs`）

> 由 `net-battle.md` 拆出（2026-10-18）。新号只许追加、旧号常量保留（停发不等于删号）；
> 号段占用：4001~4054 房间/战斗、5001/5002 位姿。各号落地细节去对应子篇。

## 2. 消息号表（`07NetGame/CmdId.cs`）
| 号 | 名称 | 方向 | 键 / 要点 |
|---|---|---|---|
| 4001/4002 | JoinRoomReq/Rsp | 成员→房主 | |
| 4003 | LeaveRoomNtf | 成员→房主 | `Leave()` 里要同时 `_wasInRoom=false` |
| 4004 | PlayerListSync | 房主→全体 | **战斗期冻结** |
| 4005 | ReadyState | 成员→房主 | 准备位只能由它改 |
| 4006 | TaskConfirm | 房主→全体 | 含 `TaskCfgDto`（以内容为准）+ 指纹 |
| 4007 | PlayerProfileNtf | 成员→房主 | 入房成功即发（否则盟友没身体） |
| 4008/4009 | PlayerArmament Ntf/Sync | | 带 `Sid` |
| 4010/4011 | PlayerBooster Ntf/Sync | | 带 `Sid` |
| 4012 | Transition | 房主→全体 | `MatchId`；成员只推 `GameRoot.GameState` |
| 4013/4014 | PlayerWeaponSwitch Ntf/Sync | | 槽位口径 = `PlayerWeaponsManager.SlotOf` |
| 4015/4016 | PlayerShoot Ntf/Sync | | **只做表现**；带 `HitX/Y/Z` + `DamageIndex`(Key 8) |
| 4017/4018 | PlayerVital Ntf/Sync | | 血/盾/倒地，传"值 + 上限" |
| 4019/4020 | PlayerAmmo Ntf/Sync | | 弹药**比例**（独立通道、10Hz + 变化判定） |
| 4021/4022 | SceneActorReq/Ntf | 成员↔房主 | 场景 actor 快照 |
| 4023 | WaveStartSync | 房主→全体 | `waveIndex` + 参数（**成员不在本地开波**） |
| 4024 | EnemyMoveSync | 房主→全体 | `NetId` + 目标点（意图式） |
| 4025 | EnemyHitNtf | 成员→房主 | 房主用 `DamagePacket` 结算 |
| 4026 | EnemyDiedSync | 房主→全体 | 成员 `Actor.Kill()` 干掉副本 |
| 4027/4028 | AirdropCall Ntf/Sync | | 战备 id + 落点；房主**不在 Ntf 里本地复现** |
| 4029 | PlayerLeftNtf | 房主→全体 | 只带 sid、**不重排名单** |
| 4030/4031 | Speech Ntf/Sync | | |
| 4032 | SceneUnitMoveSync | 房主→全体 | 键 = `Actor.Id`（不是 NetId） |
| 4033 | GameOverNtf | 房主→全体 | 接收端**本局只结束一次**（防两个 timer） |
| 4034 | EvacuateNtf | 房主→全体 | 撤离点 |
| 4035 | MissionUpdateNtf | 房主→全体 | 键 = `MissionBase.netOrder`，**必须带 State** |
| 4036 | FurnitureOperateNtf | 双向 | 键 = `Furniture_Attached.SyncId`；`State/StateKey` 非空 = **状态同步**（走 `ApplyRemoteState`，不重放 `Operate`） |
| 4037 | MarkNtf | 双向 | 表现类；`target` 是引用过不了网 ⇒ 传 null |
| 4038 | CallKaiNtf | 双向 | 凯伊真的走过去 |
| 4039 | WaveCenterNtf | 房主→全体 | 追击中心 ~2Hz |
| 4040 | EnemyDamagedSync | 房主→全体 | 与 4025 成对，血量口径统一在房主 |
| 4041 | LoadCompleteNtf | 成员→房主 | 本机加载完 |
| 4042 | BattleStartSync | 房主→全体 | 全体加载完 ⇒ 一起开打 |
| 4043 | SceneDestructibleNtf | 双向 | 键 = `FNV1a(Actor.Id + 位置0.1m)`；**只传"死了"** |
| 4044/4045 | PlayerLoadout Ntf/Sync | | 改装（档位/模组/载具）。⚠ 应用的**第一步**是 `FriendWeaponView.Setup`（`Instantiate` 后立刻 `ApplyUpgrade`）⇒ 若那把枪的 `LogicInit/InitAttribute` 没跑完，`WeaponCfg[type]` 返回 null、`AttrInfo` 构造当场 NRE、**整条 4045 处理被打断**（既有缺陷，2026-10-14 已加兜底+留痕：见 daily） |
| **4046** | KeyScreenStageNtf | 双向 | 终端家具 `SyncId`；**驱动端**上报、其余端只应用 |
| **4047** | PatrolSpawnNtf | 房主→全体 | 生成点 + 逐单位目标点（构成靠"同落点派生流"） |
| **4048** | OOPartSubmitNtf | 双向 | 容器家具 `SyncId` + count/type ⇒ 远端在幽灵背包上重放 |
| **4049** | KeyScreenInputNtf | 双向 | 终端 `SyncId` + `key/kind` |
| **4050** | PlayerReviveNtf | 双向 | **键 = sid**（房主 = 0）⇒ 被救者本机 `ReviveSelf()`。⚠ 不能蹭 4036：`Furniture_PlayerDown` 的 SyncId 含位置量化，盟友出生点比本人少 0.2m(Y)（`NextSpawnPos` 没带 `RawStartPoint` 的抬升）⇒ 键必然对不上；桥 `NetPlayerStateBridge`（09，`RuntimeInitializeOnLoadMethod` 自举）。⚠ 背包武器开火**复用 4015/4016**，槽位用虚拟值 7（`NetPlayerStateBridge.BagSlotIndex`） |
| **4053** | VehicleMountNtf | 双向 | **载具上下机**：键 = 载具 `Actor.NetId`（房主随 4028 分配，`ActorsManager.VehicleNetIdBase=0x3000_0000`）；值 = 驾驶者 sid + `Mounted`。**一个号双向**（同 4036 做法：房主转发 + 本地应用，桥内按角色分流）。⚠ 载具**从 4036 摘出**（见 §7.3） |
| **4054** | VehicleShootNtf | 双向 | **载具开火表现（只表现，不结算）**：键 = 载具号；值 = 武器下标 + 方向 + 准心落点 + 档位 + 发射点下标。**一个号双向**（同 4053）。上报钩子 = `VehicleWeaponsManager.OnWeaponShoot`（跟着武器生灭走，因为武器会被 `ClearWeapons+AddWeapon` 整批重建；`WeaponBaseController.OnShoot` 每次射击一次）；应用 = `WeaponBaseController.SpawnVisualSalvo`（**只表现**：摘 `FpsHelper.Hit`，保留弹道+命中特效）+ 收端按**开枪者档位**设 `UseDamageIndex`。⚠ 不复用 4015/4016（那是 `PlayerWeaponsManager` 槽位语义，背包已借 7）；丢自己那条按"**这辆车是不是我开的**"（键是载具号，没有 sid） |
| 5001/5002 | PlayerTransformUp / TransformBatchSync | 成员↔房主 | 位姿 20Hz 上行 / 15Hz 批量下行 |
