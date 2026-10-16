# -*- coding: utf-8 -*-
"""2026-10-16：拦截火车·火车卫队不跟随 的定位与修复（daily 新建 + topic 追加）。"""
import io, os

d = r'e:\Bluedivers\.codebuddy\memory'
daily = os.path.join(d, '2026-10-16.md')

text = io.open(daily, encoding='utf-8', errors='ignore').read() if os.path.exists(daily) else '# 2026-10-16\n'
if text.strip() == '':
    text = '# 2026-10-16\n'

text += """
## 拦截火车：火车卫队"不跟着"的定位与修复（MCP 实测数据驱动）
- 现象：卫队生了但完全不跟车
- **取数方式（可复用）**：`unity-mcp` → 钉 `Bluedivers@3d9f2357` → `execute_code`（只读反射）把
  `MissionInterceptTrain.liveTrains`（私有）里的每趟车 + `Guards` 列表 + 每只卫队的
  `EnemyController.PatrolPos` / `m_lastDestination` / `NavMeshAgent(isOnNavMesh/hasPath/velocity/remainingDistance/speed)` /
  `aiState` 一次性 dump 出来。比读日志猜快得多（`read_console` 每条都带一大坨堆栈、噪声极大）
- **实测**：火车 `unitDist=483`、引擎 (513,8.5,238)；10 只 `PMC_Missile` 全挤在出生点 (133~140,567~579) ⇒ `dTrain≈500`；
  它们的 `patrol`/`lastDest` **确实是火车当前位置**（引导这条杠杆是通的、寻路也发了），但
  **`agentSpeed=2.0` / `vel=2.0`**，而火车 `trainSpeed=8` ⇒ 60s 拉开 ~360m ＋出生 25m ≈ 500m，完全吻合
- **根因**：只挪 `PatrolPos` 不够 —— 卫队是普通巡逻兵，速度 2 m/s 永远追不上 8 m/s 的火车
- **修复（`MissionInterceptTrain_Guard.cs` + `_Train.Update`）**：① 补速度：`Speed.AddModifier(Extra, want-current)`
  （Extra 是累加 ⇒ 想写绝对值必须补差）补到 `trainSpeed × 系数`；跟住距离内**系数=1**（等速）、超出才 1.5× 追赶；
  ② 目标点带 `guardLead=20m` 前导；③ 速度**按帧**兜（`RefreshGuardSpeeds` 放 `Update`，不碰寻路），
  目标点仍留 3s Tick（碰寻路）
- ⚠ 两条硬约束（都是 `EnemyMobile` 的语义）：
  - `Patrol` 是"**到点即自毁**"的哨兵语义（`PatrolBehavior → UpdatePathDestination()` 命中 2m 内 ⇒ `Kill(true)`）
    ⇒ 速度**不能超过目标点**，否则追上就自杀；`PatrolPos` 3 秒才刷一次（车已前进 24m）⇒ 目标必须带前导量，
    否则等速卫队会先走到"过期点"上被判定到达
  - AI 在 `Patrol/Beware/Return` 切换时会自己增删速度修饰（实测 Patrol=2、Return=3）⇒ 只在 Tick 里补会留下
    最长 3 秒龟速窗口
- 验证：离线编译 `00_Utils`/`06_Gameplay` 0 错误、lints 干净；**待用户重进 Play 复测**（预期：卫队稳定跟在车后 ~35m）
"""

io.open(daily, 'w', encoding='utf-8').write(text)

topic = os.path.join(d, 'topics', 'mission-intercept-train.md')
t = io.open(topic, encoding='utf-8', errors='ignore').read()
t = t.split('## 8. 卫队跟随')[0].rstrip() + '\n'

t += """

## 8. 卫队跟随（2026-10-16 实测口径）
- **只挪 `PatrolPos` 不够**：卫队是普通巡逻兵，`PatrolSpeed` ≈ **2 m/s**，火车 `trainSpeed` = 8 m/s ⇒ 发车 60 秒
  就拉开 ~500m（实测 `dTrain≈500`、`agentSpeed=2.0`）。必须**同时补速度**：
  `controller.Speed.AddModifier(ModifierType.Extra, want - current.RawInt)`（⚠ Extra 是累加的，写绝对值要补差）
  —— 跟住距离内补到**等速（×1）**，只有被落下（超出跟住距离）才用 1.5× 追赶。
- ⚠ **速度不能超过目标点**：`Patrol` 是"到点即自毁"的哨兵语义（`EnemyMobile.PatrolBehavior → UpdatePathDestination()`
  命中 `PathReachingRadius=2m` ⇒ `Kill(true)`）。且 `PatrolPos` 每 3 秒才刷新（车已前进 ~24m）⇒
  目标点必须带 **前导量**（`guardLead=20m`），否则等速卫队会先走到"过期点"上被判到达而自毁。
- ⚠ **速度要按帧兜、目标点走 Tick**：AI 在 `Patrol/Beware/Return` 切换时会自己增删速度修饰（实测 Patrol=2、Return=3），
  只在 3 秒 Tick 里补会留下最长 3 秒龟速窗口；而按帧改 `PatrolPos` 会给 `PathRequestManager` 加压 ⇒ 两者分开。
- ⚠ 引导必须与发车**同帧**（`Tick` 里 `DepartTrain()` 之后立刻 `RefreshAllGuards()`）：晚一个 Tick 就被
  `EnemyMobile.Start` 判成"无巡逻点"进 `Idle`，而 `IdleBehavior` 不查 `PatrolPos` ⇒ 那只卫队**永远站着**。
- 取数手法：`execute_code` 反射 dump `MissionInterceptTrain.liveTrains → TrainRuntime.Guards` +
  每只的 `PatrolPos/m_lastDestination/aiState/NavMeshAgent(speed,velocity,hasPath,remainingDistance,isOnNavMesh)`
  （`read_console` 噪声太大，一条日志带一大坨堆栈）。
"""

io.open(topic, 'w', encoding='utf-8').write(t)
print('daily', len(text), 'topic', len(t))
