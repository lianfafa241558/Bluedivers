---
name: weapon-upgrade
description: Bluedivers 项目「武器升级 / 武器模组」系统专用开发指南。当需要新增或修改武器升级项（WUD_*）、武器模组（WMD_*）、在武器 prefab 的 Upgrade/Modules 列表上挂数据、调整武器属性数值与显示（WeaponAttrType / ModifierType / AttrTag）、实现新的专有机制（Special 属性 + WeaponUniqueAttributeApplier）、排查"升级项不生效/数值不变/参数面板看不到/模组装了别人看不见/名字对不上/越界报错"、或改动 SelectRoleWnd 武器改装界面、ArchivesData_SO 武器改装存档、PlayerLoadoutMsg 改装同步时触发。提供数据模型、属性引擎公式、应用链路、编辑器工具、标准工作流与避坑清单。
---

# Bluedivers 武器升级 / 武器模组开发指南

## Overview

整套系统只服务于一件事：**给武器属性打增量补丁**。

- `WeaponUpgradeData_SO`（`WUD_`）= 一次**升级选项**：解锁等级、图标、描述、价格 + 一串 `ModifyAttrData`
- `WeaponModuleData_SO`（`WMD_`）= 一个**模组**：品质（无暇/均衡/危险）、正负收益文案 + 一串 `ModifyAttrData`
- 两者的 `modifys` 格式**完全相同**，由同一套引擎应用：`WeaponPlayerController`（partial，逻辑在 `Weapon/WeaponUpgradeController.cs`）+ `WeaponAttributeFactory`
- **升级树不在 SO 里，在武器 prefab 上**：`WeaponPlayerController.Upgrade`（`List<KVP<int 解锁等级, List<WUD_>>>`）、`Modules`、`ActiveModule`

差异只有玩法规则：

| | 武器升级（WUD_） | 武器模组（WMD_） |
|---|---|---|
| 结构 | 按等级分档，每档单选（每档 ≤3 项） | 一整组互相替换（同一时刻只装一个） |
| 解锁 | 武器等级 ≥ `KVP.Key`；已购买的池子内才能选 | 无等级门槛（可选子集由 prefab 的 `Modules` 决定） |
| 代价 | `cost`（`OOPartEnum` 资源） | 无 |
| 存档 | `selectIndex`（每档选了第几项）+ `buyIndex`（位掩码） | `selectModuleIndex`（一个下标） |
| 数值方向 | **只有强化**，禁止任何负收益（规则见 §1.1 后的「数值方向」） | **必然成对**：正面收益 + 负面代价 |
| 表现 | 卡片图标 + tooltip | 边框色 + `desc` 的 ↑绿/↓红 列表 |

---

## 1 数据模型

### 1.1 `WeaponUpgradeData_SO`（`Assets/Scripts/06Gameplay/Data/`）

| 字段 | 类型 | 语义 / 陷阱 |
|---|---|---|
| `name` | `new string` | **中文显示名**。它遮蔽了 `ScriptableObject.name`（= asset 文件名 `m_Name`），两者互不相干 |
| `type` | `string` | 自由分类文本（"武器散布""费用"…），只用于 tooltip 的"类型"行 |
| `icon` | `Sprite` | `[SpritePreview(3,3)]`；UI 卡片与存档预览都用它 |
| `desc` | `string` | `[TextArea]`，可含 `\r\n`。**文案 = 风味·诙谐**（规则见 §1.3 前的「文案分工」） |
| `cost` | `List<SKVP<OOPartEnum,int>>` | 默认 `Pyroxene 1000`；确认购买时经 `PropertyManager.Pay(cost)` 扣除（**扣费唯一入口**，别再手写 SetCount 循环） |
| `modifys` | `List<ModifyAttrData>` | 拼写就是 `modifys`（不是 modifies）。**只允许强化，禁止负收益**（规则见 §1.1 后的「数值方向」） |

> ⚠ **数值方向（2026-10-28 定，硬规则）**
>
> **武器升级（`WUD_`）只允许强化，不许出现任何让武器变弱的项** —— 弹药、弹匣、装填、射速、伤害、精度、穿甲一律只加不减。
> 想让玩家"变强但要付出代价"的，一律做成**模组（`WMD_`）**，那里才是正负成对的地方。
>
> 判定"强化"看**属性方向**，不看正负号：
> - 带 `AttrTag.FlipPlus` 或 `AttrTag.Reciprocal` 的属性（`ShootInterval` / `ReloadTime` / `BulletsSpreadAngle` / `HeatPerShot` / `CoolDelay` / `OverheatDuration` / `LockInterval`…）⇒ **负值才是强化**；
> - 其余属性（`Ammo` / `Magazine` / `DirectDamage` / `DirectAP` / `WeaknessBonus` / `DirectDestruction` / `MoveSpeedToShoot`…）⇒ **正值才是强化**。
>
> `scripts/gen_weapon_tree.py` 已按这条自动拦：WUD 里出现弱化项直接 `[ERROR]` 且不写盘；WMD 不拦（允许成对）。

