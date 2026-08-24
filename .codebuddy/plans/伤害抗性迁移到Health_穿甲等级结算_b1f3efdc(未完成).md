---
name: 伤害抗性迁移到Health+穿甲等级结算
overview: 将伤害抗性从肢体(Damageable)迁移到整体(Health)控制，肢体简化为护甲等级(armorLevel)，新增穿甲等级(AP)按绝地潜兵2式"AP vs 护甲等级"结算伤害，并为直击/爆炸伤害成分分别配置AP，配套批量迁移工具。
---

I have enough context. The `InflictDamage` interface signature is `void InflictDamage(I_Damagable source, PEInt damage, List<SKVP<DamageTypeEnum, float>> damageGroups, PEInt WeaknessBonus, bool noSource, GameObject damageSource, Vector3 pos)` and `GetArmor(DamageTypeEnum)` returns `float`. Since AP is now a pure int on `DamageData` (not per-component), the AP needs to flow through `InflictDamage`. I need to determine the cleanest way to pass AP into the damage pipeline.

Given AP is an overall int per direct/explosion, the cleanest approach is to add AP as a parameter to `I_Damagable.InflictDamage`. Let me note the key design decision: AP must be added to the `InflictDamage` signature since it's not per-component (it's a whole-int value).

Let me verify how `FpsHelper` calls `InflictDamage` for both direct and explosion — I already saw it: `comp.InflictDamage(comp, damageData.GetDirectDamage(charg)*damageScale, damageData.DamageGroupDirect, damageData.GetWeaknessBonus(), damageData.NoSource || !owner, owner, point)`. The AP would come from `damageData.GetDirectAP(charg)`.

Now I have all I need. Let me generate the plan.

Key technical decisions:
1. **AP flow**: Add AP param to `I_Damagable.InflictDamage`. Since AP is a pure int per direct/explosion (not per-component), add `int AP` parameter. The settlement happens in `Damageable.InflictDamage`: `armorFactor = AP >= armorLevel ? 1 : max(0.1f, 1 - (armorLevel-AP)*0.33f)`.
2. **Health resistance**: Health gains `showArmorLists` (per-type resistance, no AllArmor). `Health.TakeDamage` multiplies each damage component by `healthResistance[type]`.
3. **I_Damagable.GetArmor**: Change semantics — but it's used as multiplier currently. Since we're replacing per-limb type resistance with armorLevel, `GetArmor(type)` should return `armorLevel` (int → float) OR be replaced with an `ArmorLevel` property. The settlement uses armorLevel, not per-type GetArmor. So add `ArmorLevel` to interface; `TransferDamageable` forwards to source's armorLevel.

Let me now decide the exact interface change. Currently:
- `float GetArmor(DamageTypeEnum type)` — returns per-type reduction coefficient (1=no reduction)
- Used in `Damageable.InflictDamage`: `source.GetArmor(item.Key)`

New model: 
- Settlement uses armorLevel (limb) + AP (attacker) + Health per-type resistance
- The `armorLevel` should be exposed. I'll change `I_Damagable` to expose `int ArmorLevel { get; }` (via `GetArmorLevel()` or property). `TransferDamageable` forwards to `source.ArmorLevel`.

For Health's per-type resistance, `Health.TakeDamage` needs access to its own `showArmorLists`. The multiplier applies in `TakeDamage` per damage type.

Now, where does the multiplier for Health's showArmorLists get applied? In `Health.TakeDamage`, when processing each `damageGroups` item: `value *= healthResistance(type)`. But note `HandleDamage` already handles Abo status. The resistance should apply to the physical damage types (Gun/Explosion/Real etc.), before `HandleDamage`. Actually it should apply to the incoming damage value. Let me apply it at the start of `TakeDamage` loop: `value *= GetResistance(item.Key)`.

Now the batch migration tool: 
- Adds `armorLevel` to Damageable
- Reads `showArmorLists` Gun & Explosion values, converts (每0.2=1, floor), takes larger → sets armorLevel
- Additionally reads health's main part, copies showArmorLists (minus Gun/Explosion) to Health's showArmorLists

The migration tool needs to handle: after adding armorLevel field to Damageable and showArmorLists to Health, it reads old Damageable.showArmorLists. But wait — the tool runs BEFORE the runtime refactor removes showArmorLists from Damageable? Or the tool runs to migrate existing prefab data after the new fields are added but before removing old fields? 

