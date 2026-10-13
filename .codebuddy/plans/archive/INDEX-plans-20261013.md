# plans 归档索引 · 2026-10-13

> 处理方式：**只归档"目标产物已在代码里"的一次性执行计划**，正文从 `plans/` 移出（本次为直接删除，`git` 可回）。
> 恢复：`git checkout HEAD -- <路径>`（`.codebuddy/plans/*.md` 都在版本控制里；唯一例外是本日新写的 `KCPNet源码分析报告.md`，它**没删**）。
> 判据一律是"**产物在不在**"，不是"计划里写了完成"。脚本类（`*.py`/`*.json`/`*.txt`/`*.ps1`）**全部保留**（按规矩"脚本留档、能重跑"）。

| 归档文件 | 一句话内容 | 归档依据（产物） |
|---|---|---|
| `EnemyFxData_SO_特效配置共享化改造_8cbede7b.md` | 把 `EnemyControllerFX` 里随实例复制的 FX 配置抽成"每敌人类型一个 SO" | `06Gameplay/AI/Fx/EnemyFxData_SO.cs` + 配套 `RendererSet.cs` / `RendererSetConfig.cs` / `FxControllerBase.cs` 均已存在 |
| `split-wavemanager-clean-unityeditor_c59e5f35.md` | 拆分 `WaveManager.cs`（+ `ZergWave`/`RobotWave`）并修 6 处运行时 `UnityEditor` 误引 | `09Manager/Battle/ZergWave.cs` 已存在；全工程 `using UnityEditor` 10 处已全部落在 `#if UNITY_EDITOR` 内（2026-10-13 复核） |
| `支援武器拾取与轮盘卸载系统_d7310c6a.md` | 支援武器实现 `IEquippable` + 地面拾取 + 轮盘卸载落地 | `06Gameplay/Interactable/Furniture_WeaponPickup.cs` 已存在 |
| `伤害_直接来源与起源拆分_执行计划.md` | `DamageCaused`（显示/AI）与 `DamageOrigin`（统计）两口径拆分 | 2026-10-12 已落地（见 `memory/topics/gameplay.md §伤害管线`、`net-battle.md`、`2026-10-12.md`） |
| `联机_NetId统一_伤害通道_执行计划.md` | NetId 统一 + 伤害通道统一（4 个联机 bug 的修复计划） | 2026-10-12 第 4~8 步已全部落地（见 `2026-10-12.md`） |
| `联机_NetTmp拆分为传输层与游戏协议层_方案.md` | `02_Net` 只做传输、游戏协议独立成 `07_NetGame` | 拆分层已成型：`NetTmp/02_Net.asmdef` + `07NetGame/07_NetGame.asmdef`，`10_UI` 已独立并按新引用图工作 |
| `联机_房间列表接入与KCPNet扩展_计划.md` | 房间列表面板接入网络层 + 给 `LanRoomInfo` 扩字段 | 该文件自身已注明"房间/入房/扩 DTO 已完成"；`LanRoomInfo` 现 16 字段、房间列表面板已上线 |
| `联机_关键物体位置同步_计划.md` | 关键物体（玩家/盟友）位姿同步设计 | `CmdId.PlayerTransformUp=5001` / `TransformBatchSync=5002` + `NetTransformFlow` 已落地 |
| `联机_成员进战斗场景链路_方案.md` | 成员从大厅进战斗场景的链路方案 | 已落地：`TeamNetBridge` 接 `TransitionNtf` + 开局加载闸门 `LoadCompleteNtf(4041)` / `BattleStartSync(4042)`（见 `net-flow.md §2`） |
| `联机_种子同步与初始化确定性_改造方案.md` | 种子同步与初始化确定性逐条改造 | `00Tools/SeedUtil.cs` 的 `Derive` 已被 `BattleManager`/`WaveManager`/`ActorsManager`/`NetSpawnKey` 等广泛使用（确定性派生已成型） |
| `p2_ns_plan.md` | `06Gameplay` 命名空间对齐（阶段 0 产出，含 `TargetData`/`IVehicleUIController` 的归属决策） | 用户 2026-10-13 确认"命名空间已经改好了" |

## 保留（未归档）及原因

| 文件 | 保留原因 |
|---|---|
| `KCPNet源码分析报告.md` | 今日产出的**参考报告**，仍在被引用（`memory/topics/net.md §3.0`） |
| `联机_未同步清单与同步方案.md` | **活跃文档**：§3 待办刚复核补勾，后续同步都按它排 |
| `联机_随机源审计清单.md` | 审计参考清单（种子区/快照区划分），随时要查 |
| `handequip-inline-gravity_c226d327.md`<br>`handequip-simple-gravity_af26dd7b.md` | 同一需求的两个方案，`SimpleGravity.cs` **不存在** ⇒ 未执行（或已换别的做法）⇒ 待人判断 |
| `伤害抗性迁移到Health_穿甲等级结算_e3e94272.md`<br>`伤害抗性迁移到Health_穿甲等级结算_b1f3efdc(未完成).md` | 代码里 `armorLevel` 有部分痕迹但 `DamagePacket` 未看到 AP 字段 ⇒ **未完成**，两份都留着 |

## 顺带记录：旧名注释已清（2026-10-13 同日完成）

全工程注释里的旧标识符/旧目录名已一次性清干净（脚本 `plans/fix_stale_names.py`，可重跑）：

| 旧 | 新 | 处理处数 |
|---|---|---|
| `04UI` | `10UI` | 15 |
| `01Manager` | `09Manager` | 21 |
| `GlobalEventSub` | `GlobalEventBus` | 22 |
| `BattleEventSub` | `BattleEventBus` | 16 |
| `WndTypeEnum` | `WndType` | 2 |
| `Assets/Scripts/02Game/` | `Assets/Scripts/06Gameplay/` | 1 |

- 口径：**历史说明也一起换**（"原 `GlobalEventSub`" 里的"原"已经承载历史语义；旧标识符留着只会让读者 grep 不到；目录名换新后指向同一处，不丢信息）。
- 共 38 个文件；复查残留 `0`；`offline_compile` 10 个程序集全 **0 error**。