### 1.2 `WeaponModuleData_SO`

| 字段 | 类型 | 语义 / 陷阱 |
|---|---|---|
| `name` / `icon` | 同上 | |
| `type` | `ModuleType` | `None/Clean/Balanced/Unstable` ⇒ 决定 `color`、`typeName`、边框图 `Icon/Frame_Module{type}` |
| `desc` | `List<SKVP<bool,string>>` | `Key=true` = 增益（绿 ↑），`false` = 代价（红 ↓）。**只写效果、不写风味、不写具体数值**（用「大幅/显著/略微/相当多的」等程度词，规则见 §1.3 前的「文案分工」） |
| `modifys` | `List<ModifyAttrData>` | 与升级同格式 |
| ~~`frame`~~ | — | 2026-10-01 已移除（数据层不许要资源）；边框由表现侧 `SelectRoleWnd.LoadModuleFrame` 按 `type` 查表 |

> ⚠ **文案分工（2026-10-27 定，别混）**
>
> - **`WUD_`（升级卡）**：`desc` 是卡面 tooltip ⇒ **风味化、诙谐**，可以整段吐槽/玩梗，效果点到为止。
>   **句式**：学 DRG 原版——先平铺直叙说效果，再补一句自嘲/吐槽（工程部、管理层、"别问"、"见不得光的改动"、"安全去他的"、"容易崩到眼睛"）；
>   **少用「不是…而是…」「不是 A，是 B」这类对称句式**，读起来像 AI 写的。
>   参考现有写法：`WUD_Damage`「工程部加班造成的身体与精神上的伤害，换取了武器的整体伤害提升。」、
>   `WUD_Rate`「…别多问，这种东西小范围享受就可以了。也别让优香知道」、
>   `WUD_Reload`「…外加那么亿丢丢见不得光的私下"改动"使得武器的换弹更加迅速。」
> - **`WMD_`（模组）**：`desc` 是绿 ↑ / 红 ↓ 的**效果行**，玩家读的是增减 ⇒ **只写效果，不写风味**，一行一个动作，不写段子、不写梗。
>   **并且不要写具体数值**：用程度词表达方向与量级 —— 「大幅提升 / 显著提高 / 略微增加 / 相当多的弹药 / 射速更快 / 装填更慢 / 散布变差」。
>   不要出现 `+5`、`-23%`、`3°→6°`、`100%→60%` 这类数字，具体数值让玩家去看参数面板。
>   正例：「射速大幅提升」「弹匣容量显著减少」「散布略微变差」；反例：「射速 -23%」「弹匣容量 -14」「散布 3°→6°」。
>   参考 `WMD_AggressiveVenting` =「攻击附带燃烧伤害」「过热时冷却速度提升」（无数字）。
> - 禁写数值的附带好处：`modifys` 或 cfg 基线调整后**不需要再回来同步文案**，不会再出现"文案写 70%→30%、实际已经是 100%→60%"这种错话。

### 1.3 `ModifyAttrData`（结构体，`Weapon/WeaponUpgradeController.cs` 末尾）

```csharp
public struct ModifyAttrData
{
    public string name;            // 仅 type==Special 时有意义（专有属性的中文名）
    public WeaponAttrType type;    // 改哪个属性
    public ModifierType modifier;  // Base=1 / Factor=2 / Extra=4
    public float value;            // 加多少（可为负）
}
```

同一文件还有两个配套结构：

- `UniqueAttrData`（武器 prefab 的 `m_UniqueAttr`）= **专有属性注册表**：`name` / `value`（默认值）/ `tag`（显示标旗）
- `RenameAttrData`（武器 prefab 的 `m_RenameAttr`）= 改显示名 + 追加/移除标旗

### 1.4 prefab 上的挂载结构

```
WeaponPlayerController (prefab)
├─ showAttr       : List<WeaponAttrType>       // 参数面板显示哪些属性（默认值来源）
├─ m_RenameAttr   : List<RenameAttrData>       // 属性改中文名 / 加 tag
├─ m_UniqueAttr   : List<UniqueAttrData>       // 专有属性注册（Special 必须先在这里）
├─ Upgrade        : List<KVP<int, List<WUD_>>> // Key = 解锁所需武器等级，每档 ≤3 个
├─ Modules        : List<WMD_>                 // ⚠ [0] 必须是 WMD_None（空模组）
└─ ActiveModule   : WMD_                        // 编辑器里的"当前展示"，运行时会被 ApplyUpgrade 覆盖
```

`Upgrade` 的 `Key` **不是等级序号而是解锁等级门槛**（BeamRifle 实测：2 / 4 / 8 / 12，UI 判 `archWeaponData.Level < Key` 即锁定）。

---

## 2 属性引擎（改数值前必读）

### 2.1 公式与修饰符

`GameAttribute`（`WeaponAttributeFactory.cs` 末尾）：

```
Final = (base + ΣBase) * (1 + ΣFactor) + ΣExtra      // OnlyInt 时再取 floor
```