User point 4: "在修改上面几处之前，先...写一个批量修改预制体（先给Damageable加上护甲等级字段，然后读取伤害抗性里面直击/爆炸的值来设置好护甲等级的值...）". So the tool is written first, and runs while Damageable still has showArmorLists, to populate the new armorLevel and Health.showArmorLists fields. So the tool must be written and run while both old (showArmorLists on Damageable) and new (armorLevel on Damageable, showArmorLists on Health) fields exist. After migration runs, the old fields can be removed.

So the implementation order:
1. Add `armorLevel` field to Damageable (keeping old fields for now)
2. Add `showArmorLists` field to Health
3. Write batch migration tool (reads old Damageable.showArmorLists → armorLevel; copies non-Gun/Explosion to Health.showArmorLists)
4. Run the tool (user executes via menu)
5. After migration confirmed: rewrite Damageable settlement to use armorLevel+AP, add AP to DamageData/SustainedDamageData, change I_Damagable interface, update FpsHelper, update Health.TakeDamage to apply resistance, delete old AllArmor/showArmorLists/armors from Damageable, update TransferDamageable, update DamageDataDrawer.

The todo list should batch these. Let me structure the plan.

Let me write the plan now. I'll keep the design section minimal since this is a logic/refactor task (no new UI), so no `<design>` tag. I'll use extensions: the `bluedivers-unity` skill (project conventions) and possibly `lsp-code-analysis` for impact analysis. Given the extension guidance, I should use skills/subagents that exist. The `bluedivers-unity` skill is relevant. `unity-developer` subagent is relevant for Unity C#. Let me use them.

Actually, let me reconsider which extensions to use. Available: skills (bluedivers-unity, lsp-code-analysis, etc.) and subagents (code-explorer, unity-developer, etc.). For this task, `bluedivers-unity` skill is directly relevant. `lsp-code-analysis` could help with impact analysis of interface changes. `code-explorer` subagent could help verify all callers. Let me include bluedivers-unity and lsp-code-analysis.

Let me generate the final plan.我已充分掌握了代码上下文与全部已确认的设计决策。现在生成实施计划。

<plan_result>
<req>
## 产品概述
将伤害结算模型从《深岩银河》式（每肢体独立减伤系数）重构为《绝地潜兵2》式（肢体护甲等级 + 攻击方穿甲等级 AP 结算），并把伤害抗性统一上收到 Health 整体控制。

## 核心功能
- 抗性从肢体 Damageable 移到 Health 整体控制，Health 新增各类型减伤乘区（showArmorLists），删除 AllArmor
- 肢体简化为护甲等级 armorLevel（int），保留现有护甲值破坏机制（armorValue/remainArmor/BreakArmor/RestoreArmor/BleedValue）
- 结算公式（穿透+递减减伤）：armorFactor = AP >= armorLevel ? 1 : max(10%, 1 - (armorLevel - AP) * 33%)，最终伤害 = 基础 × 系数 × armorFactor × Health.showArmorLists[type]
- 攻击方 DamageData / SustainedDamageData 新增 DirectAP + ExplosionAP 两个 int 字段，并支持蓄力缩放，AP 对所有伤害类型统一有效
- 爆炸遮挡/弱点/爆炸免疫等部位特性保持留在肢体不动
- 新增批量迁移工具：读取旧 showArmorLists 换算 armorLevel（Gun 与 Explosion 取较大，每 0.2 抗性 = 1 护甲等级向下取整），并把主肢体抗性（去掉 Gun/Explosion）复制到 Health
</req>

<tech>
## 技术栈
沿用项目现有 Unity 2022.3.62f2c1 / C# 9.0 / .NET Standard 2.1 栈，不引入新依赖。严格遵循 `bluedivers-unity` 项目规范（大括号另起行、私有字段 `_` 前缀、`[SerializeField] private` + `[InspectorName("中文")]`、运行时不引 UnityEditor、协程不用 Coroutine 后缀）。

## 实现思路
本次是伤害模型的**核心重构 + 编辑器迁移工具**，不涉及新 UI。核心链路改造点在 `Damageable.InflictDamage`（当前 `source.GetArmor(item.Key)` 做减伤系数乘算）与 `Health.TakeDamage`（当前不管抗性）。关键决策如下：

### 1. AP 传递链路（关键架构决策）
AP 是纯 int、按直击/爆炸整体生效（非每成分），因此**不能挂在 `DamageGroup*` 成分里**（避免污染通用 `SKVP<K,V>`）。改为在 `I_Damagable.InflictDamage` 签名新增 `int AP` 参数：
- `FpsHelper.Hit` 直击分支传 `damageData.GetDirectAP(charg)`，爆炸分支传 `damageData.GetExplosionAP(charg)`
- `Damageable.InflictDamage` 内用 `AP` 与 `armorLevel` 结算 `armorFactor`
- 蓄力缩放：`GetDirectAP(PEInt charge)` = `AP >= 0 ? lerp(AP, DirectAPChargeScale, charge) : AP`（仿 `_HandleValue`）

