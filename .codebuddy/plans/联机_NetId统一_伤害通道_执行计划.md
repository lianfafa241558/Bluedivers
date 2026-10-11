# 联机 · NetId 统一 + 伤害通道统一 · 执行计划（交接文档）

> 用途：**开新对话直接照本执行**，不需要再回看历史讨论。
> 起源：2026-10-11/12 用户报告 4 个 bug（倒地救不起 / 火箭背包不同步 / 敌人 AI 不同步 / 炮塔疑似不同步）后，
> 逐步定下的"号统一 + 创建权威 + 伤害通道统一"方案。**第 1~6 项已完成并验证，从"待做"的第 4 步开始执行。**
> 配套记忆：`.codebuddy/memory/topics/net-battle.md §7.1`（本文件是它的展开版）。

---

## 0. 一句话目标

把"`NetId` = 两端各自第 N 个被创建"这条**隐式顺序约定**干掉，换成
**"号有明确来源（sid / 派生键 / 房主分配）"**，并把伤害改成**只有一个结算端 + 增量广播**。

判据（贯穿全篇）：

> **"这个单位两端能不能算出同一个出生坐标？"** 能 ⇒ **派生键**；不能 ⇒ **房主分配（4051）**。

---

## 1. 已完成（✅ 都已 Unity 编译通过 + 反射核验，**不要重做**）

### 1.1 倒地救起（bug ①）
- `Assets/Resources/Prefabs/BattleBase/PlayerFriend.prefab` 补 3 个组件：
  `Furniture_PlayerDown`（meetTime 3 / flags 260 / 三条音效与 `Player.prefab` 同一份 / desc「扶起同学」）、`EquipController`、`PlayerMountPoint`。
- `06Gameplay/Interactable/Furniture_PlayerDown.cs`：新增 `ReviveSelf()`（**幂等**：`GetHpCurrent() > 0` 直接返回，否则重复消息会把血压回 `MaxHealth/10`）、
  `ApplyRemoteDown(bool)`（幂等；盟友实例血量是**镜像直写**，不触发 `OnDie` ⇒ 必须显式驱动 `canOperate`）。
- `06Gameplay/Player/Controller/FriendController.cs`：`ApplyVital` 末尾调 `_downFurn.ApplyRemoteDown(down)`。
- **新命令号 4050 `PlayerReviveNtf`**（键 = **sid**，房主 = 0）：
  `07NetGame/CmdId.cs` + `Msg/RoomMsg.cs`（`PlayerReviveMsg`）+ `NetRoomFlow`（`OnPlayerRevive` / `SendPlayerRevive` / `HandlePlayerReviveNtf`：房主只 `SendToAll` 不自派发，本地应用在桥里）。
- **新桥 `09Manager/Global/NetPlayerStateBridge.cs`**（`[RuntimeInitializeOnLoadMethod]` 自举，与 `SceneUnitMoveSink` 同款）：
  `GlobalEventBus.OnFurnitureOperate` 里认"操作了盟友实例的 `Furniture_PlayerDown`" ⇒ `SendPlayerRevive(fc.NetSid)`；收到 4050 且 `sid == SelfSid` ⇒ 本机 `down.ReviveSelf()`。
- `09Manager/Battle/NetFurnitureBridge.cs`：**盟友实例身上的家具不走 4036**（键含位置量化，盟友出生点比本人少 0.2m(Y) ⇒ 必对不上）。

### 1.2 火箭背包（bug ②）
- `06Gameplay/Bag/BagBase.cs`：`OnInstall` 锚点改为"`PlayerMountPoint.BackPoint` → 玩家模型根 → **保持原父级**"（原来直接取 `m_PlayerCharacterController.ModleRoot`，盟友没有 `PlayerController` ⇒ NRE 打断整条远端重放）。
- `06Gameplay/Player/PlayerMountPoint.cs`：`BackPoint` 改**惰性按标签解析**（盟友走 `FriendController.AttachModel`，收不到 `OnBodySet`）。
- `06Gameplay/Bag/WeaponBag.cs`：`m_InputHandler == null` 时不走输入分支；`OnInstall/OnUninstall/OnDestroy` 订阅 `weapon.OnShoot` ⇒ `BattleEventBus.BagShoot(Owner.gameObject, 枪口朝向, UseDamageIndex)`。
- `06Gameplay/Events/BattleEventBus.cs`：`OnBagShoot` / `BagShoot(...)`。
- `09Manager/Global/NetPlayerStateBridge.cs`：`OnLocalBagShoot` ⇒ `flow.SendShoot(NetPlayerStateBridge.BagSlotIndex = 7, dir, default, damageIndex, 0)`（**虚拟槽位 7**，真实槽位只有 0~6）。
- `09Manager/Global/NetFriendBridge.cs`：`HandleShoot` 里 `slotIndex == NetPlayerStateBridge.BagSlotIndex` ⇒ `fc.PlayBagShoot(dir, damageIndex)`。
- `06Gameplay/Player/Controller/FriendController.cs`：`PlayBagShoot` 找盟友身上那件已装备的 `WeaponBag`。
- `06Gameplay/Player/Controller/FriendWeaponView.cs`：`PlayShoot` 的表现体抽成静态 `PlayVisual(WeaponBaseController w, dir, aimPoint, damageIndex, muzzleIndex)`（背包武器复用）。

