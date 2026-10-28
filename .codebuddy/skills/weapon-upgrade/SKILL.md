---
name: weapon-upgrade
description: Bluedivers 项目「武器升级 / 武器模组」系统专用开发指南。当需要新增或修改武器升级项（WUD_*）、武器模组（WMD_*）、在武器 prefab 的 Upgrade/Modules 列表上挂数据、调整武器属性数值与显示（WeaponAttrType / ModifierType / AttrTag）、实现新的专有机制（Special 属性 + WeaponUniqueAttributeApplier）、排查"升级项不生效/数值不变/参数面板看不到/模组装了别人看不见/名字对不上/越界报错"、或改动 SelectRoleWnd 武器改装界面、ArchivesData_SO 武器改装存档、PlayerLoadoutMsg 改装同步时触发。提供数据模型、属性引擎公式、应用链路、编辑器工具、标准工作流与避坑清单。
---

# Bluedivers 武器升级 / 武器模组开发指南

> 整套系统只做一件事：**给武器属性打增量补丁**。
> 本文件是操作手册；属性全表、引擎细节、编辑器内部结构、实测样例在 `references/weapon-upgrade-guide.md`。

## 0 速查（先读这一节）

- `WeaponUpgradeData_SO`（`WUD_`）= 一个**升级选项**；`WeaponModuleData_SO`（`WMD_`）= 一个**模组**。两者 `modifys` 格式相同、走同一套引擎。
- **升级树不在 SO 里，在武器 prefab 上**：`Upgrade`（`List<KVP<解锁等级, List<WUD_>>>`）、`Modules`、`ActiveModule`、`showAttr`、`m_UniqueAttr`。

| | 武器升级（WUD_） | 武器模组（WMD_） |
|---|---|---|
| 结构 | 按等级分档，每档单选（≤3 项） | 整组互相替换，同一时刻只装一个 |
| 解锁 | 武器等级 ≥ `KVP.Key`；已购买才能选 | 无门槛（可选子集由 prefab 的 `Modules` 决定） |
| 代价 | `cost`（`OOPartEnum` 资源） | 无 |
| 存档 | `selectIndex`（每档选了第几项）+ `buyIndex`（位掩码） | `selectModuleIndex`（一个下标） |
| 数值方向 | **只强化**（§0.1） | **看品质**（§0.2） |
| 表现 | 卡片图标 + tooltip | 边框色 + `desc` 的 ↑绿/↓红 列表 |

### 0.1 数值方向（硬规则）：WUD 只准强化

**武器升级不许出现任何让武器变弱的项** —— 弹药、弹匣、装填、射速、伤害、精度、穿甲一律只加不减。
想让玩家"变强但付代价"的，一律做成**模组**，那里才是权衡的地方。

判"强化"看**属性方向**，不看正负号：

- 带 `AttrTag.FlipPlus` / `Reciprocal` 的属性（`ShootInterval` / `ReloadTime` / `BulletsSpreadAngle` / `HeatPerShot` / `CoolDelay` / `OverheatDuration` / `LockInterval` / `Recoil` / `NoiseScale` …）⇒ **负值是强化**；
- 其余属性（`Ammo` / `Magazine` / `DirectDamage` / `DirectAP` / `WeaknessBonus` / `DirectDestruction` / `MoveSpeedToShoot` …）⇒ **正值是强化**。

`scripts/gen_weapon_tree.py` 已按这条自动拦（WUD 出现弱化项直接 `[ERROR]` 不写盘；WMD 不拦）。

### 0.2 品质口径（填 `type` 前对一遍）

- **无暇 `Clean` ≈ 第六层升级**：纯增益、零代价，玩家就当"白送一档 T6"。数值按2-3档升级的量给。
- **均衡 `Balanced` = 稍微有权衡的特化**：一正一负成对，正项指向某条打法（弱点 / 穿透 / 爆发 / 续航），负项要能被玩家感知（弹匣 / 装填 / 散布 / 后坐力 / 移速）。
- **危险 `Unstable` = 大幅改变玩法的创意位**：允许动机制与行为（全自动、弹种、消耗方式、换弹节奏），创意优先；代价可以很重，甚至直接砍掉某条既有优势。
- 判据是**"这模组装上以后玩家要不要改变打法"**：不改 = Clean；改一点取舍 = Balanced；必须换一种打法 = Unstable。

### 0.3 文案分工（别混）

- **`WUD_`（升级卡）**：`desc` 是卡面 tooltip ⇒ **风味化、诙谐**，效果点到为止。
  句式学 DRG 原版——先平铺直叙说效果，再补一句自嘲/吐槽（工程部、管理层、"别问"、"见不得光的改动"、"安全去他的"）；**少用「不是…而是…」这类对称句式**，读起来像 AI 写的。
  参考：`WUD_Damage`「工程部加班造成的身体与精神上的伤害，换取了武器的整体伤害提升。」、`WUD_Rate`「…别多问，这种东西小范围享受就可以了。也别让优香知道」。