| ModifierType | 值 | 含义 | 典型 value |
|---|---|---|---|
| `Base` | 1 | 直接加减绝对值 | `+10` 弹药、`+1` 弹丸数 |
| `Factor` | 2 | 加到倍率上（**0.3 = +30%**，不是乘 0.3） | `-0.3` = 冷却 -30% |
| `Extra` | 4 | 最后叠加的额外值（`允许集` 常只开这一种） | `+1` 锁定层数 |

⚠ `value` 的**符号**只表示加减；"越低越好"的属性（`ShootInterval`/`ReloadTime`/`HeatPerShot`/`CoolDelay`/`OverheatDuration`）靠 `AttrTag.FlipPlus` 把颜色反转显示，**不要为了变绿去反写数值符号**。

### 2.2 唯一注册表：`WeaponAttributeFactory.attributeConfigs`

`Dictionary<WeaponAttrType, (bool isCurrentAttr, AttrTag tag, ModifierType allowModifier)>`，三条信息全在这里：

- `isCurrentAttr` = 建 `GameCurrentAttribute`（带 `CurrValue`，受 `LimitCurr`/`UpdateToZero` 影响：换弹、热量、蓄力）
- `tag` = 显示规则（见 2.3）
- `allowModifier` = 该属性**允许**的修饰符；不在集合里的 modifier 会在运行时 `LogError("不支持的Modifier类型")`，编辑器下拉框也已过滤

**新增属性 = 枚举 + 这张表 + 消费端订阅**，缺一张就不生效（见 §5.4）。

### 2.3 `AttrTag` 显示语义

| Tag | 位 | 效果 |
|---|---|---|
| `Percentage` | 1<<0 | 显示百分比（相对 Prime） |
| `Reciprocal` | 1<<1 | 显示倒数（射速类：间隔 ↓ 显示为 ↑） |
| `FlipPlus` | 1<<2 | 反转红/绿（减速/负面类） |
| `DefaultHide` | 1<<3 | 值 == Prime 且未被改动时隐藏 |
| `OneHide` | 1<<4 | 值 == 1 时隐藏（倍率类） |
| `TextOnly` | 1<<5 | 不显示数值，只在末尾显示 `+名称` / `-名称`（专有机制常用） |
| `IsHide` | 1<<6 | 永远不显示 |
| `ScaleFromPrime` | 1<<7 | 相对原始值的百分比 |
| `APGrade` | 1<<8 | 显示成 `(中穿)3` 形式 |
| `OnlyInt` / `UpdateToZero` / `LimitCurr` | 1<<10/11/12 | 整数化 / 重算时 Curr 归零 / Curr 限制在 `[0,Final]` |

### 2.4 `WeaponAttrType` 编号分段（新增时挑未占用值）

| 段 | 内容 |
|---|---|
| 0–16 | 通用（Ammo/Magazine/ShootInterval/弹丸数/散布/装弹）、散热（Heat/Cool/Overheat） |
| 20–26 | 伤害（DirectDamage/ExplosionDamage/ExplosionRange/BulletSpeed/DirectAP/ExplosionAP） |
| 40 | 激光（LaserWaitTime） |
| 50–57 | 蓄力（段数/耗弹/时长/三档倍率） |
| 60–64 | 锁定（距离/半径/层数/每敌层数/间隔） |
| 100–102 | 破坏系数、弱点加成 |
| 200–211 | **直击**伤害类型系数（200 动能 / 205 燃烧 / 221 无…见 references） |
| 220–231 | **爆炸**伤害类型系数 |
| 999 | `Special` 专有属性（不进 `attributeConfigs`，靠 `m_UniqueAttr` 注册） |

---

## 3 运行时应用链路

`WeaponUpgradeController.cs`（`partial class WeaponPlayerController`）是全部入口。

### 3.1 `ApplyUpgrade(int[] select, int module)` — 权威入口

1. 重建 `m_Parameter`（`Dictionary<string, AttrInfo>`）：先把 `showAttr` 每项按 `GetAttrName(type)` 建 `AttrInfo`（值取自 `cfg`，缺项兜底 0 + Warning 带键表）
2. 补 `DirectAP`/`ExplosionAP` 的 cfg 初值（预览实例可能没跑过 `InitAttribute`）
3. 把 `m_UniqueAttr` 每项建 `AttrInfo(name, value, tag)`（**不进 cfg**，独立 `GameAttribute`）
4. 对 `select[i] >= 0` 逐档 `ApplyUpgrade(y, x)`（= `ApplyAttrInfo(GetUpgrade(y,x).modifys, true)`）
5. `Modules` 非空 ⇒ `ApplyModule(旧下标, module)` 并写 `ActiveModule`
6. `WeaponUniqueAttributeApplier.Apply(this, m_Parameter)` — 专有机制落地
7. 刷新 `_showAttrInfo` 调试文本

调用方：`PlayerWeaponsManager.EquipGroundWeapon`（本地）、`FriendWeaponView.ApplyUpgrade`（盟友）、`SelectRoleWnd`（预览）。

### 3.2 `ApplyAttrInfo(List<ModifyAttrData>, bool isAdd)`

