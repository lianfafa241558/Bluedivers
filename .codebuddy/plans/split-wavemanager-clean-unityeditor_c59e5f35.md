---
name: split-wavemanager-clean-unityeditor
overview: 1) 将 WaveManager.cs（826行/3个类）拆分为 WaveManager.cs + ZergWave.cs + RobotWave.cs 三个文件；2) 修复运行时脚本中 6 处 UnityEditor 误引（EnemyMobile/RoleData_SO/SoundGroup_SO/TaskManager 的 using 未守卫，GameRoot/DrawLabelUtils 的 API 调用未守卫）。
todos:
  - id: split-wavemanager-zerg
    content: 拆分 WaveManager.cs：提取 ZergWave 类到新文件 ZergWave.cs
    status: completed
  - id: split-wavemanager-robot
    content: 拆分 WaveManager.cs：提取 RobotWave 类（含 EagleGroupInfo）到新文件 RobotWave.cs
    status: completed
  - id: clean-wavemanager
    content: 清理 WaveManager.cs：移除已提取的类，保留 WaveManager + WaveState
    status: completed
    dependencies:
      - split-wavemanager-zerg
      - split-wavemanager-robot
  - id: fix-unityeditor-using
    content: 修复 4 个 using UnityEditor 裸引用：EnemyMobile.cs、RoleData_SO.cs、SoundGroup_SO.cs、TaskManager.cs
    status: completed
  - id: fix-unityeditor-api
    content: 修复 2 个 UnityEditor API 直接调用：GameRoot.cs 和 DrawLabelUtils.cs
    status: completed
  - id: update-memory
    content: 更新 memory 和 skill 文件：修正项目总结中的错误信息并记录本次改动
    status: completed
---

## 需求概述

对现有项目代码进行两项结构调整性质的改进：

### 1. 拆分 WaveManager.cs
当前 `Assets/Scripts/01Manager/Bridge/WaveManager.cs` 约 826 行，混放了 4 个类型定义（`WaveManager` 主类、`WaveState` 枚举、`ZergWave` 类、`RobotWave` 类），违反单一职责原则。需拆分为 3 个独立文件，同目录放置，不改功能逻辑。

### 2. 清理运行时脚本中的 UnityEditor 误引用
6 个运行时脚本存在未经 `#if UNITY_EDITOR` 保护的 `using UnityEditor` 或 `UnityEditor.*` API 直接调用，违反编码规范，会导致非 Editor 平台编译失败。需逐一修复，确保仅编辑器环境引用 UnityEditor。

### 注意
- 拆分仅涉及文件结构重组，不修改任何业务逻辑
- 清理仅添加 `#if UNITY_EDITOR` 预处理宏，不改变原有逻辑
- 遵守项目编码规范（UTF-8 with BOM、文件名与类名一致、大括号另起行等）

## 技术方案

### WaveManager 拆分方案

**现状**：`WaveManager.cs` 包含 4 个类型——`WaveManager`（主调度器，行 15-242）、`WaveState`（枚举，行 244-249）、`ZergWave`（虫潮波型，行 250-449）、`RobotWave`（机器人/Kaiser 波型，行 451-826，含内部结构体 `EagleGroupInfo`）。

**拆分策略**：
- `WaveManager.cs`：保留 `WaveManager` 类 + `WaveState` 枚举（两者关系紧密，`WaveState` 被 `ZergWave` 和 `RobotWave` 共用）
- `ZergWave.cs`：提取 `ZergWave` 类
- `RobotWave.cs`：提取 `RobotWave` 类 + `EagleGroupInfo` 结构体

**关键考量**：
1. `WaveState` 枚举被 `ZergWave` 和 `RobotWave` 同时使用（switch 状态机），放在 `WaveManager.cs` 中最合理，避免循环依赖。
2. `ZergWave` 和 `RobotWave` 都独立实现了 `I_TickClass` + `System.IDisposable`，是完整独立的类，拆出无架构问题。
3. 所有类已在同一程序集（`Assembly-CSharp`），无需修改 asmdef。

### UnityEditor 清理方案

6 个文件分两类处理：

**`using UnityEditor` 裸引用（4 个）**：在 `using UnityEditor;` 前后添加 `#if UNITY_EDITOR` / `#endif` 守卫。