### 1.3 伤害增量广播（用户口径，取代"房主权威结算"）
- `07NetGame/NetHostSvc.cs`：新增 `SendToAllExcept(exceptSid, msg)`（库侧只有 `SendToAll`/`SendTo(msg,sid)`，所以遍历 `_players` 逐个发并跳过来源）。
- `07NetGame/NetRoomFlow.cs`：`SendEnemyDamaged(int netId, int damage, uint exceptSid = 0u, int attackerNetId = 0)`；`exceptSid != 0` 用 `SendToAllExcept`，且把 `Sid` 填成**开枪者**（原来恒 0 = 房主）。
- `09Manager/Battle/EnemyNetBridge.cs`：`OnRemoteHit` 应用完**再转发给除来源外的成员**。
  👉 这同时修掉了"**成员 A 的伤害从不到达成员 B**"（原来房主应用后 `ApplyingRemoteDamage` 把回传也挡了；≥3 人必现，2 人测不出来）。

### 1.4 `AttackerNetId`（批次 1，**破坏性协议变更**）
- `Msg/RoomMsg.cs`：`EnemyHitMsg` / `EnemyDamagedMsg` 各加 `[Key(3)] public int AttackerNetId;`（末尾追加 ⇒ 旧端 `Skip()` 掉，不崩）。
- `06Gameplay/Events/BattleEventBus.cs`：`OnEnemyHit` / `OnEnemyDamaged` 改成 `Action<int,int,int>`（netId / damage / attackerNetId，包装方法带默认值）。
- `06Gameplay/AI/Controller/EnemyController.cs`：`_OnDamaged` 里 `damageSource.GetComponent<Actor>().NetId` 一起发出。
- `07NetGame/NetRoomFlow.cs`：`OnEnemyHitUp` / `OnEnemyDamaged` 改 `Action<uint,int,int,int>`；`SendEnemyHit/SendEnemyDamaged` 加参数；handler 透传。
- `09Manager/Battle/EnemyNetBridge.cs`：`ApplyRemoteDamage(sid, netId, damage, attackerNetId = 0)` 的**来源三级解析**：
  ① `FindActor(attackerNetId)`（怪/炮塔/凯伊）→ ② `ResolveRemoteUser(sid)`（玩家）→ ③ `null`（NoSource）。

### 1.5 号段化（第 1 步）
- `05UnitCore/Actor/ActorsManager.cs`：`SpecNetIdBase = 1000000` + `_specUnitNetIdSeq` + `NextSpecUnitNetId()`（`Awake` 归零）。
- `09Manager/Battle/EnemyNetBridge.cs`：`FindActor` **统一只查 `ActorsManager.Actors`**（号段不相交 ⇒ 一张表按号查无歧义；顺带绕开 `Enemys` 跨局未复位的老坑）；增订 `UnitEventBus.OnSpecUnitCreate/OnSpecUnitDead`。
- `05UnitCore/Actor/Actor.cs`：`SpecUnit && !ActorFlag.Unimportant` 才发号（`Unimportant` 连 `SpecUnits` 都不注册，且多为背景/无敌物，如 `Turret_Mission_Machine` flag 177）。

