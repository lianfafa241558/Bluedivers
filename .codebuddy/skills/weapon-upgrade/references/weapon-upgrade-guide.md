# 武器升级 / 武器模组 —— 详细参考

本文是 `SKILL.md` 的展开版，按源码整理字段语义、属性全表、应用引擎细节、编辑器结构与实测样例。

---

## 1 类型与文件地图

| 类型 / 文件 | 命名空间 | 职责 |
|---|---|---|
| `WeaponUpgradeData_SO`（`06Gameplay/Data/WeaponUpgradeData_SO.cs`） | `FPSGame.GameData` | 升级项数据 |
| `WeaponModuleData_SO`（`06Gameplay/Data/WeaponModuleData_SO.cs`） | `FPSGame.GameData` | 模组数据 |
| `WeaponPlayerController`（`06Gameplay/Weapon/WeaponPlayerController.cs` + `WeaponUpgradeController.cs`） | `FPSGame.Weapon` | 武器控制器；改装逻辑在 partial 分部里 |
| `WeaponAttributeFactory` + `ModifierType` + `AttrTag` + `WeaponAttrType` + `GameAttribute` + `GameCurrentAttribute`（`Weapon/WeaponAttributeFactory.cs`） | `FPSGame.Weapon` | 属性注册表、枚举、公式 |
| `WeaponCfg`（`Weapon/WeaponCfg.cs`） | `FPSGame.Weapon` | 运行期属性字典（`Dictionary<WeaponAttrType, GameAttribute>`），序列化载体是 `_fakeAttrs` |
| `ModifyAttrData` / `UniqueAttrData` / `RenameAttrData`（`WeaponUpgradeController.cs` 末尾） | `FPSGame.Weapon` | 配置结构体 |
| `WeaponUniqueAttributeApplier`（`Weapon/WeaponUniqueAttrApply.cs`） | `FPSGame.Weapon` | 专有机制名字 → 行为表 |
| `ArchivesData_SO.WeaponUpgradeData`（`06Gameplay/Data/ArchivesData_SO.cs`） | `FPSGame.GameData` | 存档条目 |
| `WeaponUpgradeEditorWindow`（`Editor/WeaponUpgradeEditorWindow.cs`） | `FPSGame.Game.Editor` | `Tools/武器升级编辑器` |
| `ModifyAttrDataPropertyDrawer`（`Editor/Drawer/ModifyAttrDataDrawer.cs`） | `FPSGame.Game` | `modifys` 单行绘制 + modifier 过滤 |
| `WeaponModuleTabModule` / `WeaponUpgradeTabModule` / `WeaponTabModule`（`Editor/DataTabs/`） | `FPSGame.EditorExt` | 数据编辑器三页 |

依赖方向（asmdef 视角）：数据层 `WeaponUpgradeData_SO` 只依赖 `Weapon.ModifyAttrData` 与 `OOPartEnum`；表现/UI 在 `10UI`；联机 DTO 在 `07NetGame`，桥在 `09Manager`。**不要**让数据层反向引用服务或 UI（`WeaponModuleData_SO.frame` 就是因为违反这条被移除的）。

---

## 2 `WeaponAttrType` 全表 + `attributeConfigs` 配置

表头含义：`isCurrent` = 建 `GameCurrentAttribute`；`tag` = 显示标旗；`allow` = 允许的 `ModifierType`。

### 2.1 通用 / 散热

| 值 | 枚举 | InspectorName | isCurrent | tag | allow |
|---|---|---|---|---|---|
| 0 | `Ammo` | 通用/后备弹量 | ✔ | `LimitCurr｜OnlyInt` | All |
| 1 | `Magazine` | 通用/弹匣容量 | ✔ | `LimitCurr｜OnlyInt` | All |
| 2 | `ShootInterval` | 通用/射击间隔 | ✔ | `Reciprocal` | All |
| 3 | `Catapult` | 通用/弹射次数 | — | **无配置项** | — |
| 4 | `StartCool` | 通用/初始冷却 | ✘ | default | All |
| 5 | `LifeTime` | 通用/生命周期 | ✘ | `FlipPlus` | All |
| 6 | `BulletsPerShot` | 通用/弹丸数量 | ✘ | default | All |
| 7 | `BulletsSpreadAngle` | 通用/子弹散布角度 | ✘ | `FlipPlus` | All |
| 8 | `BulletsOffect` | 通用/子弹随机偏移 | ✘ | `FlipPlus` | All |
| 9 | `MoveSpeedToShoot` | 通用/射击时移动速度 | ✘ | `Percentage｜OneHide` | All |
| 10 | `ReloadTime` | 通用/手动装弹时间 | ✔ | `FlipPlus` | All |
| 11 | `AutoReloadTime` | 通用/自动装弹延迟 | ✔ | `FlipPlus｜UpdateToZero` | **Extra** |
| 12 | `AutoReloadSpeed` | 通用/自动装弹速度 | ✘ | default | All |
| 13 | `HeatPerShot` | 热量/射击热量 | ✘ | `FlipPlus` | All |
| 14 | `CoolDelay` | 热量/散热延迟 | ✘ | `FlipPlus` | All |
| 15 | `CoolSpeed` | 热量/散热速度 | ✘ | default | All |
| 16 | `OverheatDuration` | 热量/过热时间 | ✘ | `FlipPlus` | All |