- **`WMD_`（模组）**：`desc` 是绿 ↑ / 红 ↓ 的**效果行**，玩家读的是增减 ⇒ **只写效果，不写风味、不写具体数值**。
  用程度词表达方向与量级：`大幅提升 / 显著提高 / 略微增加 / 相当多的弹药 / 射速更快 / 装填更慢 / 散布变差`。
  ⚠ 不许出现 `+5`、`-23%`、`3°→6°`、`100%→60%` 这类数字 —— 具体数值让玩家去看参数面板。
  正例「射速大幅提升」「弹匣容量显著减少」；反例「射速 -23%」「散布 3°→6°」。参考 `WMD_AggressiveVenting`。
- 不写数值的附带好处：`modifys` / cfg 基线调整后**不用回头同步文案**，不会出现"文案写 70%→30%、实际已是 100%→60%"这种错话。

### 0.4 资产归属：通用收益只留根目录那一份

**多把枪都适用的收益，只允许根目录那一份**，直接挂进各武器 prefab 的 `Upgrade` 共用。复制成 `WeaponUpgrade/<武器名>/WUD_Damage.asset` 之后，同一份数值会在 N 个地方各自漂移。细则与现有池见 §4.1。

### 0.5 一次改动的标准收尾（漏一步就会"看着对、跑不起来"）

1. `gen_weapon_tree.py --dry-run` 看差异 → 写入；
2. Unity 里 `AssetDatabase.ImportAsset(prefab, ForceUpdate | ForceSynchronousImport)` —— **这批新建资产被 prefab 引用时要导两次**（一次不够，`Modules[1..]` 仍为 null）；
3. 用 `SerializedObject` 回读 `Upgrade` / `Modules` / `m_UniqueAttr`，确认 `objectReferenceInstanceIDValue != 0`；**别信文件文本**；
4. `Instantiate` 一把，反射调 `InitAttribute()` 再 `ApplyUpgrade(sel, module)`，读 `ShowText` 核对数值 —— **每个组合用独立实例**（同一实例连跑会叠加，见 §7）；
5. 进 Play 实测：悬停预览 → 购买/选择 → 参数面板变化 → 关窗重开仍在 → 盟友视角看到同样改装。

---

## 1 数据模型

### 1.1 `WeaponUpgradeData_SO`（`Assets/Scripts/06Gameplay/Data/`）

| 字段 | 类型 | 语义 / 陷阱 |
|---|---|---|
| `name` | `new string` | **中文显示名**。它遮蔽了 `ScriptableObject.name`（= asset 文件名），两者互不相干 |
| `type` | `string` | 自由分类文本（"武器散布""费用"…），只用于 tooltip 的"类型"行 |
| `icon` | `Sprite` | `[SpritePreview(3,3)]`；UI 卡片与存档预览都用它 |
| `desc` | `string` | `[TextArea]`，可含 `\r\n`。文案规则见 §0.3 |
| `cost` | `List<SKVP<OOPartEnum,int>>` | 默认 `Pyroxene 1000`；确认购买时经 `PropertyManager.Pay(cost)` 扣除（**扣费唯一入口**，别再手写 SetCount 循环） |
| `modifys` | `List<ModifyAttrData>` | 拼写就是 `modifys`（不是 modifies）。方向见 §0.1 |

### 1.2 `WeaponModuleData_SO`

| 字段 | 类型 | 语义 / 陷阱 |
|---|---|---|
| `name` / `icon` | 同上 | |
| `type` | `ModuleType` | `None/Clean/Balanced/Unstable` ⇒ 决定 `color`、`typeName`、边框图 `Icon/Frame_Module{type}`。口径见 §0.2 |
| `desc` | `List<SKVP<bool,string>>` | `Key=true` = 增益（绿 ↑），`false` = 代价（红 ↓）。文案规则见 §0.3 |
| `modifys` | `List<ModifyAttrData>` | 与升级同格式 |
| ~~`frame`~~ | — | 2026-10-01 已移除（数据层不许要资源）；边框由表现侧 `SelectRoleWnd.LoadModuleFrame` 按 `type` 查表 |

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

同一文件的两个配套结构：

- `UniqueAttrData`（prefab 的 `m_UniqueAttr`）= **专有属性注册表**：`name` / `value`（默认值）/ `tag`（显示标旗）
- `RenameAttrData`（prefab 的 `m_RenameAttr`）= 改显示名 + 追加/移除标旗

⚠ `name` 在 `ApplyAttrInfo` 里是**字典键**：`m_RenameAttr` 改过的属性，key 就是改后的中文名；`Special` 用 `ModifyAttrData.name`。升级项写旧名 ⇒ 取不到。

