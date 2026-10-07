# 主题 · 家具身份单源化（Furniture_Attached.Identity）

> 由 `topics/contract.md` 拆出（2026-10-02：contract.md 超 8000 字符预算）。触发词：家具身份 / Identity / 家具 Id / BaseObject / Furniture_Base / Furniture_Attached。

## 家具身份「单一数据源」改造（2026-10-02，已落盘，待 Unity 一次成功编译）

- **判据（从此写死）**：身份数据（`ShowName / Id / Portrait / ExtraPortrait / Color / HalfRange / HalfHeight`）**全工程只允许存在一份**，唯一持有者是挂在物体上的 `Actor` 或裸 `BaseObject`；**家具组件永不自己保存身份**。
  - 家具"要不要身份" ⇒ 给它的 GameObject 挂 `BaseObject`（要单位语义则挂 `Actor`，`Actor : BaseObject`，仍然只有一份）。
  - 家具自己**不许实现 `IEntity`**：否则 `GetComponent<IEntity>()` 出现两个候选，谁赢取决于组件顺序（编译期全绿、运行期静默选错）。
- **代码形态**：`Furniture_Attached` 持有 `[SerializeField] MonoBehaviour identitySource` + `protected IEntity Identity`，`Awake` 里 `ResolveIdentity()` 按「显式引用 > 自身 `GetComponent<IEntity>()` > 子树 `GetComponentInChildren<IEntity>(true)`」解析，缺失就报错；`ShowName/Id/Icon` 全部转发给 `Identity`。
  - ⚠ 兼容点：`Furniture_Attached.ShowName/Id/Icon` 仍保留 `virtual`，`Furniture_ReturnBag`/`Furniture_WeaponPickup` 的 override 继续生效。
  - ⚠ `Id` 现在是**只读属性**（原来 `Furniture_Base.Id` 是字段）：谁要写 Id 必须显式写回身份源，样例见 `Furniture_Pipe.SetId()`（管道用 Id 当状态键 PipeLink/PipeWait/PipeComplete/PipeError，`MissionSubConnectPipes` 依赖它）。
- **删除/合并**：`Furniture_Base.cs` 整体并入 `Furniture_Attached`（通用配置 `relatedTrans / relatedTrans2 / ExtFloatParameter / particle / obs` 上提；`ExtBoolParameter` 全仓零引用**已直接删**；身份字段与 `IEntity`/`IFurniture` 显式实现全删）；`Furniture_EquipActor.cs` 删除（它只是"不想当单位但要有身份"，补 `BaseObject` 后退化成 `Furniture_Equip`）。两文件连同 `.meta` 一并删除（guid `374b3c2e…` / `80cf9cb3…` 作废）。
- **资产侧（本轮用 MCP `execute_code` 批量完成，共 78 个家具组件 / 62 个 prefab）**：新增 39 个 `BaseObject`（把家具上的身份值搬进去，先搬迁后改代码，避免 Unity 反序列化丢数据）；清理 5 个因嵌套 prefab 重复挂上的 `BaseObject`；脚本引用替换 6 处（Bolt、MissionPoint_SubRestartedGenerator → `Furniture_Attached`；BlinkBag/JetpackBag/RocketBag/ShieldBag → `Furniture_Equip`），工具 `.codebuddy/plans/swap_furniture_script_guid.py`。**场景 `.unity` 0 命中**。终检 `FURN=78 OK_SELF=78 OK_CHILD=0 DUP=0 MISS=0`。
- **验证**：`offline_compile 06_Gameplay = 0 错误`（首轮 4 处 `CS0200 无法为只读 Id 赋值`，均在 `Furniture_Pipe`，改走 `SetId` 后归零）；`10_UI / 09_Managers / 05_UnitCore / 04_Data` 同步 0 错误。`08_Map.csproj` 报 712 错是**该 csproj 本身引用不全的既有问题**，与本改动无关。
- ⚠ **未完成的一步**：Unity 侧还没做成功编译（`is_focused=false` 一直没刷新，控制台留着先前失败编译的 4 条 `CS0200`；反射确认 `06_Gameplay` 里仍是旧程序集）。**切回 Unity 让它自动编译即可**；之后要复查 `Bolt.prefab` 等 6 个 prefab 的 `relatedTrans/particle/obs` 是否还在（我已核对替换前的 YAML 里字段齐全，理论上同名同类型不会掉值）。
- **顺带发现（不是本次引入，待用户定夺）**：
  - `SignaTower`：`Furniture_General` 的 `Id` 原为空 → 现在会读到 `BaseObject.Id = "LidarStation"`；而行为表键是 `["SignaTower"]`（`Furniture_General.cs:210` 用 `furnData.TryGetValue(Id)`）⇒ **两边永远对不上，雷达站的行为派发（含 `_InOperateUpdate` 旋转）实际一直没生效**。
  - `Airdrop/Artillery Player.prefab`：家具 `ShowName=火炮` 与 `Actor.ShowName=大地碎裂炮` **原本就两份不一致**，改后以 Actor 为准（显示会变）。
  - `Healdrone_*` / `Kei`：家具原本读不到名字（`_actor` 那条路已废），改后能读到 Actor 的值；`IEquippable.ID`（`BagBase.cs:38` / `HandEquip.cs:129` 走 `GetComponent<IFurniture>().Id`）会从空串变成真实 ID，**存档/槽位匹配要回归一下**。
  - 📌 MCP 实例实时值 = `Bluedivers@4a3e6a7b`(port 6401)，规则文档里的 `Bluedivers@3d9f2357`:6400 **已过期**。

