# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「拦截火车：轨道抽 partial + 3 条轨道 + 发车时刻表」与后续计划（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：进入"实际内容"阶段（轨道抽 partial + 3 条轨道 + 时刻表）
### 已完成（编译 0 错误，06_Gameplay 源 201）
- 轨道代码整段抽到 `Mission/Main/MissionInterceptTrain_Rail.cs`（`public partial class MissionInterceptTrain`），
  主文件只剩任务级：任务参数（destroyTarget/escapeLimit/首车延迟/发车间隔/departureCount）、
  火车/卫队参数（未接线，暂有 CS0414 警告）、`InitMission/StartMission/Unbuild/Uninit`、`BuildDepartureSchedule()`
- 新增 `RailTrack`（Index/Points/Cumulative/TotalLength/BedProfile/ProfileStep/Start/End + `SampleXZ`/`BedHeight`/`SampleForward`）
  —— 火车后续直接沿它行驶；`tracks` 列表按生成顺序存全部轨道
- 从 1 条扩到 `trackCount`(默认 3) 条：**第一阶段同步**（无 yield）逐条 规划→走廊登记→算纵坡，
  然后 `BuildDepartureSchedule()`（`random.Next(轨道数)`，两端一致 = "静态同步"口径）；
  **第二阶段有 yield** 逐条 `SmoothCorridorBed` + 铺节
- 关键点：每条轨道的走廊在规划下一条之前就登记进 ReservedCircles ⇒ 三条轨道自动互不重叠，也会被后续任务点躲开
- ⚠ 用户自己精简过主文件：删掉了 `yawOffsetDeg` / `sinkDepth` 两个字段（我保留了他们的写法，没再加回来）
### 调研结论（code-explorer，供后续接线用）
- 运行时生成单位：`WaveManager.CreatUnit(prefab,pos,rot,source,slot,target)` **房主专属**（成员端 return null），
  NetId 走 HostSpawnNetIdBase 段 + `NetHostSpawnBridge.ReportUnit` → 成员 `ReplayUnit`（置 ActorsManager.Replicating）
- 巡逻队：`BattleManager.CreatPatrol(pos)`（契约 IBattleService）**房主专属** + 4052 复刻；
  但 `EnemyController.PatrolPos` 是**静态单点**、走到即自毁（`Kill(true)`）⇒ 要"跟随火车"只能每帧改写 PatrolPos（要 NavMesh.SamplePosition 过）
- 静默移除：`I_AIController.Kill(true)` ⇒ 跳过死亡动画且不计击杀归属，但**仍会 fire UnitEventBus.OnEnemyDead**（Actor.OnDie 照跑）
- `ActorFlag.Boss` 清法是 `actor.RemoveFlag(ActorFlag.Boss)`；Boss 血条在 `HpWnd.OnEnemyCreate` 时按 flag 决定建不建
- `TransferDamageable.source` 是 **public 字段**，运行时可指定 ⇒ 车厢 spawning 时直接 `carTransfer.source = 车头Damageable` 即可
- `Actor.Awake` 里按 ForceHostSpawnNetId / ForceDerivedNetId / 敌人自增段 分配 NetId；`ForceDerivedNetId` 只适合"两端出生坐标一致"的场景实体
### 待办（下一步）
1. 把 TrainEngineSam / TrainEngineCar 两个 prefab 配成单位（Actor+HealthEnemy+Damageable[+Collider]；车厢 TransferDamageable+Collider），
   车头 flag 带 Boss、`UseHpBar`；车厢的 `source` 由生成代码运行时指定 ⇒ **需要 MCP 改 prefab 资产**
2. `TrainUnit`（车头挂）沿 `RailTrack` 行驶 + 车厢按弧长跟随；到站 → 静默移除整车
3. 发车调度（TickTime=1 秒口径）+ 卫队（CreatPatrol × N + 每帧改写 PatrolPos 跟随）+ 进度/失败判定（用 Actor 死亡事件区分"被打死"与"到站移除"）
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