### 1.4 prefab 上的挂载结构

```
WeaponPlayerController (prefab)
├─ showAttr       : List<WeaponAttrType>       // 参数面板显示哪些属性（默认值来源）
├─ m_RenameAttr   : List<RenameAttrData>       // 属性改中文名 / 加 tag
├─ m_UniqueAttr   : List<UniqueAttrData>       // 专有属性注册（Special 必须先在这里）
├─ Upgrade        : List<KVP<int, List<WUD_>>> // Key = 解锁所需武器等级，每档 ≤3 个
├─ Modules        : List<WMD_>                 // ⚠ [0] 必须是 WMD_None（空模组）
└─ ActiveModule   : WMD_                        // 编辑器里的"当前展示"，运行时被 ApplyUpgrade 覆盖
```

`Upgrade` 的 `Key` **不是等级序号而是解锁等级门槛**（BeamRifle 实测 2/4/8/12；UI 判 `archWeaponData.Level < Key` 即锁定）。

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
| `Extra` | 4 | 最后叠加的额外值（允许集常只开这一种） | `+1` 锁定层数 |

⚠ `value` 的符号只表示加减；"越低越好"的属性靠 `AttrTag.FlipPlus` 反色显示，**不要为了变绿去反写符号**。

### 2.2 唯一注册表：`WeaponAttributeFactory.attributeConfigs`

`Dictionary<WeaponAttrType, (bool isCurrentAttr, AttrTag tag, ModifierType allowModifier)>`，三条信息全在这里：

- `isCurrentAttr` = 建 `GameCurrentAttribute`（带 `CurrValue`，受 `LimitCurr`/`UpdateToZero` 影响：换弹、热量、蓄力）
- `tag` = 显示规则（§2.3）
- `allowModifier` = 该属性允许的修饰符；不在集合里的 modifier 运行期 `LogError("不支持的Modifier类型")`，编辑器下拉也已过滤

**新增属性 = 枚举 + 这张表 + 消费端订阅**，缺一张就不生效（§5.4）。全量表见 `references/weapon-upgrade-guide.md` §2。

### 2.3 `AttrTag` 显示语义

| Tag | 位 | 效果 |
|---|---|---|
| `Percentage` | 1<<0 | 显示百分比（相对 Prime） |
| `Reciprocal` | 1<<1 | 显示倒数（射速类：间隔 ↓ 显示为 ↑） |
| `FlipPlus` | 1<<2 | 反转红/绿（越低越好类） |
| `DefaultHide` | 1<<3 | 值 == Prime 且未被改动时隐藏 |
| `OneHide` | 1<<4 | 值 == 1 时隐藏（倍率类） |
| `TextOnly` | 1<<5 | 不显示数值，只在末尾显示 `+名称` / `-名称`（专有机制常用，位值 32） |
| `IsHide` | 1<<6 | 永远不显示 |
| `ScaleFromPrime` | 1<<7 | 相对原始值的百分比 |
| `APGrade` | 1<<8 | 显示成 `(中穿)3` 形式 |
| `OnlyInt` / `UpdateToZero` / `LimitCurr` | 1<<10/11/12 | 整数化 / 重算时 Curr 归零 / Curr 限制在 `[0,Final]` |

### 2.4 `WeaponAttrType` 编号分段（新增时挑未占用值）

| 段 | 内容 |
|---|---|
| 0–16 | 通用（Ammo/Magazine/ShootInterval/弹丸数/散布/装填）、散热（Heat/Cool/Overheat） |
| 17–18 | 手感倍率（`NoiseScale` 噪声 / `Recoil` 后坐力，基础值恒为 1，由 `InitAttribute` 注入） |
| 20–26 | 伤害（DirectDamage / ExplosionDamage / ExplosionRange / BulletSpeed / DirectAP / ExplosionAP） |
| 40 | 激光（LaserWaitTime） |
| 50–57 | 蓄力（段数 / 耗弹 / 时长 / 三档倍率） |
| 60–64 | 锁定（距离 / 半径 / 层数 / 每敌层数 / 间隔） |
| 100–102 | 破坏系数（直击/爆炸）、弱点加成 |
| 200–211 | **直击**伤害类型系数：200 动能 / 205 燃烧 / 211 骇入… |
| 220–231 | **爆炸**伤害类型系数：与直击组一一对应、间隔 20（220 动能 / 225 燃烧 / 231 骇入） |
| 999 | `Special` 专有属性（不进 `attributeConfigs`，靠 `m_UniqueAttr` 注册） |

注：`3 Catapult` 在 `attributeConfigs` 里**没有条目**，用它必须先补表，否则 `Create` 直接报错返回 null。

---

## 3 运行时应用链路

