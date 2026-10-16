# -*- coding: utf-8 -*-
"""2026-10-16：卫队改为"车头两侧护航编队"（daily 追加 + topic §8 改写）。"""
import io, os

d = r'e:\Bluedivers\.codebuddy\memory'

daily = os.path.join(d, '2026-10-16.md')
text = io.open(daily, encoding='utf-8', errors='ignore').read() if os.path.exists(daily) else '# 2026-10-16\n'
text += """
## 卫队改"车头两侧护航编队"（用户口径：不要排一列跟在后面 35m）
- 硬约束（必须先讲清）：`Patrol` = **到点即自毁**，而 `PatrolPos` 每 `TickTime`=3s 才刷新、中间车已前进 24m
  ⇒ **不可能"停在车头旁边不动"**（贴到 2m 判定就 `Kill(true)`）
- 做法：护航位 = 车头 + 轨道前向 × `guardLead`(24 = trainSpeed×TickTime) + 右向 × `guardLateral`(10)×±1；
  同侧再按 `guardLineSpacing`(8m) 沿轨道错开（`index>>1`）⇒ 10 只时左右各 5 槽（沿 +24/+16/+8/0/−8）
- 速度（按帧兜，`MatchGuardSpeed`，阈值相对**自己的护航位**）：> 1.5×`guardHoldDistance` ⇒ 1.35× 追；
  附近 ⇒ 1.0×（与车等速）；< 0.6×`guardHoldDistance` ⇒ 0.9× 主动落回（避开 2m 自毁判定）
- 平衡站位：卫队悬停在护航位后方 `guardHoldDistance`(15m) ≈ 车轮垂直 → 实际落在**车头两侧 ±10m、纵向 −23~+9m** 的编队
- 新增旋钮：`护航位前导(24)` / `护航位侧向(10)` / `护航位错开间距(8)` / `离护航位保持距离(15)` / `追赶速度系数(1.35)`；
  旧 `卫队跟住距离(35)` 已删（语义换成"相对护航位"的保持距离）
- ⚠ `guardHoldDistance` 别调到 <8：3s 刷新 + 24m 步长会让卫队踩进 2m 到达判定而自毁
- 验证：离线编译 0 错误、lints 干净；待用户重进 Play 复测
"""

io.open(daily, 'w', encoding='utf-8').write(text)

topic = os.path.join(d, 'topics', 'mission-intercept-train.md')
t = io.open(topic, encoding='utf-8', errors='ignore').read()
t = t.split('## 8. 卫队跟随')[0].rstrip() + '\n'

t += """
## 8. 卫队护航（2026-10-16 实测口径）
- **只挪 `PatrolPos` 不够**：卫队是普通巡逻兵，`PatrolSpeed` ≈ **2 m/s**，火车 `trainSpeed` = 8 ⇒ 发车 60 秒就拉开
  ~500m（实测 `dTrain≈500`、`agentSpeed=2.0`）。必须**同时补速度**：`Speed.AddModifier(Extra, want − 当前FinalValue)`
  （⚠ Extra 是累加的，写绝对值要补差，这样 `Patrol` 进出增删修饰后下次刷新会自纠）。
- ⚠⚠ **不能把目标点钉在车/车头位置上**：`Patrol` 是"到点即自毁"的哨兵语义
  （`EnemyMobile.PatrolBehavior → UpdatePathDestination()` 命中 `PathReachingRadius`=2m ⇒ `Kill(true)`），
  而 `PatrolPos` 每 `TickTime`(3s) 才刷新、中间车已前进 `trainSpeed×TickTime`=24m ⇒ 目标压在车上时，
  补到等速的卫队会先走到"过期点"上把自己干掉。**目标必须带前导量**（默认 24m ≈ 一个 Tick 的行程）。
- **编队口径**：护航位 = 车头 + 前向×`guardLead`(24) + 右向×`guardLateral`(10)×±1（左右分边）
  + 同侧沿轨道错开 `guardLineSpacing`(8m)×(index>>1)；整队平移到车头两侧，纵向从 +9 到 −23m。
- **速度三段**（阈值相对**自己的护航位**）：> 1.5×`guardHoldDistance` ⇒ `guardCatchUpSpeedRatio`(1.35) 追；
  附近 ⇒ 1.0×（等速跟住，不再靠近）；< 0.6×`guardHoldDistance` ⇒ 0.9× 主动落回。
  ⇒ 实际站位 = 悬停在护航位后方 `guardHoldDistance`(15m) 附近。⚠ `guardHoldDistance` 别 < 8（会被 2m 判定吃掉）。
- ⚠ **速度按帧兜、目标点走 Tick**：AI 在 `Patrol/Beware/Return` 切换时会自己增删速度修饰（实测 Patrol=2、Return=3），
  只在 3 秒 Tick 里补会留下最长 3 秒龟速窗口；而按帧改 `PatrolPos` 会给 `PathRequestManager` 加压 ⇒ 两者分开。
- ⚠ 引导必须与发车**同帧**（`Tick` 里 `DepartTrain()` 之后立刻 `RefreshAllGuards()`）：晚一个 Tick 就被
  `EnemyMobile.Start` 判成"无巡逻点"进 `Idle`，而 `IdleBehavior` 不查 `PatrolPos` ⇒ 那只卫队**永远站着**。
- 取数手法：`execute_code` 反射 dump `MissionInterceptTrain.liveTrains → TrainRuntime.Guards` +
  每只的 `PatrolPos/m_lastDestination/aiState/NavMeshAgent(speed,velocity,hasPath,remainingDistance,isOnNavMesh)`。
"""

io.open(topic, 'w', encoding='utf-8').write(t)
print('ok', len(text), len(t))