- `isAdd=false` 表示**反向减**同一串数据（卸载 / 换模组），所以 modifys 必须**幂等可逆**：不要在里面写"绝对值覆盖"
- `Special` 分支：`m_Parameter` 里查不到该 `name` ⇒ `LogError("在属性字典里面没有找到自定义属性…")` 并跳过
- 普通分支：字典没有就 `new AttrInfo(this, type, name)`（从 cfg 取，缺项按 0 建 + Warning），再 `Modify`
- 每次 `Modify` 后 `ResetChangeValue()`（刷新 `ChangeValue` 基线）

### 3.3 `TryUpgrade(oldModify, newModify)` — **只给预览用**

先补齐新属性（加 `DefaultHide`），再对字典每项 `TryModify`：卸旧 → 装新 → 记录 `ChangeValue` → 还原。UI 悬停对比数值靠它，不落盘、不影响实际属性。

### 3.4 `ShowText(out List<(string,string,bool,bool)>)`

输出参数面板行：`(名称, 值文本, 是否预览新增, 是否受改动影响)`。

- `Special && Value==0 && ChangeValue==0` 直接跳过
- `TextOnly` ⇒ 第二列是 `"+"` / `"-"`，第一列是属性名（UI 渲染成 `+名称`）
- `ToString()` 负责百分比 / 倒数 / 穿甲档位 / 差异高亮 `+X`（绿/红）

### 3.5 生效的最后一环（最容易漏）

属性改动只改 `cfg`（`GameAttribute`）**不会自动影响子弹**。真正写回玩法要有人在 `WeaponPlayerController.InitAttribute()` 里 `Set(attr, damages.SetXxx)` —— 那里订阅 `OnFinalValueChange` 把值回写到 `DamageData`。**没被订阅的属性 = 只改 cfg、只看得到、打不出效果**。

---

## 4 编辑器工具

| 入口 | 位置 | 用途 |
|---|---|---|
| `Tools/武器升级编辑器` | `Assets/Editor/WeaponUpgradeEditorWindow.cs` | 主工作台：左侧列出 `Resources/Weapons` 全部武器，右侧 Tab（General / 武器参数 / 子弹参数 / **升级模组**）。升级区每行 = 一个 `Key`(Level)，横向卡片 ≤3，可拖拽 SO / 新建；模组区横向卡片，★ = `ActiveModule` |
| `Tools/数据编辑器` | `Assets/Editor/DataEditorWindow.cs` | `武器数据`（prefab，右面板走 EditorOverride）、`武器升级`、`武器模组` 三页；升级/模组页按父目录分组排序 |
| `[CustomPropertyDrawer(typeof(ModifyAttrData))]` | `Assets/Editor/Drawer/ModifyAttrDataDrawer.cs` | `modifys` 单行四列：Special 才显示 name（下拉来自**引用此 SO 的武器**的 `m_UniqueAttr`）；modifier 下拉按 `attributeConfigs` 过滤 |
| `[CustomEditor(typeof(WeaponUpgradeData_SO))]` | `Assets/Editor/WeaponUpgradeData_SOEditor.cs` | 升级数据美化检视器（标准 Inspector + 数据编辑器右面板）：标题卡 + 基础信息/费用/修改属性 分节 + **反向引用**（现算哪些武器 prefab 的 `Upgrade` 用了它，显示「解锁等级 X · 第 i 档 · 选项 j」并可定位）。反向关系**不序列化**，靠 `Resources.LoadAll<GameObject>("Weapons")` + `UpgradeCount/UpgradeLevel/GetUpgrade` 扫，`projectChanged` 时失效重算 |
| 资产创建 | 右键 `Create/Data/武器升级`、`Create/Data/武器模组` | 文件名前缀 `WUD_` / `WMD_` |

资产目录约定：

```
Assets/Resources/GameData/WeaponUpgrade/WUD_*.asset          ← 多把武器共用的通用项（弹药/伤害/射速/装弹）
Assets/Resources/GameData/WeaponUpgrade/<武器名>/WUD_*.asset ← 该武器专属
Assets/Resources/GameData/WeaponModule/WMD_None.asset         ← 唯一的空模组（每把武器的 Modules[0]）
Assets/Resources/GameData/WeaponModule/<武器名>/WMD_*.asset
```

### 4.1 通用升级复用规则（2026-10-28 定）