`WeaponUpgradeController.cs`（`partial class WeaponPlayerController`）是全部入口。逐步细节见 `references/weapon-upgrade-guide.md` §3。

### 3.1 `ApplyUpgrade(int[] select, int module)` — 权威入口

1. 重建 `m_Parameter`：`showAttr` 每项按 `GetAttrName(type)` 建 `AttrInfo`（值取自 `cfg`，缺项兜底 0 + Warning 带键表）
2. 补 `DirectAP` / `ExplosionAP` 的 cfg 初值（预览实例可能没跑过 `InitAttribute`）
3. `m_UniqueAttr` 每项建 `AttrInfo(name, value, tag)`（**不进 cfg**，独立 `GameAttribute`）
4. 对 `select[i] >= 0` 逐档 `ApplyUpgrade(y, x)`（= `ApplyAttrInfo(modifys, true)`）
5. `Modules` 非空 ⇒ `ApplyModule(旧下标, module)` 并写 `ActiveModule`
6. `WeaponUniqueAttributeApplier.Apply(this, m_Parameter)` — 专有机制落地
7. 刷新 `_showAttrInfo` 调试文本

调用方：`PlayerWeaponsManager.EquipGroundWeapon`（本地）、`FriendWeaponView.ApplyUpgrade`（盟友）、`SelectRoleWnd`（预览）。

### 3.2 其余入口（一句话版）

- `ApplyAttrInfo(data, isAdd=false)` = **反向减**同一串 ⇒ modifys 必须**幂等可逆**，不要写"绝对值覆盖"
- `TryUpgrade(old, new)` = 只给 UI 悬停预览用：卸旧→装新→记 `ChangeValue`→还原，不动 `FinalValue`
- `ShowText` = 参数面板行；`Special` 且 `Value==0 && ChangeValue==0` 直接跳过；`TextOnly` 渲染成 `+名称`

### 3.3 生效的最后一环（最容易漏）

属性改动只改 `cfg`，**不会自动影响子弹**。真正写回玩法要有人在 `WeaponPlayerController.InitAttribute()` 里 `Set(attr, damages.SetXxx)`（订阅 `OnFinalValueChange` 回写 `DamageData`）或控制器每次读 `AttrFinal(attr)`。**没被订阅的属性 = 只改 cfg、只看得到、打不出效果**。

---

## 4 编辑器工具与资产目录

| 入口 | 位置 | 用途 |
|---|---|---|
| `Tools/武器升级编辑器` | `Assets/Editor/WeaponUpgradeEditorWindow.cs` | 主工作台：左侧列 `Resources/Weapons` 全部武器，右侧 Tab（General / 武器参数 / 子弹参数 / **升级模组**）。升级区每行 = 一个 `Key`，横向卡片 ≤3，可拖拽 SO / 新建；模组区横向卡片，★ = `ActiveModule` |
| `Tools/数据编辑器` | `Assets/Editor/DataEditorWindow.cs` | `武器数据`（prefab，右面板走 EditorOverride）、`武器升级`、`武器模组` 三页，按父目录分组排序 |
| `ModifyAttrData` 绘制 | `Assets/Editor/Drawer/ModifyAttrDataDrawer.cs` | 单行四列；`Special` 才显示 name（下拉来自**引用此 SO 的武器**的 `m_UniqueAttr`）；modifier 下拉按 `attributeConfigs` 过滤 |
| `WUD_` 检视器 | `Assets/Editor/WeaponUpgradeData_SOEditor.cs` | 标题卡 + 分节 + **反向引用**（现算哪些 prefab 的 `Upgrade` 用了它，显示「解锁等级 X · 第 i 档 · 选项 j」，可定位）。反向关系**不序列化**，`projectChanged` 时重扫 |
| 资产创建 | 右键 `Create/Data/武器升级`、`Create/Data/武器模组` | 文件名前缀 `WUD_` / `WMD_` |

```
Assets/Resources/GameData/WeaponUpgrade/WUD_*.asset          ← 多武器共用的通用项
Assets/Resources/GameData/WeaponUpgrade/<武器名>/WUD_*.asset ← 该武器专属
Assets/Resources/GameData/WeaponModule/WMD_None.asset         ← 唯一的空模组（每把枪的 Modules[0]）
Assets/Resources/GameData/WeaponModule/<武器名>/WMD_*.asset
```

⚠ 编辑器 `+ 添加等级` 建的 `Key` 默认 = 当前数组长度，**不是解锁等级**，建完要手改。

### 4.1 通用升级池（复用，禁止覆盖/重建，只能新增）

