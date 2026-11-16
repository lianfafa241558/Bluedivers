# 虚拟碰撞体（VirtualCollider）· 命中部位数学化

> 目标：把「子弹打中哪个肢体」从 PhysX 交给自研数学，从而让 1491 个肢体 collider 下线、省掉
> `Physics.SyncColliderTransform`。2026-11-16 完成 Step 1（原型 + 基准），**未接管主流程**。

## 一、方案定性（用户提的，已拍板"先做 Step 1"）

子弹的**单位命中**完全走数学，地形/墙/护盾/道具仍走 Physics：

1. `UnitQuery` 粗筛出"可能被扫到的单位"；
2. 逐肢体做「扫掠胶囊 vs 胶囊」求交；
3. **一个肢体都没命中 ⇒ 视为穿过**（这条是方案能成立的关键：主 collider 不参与命中，
   所以不会出现"打空气也掉血"的回归）。

## 二、原型文件（Step 1，已编译通过）

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/05UnitCore/VirtualCollision/VirtualCollider.cs` | 单肢体代理（胶囊轴 + 跟随 Transform + 部位下标），`TryBake(Collider)` |
| `.../VirtualColliderMath.cs` | 线段-线段最近点、胶囊×胶囊、线段×球（**全裸 float**） |
| `.../VirtualUnitCollider.cs` | 单位级集合：Bake / UpdateWorld / TryHit / OverlapsSegment |
| `Assets/Editor/VirtualColliderBenchWindow.cs` | 基准窗口（菜单 `Tools/虚拟碰撞体基准测试`），另有 `RunHeadless(...)` 供 MCP 调用 |

- 程序集：前三个在 `05_UnitCore`（与 `Damageable`/`Actor`/`UnitQuery` 同层），06 可直接引用。
- `Bake` **编辑态可用**：`Actor.Damageables` 是 `Awake` 采的，为空时退化成
  `GetComponentsInChildren<Damageable>()`（与 Awake **同序** ⇒ 部位下标跨端仍稳定）。
- 护甲片（`TransferDamageable`）通过 `td.source` 归到主肢体；编辑态 `source` 为空 ⇒ 该片被跳过
  （所以编辑态跑出来的"仅物理命中"偏高，真实数据要进 Play）。

## 三、实测数据（2026-11-16，编辑器 Mono，最重的 12 种敌人 prefab）

| 项 | 数值 |
|---|---|
| 单位 12 个 / 肢体代理 472 个（avg 39.3 / max 48） | 一次性 Bake ≈ 3~8 ms |
| 单次胶囊×胶囊 | **0.102 µs**（改裸 float 前是 0.363 µs） |
| 单次线段×球（部位级早退） | **0.053 µs** |
| 物理 `SphereCastNonAlloc`（UnitLayers） | **1.9~2.3 µs/条** |
| 数学（求交 + 懒刷新） | **4.8~5.1 µs/条** |
| 全量刷新世界坐标 | 13~15 µs/单位（**0.33~0.39 µs/肢体**） |
| 部位一致率 | **92.4~92.6%** |
| 同单位·部位不一致 | **6.7%** |
| 只有物理命中（数学判穿） | 0.7~0.9% |
| 只有数学命中（幽灵命中） | 0~0.1% |
| 每帧 30 发：现状 0.896 ms → 虚拟化 **0.297 ms（+0.60 ms）** | 每帧 90 发：1.003 → 0.590（+0.41） |

## 四、结论与判据

- ⭐ **单条查询数学比物理贵 2.3 倍**；收益**全部来自 `SyncColliderTransform` 消失（外部实测 0.83ms）**。
  所以"肢体 collider 是否真的全删"决定这个方案值不值。
- ⭐ **必须懒刷新**：`UpdateWorld` 全量刷 250 单位 × 16 肢体 ≈ 1.3ms，**比省下的 0.83ms 还贵**。
  只刷"本帧真被子弹扫到的单位"（帧内共享，用 stamp 数组）才为正。
- ⚠ 净收益对"每帧被子弹扫到的**不同**单位数"极敏感：单位越密越亏。
- ⚠ 6.7% 的部位不一致来自：盒/网格 collider 被近似成胶囊、以及 `t` 用的是**最近点**而非解析入射点
  （要做准就得换解析 entry-t）。
- ⚠ 物理侧计时按"预先建好的 collider→部位表"，生产链路每次命中还要 `GetComponent`，
  所以这里的物理成本偏低一点，真实收益略高。

## 五、Step 2 之前必须处理（未做）

1. `Damageable.ExplosionBlocking`（`Damageable.cs:244-276`）逐肢体对**自己**做 Raycast、要求
   `hit.collider == item` ⇒ 肢体 collider 一旦下线 ⇒ **永远返回 false ⇒ 爆炸只打主体**。
2. `Damageable.ClosestCollider` / `FpsHelper_Hit.PickNearestPart` 用 `ClosestPointOnBounds`。
3. `Damageable.cs:552` 护甲破坏定位用 `m_colliders[0].bounds.center`。
4. `Actor.IndexOfDamageable(Collider)` → 数学版可直接输出部位下标（联机反而更简单）。
5. 护盾 / `IgnoreHitDetection` / 触发器 / `LineArmor` / 玩家 `CharacterController` 的真 collider **必须保留**。

## 六、避坑

- ⚠ 别在每帧调 `Transform.lossyScale`（矩阵分解，很贵）⇒ 半径缩放烘焙时定住（`RadiusScale`）。
- ⚠ 别用 `Vector3.Dot` / `Mathf.Clamp01`：**跨程序集调不进来**，单次求交会从 0.10µs 涨到 0.36µs。
- ⚠ 射线长度要用**单帧位移**（`ProjectileStandard.TryHit` 的 `m_LastRootPosition → Tip`，1~2m），
  不是"射到目标的 40m 长线"——后者会让粗筛一次放进来十几个单位，测出来的数全是假的。
- ⚠ 基准窗口的临时场景用 `EditorSceneManager.NewScene(Additive)`；**活动场景若是未命名空场景会直接报错**

  （"Cannot create a new scene additively with an untitled scene unsaved"）⇒ 先把活动场景设回 `GameRoot`。
  ⚠ 域重载会把未保存的附加场景丢掉，所以"搭场景 + 跑基准"要放在**同一次** `execute_code` 里。

## 七、真实战斗实测（2026-11-16 晚，Play，BattleScene / 227 Actor）

- 本局构成：**227 Actor / 226 有部位 / 296 个肢体碰撞体**；分布 `1肢体×185 / 2×23 / 3×12 / 4×6`
  ⇒ **这局几乎全是单部位单位，肢体 collider 没什么可省的**（目标场景是 17:2x 那种 `avg 16.4 / max 56`）。
- 💀 **`UnitQuery` 不能当子弹级粗筛**（本局 227 单位实测）：

  | 查询 | 返回 | 单次 |
  |---|---|---|
  | 圆半径 0.5m | 1 | **6.83 µs** |
  | 圆 2m | 3 | 8.20 µs |
  | 圆 5m | 10 | 22.07 µs |
  | 圆 30m | 70 | 31.27 µs |
  | 全表 | 225 | 46.17 µs |

  ⭐ 0.5m 圆也要 6.8µs ⇒ 成本由「**所在 30m 格里的单位数**」决定，不是查询半径（`GatherOverlaps` 会把整格单位全过一遍）。
  每颗子弹每帧调一次 = 18 发 × 22µs ≈ **0.4ms/帧**，比省下的还多。
- ⭐ **物理 SphereCast 在本局只要 0.73 µs/次**（227 单位 / 329 collider）⇒ C# 侧任何粗筛都比它贵 10~30 倍。
  **结论：粗筛这一步不要自己造，PhysX 的 broadphase 已经是最优解。**
- ✅ **更正（同日，我先前判错）**：幽灵命中 52% 是**基准窗口自己的 mask 用错**，不是游戏问题。
  `GameRoot.prefab` 实测 `groundLayers = 9`（bit0 **Default** + bit3 Ground）、`unitLayers = 64`（bit6）
  ⇒ `HittableLayers = 0x49 = Default|Ground|Unit`，**挂在 Default 层的肢体本来就打得到**。
  基准窗口原来拿 `UnitLayers(0x40)` 当命中层 ⇒ 一半肢体被当成"物理看不见"⇒ 假幽灵命中。
  改成 `LayerDefinition.HittableLayers` 后：**2/2000 = 0.1%**。
  ⚠ 教训：算"命中集"必须用 **HittableLayers**，`UnitLayers` 只是"单位在哪一层"，两者不等价。
- `Bake(actor, layerMask)` 仍保留 `enabled` / `activeInHierarchy` / 层三重过滤（这两项是真该滤的）。
- 本局每帧在飞子弹 = **18 发**（`ProjectileStandard` 计数）。
- ⇒ 该方案在"单位多但部件少"的场景里**净亏**；只有在"部件多"的重型波次里才可能小赚。
  真正可行的形态改为：**保留 1~4 个粗胶囊给物理命中 + 命中后用数学解析部位**（首轮评估的方案 A/B 混合）。
