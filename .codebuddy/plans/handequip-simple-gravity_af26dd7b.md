---
name: handequip-simple-gravity
overview: 为手持装备（HandEquip/Furniture_HandEquip 所在物体）新增一个轻量模拟重力组件（SimpleGravity），实现"卸载落地时自然下落、CharacterController 触地即停"，并与现有装备/卸载流程打通（装备时禁用、卸载落地时启用）。
todos:
  - id: confirm-collider
    content: 用 [subagent:unity-developer] 确认落地装备物体的拾取 Collider 机制，确定 CharacterController 与交互 Collider 的共存/配置方式
    status: completed
  - id: wire-gravity-component
    content: 在落地装备 GameObject 上挂 CCGravity 与 CharacterController，并在 HandEquip 加 [SerializeField] 引用（带 InspectorName 中文）
    status: completed
    dependencies:
      - confirm-collider
  - id: wire-enable-disable
    content: 在 HandEquip.OnInstall 禁用 CCGravity、OnUninstall 落地后启用 CCGravity，用 [skill:bluedivers-unity] 校验启停与生命周期一致
    status: completed
    dependencies:
      - wire-gravity-component
  - id: verify-build
    content: 运行并验证：装备后不下落、卸载后自然下落触地停住且可再拾取，检查无编译错误
    status: completed
    dependencies:
      - wire-enable-disable
---

## 产品概述
为手持装备（HandEquip）在"丢弃/卸载落地"时增加基于 CharacterController 的模拟重力：装备从玩家手部脱离后自然下落并触地停住，实现更真实的落地表现。

## 核心功能
- 装备落地时从脱离位置自然垂直下落，触地即停（不反弹、不滚动）
- 装备携带期间（挂在玩家手上跟随移动）不应用重力
- 重力参数可配置（下落加速度、最大下落速度）
- 与现有交互/拾取流程无缝衔接：落地停稳后可被再次拾取

## 范围说明
- 重力行为：仅简单下落 + 触地停住，最轻量，符合家具定位
- 作用对象：重力组件挂在 HandEquip 与 Furniture_HandEquip 所在的同一 GameObject 上


## 技术选型与核心结论

### 关键结论：既不新写、也不抽象 BaseSelfMoveableController，而是复用现成的 CCGravity 组件

调研发现项目**已存在一个精确满足需求的通用重力组件**：

- 文件：`Assets/Scripts/Effect/EffectComp/CCGravity.cs`（命名空间 `EffectComp`）
- 实现：`CharacterController.Move(Vector3.up * _verticalVelocity * Time.deltaTime)` 垂直下落；`_controller.isGrounded` 触地时重置垂直速度为 -1（触地停住、不反弹）；`Gravity`（默认 20）控制下落加速度，`MaxFallSpeed`（默认 50）限制最大下落速度防止穿透；`Update` 仅在 `CharacterController.enabled` 为 true 时生效。
- 现状：该组件**当前无任何调用方**，是"待接入的通用组件"，恰好由 HandEquip 落地场景首次接入。

### 为什么不用其它方案
- **不抽象 BaseSelfMoveableController**：其重力逻辑强耦合玩家系统（PEMaths 定点数 `PEVector3`/`PEInt`、玩家输入 `GetInputMove`、`Health` 掉落伤害、`AudioSource` 脚步/落地声、`PlayerCamera` 晃动、`GrounderFBBIK`、动画等），对落地装备是过度设计且重构玩家移动控制风险极高。
- **不新写组件**：重复造轮子，违反 DRY；现成 `CCGravity` 已完全覆盖"简单下落+触地停住"需求。

### 程序集依赖可行性（已验证）
- `05_EffectComp.asmdef` 存在且 `autoReferenced = true`。
- `02Game` 目录**无 asmdef**（HandEquip、Furniture_HandEquip 均在 `02Game` 下，落入 Assembly-CSharp）。
- 因此 `HandEquip`/`Furniture_HandEquip` 可通过 `using EffectComp;` 直接引用 `CCGravity`，无程序集阻塞。

## 实现思路
1. 复用 `CCGravity` 组件：在落地装备的 GameObject 上挂载 `CCGravity` 与 `CharacterController`。
2. 打通启停状态（本方案核心工作量）：
   - **装备时（HandEquip.OnInstall）**：物体被 `SetParent` 到玩家手部跟随移动，此时禁用 `CCGravity`（`enabled = false`），避免下落。
   - **卸载落地时（HandEquip.OnUninstall）**：在 `transform.SetParent(null, true)` 脱离父级后，启用 `CCGravity`（`enabled = true`），装备从手部高度自然下落触地。
   - 启停逻辑集中在 `HandEquip` 的 `OnInstall`/`OnUninstall` 生命周期方法中，因为这两处正好对应装备/落地的完整生命周期；`Furniture_HandEquip` 的 `Operate`/`ResetForPickup` 无需改动（其落地路径由 `HandEquip.OnUninstall` 触发）。

## 实现注意点
- **Collider 冲突**：落地装备物体上可能已存在交互用 Collider（`Furniture_Base.Awake` 会 `TryGetComponent<Collider>`）。`CharacterController` 本身也是 Collider，若已存在 MeshCollider/BoxCollider，两者共存会导致物理异常。需将原交互 Collider 设为 `isTrigger` 或移除，仅保留 `CharacterController` 作为物理体，交互检测改走 `OnTrigger`（确认现有拾取交互机制是否基于 Trigger，若不是需评估）。
- **接地检测层**：`CCGravity` 使用 `CharacterController.isGrounded`，依赖地形/地面层被碰撞。需确认地面是否在可碰撞层内（项目 `LayerDefinition.MoveableLayers` 已覆盖地面层，落地装备处于该层即可）。
- **CCGravity 命名字段**：`CCGravity` 使用 `Gravity`、`MaxFallSpeed` 字段（PascalCase public），符合项目现状；若需在 Inspector 中文化，可补 `[InspectorName]` 属性（可选增强）。
- **状态复位**：落地停稳后 `CCGravity` 内部 `_verticalVelocity` 已复位；再次拾取时由 `OnInstall` 禁用组件，无需手动复位垂直速度。


## Agent 扩展
### Skill
- **bluedivers-unity**：用于在实现 HandEquip 落地重力时遵循项目架构约定（组合优于继承、IPhysical 契约、命名空间/程序集依赖、Inspector 中文化）。预期产出：重力启停逻辑与项目既有模块风格一致、无程序集阻塞。
### SubAgent
- **unity-developer**：用于评估落地装备物体的 Collider 与 CharacterController 共存问题，确认拾取交互检测机制，避免物理异常。预期产出：给出落地装备物体上 Collider/CharacterController 的正确配置方式。