- 【必须】**改数值直接改资产内容，不改名/不移动**：文件名就是身份，`Upgrade` 里存的是 guid，改名会断开所有引用。
- 【必须】新增通用项放**根目录**，命名 `WUD_<概念>`，`cost=1000`、`icon` 取 `Resources/Images/SkillIcon/` 那套，与既有项对齐。
- 归属判定：**通用** = 任意枪装上都不违和（弹药 / 弹匣 / 伤害 / 射速 / 装填 / 散布 / 弹速 / 爆炸伤害 / 爆炸范围）；**专属** = 依赖该枪独有机制或节奏（`Special`、蓄力/锁定/散热档位、特定玩法联动）⇒ 放 `<武器名>/`。
- 通用项要求属性**有真实基础值**：`Ammo`/`Magazine`/`ShootInterval`/`ReloadTime` 全武器 cfg 都有；`BulletSpeed`/`DirectDamage`/`ExplosionDamage`/`ExplosionRange` 由 `InitAttribute` 从 `Damages` 补 ⇒ 这两类适合做通用项。`CoolSpeed`/`HeatPerShot` 只有 `BeamRifle` 有基础值，别当通用项。
- 【必须】通用项上枪前先确认**这把枪真有这个杠杆**：热量武器（`HeatPerShot > 0`）没有"装填/备弹"概念（`Ammo=0`、`Magazine` 是能量池）⇒ `WUD_AmmoBag`/`WUD_Magazine`/`WUD_Reload` 全是死卡片；自动换弹武器同理不吃 `ReloadTime`（见 §7）。
- 现有池：`WUD_AmmoBag`(总弹药 ×1.4) · `WUD_Damage`(直伤 ×1.3) · `WUD_Damage20`(直伤 ×1.2) · `WUD_Rate`(射击间隔 ×0.85) · `WUD_Reload`(换弹 -0.5s，⚠ 自动换弹武器无效，见 §7) · `WUD_Magazine`(弹匣 +2) · `WUD_Accuracy`(散布 -25%) · `WUD_BulletSpeed`(弹速 +30%) · `WUD_HardenedRounds`(穿甲 +1，cost 2500) · `WUD_ExplosionDamage` / `WUD_ExplosionRange` · `WUD_Grenade_Ammo` / `WUD_Grenade_Range`（投掷物专用）
- 批量新增用 `.codebuddy/plans/gen_common_wud.py`（只增不改，已存在直接 SKIP）。

---

## 5 标准工作流

### 5.1 新增一个升级选项

1. `Tools/武器升级编辑器` → 选武器 → 找目标 Level 行（行号右侧 `Key` = 解锁等级）
2. **先看根目录通用池**（§4.1）：通用收益直接挑现成 WUD_ 共用；专属收益才 `新建选项...`（新建默认落 `WeaponUpgrade/` 根，记得挪进 `<武器名>/`），**每档上限 3**
3. 填 `name` / `type` / `icon` / `desc`（文案见 §0.3）/ `cost`
4. `modifys` 逐条填：属性 → modifier（下拉已按允许集过滤）→ value
   - 百分比用 `Factor`（`0.2` = +20%）；绝对值用 `Base`；层数/额外量用 `Extra`
   - ⚠ 只写强化（§0.1），代价属于模组
5. 属性不在武器 `showAttr` ⇒ 补进去（不补也生效，但玩家无从感知）
6. `Special` 见 §5.3；要让新属性真正影响伤害见 §5.4
7. 验证按 §0.5

### 5.2 新增一个模组

1. 新建 `WMD_*` 资产（`WeaponModule/<武器名>/`）
2. 填 `name` / `type`（品质见 §0.2）/ `icon` / `desc`（`Key=true` 写收益、`false` 写代价，与 modifys 一一对应；文案见 §0.3）
3. `modifys`：**无暇 = 纯增益**（唯一可以不成对）／**均衡 = 一正一负**／**危险 = 敢改玩法，代价可重**
4. 武器升级编辑器 → 模组区 `+ 添加模组`（追加末尾）或拖拽
5. ⚠ **不要**插到 `Modules[0]`；`[0]` 必须是 `WMD_None`（UI 点当前模组 = 卸载回下标 0）
6. 验证：选中 → 参数面板对比 → 再点一次卸载 → 看存档 `selectModuleIndex` 与盟友视角

### 5.3 新增一种专有机制（Special）— 三步缺一不可

```csharp
// 1) 武器 prefab 的 m_UniqueAttr 注册（name 必须逐字一致，tag = 32 即 TextOnly）
// 2) 升级/模组的 modifys：type = Special, name = "恐惧尾迹", modifier = Base, value = 1
// 3) 消费端（二选一）
//    a. WeaponUniqueAttrApplier.data 表内追加一条
//    b. 玩法侧主动查 weapon.GetAttr("恐惧尾迹")
```

