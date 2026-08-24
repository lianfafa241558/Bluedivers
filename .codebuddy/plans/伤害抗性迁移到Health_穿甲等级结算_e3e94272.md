---
name: 伤害抗性迁移到Health+穿甲等级结算
overview: 将伤害抗性从肢体(Damageable)迁移到整体(Health)控制，肢体简化为护甲等级(armorLevel)，新增穿甲等级(AP)按绝地潜兵2式"AP vs 护甲等级"结算伤害，为直击/爆炸分别配置AP，并将爆炸免疫bool改为float爆炸抗性，配套批量迁移工具。
todos:
  - id: add-fields
    content: 给 Damageable 新增 armorLevel(int) 与 explosionResistance(float) 字段、给 Health 新增 showArmorLists 字段（保留旧字段以便迁移读取）
    status: completed
  - id: migration-tool
    content: 用 [skill:bluedivers-unity] 编写 Assets/Editor/ArmorLevelMigrationTool.cs：换算 armorLevel(Gun/Explosion 取较大、每0.2向下取整)、复制主肢体非 Gun/Explosion 抗性到 Health、迁移 bool 爆炸免疫到 float，仿 ExplosionImmunitySetter 批量处理全项目 Prefab
    status: completed
    dependencies:
      - add-fields
  - id: interface-ap
    content: 用 [skill:lsp-code-analysis] + [subagent:code-explorer] 全量核查后，修改 I_Damagable（移除 GetArmor/IsExplosionImmunity，新增 int ArmorLevel + float ExplosionResistance，InflictDamage 加 int AP）及 Damageable/TransferDamageable 实现
    status: completed
    dependencies:
      - add-fields
  - id: damage-data-ap
    content: 用 [skill:lsp-code-analysis] 核查后，给 DamageData/SustainedDamageData 新增 DirectAP+ExplosionAP 及蓄力缩放字段和 Get*AP 方法，更新 IDamageData 接口与 DamageDataDrawer 绘制
    status: completed
    dependencies:
      - interface-ap
  - id: health-resistance
    content: 在 Health.TakeDamage 每成分乘 Health.showArmorLists 减伤，统一 Abo 伤害路径抗性，删除 Damageable 旧 AllArmor/showArmorLists/armors/GetArmor/isExplosionImmunity(bool)
    status: completed
    dependencies:
      - interface-ap
  - id: settle-damage
    content: "用 [subagent:unity-developer] 复核后，重构 Damageable.InflictDamage 结算：armorFactor = AP>=armorLevel ? 1 : max(0.1f, 1-(armorLevel-AP)*0.33f)，爆炸成分乘(1-explosionResistance)，FpsHelper 直击/爆炸分支传 AP 并按 ExplosionResistance 乘算"
    status: completed
    dependencies:
      - health-resistance
      - damage-data-ap
  - id: verify
    content: 运行迁移工具并核对结果，用 [skill:lsp-code-analysis] 全量核查 InflictDamage/TakeDamage/GetArmor/IsExplosionImmunity 所有调用点，确认无编译错误与行为回归
    status: completed
    dependencies:
      - settle-damage
---


## 产品概述
将伤害结算模型从《深岩银河》式（每肢体独立减伤系数）重构为《绝地潜兵2》式（肢体护甲等级 + 攻击方穿甲等级 AP 结算），并把伤害抗性统一上收到 Health 整体控制。同时把肢体爆炸免疫从 bool 改为 float 爆炸抗性（减伤系数）。

## 核心功能
- 抗性从肢体 Damageable 移到 Health 整体控制，Health 新增各类型减伤乘区（showArmorLists），删除 AllArmor
- 肢体简化为护甲等级 armorLevel（int），保留护甲值破坏机制（armorValue/remainArmor/BreakArmor/RestoreArmor/BleedValue）
- 结算公式（穿透+递减减伤）：armorFactor = AP >= armorLevel ? 1 : max(10%, 1 - (armorLevel - AP) * 33%)
- 最终伤害 = 基础 × 成分系数 × armorFactor(AP, armorLevel) × Health.showArmorLists[type]，爆炸成分再乘 (1 - 肢体爆炸抗性 float)
- 攻击方 DamageData / SustainedDamageData 新增 DirectAP + ExplosionAP（int）+ 蓄力缩放，AP 对所有伤害类型统一有效
- 爆炸遮挡/弱点等部位特性保留在肢体；isExplosionImmunity(bool) 改为 float 爆炸抗性
- 新增批量迁移工具：换算 armorLevel + 复制抗性到 Health + 迁移 bool 爆炸免疫到 float