**`UnityEditor.*` API 直接调用（2 个）**：在调用代码块前后添加 `#if UNITY_EDITOR` / `#endif` 守卫，并确保非 Editor 平台有合理的替代行为或空操作。

## 实现细节

### 目录结构（仅展示修改文件）

```
Assets/Scripts/01Manager/Bridge/
├── WaveManager.cs          # [MODIFY] 保留 WaveManager 类 + WaveState 枚举
├── ZergWave.cs             # [NEW] ZergWave 类（从 WaveManager.cs 中提取）
└── RobotWave.cs            # [NEW] RobotWave 类 + EagleGroupInfo（从 WaveManager.cs 中提取）

Assets/Scripts/02Game/AI/StateMachine/
└── EnemyMobile.cs           # [MODIFY] using UnityEditor 添加 #if UNITY_EDITOR

Assets/Scripts/02Data/
├── RoleData_SO.cs           # [MODIFY] using UnityEditor 添加 #if UNITY_EDITOR
└── SoundGroup_SO.cs         # [MODIFY] using UnityEditor 添加 #if UNITY_EDITOR

Assets/Scripts/01Manager/Bridge/
└── TaskManager.cs           # [MODIFY] using UnityEditor 添加 #if UNITY_EDITOR

Assets/Scripts/01Manager/Global/
└── GameRoot.cs              # [MODIFY] UnityEditor.EditorApplication.isPaused 添加 #if UNITY_EDITOR

Assets/Scripts/00Tools/Test/
└── DrawLabelUtils.cs        # [MODIFY] UnityEditor.Handles.Label 添加 #if UNITY_EDITOR
```

### WaveManager 拆分注意事项

1. **using 语句分配**：拆出 `ZergWave.cs` 和 `RobotWave.cs` 时，各自只保留实际需要的 using 语句，避免冗余。
   - `ZergWave` 需要：`System.Collections.Generic`、`UnityEngine`、`UnityEngine.AI`、`Unity.FPS.Game`、`Core.Interface`、`Utils`、`Random = System.Random`
   - `RobotWave` 需要：`System.Collections.Generic`、`UnityEngine`、`UnityEngine.AI`、`Unity.FPS.Game`、`Core.Interface`、`Utils`、`Random = System.Random`
   - `WaveManager` 保留：`System.Collections.Generic`、`System.Linq`、`Core`、`Core.Interface`、`GameContract`、`Unity.FPS.AI`、`Unity.FPS.Game`、`UnityEngine`、`UnityEngine.AI`、`Utils`、`Random = System.Random`、`UnitWeightCfg = CampData_SO.UnitWeightCfg`

2. **`EagleGroupInfo` 结构体**：这是 `RobotWave` 的内部类，放在 `RobotWave.cs` 中作为 private 内部类即可。

3. **无 `KaiserWave`**：之前记忆中提到的 `KaiserWave` 不存在，`EnemyType.Kaiser` 在 `CreatWave` 中映射的是 `RobotWave`。

4. **文件编码**：所有新文件使用 UTF-8 with BOM，与项目规范一致。

### UnityEditor 清理注意事项

1. **GameRoot.cs 第 140 行**：`UnityEditor.EditorApplication.isPaused = true` 仅编辑器调试用，非 Editor 平台不需要此行逻辑，直接 `#if UNITY_EDITOR` 包裹即可。

2. **DrawLabelUtils.cs 第 78 行**：`UnityEditor.Handles.Label()` 是 `OnDrawGizmos` 风格调试工具，需确保该类本身仅在编辑器下实例化/使用。如果该类在运行时也会被调用，需要额外添加判空保护。

3. **EnemyMobile.cs / RoleData_SO.cs / SoundGroup_SO.cs / TaskManager.cs**：仅 `using UnityEditor;` 未守卫，实际未使用或仅在 `[InitializeOnLoad]` 等编辑器上下文中使用。直接添加 `#if UNITY_EDITOR` 守卫即可。

4. **不处理 `00Tools/Test/` 下的 `DisplayBone.cs`、`CopyUtils.cs`**：这些是纯编辑器调试工具，已有正确守卫或仅编辑器使用。
