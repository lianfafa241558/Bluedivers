# 主题 · 联机 · 生成器复刻实例（房主唯一创建 + 4052）

> 由 `net-battle.md` 拆出（2026-10-18），含原 §7.2f 上半（生成侧复核）+ §7.2x（生成器第 4 例 = 任务车厢炮塔）。
> 号空间与 `CreatUnit` 签名见 `net-netid.md`；拦截火车全貌见 `mission-intercept-train.md`。
> ⚠ 原 §7.2f 下半（两族合并 / 伤害落地 / 验证）已拆到 `net-damage.md`。

### 7.2f 复核结论（2026-10-12）
- ✅ **玩家部署物（地雷/哨戒炮）不是缺口**：全部由 `10_Effect/VFX/VFXAirdropEffect.cs` 创建 —— 远端走 `TmpAirdrop`（`BattleManager.ReleaseAirdrop` + `SetCallerSid`），
  本机走 `SetOwner` 抛 `BattleEventBus.Airdrop` ⇒ 上报 4027 / 房主 4028 广播 ⇒ **两端各自 Instantiate 同一发战备的 `creatObect`**（落点/战备 id 已过网）。
  残留：落点取各自的地面射线命中点 ⇒ 地形被炸过后会略有差异（与"地形破坏不同步"同源）。
- ✅ **`EnqueueInit` 三件套复核**：`CreateBuilding` **无随机**（位置/旋转取 transform、prefab 由 `data.Get(TaskManager.Instance.EnemyVarietyType)` 决定）⇒ 两端一致，不用改；
  `CreatOOPart` / `CreateSupple` 有随机（`RandomUtils.RandomVector2` 全局流 + `BattleRandom.Bool` + `prefabs.RandomTake`）⇒ **已改**
  `SeedUtil.NewPlacementStream(seed, SeedStream.PropSpawn, transform.position)`（按生成器坐标量化派生 ⇒ 两端同落点/同数量/同 prefab），
  顺带修掉"生成物在战斗前创建 ⇒ 取**派生号**（含位置）⇒ 落点一分叉号就错"的老问题。
- ✅ **`DieLoot` 已静态同步**：掉落数量从 `UnityEngine.Random` 改成 `Derive(Derive(seed, SeedStream.DieLoot), NetId)`（在 `Start` 里算，
  因为同 GameObject 上 Actor/DieLoot 的 Awake 顺序不保证）⇒ 两端数量一致、零消息；位置本来就是常量偏移。
- ⏳ **`UnitSkill_OrbitArmor`（无敌装甲）**：用户问"伤害能同步，装甲状态就能同步吧？" ⇒ **成立，但有两个前提**：
  ① 装甲单位要有**两端一致的号**（现在 `:71` 裸 `Instantiate` ⇒ 必须并进统一生成入口 / 合并后的 `HostSpawn`）；
  ② 主单位的 `PartController.AddInvincibleArmor` 是**本地列表**（`:77`）⇒ 成员端复刻时也必须把装甲注册进它那份 PartController。
  齐了之后：装甲掉血/死亡两端一致 ⇒ `OnAllInvincibleArmorDestroyed` 两端同时触发 ⇒ 无敌状态自然一致。

### 7.2x ⭐ 生成器第 4 例 = 任务（车厢附加炮塔，2026-10-15）
> 全貌（轨道 / 路基 / 列车 / 炮塔）→ `topics/mission-intercept-train.md`；这里只留**联机口径**。
- `MissionInterceptTrain : MissionBase, INetSpawner`；`InitMission` / `Uninit` 成对 `SpawnerRegistry.Register/Unregister`
  （⚠ 不能放 `Start`：复刻可能在 `Instantiate` 内就发生）。
- 槽位 `slot = 班次 × maxCarCount + 车厢序号`（两端算式一致）；**槽位不带 prefab** ⇒ 这一节挂哪门从两端各持一份的**开局计划表**读
  （计划表在第一个 yield 之前用同一条 `BattleRandom` 算好 ⇒ 抽签一致；发车只查表、不再动随机流）。
- ⚠ **`SpawnSourceKey` 不能照抄 `NetSpawnKey.Of(gameObject)`**：任务根是 `MissionController` 的子物体（transform 位置全一样）、
  名字又只差 "(Clone)" ⇒ 多个任务互相撞键、复刻到别的任务里。任务类改用
  `ActorsManager.DerivedNetId(类名 + "#" + MissionBase.netOrder, transform.position)`（`netOrder` = 任务跨端稳定键）。
- ⚠ **`CreatUnit` 固定把单位挂 `ActorsManager` 下**（"生成者死了不能把单位一起带走"）⇒ 要挂到移动挂点 / 载具**必须自己 re-parent**
  （`SetParent(mount,false)` + 局部零位；成员侧 `Instantiate(prefab, mount.position, mount.rotation, mount)` 天然同款局部值）。
- ⚠ **新形态：复刻早于"载体"出现**。两端各自 Tick 驱动建车厢，房主 4052 可能比成员建车早 1~2 帧 ⇒ `ReplaySpawn` 找不到车厢时
  **不能直接失败**（会掉在轨道上不跟车）：记 `pendingTurrets`、`Update` 里重试、10s 超时丢弃 + `Warn`；
  复刻位置/朝向取**本端挂点**，别用消息里的 pos（含 RTT 期间车厢前进的位移）。
- ⚠ 这条通道的 prefab 必须**有 `Actor` 且 `UnitTypeEnum != Player/Friend`**（这两类不取号 ⇒ `ReportUnit` 只告警不上报 ⇒ 成员端静默缺失）。