- 表驱动（`Weapon/WeaponUniqueAttrApply.cs`）：`激光制导射程` · `恐惧尾迹` · `电磁尾迹` · `燃烧尾迹` · `尾迹范围` · `全自动模式`，在 `ApplyUpgrade` 末尾统一执行
  ⚠ 前五个都写 `Damages[1]` —— **只有一条 `Damages` 的武器（如 ArcPistol）挂它们会 `IndexOutOfRangeException`**；这种枪只能用 `全自动模式` 这类不碰 `Damages` 的
- 查询式：`VFXAirdropEffect` 读 `取消空投准备时间` / `自费空投` / `空投降落速度`；**新写法一律 `weapon.GetAttr("名字")`**（取不到返回 0，写错名字 = 静默失效）
- 没有任何消费端的 Special ⇒ 只在面板显示 `+名字`，不产生效果（"能看见但不生效"的头号原因）

### 5.4 新增一种可升级的基础属性

1. `WeaponAttrType` 加枚举值（挑未占用编号 + `[InspectorName("分组/中文名")]`）
2. `WeaponAttributeFactory.attributeConfigs` 加一行 `(isCurrent, tag, allowModifier)` —— 不加则 `GetTag` 返 default、modifier 下拉不过滤、`Create` 直接 `LogError("找不到对应的配置")`
3. 消费端订阅：`InitAttribute()` 的 `Set(attr, damages.SetXxx)`，或控制器每次读 `AttrFinal(attr)`
4. 要显示 ⇒ 加 `showAttr`；要改名/加标旗 ⇒ `m_RenameAttr`

### 5.5 给一把武器开整棵升级树

1. 先按职责排档（学 DRG，玩家会形成肌肉记忆）：**T1 手感/容量**（弹匣、射速、装填、散布）→ **T2 伤害/弹药** → **T3 机制型**（穿透、弱点、换弹方式）→ **T4 专用乘区**（弱点/破甲/容量）→ **T5 签名**（改行为的强效果）。别 5 档全是 +伤害。
2. 按档建 WUD_（通用收益直接复用根目录，如 `WUD_AmmoBag` / `WUD_Damage` / `WUD_Rate`），每档 1–3 项
3. prefab 的 `Upgrade` 按**解锁等级升序**加 `KVP`，手改 `Key` 成目标门槛
4. ⚠ 存档键 = `角色ID + "_" + WeaponName`：**改 `WeaponName` 等于换一把枪，旧存档改装全丢**
5. ⚠ 增删档位会让旧存档 `selectIndex` 长度不匹配（§6）
6. 留 `selectIndex = -1` 表示"该档空着"，是合法状态

---

## 6 存档与联机（要点）

`weaponUpgradeDic : DisplayDic<string, WeaponUpgradeData>`，键 = `角色ID + "_" + 武器名`。

```csharp
class WeaponUpgradeData {
    int[] selectIndex;        // 每档选中项，-1 = 未选（长度 = Upgrade.Count）
    int[] buyIndex;           // 每档已购买位掩码 bit0..bit2（>3 项存不住）
    int selectModuleIndex;    // 选中的模组下标
    int level = 1;            // ⚠ 老存档反序列化得 0，只能经 Level 属性读（自动钳 ≥1）
    int exp;                  // 当前级内经验（满 ExpMeet = 1000 升一级，与角色同曲线）
}
```

- 写：`SelectRoleWnd.HideWnd()` 里 `meetSave` ⇒ `ArchivesData_SO.Current.Save()` + `TeamNetBridge.SendSelfLoadout()`
- 涨：`GainWeaponExp(ID, gain)` 给该角色身上 6 把武器一起加（`Armor` 等无改装树的跳过）
- 联机：`PlayerLoadoutMsg` 的 `[Key(1)] int[][] Upgrades`（按 `WeaponTypeEnum`）+ `[Key(2)] int[] Modules`（`-1` = 无）→ `FriendWeaponView.Setup` → `inst.ApplyUpgrade(select, module)`
- ⚠ `FriendWeaponView` 会把 `module < 0` 夹成 0、`upgrades` 长度不足时早退；**而 `ApplyUpgrade` 本身不夹**，直接 `Modules[module]` ⇒ 越界即 `IndexOutOfRangeException`
- ⚠ 空数组 = "没带这条"，接收端不会清空旧值；要"清空模组"必须靠 `-1`/`0` 语义
- 新加一种需要跨端一致的改装字段 ⇒ 必须同时改 DTO + 发送端 + 落表 + 消费端，少一处就是"我看得到、别人看不到"

---

## 7 排错清单（症状 → 原因）