### 2. 肢体护甲等级暴露
`I_Damagable` 新增 `int ArmorLevel { get; }`（替代 `GetArmor(type)` 语义）。`Damageable.ArmorLevel => armorLevel`；`TransferDamageable.ArmorLevel => source.ArmorLevel`。旧的 `float GetArmor(DamageTypeEnum)` 移除。

### 3. Health 整体抗性叠加
`Health` 新增 `[SerializeField] private List<SKVP<DamageTypeEnum,float>> showArmorLists`（无 AllArmor）。在 `TakeDamage` 的 `damageGroups` 循环开头对每项 `value *= GetResistance(item.Key)`（默认 1，列表未配置即 1），再进 `HandleDamage`。

### 4. 批量迁移工具（先写，运行时两者并存期间执行）
仿 `ExplosionImmunitySetter`：`PrefabUtility.LoadPrefabContents` + `GetComponentsInChildren<Damageable>` + `SerializedObject` 读写 + `SaveAsPrefabAsset`。遍历每个 Damageable：读 `showArmorLists` 中 Gun 与 Explosion 值，`floor(value / 0.2f)` 换算，取较大写入 `armorLevel`；再定位其 Health 主肢体，把非 Gun/Explosion 的抗性项复制到 `Health.showArmorLists`。工具需在**移除旧字段前**执行。

### 实施顺序（关键，避免数据丢失）
必须先加新字段→写并跑迁移工具→确认迁移成功后，再改结算逻辑并删除旧字段（AllArmor/showArmorLists/armors/GetArmor）。

## 性能与回归注意
- `Damageable.InflictDamage` 每成分只做一次 `armorFactor` 计算（O(1)），无额外遍历；`armors` 字典删除后减少一次 Dictionary 初始化
- `Health.TakeDamage` 每项做一次 `GetResistance(type)` 字典/列表查找，用 `DisplayDic` 或缓存 `Dictionary<DamageTypeEnum,float>` 避免每帧分配
- 改 `I_Damagable` / `Health.TakeDamage` 签名会影响多个调用方（FpsHelper、Projectile 系列、SPEffect、Abo 伤害等），需用 [skill:lsp-code-analysis] 做调用点全量核查，避免漏改导致编译错误
- 保留 `HpItemBoss.GetArmorRatio()` 与 `UnitSkill_ShieldRestore.remainArmor` 逻辑不变（护甲破坏机制保留）

## 架构设计
```mermaid
graph TD
    A[FpsHelper.Hit / Projectile] -->|InflictDamage + AP| B[Damageable 肢体]
    B -->|armorFactor = AP vs armorLevel| B
    B -->|finalDamageGroups| C[Health.TakeDamage]
    C -->|x Health.showArmorLists[type]| C
    C --> D[血量/护盾扣减 + HandleDamage 异常状态]
    B -.保留.-> E[弱点/爆炸免疫/爆炸遮挡/护甲值破坏机制]
    B -.保留.-> F[TransferDamageable 转发 ArmorLevel]
```

## 目录结构
```
Assets/
├── Scripts/
│   ├── 00GameContract/
│   │   └── Interface_Game.cs          # [MODIFY] I_Damagable：移除 GetArmor(type)，新增 int ArmorLevel；InflictDamage 新增 int AP 参数
│   ├── 02Game/Game/Shared/
│   │   ├── Damageable.cs              # [MODIFY] 新增 armorLevel；结算改 armorFactor(AP,armorLevel)；删除 AllArmor/showArmorLists/armors/GetArmor；保留护甲值破坏+部位特性
│   │   ├── TransferDamageable.cs      # [MODIFY] ArmorLevel 转发 source；移除 GetArmor 的 extraLists 叠加
│   │   ├── Health.cs                  # [MODIFY] 新增 showArmorLists；TakeDamage 每成分乘 GetResistance(type)
│   │   ├── Health_AboState.cs         # [MODIFY] 适配 TakeDamage 签名（若加参数）；Abo 伤害路径补 resistance 一致处理
│   │   ├── HealthEnemy.cs             # [MODIFY] 若 Health 抽象/签名变化做相应适配（通常不变）
│   │   └── Weapon/
│   │       ├── DamageData.cs          # [MODIFY] 新增 DirectAP/ExplosionAP(int)+DirectAPChargeScale/ExplosionAPChargeScale+GetDirectAP/GetExplosionAP；IDamageData 接口扩展
│   │       └── SustainedDamageData.cs # [MODIFY] 新增 DirectAP/ExplosionAP(int)+Get*AP
│   ├── 00Tools/
│   │   └── FpsHelper.cs               # [MODIFY] Hit 直击/爆炸分支传 AP 进 InflictDamage
│   └── 00Core/
│       └── ResCfg.cs                  # [MODIFY 可选] DamageTypeEnum 若需 AP 相关辅助（通常不需要）
├── Editor/
│   ├── ArmorLevelMigrationTool.cs     # [NEW] 批量迁移工具：换算 armorLevel + 复制抗性到 Health（仿 ExplosionImmunitySetter）
│   └── Drawer/
│       └── DamageDataDrawer.cs        # [MODIFY] 新增 DirectAP/ExplosionAP + 蓄力缩放字段绘制与高度计算
```
</tech>