- 【必须】**多把枪都适用的收益只允许根目录那一份**，直接把它挂进各武器 prefab 的 `Upgrade` 里共用；不要再复制成 `WeaponUpgrade/<武器名>/WUD_Damage.asset`。复制之后同一份数值在 N 个地方各自漂移，改一处其余全部变成骗人的数据。
- 【必须】新增通用项放**根目录**，命名 `WUD_<概念>`（`WUD_Magazine` / `WUD_Accuracy`…），`cost=1000`、`icon` 取 `Resources/Images/SkillIcon/` 那套，与既有项对齐。
- 【必须】**改数值直接改资产内容，不要改名/移动**：文件名就是资产身份，改名会断开所有已挂载引用（`Upgrade` 里存的是 guid）。
- 判定归属：**通用** = 任意枪装上都不违和（弹药/弹匣/伤害/射速/装弹/散布/弹速/爆炸伤害/爆炸范围）；**专属** = 依赖该武器独有机制或节奏（`Special` 专有属性、蓄力/锁定/散热档位、特定玩法联动）⇒ 放 `<武器名>/`。
- 通用项只能改 `attributeConfigs` 里允许的修饰符，且属性要有真实基础值：`Ammo`/`Magazine`/`ShootInterval`/`ReloadTime` 全武器 cfg 都有；`BulletSpeed`/`DirectDamage`/`ExplosionDamage`/`ExplosionRange` 由 `InitAttribute` 从 `Damages` 自动补 ⇒ 这两类才适合做通用项。`CoolSpeed`/`HeatPerShot` 之类只有 `BeamRifle` 有基础值，别当通用项。
- 现有根目录池（**正在手调，禁止覆盖/重建**，只能新增）：
  `WUD_AmmoBag`(总弹药 ×1.4) · `WUD_Damage`(直伤 ×1.3) · `WUD_Damage20`(直伤 ×1.2) · `WUD_Grenade_Ammo`(弹药 +1) · `WUD_Grenade_Range`(弹速 ×1.25 + 投掷距离 +10) · `WUD_Rate`(射击间隔 ×0.85) · `WUD_Reload`(换弹 -0.5s)
  ＋ 2026-10-28 新增：`WUD_Magazine`(弹匣 +2) · `WUD_Accuracy`(散布 -25%) · `WUD_BulletSpeed`(弹速 +30%) · `WUD_ExplosionDamage`(爆炸伤害 +30%) · `WUD_ExplosionRange`(爆炸范围 +25%)
- 批量新增/重跑用 `.codebuddy/plans/gen_common_wud.py`（导入本 skill 的 `render_wud`/`guid_of` + `attr_whitelist` 校验，**只增不改**：已存在的资产直接 SKIP）。

---

## 5 标准工作流

### 5.1 新增一个升级选项

1. `Tools/武器升级编辑器` → 选武器 → 找到目标 Level 行（行号右侧的 `Key` = 解锁等级）
2. **先看根目录通用池**（§4.1）：通用收益直接 `+ 添加选项` 里挑现成 WUD_ 共用，不要为这把枪再复制一份；专属收益才 `新建选项...`（新建默认落在 `WeaponUpgrade/` 根，记得挪进 `<武器名>/`），**每档上限 3**
3. 填 `name`（中文名）/ `type`（分类文本）/ `icon` / `desc` / `cost`；`desc` 按 §1「文案分工」写成**诙谐风味文案**
4. `modifys` 逐条填：`type` 选属性 → `modifier` 从过滤后的下拉选 → `value`
   - 百分比收益用 `Factor`（`0.2` = +20%）；绝对值用 `Base`；层数/额外量常用 `Extra`
   - ⚠ **一律写强化**：`ShootInterval`/`ReloadTime`/`BulletsSpreadAngle` 这类"越低越好"的属性写负值（负值=增益），其余写正值；
     **不要在这里写任何代价** —— 代价属于模组（见 §1「数值方向」）
5. 属性若不在武器 `showAttr` ⇒ 补进去（不补也能生效，但参数面板看不到，玩家无从感知）
6. `Special` 类型见 5.3；要让新属性真正影响伤害见 5.4
7. 验证：进游戏 → 角色 → 武器 → 悬停卡片看预览数值 → 购买（确认后资源余额减少；买不起时确认按钮不出现）→ 选择 → 关闭窗口（触发 `Save` + `SendSelfLoadout`）

### 5.2 新增一个模组

1. 新建 `WMD_*` 资产（`WeaponModule/<武器名>/`）
2. 填 `name` / `type`（品质决定颜色与边框）/ `icon` / `desc`（`Key=true` 写收益、`false` 写代价，与 modifys 一一对应）。⚠ `desc` **只写效果、不写风味、不写具体数值**（用「大幅/显著/略微/相当多的」这类程度词，与 WUD 相反，见 §1「文案分工」）
3. `modifys`：**成对**写（一项正面 + 一项负面），保持"无暇/均衡/危险"的强度预期
4. 武器升级编辑器 → 模组区 `+ 添加模组`（追加到末尾）或拖拽
5. ⚠ **不要**把新模组插到 `Modules[0]`；`[0]` 必须是 `WMD_None`（UI 点当前模组 = 卸载回下标 0）
6. 验证：选中模组 → 参数面板对比 → 再点一次卸载 → 检查存档 `selectModuleIndex` 与盟友视角（联机见 §6）

### 5.3 新增一种专有机制（Special）— 三步缺一不可

```csharp
// 1) 武器 prefab 的 m_UniqueAttr 注册（name 必须逐字一致）
//    name="恐惧尾迹" value=0 tag=32(TextOnly)
// 2) 升级/模组的 modifys：type=Special, name="恐惧尾迹", modifier=Base, value=1
// 3) 消费端（二选一）
//    a. WeaponUniqueAttributeApplier.data 表内追加一条
//    b. 玩法侧主动查 weapon.GetAttr("恐惧尾迹")
```

