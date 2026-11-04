# 主题 · 联机 · 消息号表（`07NetGame/CmdId.cs`）

> **2026-11-04 全表重排**：号已按模块连续分配（4001-4019 会话 / 4020-4049 表现 / 4050-4089 世界 / 4090-4099 停用 / 500x 位姿 / 600x 配置）。
> 本文件是唯一权威口径；`daily/` 与 `archive/` 里的旧号是当时的记录（对照表见 `2026-11-04.md`），**不要照旧号查实现**。
> 落地细节去对应子篇；新增号只许往本段末尾追加。

## 1. 号段

| 段 | 用途 |
|---|---|
| 1001/1002 | 传输控制（`NetTmp/Services/NetCmdId`，不在本表） |
| 4001-4019 | 会话 / 名单 / 开局闸门 |
| 4020-4049 | 玩家表现（切枪/开火/生命/弹药/复活/语音/战备/载具开火） |
| 4050-4089 | 局内世界状态（波次/敌人/场景单位/家具/生成族/终局） |
| 4090-4099 | **已停用**：只占位防复用，永不收发 |
| 5001-5099 | 位姿 |
| 6001-6099 | 玩家配置（资料 / 舰桥准备 / 改装） |

## 2. 消息号表

| 号 | 名称 | 方向 | 键 / 要点 |
|---|---|---|---|
| 4001/4002 | JoinRoomReq/Rsp | 成员↔房主 | |
| 4003 | LeaveRoomNtf | 成员→房主 | `Leave()` 里要同时 `_wasInRoom=false` |
| 4004 | PlayerListSync | 房主→全体 | **战斗期冻结** |
| 4005 | ReadyState | 成员→房主 | 准备位只能由它改 |
| 4006 | TaskConfirm | 房主→全体 | 含 `TaskCfgDto`（以内容为准）+ 指纹 |
| 4007 | Transition | 房主→全体 | `MatchId`；成员只推 `GameRoot.GameState` |
| 4008 | PlayerLeftNtf | 房主→全体 | 只带 sid、**不重排名单** |
| 4009 | LoadCompleteNtf | 成员→房主 | 本机加载完 |
| 4010 | BattleStartSync | 房主→全体 | 全体加载完 ⇒ 一起开打 |
| 4020/4021 | PlayerWeaponSwitch Ntf/Sync | | 槽位口径 = `PlayerWeaponsManager.SlotOf` |
| 4022/4023 | PlayerShoot Ntf/Sync | | **只做表现**；带 `HitX/Y/Z` + `DamageIndex`(Key 8) |
| 4024/4025 | PlayerVital Ntf/Sync | | 血/盾/倒地，传"值 + 上限" |
| 4026/4027 | PlayerAmmo Ntf/Sync | | 弹药**比例**（独立通道、10Hz + 变化判定） |
| 4028 | PlayerReviveNtf | 双向 | **键 = sid**（房主 = 0）⇒ 被救者本机 `ReviveSelf()`。⚠ 不能蹭 4059：`Furniture_PlayerDown` 的 SyncId 含位置量化，盟友出生点比本人少 0.2m(Y)（`NextSpawnPos` 没带 `RawStartPoint` 的抬升）⇒ 键必然对不上；桥 `NetPlayerStateBridge`。⚠ 背包武器开火**复用 4022/4023**，槽位用虚拟值 7（`NetPlayerStateBridge.BagSlotIndex`） |
| 4029/4030 | Speech Ntf/Sync | | |
| 4031 | MarkNtf | 双向 | 表现类；`target` 是引用过不了网 ⇒ 传 null |
| 4032 | CallKaiNtf | 双向 | 凯伊真的走过去 |
| 4033/4034 | AirdropCall Ntf/Sync | | 战备 id + 落点；房主**不在 Ntf 里本地复现**；载具号随 4034 由房主分配（成员发 0） |
| 4035 | VehicleShootNtf | 双向 | **载具开火只表现不结算**：键 = 载具号；值 = 武器下标 + 方向 + 准心落点 + 档位 + 发射点下标 + 弹匣/后备弹。⚠ 不复用 4022/4023（那是 `PlayerWeaponsManager` 槽位语义，背包已借 7）；丢自己那条按"这辆车是不是我开的" |
| 4050 | WaveStartSync | 房主→全体 | `waveIndex` + 参数（**成员不在本地开波**） |
| 4051 | WaveCenterNtf | 房主→全体 | 追击中心 ~2Hz 下发，客户端只插值跟随 |
| 4052 | EnemyMoveSync | 房主→全体 | `NetId` + 目标点（意图式） |
| 4053 | EnemyHitNtf | 成员→房主 | 房主用 `DamagePacket` 结算 |
| 4054 | EnemyDamagedSync | 房主→全体 | 与 4053 成对；**死亡也在里面**（`DamageMsg.IsDeath`） |
| 4055 | SceneUnitMoveSync | 房主→全体 | 键 = `Actor.Id`（不是 NetId） |
| 4056/4057 | SceneActorReq/Ntf | 成员↔房主 | 场景 actor 快照 |
| 4058 | SceneDestructibleNtf | 双向 | 键 = `FNV1a(Actor.Id + 位置0.1m)`；**只传"死了"** |
| 4059 | FurnitureOperateNtf | 双向 | 键 = `Furniture_Attached.SyncId`；`State/StateKey` 非空 = **状态同步**（走 `ApplyRemoteState`，不重放 `Operate`）。⚠ **载具上下机也走这条**（`NetId != 0` 分支，远端应用走 `NetVehicleBridge` 挂点，不走 `ApplyRemoteOperate`） |
| 4060 | OOPartSubmitNtf | 双向 | 容器家具 `SyncId` + count/type ⇒ 远端在幽灵背包上重放 |
| 4061 | KeyScreenStageNtf | 双向 | 终端家具 `SyncId`；**驱动端**上报、其余端只应用 |
| 4062 | KeyScreenInputNtf | 双向 | 终端 `SyncId` + `key/kind` |
| 4063 | HostSpawnNtf | 房主→全体 | 生成族（`Kind = Patrol / Unit`）：房主唯一生成权威，成员按来源键复刻并覆盖 NetId |
| 4064 | GameOverNtf | 房主→全体 | 接收端**本局只结束一次**（防两个 timer） |
| 4065 | EvacuateNtf | 房主→全体 | 撤离点 |
| 4066 | MissionUpdateNtf | 房主→全体 | 键 = `MissionBase.netOrder`，**必须带 State** |
| 4090 | EnemyDiedSync【停用】 | — | 已并入 4054 的 `IsDeath` |
| 4091/4092 | PatrolSpawnNtf / EnemySpawnNtf【停用】 | — | 已并入 4063（`Kind` 区分） |
| 4093 | VehicleMountNtf【停用】 | — | 已并入 4059 的 `NetId` 分支 |
| 4094 | ShieldDamageNtf【停用】 | — | 已并入伤害族（`DamageMsg.OwnerSid`，走 4053/4054） |
| 5001/5002 | PlayerTransformUp / TransformBatchSync | 成员↔房主 | 位姿 20Hz 上行 / 15Hz 批量下行 |
| 6001 | PlayerProfileNtf | 成员→房主 | 入房成功即发（否则盟友没身体） |
| 6002/6003 | PlayerSetup Ntf/Sync | | 战备槽 0-3 / 强化槽 4 |
| 6004/6005 | PlayerLoadout Ntf/Sync | | 改装（档位/模组/载具）。⚠ 应用的**第一步**是 `FriendWeaponView.Setup`（`Instantiate` 后立刻 `ApplyUpgrade`）⇒ 若那把枪的 `LogicInit/InitAttribute` 没跑完，`WeaponCfg[type]` 返回 null、`AttrInfo` 构造当场 NRE、**整条 6005 处理被打断**（既有缺陷，2026-10-14 已加兜底+留痕：见 daily） |