<extensions>
## Agent Extensions
### Skill
- **bluedivers-unity**
  - 用途：贯穿全程的 Unity 项目编码规范、模块划分、命名约定与架构解耦约束，确保新代码贴合项目现有约定（数字前缀模块、_ 私有字段、[SerializeField] private + InspectorName 中文、运行时不引 UnityEditor、协程不用 Coroutine 后缀）。
  - 预期结果：新增/修改的 Damageable、Health、DamageData 与迁移工具均符合项目规范，无跨层依赖违规。
- **lsp-code-analysis**
  - 用途：对 I_Damagable 接口签名（新增 ArmorLevel、InflictDamage 增加 AP 参数）与 Health.TakeDamage 签名变更做全量调用点/引用核查，找出所有受影响实现与调用方（Damageable、TransferDamageable、FpsHelper、Projectile 系列、SPEffect、Abo 伤害路径、SympatheticDetonation 等）。
  - 预期结果：完整覆盖全部引用点，避免签名变更导致的漏改与编译错误。

### SubAgent
- **code-explorer**
  - 用途：在改动签名前系统性定位所有 `InflictDamage` / `GetArmor` / `TakeDamage` 的调用与实现位置，以及 `DamageGroupDirect/DamageGroupExplosion` 的所有消费方，作为接口变更影响面清单。
  - 预期结果：产出一份完整的调用点清单，支撑 lsp-code-analysis 的精确核查。
</extensions>

<todolist>
<item id="add-fields" deps="">给 Damageable 新增 armorLevel(int) 字段、给 Health 新增 showArmorLists 字段（此时保留旧字段以便迁移读取）</item>
<item id="migration-tool" deps="add-fields">用 [skill:bluedivers-unity] 编写 Assets/Editor/ArmorLevelMigrationTool.cs：读旧 showArmorLists 的 Gun/Explosion 换算 armorLevel（取较大、每0.2向下取整），并把主肢体非 Gun/Explosion 抗性复制到 Health.showArmorLists，仿 ExplosionImmunitySetter 批量处理全项目 Prefab</item>
<item id="interface-ap" deps="add-fields">用 [skill:lsp-code-analysis] + [subagent:code-explorer] 全量核查后，修改 I_Damagable（移除 GetArmor(type)，新增 int ArmorLevel，InflictDamage 加 int AP 参数）及 Damageable/TransferDamageable 实现</item>
<item id="damage-data-ap" deps="interface-ap">给 DamageData/SustainedDamageData 新增 DirectAP+ExplosionAP 及蓄力缩放字段和 Get*AP 方法，更新 IDamageData 接口与 DamageDataDrawer 绘制</item>
<item id="health-resistance" deps="interface-ap">在 Health.TakeDamage 中每成分乘 Health.showArmorLists 减伤，统一 Abo 伤害路径抗性处理，删除 Damageable 旧 AllArmor/showArmorLists/armors/GetArmor</item>
<item id="settle-damage" deps="health-resistance,dpsap">重构 Damageable.InflictDamage 结算：armorFactor = AP>=armorLevel ? 1 : max(0.1f, 1-(armorLevel-AP)*0.33f)，叠加 Health 抗性，FpsHelper 直击/爆炸分支传 AP</item>
<item id="verify" deps="settle-damage">运行迁移工具并核对结果，编译全量核查 InflictDamage/TakeDamage/GetArmor 所有调用点，确认无编译错误与行为回归</item>
</todolist>
</plan_result>