- 表驱动的消费端（`Weapon/WeaponUniqueAttrApply.cs`）：`["激光制导射程"]`、`["恐惧尾迹"]`、`["电磁尾迹"]`、`["燃烧尾迹"]`、`["尾迹范围"]`、`["全自动模式"]` —— 在 `ApplyUpgrade` 末尾统一执行
- 查询式消费端：`VFXAirdropEffect` 读 `"取消空投准备时间"` / `"自费空投"` / `"空投降落速度"`；**新写法一律用 `weapon.GetAttr("名字")`**
- 没有任何消费端的 Special ⇒ 只在参数面板显示 `+名字`，不产生任何效果（"能看见但不生效"的头号原因）

### 5.4 新增一种可升级的基础属性

1. `WeaponAttrType` 加枚举值（挑未占用编号 + `[InspectorName("分组/中文名")]`）
2. `WeaponAttributeFactory.attributeConfigs` 加一行：`(isCurrent, tag, allowModifier)` —— 不加则 `GetTag` 返回 default、modifier 下拉不过滤、`Create` 直接 `LogError("找不到对应的配置")`
3. 消费端订阅：`WeaponPlayerController.InitAttribute()` 的 `Set(attr, damages.SetXxx)`（写回子弹数据）或对应控制器每次读 `AttrFinal(attr)`
4. 要在 UI 显示 ⇒ 武器 `showAttr` 增加；要改名/加标旗 ⇒ `m_RenameAttr`

### 5.5 给一把武器开整棵升级树

1. 按档位建 WUD_（通用收益可直接复用根目录的 `WUD_Ammo` / `WUD_Damage` / `WUD_Rate` / `WUD_Reload`）
2. prefab 的 `Upgrade` 按**解锁等级**升序加 `KVP`（编辑器 `+ 添加等级` 建的 Key 默认 = 当前数组长度，需手改成想要的等级门槛）
3. ⚠ 存档键 = `角色ID + "_" + WeaponName`：**改 `WeaponName` 等于换一把枪，旧存档的改装全丢**
4. ⚠ 增删档位会让旧存档的 `selectIndex` 长度与新档数不一致（见 §6 兼容）
5. 每档 1–3 个选项；留 `selectIndex=-1` 表示"该档空着"，是合法状态

---

## 6 存档与联机

### 6.1 存档结构（`ArchivesData_SO.cs`）

`weaponUpgradeDic : DisplayDic<string, WeaponUpgradeData>`，键 = `角色ID + "_" + 武器名`。

```csharp
class WeaponUpgradeData {
    int[] selectIndex;        // 每档选中项下标，-1 = 未选（长度 = Upgrade.Count）
    int[] buyIndex;           // 每档已购买位掩码：bit0..bit2，超过 3 项无法记录
    int selectModuleIndex;    // 选中的模组下标
    int level = 1;            // 武器等级（⚠ 老存档反序列化得 0，只能经 Level 属性读，自动钳到 ≥1）
    int exp;                  // 当前等级内经验（与角色同曲线 ExpMeet = 1000）
}
```

- 读：`TryGet(ID + "_" + weapon.WeaponName, new(ID + "_" + …, weapon.UpgradeCount().Length))`
- 涨：`GainWeaponExp(ID, gain)` 给**该角色身上 6 把武器**一起加（`Armor` 之类无改装树的跳过）
- 写：`SelectRoleWnd.HideWnd()` 里 `meetSave` ⇒ `ArchivesData_SO.Current.Save()` + `TeamNetBridge.SendSelfLoadout()`

### 6.2 联机（`NetTmp` 消息 → 盟友表现）

`PlayerLoadoutMsg`（`07NetGame/Msg/RoomMsg.cs`）：

- `[Key(1)] int[][] Upgrades` — 每类武器（下标 = `WeaponTypeEnum`）各档选中项
- `[Key(2)] int[] Modules` — 每类武器选中的模组下标（`-1` = 无）

链路：`ArchivesData_SO.GetWeaponUpgrade/GetWeaponModules` → `TeamNetBridge.ToLoadout` → `HandleLoadoutSync` → `PlayerData.Upgrades/weaponModules` → `FriendWeaponView.Setup(prefabs, upgrades, modules)` → `inst.ApplyUpgrade(select, module)`。

- ⚠ `FriendWeaponView` 会把 `module < 0` 夹成 0、`upgrades` 长度不足时早退；**而 `WeaponPlayerController.ApplyUpgrade` 本身不夹**，直接 `Modules[module]` ⇒ 越界即 `IndexOutOfRangeException`
- 新加一种需要跨端一致的改装字段 ⇒ 必须同时改 DTO + 发送端 + 落表 + 消费端，少一处就是"我看得到、别人看不到"

---

## 7 排错清单（症状 → 原因）

