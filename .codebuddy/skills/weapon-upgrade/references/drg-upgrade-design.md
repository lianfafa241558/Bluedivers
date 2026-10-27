# 外部参考：Deep Rock Galactic 的升级 / 超频体系（本项目的对标物）

> 用途：设计 `WUD_*` / `WMD_*` 时对照"成熟商业游戏的同类系统长什么样"。
> 数据来源：`deeprockgalactic.wiki.gg` 24 个武器条目页，2026-10-27 抓取。
> 明细数据：`.codebuddy/notes/drg-weapons-upgrades.json`（机器可读）；中文可读版 `.codebuddy/notes/drg-weapons-upgrades.md`。
> ⚠ 只借结构与尺度，**不要照搬数值** —— 两套游戏的伤害基数、弹药量、敌人血量完全不同。

---

## 1 结构对照（DRG ↔ 本项目）

| DRG 概念 | 本项目对应物 | 差异要点 |
|---|---|---|
| 5 层升级（Tier 1~5），每层 1 选 1 | `Upgrade: List<KVP<int 解锁等级, List<WUD_>>>` | **几乎一对一**：`KVP.Key` 就是"该档解锁等级"，DRG 的 T2~T5 = 4/8/12/16 正好是等级门槛，语义完全吻合 |
| 层内 2~3 个选项 | 每档 ≤3 个 WUD_ | DRG 每档最多 3 个，与本项目"每档 ≤3（`buyIndex` 只有 3 位）"**天然对齐**，不会撞已知债务 |
| Mod 总数 12~14 | `UpgradeCount()` 累计 | 一把枪整套约 12~14 个 WUD_ 资产 |
| 超频（Overclock），装 1 个 | `Modules: List<WMD_>` + `ActiveModule` | 本项目的模组是"整组互相替换、单选"，与 OC 语义一致；`Modules[0]` 必须是 `WMD_None` = DRG 的"不装超频" |
| 超频三级 Clean / Balanced / Unstable | `ModuleType: None/Clean/Balanced/Unstable` | **枚举名完全一致**，只是本项目多了 `None`（空模组） |
| OC 数量 5~8 | `Modules.Count` 约 6~9（含 None） | 量级参考 |
| `modifys` 一串增量 | `List<ModifyAttrData>` | DRG 的面板加成刚好也是"加法 + 乘法"两类，可映射到 `Base` / `Factor` |

### 1.1 关键结论

- **不需要为对标改任何数据结构**：`Upgrade` 的分档 + 每档 ≤3 + `Modules` 单选，正好覆盖 DRG 的全套形态。
- 唯一缺口是**"层内 2 个选项"**的情况（如 CRSPR 的 T1 只有 2 个、Wave Cooker 的 T4 只有 2 个）—— 本项目允许某档只挂 2 个，无影响。
- 另一缺口：DRG 的 5 层等级门槛是 `0/4/8/12/16`（主武器）与 `0/6/10/14/18`（副武器），本项目 `KVP.Key` 随便填，可直接沿用这套节奏。

---

## 2 可直接借鉴的 6 条设计原则

### 2.1 分层职责固定：T1~T4 量变，T5 质变

DRG 的每一层干什么是**全局统一**的，玩家形成肌肉记忆：

| 层 | 职责 | 典型内容 |
|---|---|---|
| T1 | 手感 / 容量 | 弹匣、射速、装填、散布、射程、电池 |
| T2 | 伤害 / 弹药 | `+N Damage`、`+MaxAmmo`、破甲 |
| T3 | 机制型 | 穿透、眩晕、连发模式、换弹取消、特殊弹种 |
| T4 | 专用乘区 | 弱点加成、破甲倍率、减速、容量 |
| T5 | **签名效果** | 改变武器行为的唯一性强效果，几乎必带代价 |

⇒ 本项目建议：给每把枪写升级树前，先按这张表分工，避免"5 层全是 +伤害"。

### 2.2 T5 才允许"改玩法"，且必须自带代价

DRG 的 T5 是最像 `Special` 的一层：

- CRSPR `Heat Radiance`（开火时周身热辐射）
- Breach Cutter `Triple Split Line`（1 条光束 → 3 条）
- Hurricane `Nitroglycerin Compound`（滞空越久伤害越高）
- Boomstick `Special Powder`（OC 层的"霰弹跳"）

它们不是数值，而是**新行为**。映射到本项目 = `WeaponAttrType.Special` + `m_UniqueAttr` 注册 + `WeaponUniqueAttributeApplier` 消费端（见 SKILL §5.3）。**没有消费端的 Special 只会显示 `+名字`、不生效** —— 这条坑 DRG 用行为实现，本项目用属性表实现，必须走完三步。

### 2.3 超频三级 = 本项目的模组品质

| DRG | 语义 | 本项目 `ModuleType` | 文案写法（`desc: List<SKVP<bool,string>>`） |
|---|---|---|---|
| Clean | 纯增益，无代价 | `Clean` | 只写 `Key=true` 的收益行 |
| Balanced | 明确一正一负 | `Balanced` | `true` 收益 + `false` 代价，成对 |
| Unstable | 改变武器行为 | `Unstable` | 同上 + 行为描述 |

DRG 的实际比例（24 把枪共约 160 个 OC）：Clean 约 25%、Balanced 约 40%、Unstable 约 35%。
本项目若照搬强度预期：`WMD_` 的 `modifys` 里 **Balanced/Unstable 必须成对写正负**，且负项要有实感（`-弹药 / -弹匣 / +装填 / +散布 / -移速`）。