## 技术栈
沿用项目现有 Unity 2022.3.62f2c1 / C# 9.0 / .NET Standard 2.1 栈，不引入新依赖。严格遵循 `bluedivers-unity` 项目规范（大括号另起行、私有字段 `_` 前缀、`[SerializeField] private` + `[InspectorName("中文")]`、运行时不引 UnityEditor、协程不用 Coroutine 后缀）。

## 实现思路
本次是伤害模型的**核心重构 + 编辑器迁移工具**，不涉及新 UI。核心链路改造点在 `Damageable.InflictDamage`（当前 `source.GetArmor(item.Key)` 做减伤系数乘算）与 `Health.TakeDamage`（当前不管抗性）。关键决策如下。

### 1. AP 传递链路（关键架构决策）
AP 是纯 int、按直击/爆炸整体生效（非每成分），因此**不能挂在 `DamageGroup*` 成分里**（避免污染通用 `SKVP<K,V>`）。改为在 `I_Damagable.InflictDamage` 签名新增 `int AP` 参数：
- `FpsHelper.Hit` 直击分支传 `damageData.GetDirectAP(charg)`，爆炸分支传 `damageData.GetExplosionAP(charg)`
- `Damageable.InflictDamage` 内用 `AP` 与 `armorLevel` 结算 `armorFactor`
- 蓄力缩放：`GetDirectAP(PEInt charge)` = 仿 `_HandleValue`，`AP >= 0 ? lerp(AP, DirectAPChargeScale, charge) : AP`

### 2. 肢体护甲等级与爆炸抗性暴露
`I_Damagable` 移除 `float GetArmor(DamageTypeEnum)` 与 `bool IsExplosionImmunity()`，新增：
- `int ArmorLevel { get; }`（肢体护甲等级）
- `float ExplosionResistance { get; }`（爆炸抗性，0~1，1=完全免疫）
`Damageable` 实现为 `armorLevel` 与 `explosionResistance` 字段；`TransferDamageable` 转发 `source.ArmorLevel` 与 `source.ExplosionResistance`。

### 3. Health 整体抗性叠加
`Health` 新增 `[SerializeField] private List<SKVP<DamageTypeEnum,float>> showArmorLists`（无 AllArmor），运行时构建 `Dictionary<DamageTypeEnum,float>` 缓存（默认 1）。在 `TakeDamage` 的 `damageGroups` 循环开头对每项 `value *= GetResistance(item.Key)`，再进 `HandleDamage`。

### 4. 批量迁移工具（先写，运行时两者并存期间执行）
仿 `ExplosionImmunitySetter`：`PrefabUtility.LoadPrefabContents` + `GetComponentsInChildren<Damageable>` + `SerializedObject` 读写 + `SaveAsPrefabAsset`，遍历 Assets 下全部 Prefab。每个 Damageable 处理三项：
- 读 `showArmorLists` 中 Gun 与 Explosion 值，`floor(value / 0.2f)` 换算，取较大写入 `armorLevel`
- 定位其 Health 主肢体，把非 Gun/Explosion 的抗性项复制到 `Health.showArmorLists`
- 读 `isExplosionImmunity`(bool)，true 则写 `explosionResistance`=1，并移除旧 bool 字段
工具需在**移除旧字段前**执行。

### 实施顺序（关键，避免数据丢失）
先加新字段（armorLevel/explosionResistance/Health.showArmorLists）→ 写并跑迁移工具 → 确认迁移成功后，再改结算逻辑、改 I_Damagable 接口、删旧字段（AllArmor/showArmorLists/armors/GetArmor/isExplosionImmunity）。

## 性能与回归注意
- `Damageable.InflictDamage` 每成分只做一次 `armorFactor` 计算（O(1)），删除 `armors` 字典减少一次 Dictionary 初始化
- `Health.TakeDamage` 每项用缓存的 `Dictionary<DamageTypeEnum,float>` 查减伤，避免每帧分配（可用 `DisplayDic`）
- 改 `I_Damagable` / `Health.TakeDamage` 签名会影响多个调用方（FpsHelper、Projectile 系列、SPEffect、Abo 伤害、SympatheticDetonation 等），用 [skill:lsp-code-analysis] + [subagent:code-explorer] 全量核查，避免漏改
- `FpsHelper` 爆炸分支改为按 `ExplosionResistance` 乘算减伤；若 float>=0.95 视为免疫可保留过滤以优化
- `Damageable.ExplosionBlocking` 中 `if(IsExplosionImmunity()) return false` 改为 `if(ExplosionResistance >= 0.95f) return false`
- 保留 `HpItemBoss.GetArmorRatio()` 与 `UnitSkill_ShieldRestore.remainArmor` 逻辑不变（护甲破坏机制保留）
- 约 17 个 Prefab 的 bool 爆炸免疫数据需随迁移工具同步迁移，防止旧字段残留