⚠ 值 3 `Catapult` 在 `attributeConfigs` 里**没有条目** ⇒ `WeaponAttributeFactory.Create` 会 `Debug.LogError("找不到对应的配置")` 并返回 `default`（null），随后 `AttrInfo` 里 `attr == null`。要使用它必须先补表。

### 2.2 伤害

| 值 | 枚举 | InspectorName | isCurrent | tag | allow |
|---|---|---|---|---|---|
| 20 | `DirectDamage` | 伤害/直击伤害 | ✘ | default | All |
| 21 | `ExplosionDamage` | 伤害/范围伤害 | ✘ | default | All |
| 22 | `ExplosionRange` | 伤害/作用半径 | ✘ | default | All |
| 23 | `BulletSpeed` | 通用/投射物速度 | ✘ | `ScaleFromPrime｜OneHide` | **Factor** |
| 25 | `DirectAP` | 伤害/穿甲等级 | ✘ | `APGrade` | All |
| 26 | `ExplosionAP` | 伤害/爆炸穿甲等级 | ✘ | `APGrade` | All |

穿甲档位名（`WeaponAttributeFactory.GetAPGradeName`）：`≤1` 轻穿 / `2–3` 中穿 / `4` 重穿 / `≥5` 反坦克。显示为 `(中穿)3`。
`DirectAP` / `ExplosionAP` 的初值来自 `Damages[0]`，在 `ApplyUpgrade` 开头补进 `cfg`（预览实例可能没跑过 `InitAttribute`）。

### 2.3 激光 / 蓄力 / 锁定

| 值 | 枚举 | InspectorName | isCurrent | tag | allow |
|---|---|---|---|---|---|
| 40 | `LaserWaitTime` | 激光/预热时间 | ✘ | default | All |
| 50 | `ChargeLowestStage` | 蓄力/最低蓄力段数 | ✘ | `FlipPlus｜OnlyInt` | **Base** |
| 51 | `ChargeHigheststage` | 蓄力/最高蓄力段数 | ✘ | `OnlyInt｜UpdateToZero` | **Base** |
| 52 | `ChargeAmmoOnLowest` | 蓄力/最低蓄消耗弹药 | ✘ | `FlipPlus｜OnlyInt` | All |
| 53 | `ChargeAmmoOnHighest` | 蓄力/满蓄消耗弹药 | ✘ | `FlipPlus｜OnlyInt` | All |
| 54 | `ChargeDuration` | 蓄力/蓄力时间 | ✔ | `FlipPlus｜LimitCurr｜UpdateToZero` | All |
| 55 | `ChargeDamageScale` | 蓄力/伤害倍率 | ✘ | `Percentage｜OneHide` | All |
| 56 | `ChargeExplosionRangeScale` | 蓄力/作用半径倍率 | ✘ | `Percentage｜OneHide` | All |
| 57 | `ChargeHeatScale` | 蓄力/热量倍率 | ✘ | `Percentage｜OneHide` | All |
| 60 | `LockDistance` | 锁定/锁定距离 | ✘ | `OnlyInt` | All |
| 61 | `LockRange` | 锁定/锁定半径 | ✘ | `OnlyInt｜OneHide` | **Factor** |
| 62 | `LockLayers` | 锁定/锁定最大层数 | ✘ | `OnlyInt｜UpdateToZero` | **Extra** |
| 63 | `LockPerCount` | 锁定/锁定每敌最大层数 | ✘ | `OnlyInt` | **Extra** |
| 64 | `LockInterval` | 锁定/锁定间隔 | ✔ | `Reciprocal｜UpdateToZero` | All |