### 2.4 数值只用两种表达：`+N` 与 `xN`

DRG 面板全是 `+3 Damage` / `x0.5 Spread`，**没有第三种写法**（没有"每秒 +X"这种需要积分的表述，特殊情况都靠独立词条）。

⇒ 本项目映射：
- `+N` → `ModifierType.Base`
- `xN` → `ModifierType.Factor`（注意本项目 `Factor` 是**加法倍率**：`0.3` = +30%，不是乘 0.3）
- `Extra` 只在"层数/额外量"这类离散量上用（DRG 的 `+1 Penetration`、`+6 Max Locks` 对应它）

⚠ 最容易错的一处：DRG 的 `x0.5 Spread`（变好）在本项目若写成 `Factor = -0.5` 才对（减半）；写成 `0.5` 会变成 +50% 散布。

### 2.5 状态系统作为跨武器"条件乘区"

DRG 大量用状态做乘法钩子，同一个状态被 5~6 把武器引用：

| 状态 | 引用示例 |
|---|---|
| 触电 | Stubby `Conductive Bullets +25%` / GK2 `Bullets of Mercy +50%` / Zhukov `Conductive Bullets +33%` / Shard Diffractor `Hydrogen Rupturing +33%` / Boltshark `Magnetic Shafts +25%` |
| 燃烧 | Subata `Volatile Bullets +50%` / Bulldog `Volatile Bullets +300%` / ArmsKore `Necro-Thermal +30%` |
| 冰冻 | Cryo 系 + `Ice Storm` 对冻结目标 +100% |
| 中毒/腐蚀 | Wave Cooker `Contagion Transmitter +30%` / Sludge Pump 体系 |

⇒ 若本项目要做"武器互相配合"，**先有统一的状态枚举 + 统一的"对处于 X 的目标 +N%"属性**，再谈个别武器的花活。否则每把枪都在自己算状态，组合永远做不出来。

### 2.6 强项必带代价，且代价要能被玩家感知

DRG 的代价基本只落在 5 个维度：`弹药 / 弹匣 / 装填 / 散布 / 移速`，极端情况直接禁用机制：

- Minigun `Lead Storm`：`移速 x0`
- Zhukov `Gas Recycling`：`弱点倍率 x0`（打哪都一样）
- M1000 `Supercooling Chamber`：`聚焦时不能移动`
- Subata `Explosive Reload`：`弹药 x0.5、弹匣 x0.49`

⚠ 本项目的 `FlipPlus` 就是为这类"越低越好"准备的：**数值按原语义写（负数就是减），不要为了显示成绿色去反写符号**。

> ⚠ **本项目口径更严（2026-10-28 定）**：DRG 的**升级（Mod）层面**也允许代价（如某些档位一边加伤害一边削弹药），
> 但本项目规定 **`WUD_` 只准强化、任何代价只能出现在 `WMD_`**。
> 所以对标 DRG 时，**只借它的分层结构与强度尺度，不要把带负面的 Mod 直接翻译过来** —— 要么砍掉代价、要么把它降级成一个模组。

---

## 3 落地到本项目：一把枪的完整数据规模

以 DRG 13 Mod + 7 OC 的枪为模板，本项目需要：

```
Assets/Resources/GameData/WeaponUpgrade/<武器名>/WUD_*.asset   × 12~14
Assets/Resources/GameData/WeaponModule/<武器名>/WMD_*.asset    × 5~8（不含 WMD_None）
prefab 上：Upgrade = 5 个 KVP（Key = 0/4/8/12/16 或 0/6/10/14/18）
           Modules = [WMD_None, ...按品质排序]
           showAttr 补齐所有被改动的属性（否则面板看不到）
           m_UniqueAttr 注册所有 Special
```

检查清单（照 SKILL §5 走）：

1. 每档 ≤3 —— 与 `buyIndex` 3 位一致，别超。
2. `KVP.Key` = 解锁等级（不是档序号），本项目 UI 判 `archWeaponData.Level < Key`。
3. 新增档位 ⇒ 老存档 `selectIndex` 长度不会自动扩容，必须做迁移或重置。
4. 改 `WeaponName` ⇒ 存档键变 ⇒ 改装全丢。
5. `Special` 三步（注册 / modifys / 消费端）缺一不可。
6. 只改 `cfg` 不订阅 `InitAttribute` 的 `Set` ⇒ 数值看得到、打不出。

---

## 4 不要照搬的部分

| DRG 做法 | 为什么不适用 |
|---|---|
| 面板同时乘算十几条 OC/Mod 的浮点结果 | 本项目 `Factor` 是**加和**再乘（`1 + ΣFactor`），不是逐条连乘；写数据时按加和算，别按 DRG 的连乘直觉 |
| 大量 `x0.5 伤害转火焰` 的复合类型 | 本项目伤害类型系数是独立枚举段（200~231），转换需要专门的消费逻辑，不是一个属性就能表达 |
| 弹药按"成对消耗"（Minigun、Zhukov） | 本项目 `Ammo` 是真实发数，没有"每 2 发消耗射 1 发"的隐藏换算；照搬数值会直接错一半 |
| 每把枪 12~14 个 Mod 全做 | 本项目每档 ≤3 已定，但**总量不必对齐**；先把 5 档 × 2~3 做扎实，比凑数更有价值 |
| OC 的数值随机化（Homebrew Powder `x0.8~x1.4`） | 本项目 `ModifyAttrData.value` 是常量，随机化需要额外运行时逻辑，不建议作为首批 |