### 1.6 派生键（第 2、3 步）
- `ActorsManager.cs`：`DerivedNetIdBase = 0x4000_0000`；`DerivedNetId(string id, Vector3 pos)` = `FNV1a(Id + 位置0.1m量化 + 同Id同位置序号)`；私有 `Fnv1a`；`ForceDerivedNetId`（静态开关）。
- `Actor.Awake` 取号判据：
  ```csharp
  if (ActorsManager.Instance != null && type != UnitTypeEnum.Player && type != UnitTypeEnum.Friend)
  {
      if (ActorsManager.ForceDerivedNetId || !FPSGame.Data.BattleState.IsStartBattle)
          NetId = ActorsManager.DerivedNetId(Id, transform.position);   // 场景摆放 / 任务实体 / 大厅
      else if (type == UnitTypeEnum.Enemy) NetId = ActorsManager.NextEnemyNetId();          // 运行时生成
      else if (type == UnitTypeEnum.SpecUnit && !HasFlag(ActorFlag.Unimportant)) NetId = ActorsManager.NextSpecUnitNetId();
  }
  ```
- `06Gameplay/Mission/MissionBase.cs` (`InitMission`)：`try { ActorsManager.ForceDerivedNetId = true; Instantiate(...) } finally { = false }`
  ⇒ 任务实体里的 `Actor` 在各自 `Awake` 取派生号（`Instantiate` 同步跑完子树所有 `Awake` 才返回）。
  ⚠ `MissionBase` 的命名空间是 **`FPSGame.Mission`**（不是 `Gameplay`）。
- 派生序号能成立的前提：**每个 `Actor` 是自己 `Awake` 结束才 Add 进 `Actors`** ⇒ 同组逐个数的序号唯一且两端一致（不要改成"先全部 Add 再算"）。

---

## 2. 待做（从这里开始）

> ✅ **2026-10-12 全部落地（第 4~8 步）**，本文件的 4~8 步已按顺序执行完：4~7 步走"统一入口 + 4051 + 三处闸门"，第 8 步把 `Other` 可破坏物并进统一伤害通道
> （保留 4043 兜底）。⚠ **实装与本文档有 3 处偏差**（另开房主分配号段 `0x2000_0000`、来源键改为 `Actor.NetId` 优先、入口多一个 `target` 参数）——
> 差异与理由记在 `.codebuddy/memory/topics/net-battle.md §7.2a`，**不要再按本文档第 4~7 步重做**。
> 剩余未做：`HitPartIndex`（§7 小项）。（§1.6 备注的"任务实体取号"已由 §1.6 的 `MissionBase.InitMission` try/finally + `ForceDerivedNetId` 落地解决。）

### 第 4 步 · `WaveManager.CreatUnit` 统一创建入口
**先读**（不要凭猜）：`WaveManager.CreatUnit` 全文、`ZergWave.TickOngoing`、`RobotWave`、`CreatPatrol`/`CreatPatrolRemote`，
确认：① 内部有没有"非派生随机"（装备/等级/落点抖动）；② 现有成员闸门长什么样（`online && !flow.IsHost` 的写法）。

改法：
- 加一个 prefab 级重载，作为**所有"运行时生成"的唯一入口**：
  ```csharp
  // source = 生成者（nest/summoner/CreatEnemy 组件本身），slot = 生成者在自己的表里的下标
  public GameObject CreatUnit(GameObject prefab, Vector3 pos, Quaternion rot, GameObject source, int slot)
  ```
  内部：**房主**（或单机）⇒ `Instantiate` + 让 `Actor.Awake` 正常发号 + 上报 4051；**成员** ⇒ 直接 `return null`（不生成，等 4051）。
- 波次（`ZergWave`/`RobotWave`）**不改**：它们已有 4023 整批复刻，保持走"自增号"（两端同序）。

### 第 5 步 · 新消息 **4051 `EnemySpawnNtf`**（破坏性协议变更）
- `CmdId.cs`：`public const int EnemySpawnNtf = 4051;`
- `Msg/*.cs`：
  ```csharp
  [MessagePackObject]
  public class EnemySpawnMsg
  {
      [Key(0)] public int SourceKey;   // FNV1a(生成者 Actor.Id + 量化位置) —— 成员用它找"自己那份生成器"
      [Key(1)] public int Slot;        // 生成者表里的下标（nest: creatData[i] / summoner: summoned[i] / CreatEnemy: (int)tier）
      [Key(2)] public int NetId;       // 房主分配的号 —— 成员复刻后**覆盖**
      [Key(3)] public float X; [Key(4)] public float Y; [Key(5)] public float Z;
      [Key(6)] public float Yaw;
      [Key(7)] public float Tx; [Key(8)] public float Ty; [Key(9)] public float Tz;   // 首帧目标点（可全 0 = 无）
  }
  ```
- `NetRoomFlow`：`SendEnemySpawn(msg)`（房主 `SendToAll`）+ `HandleEnemySpawnNtf`（成员）+ `static event Action<EnemySpawnMsg> OnEnemySpawn`（注册/注销照抄 `EnemyDamagedSync` 的写法）。