⚠ `LockRange` 在 `WeaponController` 里是**屏幕像素**半径（`WorldToScreenPoint` 后比距离），不是世界米。
⚠ `LockLayers` / `LockPerCount` / `WeaknessBonus` / `AutoReloadTime` 只接受 `Extra` —— 用 `Base`/`Factor` 会 `LogError`。

### 2.4 破坏 / 弱点 / 伤害类型系数

| 值 | 枚举 | InspectorName | allow |
|---|---|---|---|
| 100 | `DirectDestruction` | 直击/护甲破坏 | All（tag `Percentage｜OneHide`） |
| 101 | `ExplosionDestruction` | 爆炸/护甲破坏 | All（同上） |
| 102 | `WeaknessBonus` | 伤害/弱点加成 | **Extra**（tag `Percentage｜DefaultHide`） |

伤害类型系数（直击组 200 起、爆炸组 220 起，**两组一一对应、间隔 20**），全部 `Percentage｜OneHide` + `All`：

| 偏移 | 类型 | 直击组 | 爆炸组 |
|---|---|---|---|
| +0 | 动能 Gun | 200 | 220 |
| +1 | 爆炸 Explosion | 201 | 221 |
| +2 | 护甲破坏 Destruction | 202 | 222 |
| +3 | 真实 Real | 203 | 223 |
| +4 | 毒 Toxicity | 204 | 224 |
| +5 | 燃烧 Burn | 205 | 225 |
| +6 | 冰冻 Freeze | 206 | 226 |
| +7 | 电击 Electric | 207 | 227 |
| +8 | 眩晕 Vertigo | 208 | 228 |
| +9 | 恐惧 Terror | 209 | 229 |
| +10 | 辐射 Radiation | 210 | 230 |
| +11 | 骇入 Hacker | 211 | 231 |

`999 Special` 不在这张表里：不进 `attributeConfigs`、不走 `cfg`，完全由 `m_UniqueAttr` 提供初值 + `AttrTag`。

---

## 3 应用引擎细节

### 3.1 `ApplyUpgrade(int[] select, int module)`（`WeaponUpgradeController.cs`）

```csharp
public void ApplyUpgrade(int[] select, int module)
{
    // 1) 重建参数表：null 则 new，否则 Clear
    if (m_Parameter == null) m_Parameter = new();
    else m_Parameter.Clear();
    // 1.1 穿甲初值兜底（cfg 里没有就用 Damages[0] 的）
    // 1.2 showAttr → m_Parameter[name] = new AttrInfo(this, type, name)
    // 1.3 m_UniqueAttr → m_Parameter[name] = new AttrInfo(name, value, tag)   // 不入 cfg
    // 2) 逐档应用已选项（只加不减）
    for (i...) if (select[i] >= 0) ApplyUpgrade(i, select[i]);
    // 3) 模组：卸载旧的、装新的
    if (Modules.Count > 0) { ActiveModule = Modules[module]; ApplyModule(old, module); }
    // 4) 专有机制
    WeaponUniqueAttributeApplier.Apply(this, m_Parameter);
    // 5) 调试文本
}
```

要点：

- 它**假设 `select.Length == Upgrade.Count`**；`module` 也**不判边界**（`Modules[module]`）
- `ApplyModule(old, new)` = `ApplyAttrInfo(Modules[old].modifys, false)` + `ApplyAttrInfo(Modules[new].modifys, true)`
- 如果 `ActiveModule` 不在 `Modules` 里，`oldIndex` 取 `0`（空模组）—— 所以空模组必须是下标 0

### 3.2 `ApplyAttrInfo(List<ModifyAttrData> data, bool isAdd)`

| 分支 | 行为 |
|---|---|
| `type == Special` | `m_Parameter` 必须已有该 `name`（来自 `m_UniqueAttr`），否则 `LogError("在属性字典里面没有找到自定义属性{name}")`；命中则 `Modify(modifier, ±value)` |
| 普通属性 | `m_Parameter` 没有就 `new AttrInfo(this, type, GetModifyDataName(item))`（从 `cfg` 取；缺项按 0 建 + `LogWarning` 带 `cfg.KeyCount/DumpKeys`），再 `Modify(modifier, ±value)` |

`GetModifyDataName(data)`：`Special` 且 `name` 非空时用 `name`，否则用 `GetAttrName(type)`（= `m_RenameAttr` 里改过的中文名，或枚举 `InspectorName` 去前缀）。

