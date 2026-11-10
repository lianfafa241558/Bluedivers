# 主题 · 联机 · 谁该上报伤害（唯一入口 / 判据 / 护盾通道）

> 2026-11-11 收口。历史通道拆分过程见 `net-damage.md`、`net-dup-damage.md`；本文是**现状**。

## 一、唯一上报入口

**全仓只有一处**会因为"本端刚结算的一笔伤害"去发 4053/4054：`09Manager/Battle/NetUnitDamageReporter.cs`（静态类，`[RuntimeInitializeOnLoadMethod]` 自举，订阅 `UnitEventBus.OnUnitSettled`）。

```csharp
if (!victimGo.TryGetComponent(out Actor actor)) return;   // 不是单位 / Health 与 Actor 不同 GO
NetDamageReport.Report(actor, Mathf.RoundToInt(damage.RawFloat), hitBody, caused, origin, "单位·伤");
```

判据只有两条，**不要再加"有没有 EnemyController"这类按物件类型的分叉**（那是 2026-11-10 之前的样子，导致每种物件各抄一座桥）：

1. **有号**（`Actor.NetId != 0`）；
2. **带真伤害或非零成分**（`dmg > 0 || HasEffectGroups`，后者口径是"成分**值** > 0"，不是"列表非空"）。

`NetDamageReport.Report` 内部依次处理：回环门（`EnemyController.ApplyingRemoteDamage`）→ 号为零的 Warn（**排除 Player/Friend**，它们身份是 sid）→ 成分打包（`_scratch` 静态复用，实测稳态 **0 GC**）→ 方向（`RemoteDrivenMovement` = 成员上报 / 否则房主下发）→ 部位下标（`Actor.IndexOfDamageable`）。

历史（别再复活）：`EnemyController._OnSettled`、`NetVehicleDamageBridge` 上行段、`NetKeiShieldBridge`、`NetDestructibleBridge` 掉血段 —— 全部已删/已迁出。

## 二、三条**不走**这条链的（各有权责，不是遗漏）

| 场景 | 通道 | 原因 |
|---|---|---|
| 背包护盾（`ShieldBag`） | `NetShieldBagBridge`，**归属者 sid** 定址（`bag.Owner` → `FriendController.NetSid`） | 装在盟友身上，身份是 sid，不是世界编号；本机 `IsLocalDriver` 即权威 |
| 无号场景物（战斗中途生成） | 4058（`NetDestructibleBridge`），只报"打掉了" | 没有号 ⇒ 对端无法按号寻址 |
| tick / 无成分伤害 | 不报 | `Health.TakeDamage` 里 `rawGroups != null` 才发 `OnSettled`/`UnitSettled` |

## 三、护盾清单（2026-11-11 实测，玩家/战备可用）

| prefab | 生成 | Actor/type | 同步 |
|---|---|---|---|
| `Airdrop/BarrierShield`（战备 222） | 空投 `creatObect` | 1 个 `Other` | 4034 `VehicleNetId` 盖章 ⇒ 单位伤害 ✔ |
| `Airdrop/BarrierShieldSmall`（表项 2001） | `Pod.prefab` 的 `BoosterSpawnEffect` | 1 个 `Other` | 表通道 `SpawnAt` ⇒ 单位伤害 ✔ |
| `BattleBase/KeiShield`（表项 2002） | `Kei.prefab` 的 `KeiShieldEffect` | 1 个 | 4063 表通道 ⇒ 单位伤害 ✔ |
| `Airdrop/ShieldBag`（战备 302） | 空投 `creatObect` | **无 Actor** | OwnerSid（见上表） |
| `Airdrop/Magic shield`（战备 221） | 空投 `creatObect` | **无 Actor** | ⚠ 曾**完全没有同步**（`StampCreatUnitNetId` 只认 Actor，找不到就 return） |

- 盘点口径：**别只扫 `HealthShield`** —— `Magic shield` 用的是 `HealthPlayer`，会漏；按 `ShieldRebuild` / `ShieldBag` / `IgnoreHitDetection` 一起扫。
- 需要同步的护盾必须**有 `Actor`**：`VFXAirdropEffect.StampCreatUnitNetId` / `MarkGeneratedUnit` 都是 `GetComponentInChildren<Actor>()` 拿不到就 return（不盖章、不打 `HostAuthorityUnit`）。

## 四、⚠ `UnitTypeEnum.None` 吃不到**任何范围伤害**

范围伤害走 `UnitQuery` 网格，不是物理 Overlap：`FpsHelper_Hit` 爆炸段 `UnitQuery.FindUnits(new PECircle(...), new(), null, buf)`，`new TargetCfg()` 默认 `targetType = All`；过滤在 `UnitQueryGrid.AddUnits` 的 `(targetType & unit.Type) != 0`。

`None = 0` ⇒ `All & 0 == 0` ⇒ **永远匹配不到**（只能被 `Collider + Damageable` 直接命中打）。

- 要挨范围伤害又不想被 AI 索敌 ⇒ 用 **`Other`**：`TargetCfg.All` 含它 ✔，`TargetCfg.EnemyAI = All & ~Other` 排除它 ✔；`Actor.OnDie` 的 `Other` 分支只有 `AutoRegister` flag 才发事件（不加就安静）。
- 取号不受 type 影响：`Actor.Awake` 里 `ForceHostSpawnNetId` / `Replicating` 分支在 type 判断**之前**。

## 五、`ShieldBag` 不要效仿 `KeiShield`

判据一句话：**"站在世界里的"给号（`KeiShield` / `Magic shield`），"长在人身上的"给 sid（`ShieldBag`）**。

`ShieldBag` 用 sid 的理由：`BagBase.OnInstall` 把罩子挂到持有者 `BackPoint` 下（位置天然跟随）、`IsLocalDriver` 直接表达权威、装卸可反复而 4063 没有撤销、数量随玩家数增长。

## 六、已知遗留

- 破盾后的 10 秒重塑由 `ShieldRebuild` **两端各自计时**（没同步），重置时刻会偏差。
- `ShieldBag` 空投落地未装备期（`Owner == null`）两端各自结算（各端各算，影响极小）。