### 第 6 步 · 桥 `09Manager/Battle/NetEnemySpawnBridge.cs`
- `Install/Uninstall`（`WaveManager` 里与 `EnemyNetBridge` 同批）：
  - `BattleEventBus.OnOpenSpawn`（**新增**，见第 7 步）→ 房主上报 4051；
  - `NetRoomFlow.OnEnemySpawn` → 成员复刻。
- **来源键**：`FNV1a(生成者 Actor.Id + 量化位置)`（与 `Furniture_Attached.SyncId` 同口径）—— 生成者是**两端都有的场景/任务物体**，所以成员能凭它找到"自己那份生成器"。
- 成员复刻：在 `ActorsManager.Actors` 里按 `SourceKey` 找到生成者组件 → 调它的 `ReplaySpawn(slot, netId, pos, yaw, target)`：
  ```csharp
  // 各生成器自己实现：用**自己那份** prefab 引用复刻，再覆盖号
  public void ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target)
  {
      var go = Instantiate(表[slot]的prefab, pos, rot, transform.parent);
      go.GetComponent<Actor>().NetId = netId;                      // ★ 必须早于下一行
      var ec = go.GetComponent<EnemyController>();
      if (ec != null && target.sqrMagnitude > 0.0001f) ec.ApplyRemoteDestination(target);
  }
  ```
  ⚠ **覆盖号必须早于 `SetNavDestination`/`ApplyRemoteDestination`**（同帧就要用到号）。
  ⚠ 复刻期间 **`Actor.Awake` 不能自增**（见"红线"）。

### 第 7 步 · 三处闸门 + 上报（各自的 `ReplaySpawn`）
| 文件 | 现在的问题 | 改法 |
|---|---|---|
| `06Gameplay/AI/StateMachine/EnemyNestBuild.cs` | `ActiveBehavior` 裸 `Instantiate`（:58）；`RandomUtils.RandomTake(creatData)`（:56，**全局流**）；Idle↔Active 由本地检测驱动（`:93-100`） | 走第 4 步统一入口 + 上报；`RandomTake` → `EnemyRandom.For(this).Take(...)`；加 `ReplaySpawn(slot,…)` |
| `06Gameplay/AI/Skill/UnitSkill_Summoner.cs` | `SkillStart` 裸 `Instantiate`（:23） | 同上（slot = `summoned` 下标） |
| `10_Effect/CreatEnemy.cs` | `Update` 里 `BattleManager.Instance.BattleRandom.Bool(probability)`（:83，**共享流**） | 房主才抽随机并生成 + 上报（slot = `(int)tier`）；成员完全不跑 |

- 新增 `BattleEventBus.OnOpenSpawn(GameObject source, int slot, GameObject spawned)` + `OpenSpawn(...)`（06 抛、09 桥收）。
- ⚠ 场景摆放 / 任务实体**不要**走这条（它们已有派生号，走这条会变成"欠补发全量"）。

### 第 8 步 · 可选：`Other` 可破坏物并入统一伤害事件
- 现状：`NetDestructibleBridge`（4043）只传"死了"，键 = `FNV1a(Actor.Id + 位置0.1m)`，房主侧有 `_destroyed` 账本给晚入房补发。
- 目标：受害者号改成**派生号**（`Other` 在战斗前创建 ⇒ 已走 `DerivedNetId`），把伤害/死亡统一走 `EnemyDamagedMsg` 那条通道；保留"只传死了 + 账本补发"作为兜底。

---

## 3. 红线（踩了就是回退，别改）

1. **`SendToAllExcept` 不能用 `SendToAll` 代替** —— 来源端本地已经扣过一次，发回去就是双扣。
2. **接收端一律 `DamagePacket.Resolved = true`**（跳过 AP/护甲/爆炸抗性重算）—— 否则同一发两端算出的数不同。
3. **玩家血量不走这条通道**：玩家自己是权威，`PlayerVital`(4017/4018) 只做表现；这条只管"打世界单位（怪/炮塔）"。
4. **不要动 `_enemyNetIdSeq` 的语义**：波次/巡逻仍在用（它们两端同序，是"还没被取代"的那批）。
5. **不要用 `ForceDerivedNetId` 当"正在复刻"的门**（语义不同，会把成员复刻出来的单位也算成派生号）。复刻那条要**单独的静态门**（如 `ActorsManager.Replicating`），或在 `Instantiate` 后立刻覆盖 `NetId` 并保证 `Awake` 期间自增也能被覆盖掉。
6. **派生序号依赖"Awake 结束才 Add 进 Actors"**，别改成先 Add 再算号。
7. 每个 `ReplaySpawn` 里 **`NetId` 覆盖必须早于任何用到号的动作**（`SetNavDestination` / 加入 `SpecUnits` 之后的查询）。