⚠ 因此 **`name` 是"字典键"**：重命名 `m_RenameAttr` 会改变键；升级项在写的时候若用旧名，就取不到。

### 3.3 `TryUpgrade(oldModify, newModify)` —— 纯预览

```csharp
// 1) 新属性先补进字典（加 DefaultHide，避免凭空多显示一行 0）
// 2) 对字典每项：TryModify(old, new)
//    TryModify = Modify(old, -old.value) → Modify(new, +new.value)
//                → ChangeValue = Value（记下预览值）
//                → Modify(old, +old.value) → Modify(new, -new.value)  // 还原
```

用途：`SelectRoleWnd` 悬停升级卡片 / 悬停模组时对比数值。**它不改变 `FinalValue`**，只改 `ChangeValue`；退出悬停要 `ResetAllChangeValues()`。

### 3.4 `AttrInfo` 三值语义

| 成员 | 来源 | 含义 |
|---|---|---|
| `Value` | `attr.FinalValue.RawFloat` | 当前生效值 |
| `PrimeValue` | `attr.PrimeValue.RawFloat` | 原始值（`GameAttribute` 构造时那个，永不变） |
| `ChangeValue` | 由 `ResetChangeValue` / `TryModify` 写 | 用于"预览值 vs 当前值"的高亮与差异显示 |

`IsHide()`：

```
IsHide      → 永真
DefaultHide → Value == PrimeValue && ChangeValue == Value
OneHide     → (ScaleFromPrime ? Value==Prime && Change==Value : Value==1 && Change==1)
```

`ToString()`：

1. `APGrade` → `(中穿)3`（直接返回，不参与 padding）
2. `Reciprocal` → `1/x`
3. `Percentage` → `OneHide` 时 `x*100`，否则 `x/Prime*100`，补 `%`
4. `ScaleFromPrime` → `x/Prime*100` 补 `%`
5. 与 `Value` 有差异时拼 `<color=#...>±N</color>`（`FlipPlus` 交换红绿），再 `PadLeft` 对齐

### 3.5 `ShowText` 输出

```csharp
// (名称, 值文本, Item3, Item4)
normal.Add(new(name, re.ToString(),
    Item3: re.ChangeValue != re.PrimeValue,   // 相对原始值变过（UI 用青色）
    Item4: re.ChangeValue != re.Value));      // 本次有改动（UI 行背景半透明）
```

- `Special && Value == 0 && ChangeValue == 0` → 跳过（未激活的专有机制不占行）
- `TextOnly` → 第二列写 `"+"` 或 `"-"`，第一列是名字；`SelectRoleWnd.SetParameter` 把它渲染成 `+名称`（预览新增绿 / 已拥有青 / 卸载红）

### 3.6 专有机制（Special）消费位置

```csharp
// 表驱动：ApplyUpgrade 末尾统一执行
private static Dictionary<string, UnityAction<Weapon, Info>> data = new() {
    ["激光制导射程"] = (w, a) => w.Damages[1].MaxRange = a.Value,
    ["恐惧尾迹"]     = (w, a) => w.Damages[1].SetExplosionTypeVertigo(new(a.Value)),
    ["电磁尾迹"]     = (w, a) => w.Damages[1].SetExplosionTypeElectric(new(a.Value)),
    ["燃烧尾迹"]     = (w, a) => w.Damages[1].SetExplosionTypeBurn(new(a.Value)),
    ["尾迹范围"]     = (w, a) => w.Damages[1].SetExplosionTypeElectric(new(a.Value)),   // ⚠ 疑似复制粘贴残留
    ["全自动模式"]   = (w, a) => w.ShootType = a.Value > 0 ? WeaponShootType.Automatic : WeaponShootType.Manual,
};
```

```csharp
// 查询式：玩法侧按名字读
if (weapon.GetAttr("取消空投准备时间") > 0) { ... }   // VFXAirdropEffect
```

新增 Special 时二选一；`GetAttr` 查不到返回 0（不报错），所以"名字写错"表现为**静默失效**，比报错更难查。

---

## 4 编辑器窗口结构（`WeaponUpgradeEditorWindow`）

菜单 `Tools/武器升级编辑器`，`minSize = 960×640`。