| 症状 | 原因 | 处理 |
|---|---|---|
| 参数面板看不到某条属性 | 不在 `showAttr`；或 `Special` 且 `Value==0 && ChangeValue==0`；或 tag 是 `IsHide`/`DefaultHide` 且值没变 | 补 `showAttr` / 改 tag / 确认真有改动 |
| 升级/模组装了但数值没变 | 属性只在 `cfg` 里、没有消费端（`InitAttribute` 的 `Set` 链外） | §3.3 / §5.4 |
| **换弹类升级"装了没用"** | 自动换弹武器（`WeaponFlag` 含 `AutomaticReload`）的 `ReloadTime` 由 `LogicInit` 以 0 兜底建出、没有消费端 | 换弹只能改 `AutoReloadSpeed`（All）或 `AutoReloadTime`（**只吃 Extra**）；`WUD_Reload` 别挂这类枪 |
| **热量武器（`HeatPerShot > 0`）挂通用池的"装填/弹药"项没效果** | 它的节奏由热量决定：`Ammo` 常为 0（无后备）、`Magazine` 是整条能量池 ⇒ `WUD_AmmoBag`(按 Ammo 比例) / `WUD_Magazine`(+2 在 128 的池里没感觉) / `WUD_Reload` 全是死项 | 只用热量四件套：`HeatPerShot`(FlipPlus 负值) / `CoolDelay`(FlipPlus 负值) / `CoolSpeed`(正值) / `OverheatDuration`(FlipPlus 负值) |
| **同一实例连续 `ApplyUpgrade` 后数值越叠越高** | 它只 add 不 remove，`cfg` 的 modifier 会留着（换档靠 `RemoveUpgrade`） | 验数值时每个组合用独立实例 |
| 预览面板里 `直击伤害` 恒为 0 + 刷一条"cfg 缺少 X" | `SelectRoleWnd` 的预览实例**不跑 `InitAttribute`**，`DirectDamage` 类属性只在 `cfg` 里没值 | 属既有行为；预览只看相对变化，别拿它当伤害基准 |
| `在属性字典里面没有找到自定义属性X` | `Special` 的 `name` 没在 `m_UniqueAttr` 注册，或差一个字 | 逐字对齐注册名（含空格/前缀） |
| Special 显示 `+名字` 但没效果 | 没有消费端；或消费端写死 `Damages[1]` 而该枪只有 1 条 | §5.3 |
| `不支持的Modifier类型` | modifier 不在该属性 `attributeConfigs` 允许集（典型：`WeaknessBonus`/`AutoReloadTime`/`LockLayers` 只收 `Extra`） | 用编辑器下拉重选，别手改 asset |
| 数值颜色/方向反了 | `FlipPlus`（越低越好）与给的数值符号打架 | 值按原语义写，改 tag 或换属性 |
| `IndexOutOfRangeException` 在 `ApplyUpgrade` | `module` 越界（旧存档模组列表 / 联机资料不匹配） | 调用前夹到 `[0, Modules.Count-1]` |
| 新档位显示"锁定" | 武器 `Level < Upgrade[i].Key` | 改小 Key 或给武器加经验 |
| 每档第 4 个选项选不上/存不住 | `buyIndex` 只有 3 位（`GetBuy` 硬判 `x > 2`），UI 也是 3 列 | 每档最多 3 项 |
| 升级项带负收益（越升越弱） | 违反 §0.1 | 改成纯强化，代价移到某个 `WMD_` |
| 模组文案出现 `+5` / `-23%` / `3°→6°` | 违反 §0.3 | 换程度词：「显著减少」「大幅提升」「略微变差」 |
| 新建资产在、guid 也对，prefab 引用却是 `null` | prefab 先于新资产导入，坏引用被固化；prefab 文件没变 ⇒ 不会自动重导 | `ImportAsset(prefab, ForceUpdate\|ForceSynchronous)` **导两次**，再用 `SerializedObject` 回读确认（§0.5） |
| 模组装了盟友看不见 | `PlayerLoadoutMsg.Modules` 没带 / 改完没 `SendSelfLoadout` | 查 DTO 与关窗保存链路 |
| 模组边框/配色不对 | 由 `type` 决定（`Frame_Module{type}.png` + `color`） | 核对 `ModuleType` 与图标资源存在 |
| 改档数后老存档越界 / 装备异常 | `selectIndex` 长度不随 `Upgrade.Count` 扩容 | 迁移存档或读取处按 `Min` 夹取 |
| 确认购买后资源没减少 | 购买分支漏了扣费（`TipWnd` 只校验与显示，不扣） | `optA_Click` 里调 `propertyManager.Pay(cost)` |
| 改 `WeaponName` 后改装全没了 | 存档键含武器名 | 不要改名，或做存档迁移 |

### 已知债务（改到这里时留意）

- `WeaponUniqueAttributeApplier["尾迹范围"]` 的实现是 `SetExplosionTypeElectric`（疑似复制粘贴残留），真正的"范围"没有落点
- `WMD_None.asset` 里残留旧字段（`targetAttr` / `m_UniqueAttr`），当前 SO 已无这些字段 ⇒ Unity 忽略，别照抄
- `WeaponModuleData_SO.name` 遮蔽内置 `name`，读代码时别把 `asset.name` 当显示名

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

