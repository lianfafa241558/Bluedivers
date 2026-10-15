# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「车厢附加炮塔改走 CreatUnit + 4052」更正（可重跑）。"""
import io, os

# ---------- ① daily：追加更正条目（不重写整份） ----------
daily = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(daily, encoding='utf-8', errors='ignore').read() if os.path.exists(daily) else ''

text += """

## 【更正】车厢附加炮塔：改走 CreatUnit + 4052（上面那条"两端各自 Instantiate/不发消息"作废）
- ❌ 错误口径：炮塔是**有 `Actor` 的单位**，成员端自造会在 `Actor.Awake` 领号 ⇒ 与房主号段撞车（撞号后房主下发的移动/伤害会作用到别的单位上）
- ✅ 正确口径（用户指正 + 已落地）：
  1. `MissionInterceptTrain : MissionBase, INetSpawner`（主文件），`InitMission` 里 `SpawnerRegistry.Register(this)`、`Uninit` 里 `Unregister`（成对，不能放 Start）
  2. 房主：`manager.CreatUnit(prefab, mount.position, mount.rotation, this, slot)` → 内部 Instantiate（**固定挂 `ActorsManager` 下**）+ `ReportUnit` 发 4052；
     生成后**必须自己 re-parent 到车厢挂点**（`SetParent(mount,false)` + 局部零位），否则不跟车走
  3. 成员：`ReplaySpawn(slot, netId, pos, rot, target, prefabId)` —— 按名字找回本端车厢 → `Instantiate(prefab, mount.position, mount.rotation, mount)`
     → **立刻 `Actor.NetId = netId`**（复刻期间 `ActorsManager.Replicating = true`，Awake 一个号都不发）→ 记进该趟车的 `Turrets`
  4. 槽位口径：`slot = 班次 * maxCarCount + 车厢序号`（两端算式一致）；**槽位不带 prefab**，挂哪门从两端同一份开局计划表 `turretPlan` 读
  5. ⚠ 两端发车各自 Tick 驱动 ⇒ 复刻可能比本端建车早 1~2 帧：`ReplaySpawn` 找不到车厢时**挂起重试**（`pendingTurrets` + `Update` 里 `FlushPendingTurrets`，超时 10s 丢弃并告警），否则炮塔会掉在轨道上不跟车
- ⚠ `SpawnSourceKey` **不能**用 `NetSpawnKey.Of(gameObject)`：任务根是 `MissionController` 的子物体（transform 位置都一样）、名字又只差 "(Clone)"
  ⇒ 多个任务的键会撞（复刻到别的任务里）。改用 `ActorsManager.DerivedNetId(nameof(MissionInterceptTrain) + "#" + netOrder, transform.position)`（`netOrder` 是任务跨端稳定键）
- ⚠ 炮塔预制体必须是**有 `Actor` 的单位**、且 `UnitTypeEnum` 不是 Player/Friend（那两个不取号 ⇒ `ReportUnit` 只告警不上报，成员端永远收不到）
- 验证：离线编译 00_Utils/06_Gameplay 0 错误；lints 干净
"""

io.open(daily, 'w', encoding='utf-8').write(text)

# ---------- ② topic：把可复用套路写进 net-battle.md ----------
topic = r'e:\Bluedivers\.codebuddy\memory\topics\net-battle.md'
t2 = io.open(topic, encoding='utf-8', errors='ignore').read() if os.path.exists(topic) else ''

t2 += """

### 7.2x ✅ 第 4 个生成器实现者 = 任务（车厢附加炮塔，2026-10-15）
> 场景：拦截火车任务的"车厢附加炮塔"——炮塔要挂在**会移动的车厢挂点**（`TrainEngineCar` 的 `Additional`）下，且必须联机同步。
- **口径**：`MissionInterceptTrain : MissionBase, INetSpawner`（⚠ 任务也能当生成器，与巢穴/召唤师/刷怪器同一条通道）。
  `InitMission` → `SpawnerRegistry.Register(this)`；`Uninit` → `Unregister`（成对；⚠ 复刻可能在 `Instantiate` 内就发生 ⇒ 不能放 `Start`）。
- **槽位**：`slot = 班次 × maxCarCount + 车厢序号`（两端算式一致）。**槽位里不放 prefab** —— "这一节挂哪门"从两端各持一份的**开局计划表**读
  （计划表在第一个 yield 之前用同一条 `BattleRandom` 流算好 ⇒ 两端抽签一致；发车时只查表、不再动随机流）。
- ⚠ **`SpawnSourceKey` 别照抄 `NetSpawnKey.Of(gameObject)`**：它退回 `DerivedNetId(名字 + 位置)`，而**任务根是 `MissionController` 的子物体**
  （transform 位置全都一样）、名字又只差 "(Clone)" ⇒ 多个任务互相撞键、复刻到别的任务里。任务类应改用
  `ActorsManager.DerivedNetId(类名 + "#" + MissionBase.netOrder, transform.position)`（`netOrder` 就是任务跨端稳定键）。
- ⚠ **`CreatUnit` 生成出来的物体固定挂 `ActorsManager` 下**（"生成者死了不能把单位一起带走"）⇒ 要挂到别的父节点（挂点/载具）**必须自己 re-parent**：
  `SetParent(mount, false)` + `localPosition = Vector3.zero` + `localRotation = identity`（两边一致，成员侧 `Instantiate(prefab, mount.position, mount.rotation, mount)` 天然同款局部值）。
- ⚠ **新形态：复刻早于"载体"出现**。两端各自 Tick 驱动建车厢，房主的 4052 可能比成员建车早 1~2 帧 ⇒ 成员在 `ReplaySpawn` 里找不到车厢时
  **不能直接失败**（那样炮塔掉在轨道上不跟车）：记进 `pendingTurrets`（带 NetId/落点/超时 10s），在 `Update` 里重试挂载；超时才丢弃 + `Warn`。
  ⚠ 复刻位置/朝向一律取**本端车厢挂点**，不要用消息里的 pos（那含 RTT 期间车厢前进的位移）。
- ⚠ 走这条通道的 prefab 必须**有 `Actor` 且 `UnitTypeEnum != Player/Friend`**（`Actor.Awake` 只给这两类之外的取号；没号时 `ReportUnit` 只告警不上报 ⇒ 成员端静默缺失）。
- 同类参考：`MissionInterceptTrain_Train.cs` 的 `#region 生成器（INetSpawner…）`（`SpawnSourceKey` / `ReplaySpawn` / `FlushPendingTurrets` / `SpawnTurret` / `MountTurret`），
  清理口径 = 随车厢一起静默移除（有 `I_AIController` 就 `Kill(true)`）。
"""

io.open(topic, 'w', encoding='utf-8').write(t2)
print('ok', len(text), len(t2))