### 家具身份单源化 · 编译后终检（2026-10-02，已全部通过）

- **代码侧（反射实测 `06_Gameplay`）**：`Furniture_Base` / `Furniture_EquipActor` 类型**已消失**；`Furniture_Attached` 上 `relatedTrans`/`ExtFloatParameter`/`identitySource`/`Identity` 全部存在；**`IEntity.IsAssignableFrom(Furniture_Attached) = False`**（家具不再是身份候选）✓。Console **0 error** ✓。
- **资产侧**：1044 个 prefab 全扫 → 家具组件 **78**、**无身份源 = 0**、**重复身份源 = 0**、旧 guid（`374b3c2e…`/`80cf9cb3…`）全仓 **0 命中**。6 个改过脚本引用的 prefab 明细：Bolt `[Furniture_Attached]→BaseObject 控制栓/Bolt`、Generator Variant `→BaseObject 发电机/Generator`、BlinkBag `[Furniture_Equip]→BaseObject 传送背包/BlinkBag`、SignaTower `→BaseObject 雷达站/SignaTower`、Kei `→Actor Kei/Kei`、Healdrone_Normal `→Actor "护卫犬"/GuardDog`；`relatedTrans/particle/obs/ExtFloatParameter` 字段**全部保留**（无丢失）。
- **用户已顺手修掉 SignaTower 的历史 bug**：4 个含雷达站的 prefab（`GameEvent/SignaTower`、`Mission_LidarStation`、`UploadData`、`PMC_NestLarge`）的 `BaseObject.Id` 均已写为 `SignaTower`，与 `furnData["SignaTower"]` 对齐 ⇒ 雷达站行为派发（含 `_InOperateUpdate` 旋转）**恢复生效**。
- **`Furniture_General.Id` ↔ `furnData` 键比对**（14 键）：命中 `BlackBox / Caisson / KeyScreen / PipeTarget / SignaTower / Supply`；**唯一无配置的是 `Assets/Art/Prefabs/Beacon.prefab` 的 4 个 `Plane`（Id 空）** ⇒ `Operate()` 只做 `action._Operate?.Invoke`，空 action 时**连 `base.Operate()` 都不走，等于点了没反应**（改造前后一致，非回归，但建议给它们补 Id 或改 `Operate()` 兜底 `base`）。另有 8 个键（`AirdropConfigWnd / GuideWnd / MedicalBag / SelectAirdrop / SelectRole / SelectTask / SelectVehicle / SettingWnd`）**场上无同 Id 家具**，全仓也搜不到任何"给家具写 Id"的代码（除 `Furniture_Pipe` 的状态键）⇒ 要么由别处运行时写，要么是死配置，待用户确认。
- **非本次引入的遗留缺失脚本（92 个，独立排查）**：89 个在第三方 `Assets/Plugins/Art/WarFX/**`；项目自己的 2 个 = `Resources/Prefabs/Projectiles/Projectile_Disc.prefab`（1 个）与 `Resources/VFX/Environment/Directional Light.prefab`（1 个），其 `m_Script` guid `474bcb49…`/`dd9e0c18…`/`da2b0de2…` 在 AssetDatabase 里解析不到（`de99b30e…=ProjectileStandard`、`3614fdf6…=ProjectileChargeParameters` 正常）。
- **建议 Play 回归点**：① `Furniture_Pipe` 的 `SetId` 现在把状态写进自身 `BaseObject`（管道任务 `MissionSubConnectPipes` 依赖 `Id=="PipeWait"`）；② `Healdrone_*`/`Kei` 的 `IEquippable.ID` 由空串变真实值，影响 `BagBase`/`HandEquip` 槽位匹配与存档。