---

## 9 工具：批量生成 / 校验（`scripts/`）

> 场景：给一把新武器铺整棵升级树 + 一组模组，或批量调数值。手写 40 个 `WUD/WMD + meta`、再在 prefab YAML 里做 4 处替换，既慢又容易静默改错。

| 脚本 | 作用 |
|---|---|
| `scripts/gen_weapon_tree.py` | spec(JSON) → `WUD_*.asset` / `WMD_*.asset` + `.meta` + prefab 补丁（`cfg._fakeAttrs` / `showAttr` / `Upgrade` / `Modules` + `ActiveModule`）。幂等、锚点唯一命中断言、写前 `.bak` |
| `scripts/verify_weapon_tree.py` | 静态校验（不依赖 Unity）：资产逐字节比对、guid 唯一且与派生值一致、prefab 引用集合、`showAttr`/`cfg` 回读，退出码可挂 hook |
| `scripts/attr_whitelist.py` | 从 C# 源码解析 `WeaponAttrType` 编号、`attributeConfigs` 允许的修饰符、`InitAttribute` 会自动补 base 的属性（`Set(Attr.X,…)` 取自 `Damages`；`cfg.Add(Attr.X,1)` 是常量注入，如 `NoiseScale`/`Recoil`） |
| `scripts/examples/Logic.json` | spec 写法范例（`Logic.prefab` 的 12 升级 + 7 模组）。⚠ **已不是回归用例**：prefab 后来被手工挂上了根目录通用项（AmmoBag / HardenedRounds…），直接跑会被下面的落差门拦下；且它的 `WMD.desc` 写于"禁写数值"之前，别照抄文案 |
| `.codebuddy/plans/gen_arcpistol_tree.py` | 进阶范例：支持**复用根目录通用 WUD**（spec 里只写 `{"common": "Damage"}`）+ **Special + `m_UniqueAttr` 补丁** + `--verify` 回读；`gen_weapon_tree.py` 做不到这三点。⚠ 它没有落差门，重跑会整块覆盖 prefab 的 `Upgrade`/`Modules`（内容一致才 no-op） |

```bash
python scripts/gen_weapon_tree.py --spec <你的 spec>.json --dry-run   # 只看差异
python scripts/gen_weapon_tree.py --spec <你的 spec>.json             # 写入
python scripts/verify_weapon_tree.py --spec <你的 spec>.json          # 校验
```

⚠ **落差门**：prefab 若已被人手工改过（引用了 spec 之外的资产），`gen_weapon_tree.py` 会**拒绝生成**并列出多余 guid —— 这是保护，别急着加 `"adopt": true`；先把 spec 对齐现状，真要用 spec 覆盖时才显式接管。

生成前会拦掉五类错（全是真实踩过的）：

1. 属性名拼错，或不在 `attributeConfigs`；
2. 修饰符不被该属性允许 —— 否则要到运行时才报 `不支持的Modifier类型`；
3. **属性没有真实基础值**（既不在 `cfg._fakeAttrs`、也不由 `InitAttribute` 补）⇒ `Factor`/`Extra` 全在 0 上做文章，装了没效果；
4. **`WUD_` 里出现弱化项**（按 §0.1 判定）；
5. 每档超过 3 项；不足 3 项给"存档 `selectIndex` 悬空"警告。

用法要点：

- **属性一律写枚举名**（`BulletsSpreadAngle` 而不是 `7`），编号由工具查源码；`Base` 求精确值、`Factor` 表达百分比（注意是 `(base+ΣBase)*(1+ΣFactor)` 加和再乘）。
- 脚本**只碰资产与 prefab**，不改 `.cs`、不改名/删除。
- ⚠ **`gen_weapon_tree.py` 做不了 Special**：它按 `attributeConfigs` 校验 ⇒ `Special` 直接判错，且不补 `m_UniqueAttr`。要 Special 就照 `gen_arcpistol_tree.py` 自己写一层，或手写那条 WUD + 手改 prefab。
- ⚠ 跑完按 §0.5 收尾（**prefab 导两次** + 回读）。

---

## Resources

- `references/weapon-upgrade-guide.md`：`WeaponAttrType` 全表 + `attributeConfigs` 全量条目、`AttrInfo`/`ShowText` 细节、编辑器窗口结构、存档/联机分解、实测样例与自检清单
- `references/drg-upgrade-design.md`：外部对标（DRG 5 层升级 + Clean/Balanced/Unstable 超频）与本项目结构的对照、6 条可借鉴原则、一把枪的数据规模与坑；明细数据在 `.codebuddy/notes/drg-weapons-upgrades.json`