## 架构设计
```mermaid
graph TD
    A[FpsHelper.Hit / Projectile] -->|InflictDamage + AP| B[Damageable 肢体]
    B -->|armorFactor = AP vs armorLevel| B
    B -->|爆炸乘 1-ExplosionResistance| B
    B -->|finalDamageGroups| C[Health.TakeDamage]
    C -->|x Health.showArmorLists[type]| C
    C --> D[血量/护盾扣减 + HandleDamage 异常状态]
    B -.保留.-> E[弱点/爆炸遮挡/护甲值破坏机制]
    B -.转发.-> F[TransferDamageable ArmorLevel/ExplosionResistance]
```

## 目录结构
```
Assets/
├── Scripts/
│   ├── 00GameContract/
│   │   └── Interface_Game.cs          # [MODIFY] I_Damagable：移除 GetArmor(type)/IsExplosionImmunity()，新增 int ArmorLevel + float ExplosionResistance；InflictDamage 加 int AP 参数
│   ├── 02Game/Game/Shared/
│   │   ├── Damageable.cs              # [MODIFY] 新增 armorLevel + explosionResistance(float)；结算改 armorFactor(AP,armorLevel) 且爆炸乘(1-explosionResistance)；删除 AllArmor/showArmorLists/armors/GetArmor/isExplosionImmunity(bool)
│   │   ├── TransferDamageable.cs      # [MODIFY] 转发 ArmorLevel/ExplosionResistance 到 source；移除 GetArmor 的 extraLists 叠加
│   │   ├── Health.cs                  # [MODIFY] 新增 showArmorLists；TakeDamage 每成分乘 GetResistance(type)
│   │   ├── Health_AboState.cs         # [MODIFY] 适配 TakeDamage 签名；Abo 伤害路径补 resistance 一致处理
│   │   ├── HealthEnemy.cs             # [MODIFY] 适配（通常不变）
│   │   └── Weapon/
│   │       ├── DamageData.cs          # [MODIFY] 新增 DirectAP/ExplosionAP(int)+DirectAPChargeScale/ExplosionAPChargeScale+GetDirectAP/GetExplosionAP；IDamageData 接口扩展
│   │       └── SustainedDamageData.cs # [MODIFY] 新增 DirectAP/ExplosionAP(int)+Get*AP
│   └── 00Tools/
│       └── FpsHelper.cs               # [MODIFY] Hit 直击/爆炸分支传 AP；爆炸改为按 ExplosionResistance 乘算减伤
├── Editor/
│   ├── ArmorLevelMigrationTool.cs     # [NEW] 批量迁移工具：换算 armorLevel + 复制抗性到 Health + 迁移 bool 爆炸免疫到 float（仿 ExplosionImmunitySetter）
│   └── Drawer/
│       └── DamageDataDrawer.cs        # [MODIFY] 新增 DirectAP/ExplosionAP + 蓄力缩放字段绘制与高度计算
```


## Agent Extensions
### Skill
- **bluedivers-unity**
  - 用途：贯穿全程的 Unity 项目编码规范、模块划分、命名约定与架构解耦约束，确保新代码贴合项目现有约定。
  - 预期结果：新增/修改的 Damageable、Health、DamageData 与迁移工具均符合项目规范，无跨层依赖违规。
- **lsp-code-analysis**
  - 用途：对 I_Damagable 接口签名（新增 ArmorLevel/ExplosionResistance、InflictDamage 增加 AP 参数）与 Health.TakeDamage 签名变更做全量调用点/引用核查。
  - 预期结果：完整覆盖全部引用点，避免签名变更导致的漏改与编译错误。
### SubAgent
- **code-explorer**
  - 用途：在改动签名前系统性定位所有 InflictDamage / GetArmor / TakeDamage / IsExplosionImmunity 的调用与实现位置，以及 DamageGroupDirect/DamageGroupExplosion 的全部消费方。
  - 预期结果：产出一份完整调用点清单，支撑 lsp-code-analysis 的精确核查。
- **unity-developer**
  - 用途：复核 Unity 伤害结算与序列化字段迁移的正确性，特别是 Prefab 批量迁移工具（PrefabUtility）与运行时段落的边界。
  - 预期结果：确保迁移工具不破坏 Prefab 层级、运行时逻辑无误。