| 症状 | 原因 | 处理 |
|---|---|---|
| 参数面板看不到某条属性 | 不在 `showAttr`；或 `Special` 且 `Value==0 && ChangeValue==0`；或 tag 是 `IsHide` / `DefaultHide` 且值没变 | 补 `showAttr` / 改 tag / 确认确实有改动 |
| 升级/模组装了但数值没变 | 属性只在 `cfg` 里、没有消费端订阅（`InitAttribute` 的 `Set` 链外的属性） | 见 §3.5 / §5.4 |
| `在属性字典里面没有找到自定义属性X` | `Special` 的 `name` 没在武器 `m_UniqueAttr` 注册，或名字差一个字 | 逐字对齐注册名（含空格/前缀） |
| `不支持的Modifier类型` | modifys 的 `modifier` 不在该属性 `attributeConfigs` 允许集 | 用编辑器下拉重选（不要手改 asset） |
| 数值颜色/方向反了 | `FlipPlus`（越低越好）与给的数值符号打架 | 值按原语义写，改 tag 或换属性 |
| `IndexOutOfRangeException` 在 `ApplyUpgrade` | `module` 越界（存档是旧模组列表 / 联机资料不匹配） | 调用前夹到 `[0, Modules.Count-1]` |
| 新档位显示"锁定" | 武器 `Level < Upgrade[i].Key` | 改小 Key 或给武器加经验 |
| 每档第 4 个选项选不上/存不住 | `buyIndex` 只有 3 位（`GetBuy` 硬判 `x > 2`），UI 也是 3 列 | 每档最多 3 项 |
| 升级项带负收益（越升越弱） | 违反「WUD 只强化」（§1「数值方向」） | 改成纯强化，把它想表达代价的部分移到某个 `WMD_` 里 |
| 模组文案里出现具体数值（`+5`、`-23%`、`3°→6°`） | 违反「WMD 不写数值」（§1「文案分工」） | 换成程度词：「显著减少」「大幅提升」「略微变差」 |
| 新建的资产明明在、guid 也对，prefab 里引用却是 `null` | prefab 先于新资产被导入，坏引用被固化；而 prefab 文件本身没变 ⇒ Unity 不会重导它 | 强制重导 prefab：`AssetDatabase.ImportAsset(path, ForceUpdate \| ForceSynchronousImport)`；或再 refresh 一次。⚠ 批量新建+挂引用后必查这一条 |
| 模组装了盟友看不见 | `PlayerLoadoutMsg.Modules` 没带 / 改完没 `SendSelfLoadout` | 检查 DTO 与关窗保存链路 |
| 模组边框/配色不对 | 由 `type` 决定（`Frame_Module{type}.png` + `color`） | 核对 `ModuleType` 与图标资源存在 |
| 改了档数后老存档报越界 / 装备异常 | `selectIndex` 长度不随 `Upgrade.Count` 自动扩容 | 迁移或重置该武器的存档条目 |
| 确认购买后资源没减少 | 购买分支漏了扣费（`TipWnd` 只负责校验与显示，不扣） | 在 `optA_Click` 里调 `propertyManager.Pay(cost)`；新增任何消耗类操作都走这个入口 |
| 改 `WeaponName` 后改装全没了 | 存档键含武器名 | 不要改名，或做存档迁移 |

### 已知债务（改到这里时留意）

- `WeaponUniqueAttributeApplier["尾迹范围"]` 的实现是 `SetExplosionTypeElectric`（疑似复制粘贴残留），真正的"范围"没有落点
- ~~升级购买不扣资源~~ **已修（2026-10-27）**：扣费收口到 `PropertyManager.Pay(IList<SKVP<OOPartEnum,int>>)`，战备与武器升级同走一条
- `WMD_None.asset` 里残留旧字段（`targetAttr` / `m_UniqueAttr`），当前 SO 已无这些字段 ⇒ Unity 忽略，不要照抄
- `WeaponModuleData_SO.name` 遮蔽 Unity 内置 `name`，读代码时别把 `asset.name` 当显示名

---

## 8 关键文件索引

| 职责 | 文件 |
|---|---|
| 升级数据定义 | `Assets/Scripts/06Gameplay/Data/WeaponUpgradeData_SO.cs` |
| 模组数据定义 | `Assets/Scripts/06Gameplay/Data/WeaponModuleData_SO.cs` |
| 应用引擎（partial） | `Assets/Scripts/06Gameplay/Weapon/WeaponUpgradeController.cs` |
| 属性工厂 / 枚举 / 公式 | `Assets/Scripts/06Gameplay/Weapon/WeaponAttributeFactory.cs` |
| 属性容器（cfg） | `Assets/Scripts/06Gameplay/Weapon/WeaponCfg.cs` |
| 专有机制消费表 | `Assets/Scripts/06Gameplay/Weapon/WeaponUniqueAttrApply.cs` |
| 属性 → 子弹回写 | `Assets/Scripts/06Gameplay/Weapon/WeaponPlayerController.cs`（`InitAttribute`） |
| 改装界面 | `Assets/Scripts/10UI/Wnd/SelectRoleWnd.cs`（`#region 武器界面` / `#region 模组相关`） |
| 存档 | `Assets/Scripts/06Gameplay/Data/ArchivesData_SO.cs`（`WeaponUpgradeData` / `GetWeaponUpgrade` / `GetWeaponModules`） |
| 本地装备应用 | `Assets/Scripts/06Gameplay/Player/PlayerWeaponsManager.cs`（`EquipGroundWeapon`） |
| 盟友应用 | `Assets/Scripts/06Gameplay/Player/Controller/FriendWeaponView.cs` |
| 联机同步 | `Assets/Scripts/09Manager/Global/TeamNetBridge.cs`、`07NetGame/Msg/RoomMsg.cs` |
| 可视化编辑器 | `Assets/Editor/WeaponUpgradeEditorWindow.cs` |
| modifys 绘制 | `Assets/Editor/Drawer/ModifyAttrDataDrawer.cs` |
| 数据编辑器页 | `Assets/Editor/DataTabs/Weapon{,Module,Upgrade}TabModule.cs` |