---

## 4. 验证 SOP（每落一段就做一遍）

```
1) refresh_unity  { "mode":"force", "scope":"all", "compile":"request" }
   → 返回 "editor is ready"；再读 mcpforunity://editor/state 确认 is_compiling=false、external_changes_dirty=false
2) read_console   { "action":"get", "types":["error"], "count":"25", "format":"detailed" }
   → 必须 0 条
3) execute_code 反射核验（**必做**：Console 0 error 有"假空转/假缺失 meta"两种假象）
```
通用反射片段（把类型/成员换成这一批新增的）：
```csharp
System.Func<string,System.Type> F = n => {
    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies()) {
        var t = a.GetType(n); if (t != null) return t;
    } return null; };
var sb = new System.Text.StringBuilder();
// ⚠ 先确认真实命名空间（猜错会得到假 False）：MissionBase 在 FPSGame.Mission，NetHostSvc 不在 FPSGame.Managers
var t = F("真实命名空间.类型名");
sb.Append("成员=" + (t != null && t.GetMethod("方法名") != null));
return sb.ToString();
```
> ⚠ 类型名拿不准时，先扫一遍：`foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) if (a.GetType("命名空间.类型") != null) ...`
> —— 本 session 就因为猜错命名空间得到过两次假 `False`（`NetHostSvc`、`MissionBase`）。

## 5. 实测观察点（`NetSyncLog` 两端都开）

| 验什么 | 日志 tag |
|---|---|
| 倒地能被扶起（每端只应一次 `收`） | `救人·发` / `救人·收` |
| 背包捡起 + 开火 | `背包开火·发` / `远端开火`（名字应是背包那把武器） |
| 成员 A→成员 B 伤害（≥3 人） | `命中上报·发`（含 `攻击者号=`）/ `伤害下发·发` |
| 仇恨来源是否重建 | `远端伤害落地`（来源不该恒为 `<没解析到>` 或错算成玩家） |
| 4051 复刻 | 新增 `生成·发` / `生成·收`（建议照 `家具/操作·发|收` 的写法，**失败必须 Warn + 打来源键表**） |
| 号是否还错位 | `[异常/敌人移动·放弃]`、`[异常/远端伤害落地] 找不到 netId` 应显著减少/消失 |

## 6. 决策记录（用户已拍板，新对话不要再问）

1. 号空间**保留 `int`**、**号段分离**（`sid` / 派生 `0x4000_0000` / SpecUnit `1000000` / 敌人自增 `1..`），
   `FindActor` **统一查 `Actors`**；**不做** `long`/复合键。
2. **场景/任务实体 → 派生键**（两端各算、零消息、不补发全量）；**运行时生成 → 房主分配（4051）**；玩家/盟友 → `sid`。
3. 伤害 = **来源端先扣 → 上报房主 → 房主 `SendToAllExcept(来源)` 转发**（**不是**房主权威结算，来源端零延迟）。
4. **只做 3a**：传伤害 + `HitPartIndex`（**`HitPartIndex` 还没做**，见下）+ `AttackerNetId`；**不做**全量部位快照（不支持重连/中途入房）。
5. `Damageable` 每部位自带 `remainArmor`/破甲效果 ⇒ **不能**用"单条血量快照"代替伤害通道。
6. `Unimportant` 的 SpecUnit 不参与同步（背景物/无敌物）。
7. 可选：`Other` 可破坏物并入统一伤害事件。

## 7. 顺带未做的小项

- **`HitPartIndex`**（部位下标 = `Actor.Damageables[]` 的稳定顺序）：接收端目前仍用自己的 `ResolveHitCollider` 挑部位 ⇒
  极端情况下装甲/破甲结算与开枪端略有差异。做法：`_OnDamaged` 里 `System.Array.IndexOf(actor.Damageables, collider.GetComponent<Damageable>())` 随消息发出，
  `ApplyRemoteDamage` 用它取 `damageAffected`（取不到就退回现有逻辑）。
- `Enemys` 在 `ActorsManager.Awake` **漏了复位**（只 new 了 `Actors/Players/SpecUnits`）—— 目前已靠"统一查 `Actors`"绕过；
  若还有别处读 `Enemys`，顺手补上复位。