- **顶部工具栏**：刷新列表（`Resources.LoadAll<GameObject>("Weapons")`，筛出含 `WeaponPlayerController` 的 prefab）、当前武器名
- **左侧列表**（240px）：武器名 / 类型 / 图标，选中后抓 `SerializedObject`
- **右侧面板** 四个 Tab（`RightPanelTab`）：
  - `General`：基础字段
  - `WeaponParams`：武器参数（`DrawPropertiesContent` + 指定 `propPaths`）
  - `BulletParams`：`ProjectilePrefab` / `BulletFlag` / `Damages` / `UseDamageIndex` 等
  - `UpgradeModules`：`DrawUpgradeSection` + `DrawShowAttrSection` + `DrawModuleSection` + 底部选中 SO 的 Inspector
- **升级区**：每行 = 一个 `Key`（显示 `Level {Key}`），卡片 `155×72`，最多 3 个，`+ 添加选项`（`SOPickerPopup<WeaponUpgradeData_SO>`）/ `新建选项...` / 整行拖拽；`+ 添加等级` 追加行（`Key = 当前数组长度`）
- **显示属性区**：`ReorderableList` 直接编辑 `showAttr`，新增走 `GenericMenu`（枚举全量，排除 `Special`）
- **模组区**：横向卡片 `160×80`，`★` = `ActiveModule`，`+ 添加模组` / `新建模组...` / 右键菜单（设为激活、移除）/ 拖拽
- 选中卡片后底部用缓存的 `UnityEditor.Editor` 画该 SO 的 Inspector（`_cachedSOEditor`，`OnDisable` 销毁）

⚠ `AddUpgradeLevel` 的 `Key` 默认是数组长度（第 0 行 = 0、第 1 行 = 1…），**不是"解锁等级"语义**。要做成 2/4/8/12 那种门槛，建完行后手改 Key。

---

## 5 存档细节

```csharp
// ArchivesData_SO.WeaponUpgradeData
int[] selectIndex;              // 每档选中项；-1 = 未选；长度 = Upgrade.Count
int[] buyIndex;                 // 位掩码：bit0..bit2
int selectModuleIndex;          // 模组下标（0 = 空模组）
int level = 1; int exp;         // 武器等级 / 当前级内经验（ExpMeet = 1000）

bool GetBuy(int y, int x) => (buyIndex[y] & (1 << x)) > 0;   // x > 2 直接 false
void SetBuy(int y, int x) => buyIndex[y] |= (1 << x);
int  BuyCount => Σ CountOnes(buyIndex[i]);                   // UI 的"已购买格数"
```

- `Level` 属性对老存档兼容：`level <= 0 ⇒ 1`（`JsonUtility` 不走构造函数，字段初始值也不生效）
- `ExpScale => exp / 1000f`，UI 进度条
- `GainExp(int)`：满 1000 升级、余数留下，返回是否升级
- 键 = `arch.ID + "_" + weapon.WeaponName`；`GetWeaponUpgrade/GetWeaponModules` 按 `WeaponTypeEnum`(0–5) 遍历 6 槽
- ⚠ 增删 `Upgrade` 档位后，已存在的存档 `selectIndex` 长度不会自动变 ⇒ `selectIndex[y]` 可能越界。要么迁移存档，要么在读取处按 `Math.Min` 夹取

---

## 6 联机链路（改装相关）

```
本地改档/换模组
  └─ SelectRoleWnd.HideWnd()  ─ meetSave ─▶ ArchivesData_SO.Save()
                                          └▶ TeamNetBridge.SendSelfLoadout()
                                               └ RefreshSelfWeaponConfig()  // 从存档重算 Upgrades/Modules 写回 PlayerData
                                               └ ToLoadout() → PlayerLoadoutMsg { Weapons, Upgrades, Modules, Vehicles }
房主/接收端 HandleLoadoutSync(PlayerLoadoutMsg)
  └ PlayerData.Upgrades / weaponModules 落表
  └ NetFriendBridge.RefreshLoadout → FriendWeaponView.Setup(prefabs, upgrades, modules)
       └ FriendWeaponView.ApplyUpgrade(inst, upgrades, modules)
            └ inst.ApplyUpgrade(select, module)   // module<0 夹 0；select 为空早退
```

- `PlayerLoadoutMsg`（`07NetGame/Msg/RoomMsg.cs`）：`[Key(0)] Sid`、`[Key(1)] int[][] Upgrades`、`[Key(2)] int[] Modules`、`[Key(3)] VehicleCustomDto[]`、`[Key(4)] int[] Weapons`
- 发送侧 `TeamNetBridge`：`if (m.Upgrades?.Length > 0) data.Upgrades = …`、`if (m.Modules?.Length > 0) data.weaponModules = …`
- ⚠ 空数组 = "没带这条"，接收端不会清空旧值；真正要"清空模组"必须靠 `-1`/`0` 语义而不是空数组
- `Support` 类型武器不套存档改装（`PlayerWeaponsManager.EquipGroundWeapon` 里显式跳过）