## 9 工具：批量生成 / 校验（`scripts/`）

> 场景：给一把新武器铺整棵升级树 + 一组模组，或批量调数值。手写 40 个 `WUD/WMD + meta`、再在 prefab 的 YAML 里做 4 处替换，既慢又容易静默改错 —— 用这几个脚本。

| 脚本 | 作用 |
|---|---|
| `scripts/gen_weapon_tree.py` | spec(JSON) → `WUD_*.asset` / `WMD_*.asset` + `.meta` + prefab 补丁（`cfg._fakeAttrs` / `showAttr` / `Upgrade` / `Modules` + `ActiveModule`）。幂等、断言锚点唯一命中、写前 `.bak` |
| `scripts/verify_weapon_tree.py` | 静态校验（不依赖 Unity）：资产逐字节比对、guid 唯一且与派生值一致、prefab 引用集合、`showAttr`/`cfg` 回读，退出码可挂 hook |
| `scripts/attr_whitelist.py` | 从 C# 源码解析 `WeaponAttrType` 编号、`attributeConfigs` 允许的修饰符、`InitAttribute` 会自动补 base 的属性集合 |
| `scripts/examples/Logic.json` | 现成范例（`Logic.prefab` 的 12 升级 + 7 模组），同时是回归用例 |

```bash
python scripts/gen_weapon_tree.py --spec scripts/examples/Logic.json --dry-run   # 只看差异
python scripts/gen_weapon_tree.py --spec scripts/examples/Logic.json             # 写入
python scripts/verify_weapon_tree.py --spec scripts/examples/Logic.json          # 校验
```

生成前会拦掉五类错（全是本项目真实踩过的）：

1. 属性名拼错，或不在 `attributeConfigs` 里；
2. 修饰符不被该属性允许（例：`WeaknessBonus` 只收 `Extra`）—— 否则要到运行时才报 `不支持的Modifier类型`；
3. **属性没有真实基础值**（既不在 `cfg._fakeAttrs`、也不由 `InitAttribute` 从 `Damages` 补）⇒ `Factor`/`Extra` 都在 0 上做文章，装了完全没效果；
4. **`WUD_` 里出现弱化项**（按属性方向判定，见 §1「数值方向」）—— 升级只强化，代价放模组；
5. 每档超过 3 项（`buyIndex` 只有 3 位）；不足 3 项会给"存档 `selectIndex` 悬空"的警告。

**写 spec 时属性一律用枚举名**（`BulletsSpreadAngle` 而不是 `7`），编号由工具查源码；`value` 用 `Base` 求精确值、用 `Factor` 表达百分比（`0.3` = +30%，`-0.3` = -30%，注意本项目是 `(base+ΣBase)*(1+ΣFactor)` 的加和再乘）。

硬规则：脚本**只碰资产与 prefab**，不改 `.cs`、不做改名/删除；跑完仍要让 Unity `refresh` 一次，并按 §3.5 确认数值真的落到子弹上。
⚠ 如果是**这批新建**的资产被 prefab 引用（不是复用已有资产），refresh 之后**还要强制重导一次 prefab**，否则用到新条目全是 `null`（见 §7 最后两行）。

spec 里的 `desc` 也受 §1「文案分工」约束：`WUD.desc` 写风味（诙谐），`WMD.desc` 只写效果（绿 ↑/红 ↓ 行）**且不写具体数值**（改用程度词）。
⚠ 这两条**没有**自动化校验（工具只管属性/修饰符/base/弱化项），写 spec 时要自己守。

## Resources

- `references/weapon-upgrade-guide.md`：完整属性编号表、`attributeConfigs` 全量条目、`AttrInfo`/`ShowText` 细节、编辑器窗口结构、代码片段与实测样例
- `references/drg-upgrade-design.md`：外部对标（Deep Rock Galactic 5 层升级 + Clean/Balanced/Unstable 超频）与本项目 `Upgrade`/`Modules` 的结构对照、可借鉴的 6 条设计原则、一把枪的数据规模与坑；明细数据在 `.codebuddy/notes/drg-weapons-upgrades.json`