---

## 7 实测样例

### 7.1 BeamRifle 的升级树（`Resources/Weapons/BeamRifle.prefab`）

```yaml
Upgrade:
- Key: 2   → 3 个选项
- Key: 4   → 2 个
- Key: 8   → 2 个
- Key: 12  → 3 个
Modules:
- WMD_None (guid a19bae…)      # [0] 空模组
- WMD_…                        # 可装模组
- WMD_…
ActiveModule: WMD_None
```

⇒ 4 档，解锁等级 2/4/8/12（不是 0/1/2/3）。

### 7.2 `WUD_CoilAlignment`（减少散布）

```yaml
name: 定制线圈对准
type: 武器散布
cost: [{ Key: 0(Pyroxene), Value: 1000 }]
modifys:
- type: 7 (BulletsSpreadAngle)   modifier: 2 (Factor)   value: -0.7
```

⇒ `factor += -0.7` = 散布 **-70%**；因为 `BulletsSpreadAngle` 带 `FlipPlus`，UI 自动显示为绿色负值。

### 7.3 `WMD_AggressiveVenting`（危险模组）

```yaml
type: 1 (Unstable)  → 红色 + Frame_ModuleUnstable
desc: [ {Key:1, "攻击附带燃烧伤害"}, {Key:1, "过热时冷却速度提升"} ]
modifys:
- 205 DamageTypeBurn        Factor +0.3    # 直击燃烧系数 +30%
- 225 ExplosionTypeBurn     Factor +0.3    # 爆炸燃烧系数 +30%
- 200 DamageTypeGun         Factor -0.3    # 直击动能 -30%
- 221 ExplosionTypeExplosion Factor -0.3   # 爆炸伤害 -30%
- 16  OverheatDuration      Factor -0.2    # 过热时间 -20%（FlipPlus → 显示为收益）
```

### 7.4 Special 样例

```yaml
# 武器 prefab
m_UniqueAttr:
- name: "恐惧尾迹"  value: 0   tag: 32     # 32 = TextOnly
# 升级项
modifys:
- name: "恐惧尾迹"  type: 999 (Special)  modifier: 1 (Base)  value: 10
```

⇒ 参数面板出现 `+恐惧尾迹`（TextOnly 不显示数字），`WeaponUniqueAttributeApplier` 里把 `Damages[1]` 的恐惧系数设为 10。

```yaml
# 数值型专有属性（tag: 0，正常显示数字）
m_UniqueAttr: [{ name: "尾迹范围", value: 0.3, tag: 0 }]
```

---

## 8 新增项自检清单

新增**升级/模组数据**后：

- [ ] asset 文件名以 `WUD_` / `WMD_` 开头，放在 `GameData/WeaponUpgrade|WeaponModule/<武器名>/`（通用项才放根目录）
- [ ] `name` 字段填了中文显示名（不是文件名）
- [ ] `icon` 已赋值（UI 卡片空白通常就是漏了）
- [ ] 每个 modifys 的 `modifier` 都在该属性的 `allowModifier` 里
- [ ] 用到的属性都在武器 `showAttr`（否则玩家看不到变化）
- [ ] Special 的 `name` 与武器 `m_UniqueAttr` 逐字一致，且存在消费端
- [ ] 升级项已挂到目标 `Key` 行（每档 ≤3）；模组已追加到 `Modules`（`[0]` 仍为 `WMD_None`）
- [ ] 进游戏实测：悬停预览 → 购买/选择 → 参数面板数值变化 → 关窗后重开仍在（存档）

新增**属性**（`WeaponAttrType`）后：

- [ ] 枚举值未与现有序号冲突，带 `[InspectorName("分组/中文名")]`
- [ ] `attributeConfigs` 有对应条目（缺了 `Create` 直接报错返回 null）
- [ ] 消费端已接（`InitAttribute` 的 `Set` 链或控制器读取 `AttrFinal`）
- [ ] 需要显示则加进 `showAttr`；需要改名/加标旗则配 `m_RenameAttr`
- [ ] 离线编译通过（`plans/offline_compile.py`，属性枚举与配置表都在 `06Gameplay`，不涉 prefab 时可纯编译验证）
