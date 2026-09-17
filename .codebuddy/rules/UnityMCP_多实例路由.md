---
description: Unity MCP（unity-mcp）多实例环境下的路由规则：本工作区所有工具调用必须落在 Bluedivers 实例上
alwaysApply: true
enabled: true
provider:
---

# Unity MCP 多实例路由规则

本机长期同时开着两个 Unity 项目、两个 CodeBuddy 窗口，MCP for Unity 同时连接着两个实例：

| 项目 | 实例 ID | Unity 桥端口 | 说明 |
| --- | --- | --- | --- |
| **Bluedivers（本工作区）** | `Bluedivers@3d9f2357` | 6400 | **本规则的作用对象：所有操作默认只能落在它上面** |
| RTSClient（另一个项目） | `RTSClient@6365de15` | 6401 | 非本工作区，默认禁止操作 |

实例 ID 由项目路径派生（路径不变则 hash 稳定）；如果换了项目路径或重装了 MCP，以 `mcpforunity://instances` 的实时结果为准。

> 注意：另一台设备上还有一份 `D:\Pro\Bluedivers` 副本，它的实例 hash、桥端口都**与本文不同**，不要跨机照抄。

## 【必须】

1. **本工作区的所有 unity-mcp 工具调用（含资源读取）都必须落在 Bluedivers 实例上。**
   - 会话开始（或不确定当前实例时）第一件事：调用 `set_active_instance(instance="Bluedivers@3d9f2357")`。
   - 活动实例是**会话级、只存在内存里**（不落盘）：重开窗口、重启 IDE、重启 MCP server 进程后**必须重设一次**。
2. **不要省略路由信息**：连续做批量操作时，宁可每次调用都带上 `unity_instance="Bluedivers@3d9f2357"`（单次路由，不改变会话默认值），也不要依赖"默认实例"这种隐含状态。
3. **写操作前先自检**：任何写入类操作（场景/资源/脚本/组件）之前，用 `mcpforunity://project/info` 或 `mcpforunity://editor/state` 确认 `projectRoot == E:/Bluedivers`；若返回 `D:/Project/RTSClient`，说明路由错了，**立即停止并纠正**，然后再执行。

## 【禁止】

- 禁止在未确认目标实例的情况下执行写入类工具（`manage_*`、`create_script`、`apply_text_edits`、`execute_code`、`refresh_unity`、`run_tests` 等）。
- 禁止把 MCP 的报错"换一个实例重试"来绕过 —— 报错信息应原样回报给用户。
- 禁止用全局 `~/.codebuddy/mcp.json` 的 `env.UNITY_MCP_DEFAULT_INSTANCE` 来"一劳永逸"：它是**全局环境变量**，会把另一个窗口也钉到 Bluedivers 上，反而制造交叉。

## 常见错误与处理

| 报错/现象 | 原因 | 处理 |
| --- | --- | --- |
| `Multiple Unity instances are connected and none is selected. ...` | 本会话还没选实例（多实例下 server **拒绝猜测**） | 执行 `set_active_instance(instance="Bluedivers@3d9f2357")` 后重试 |
| `Instance 'xxx' not found. Available: ...` | 实例 ID 变了（项目路径变更 / 重装） | 先读 `mcpforunity://instances` 取实时列表，按实时 ID 操作，并把变化告知用户 |
| `Available: none` / 立即失败 | Unity 正在编译触发域重载的瞬时窗口 | 命令未到达 Unity，属安全失败，等编译完成直接重试 |
| 超 30 秒预算的调用（`execute_code`、长时间导入、`run_tests`）失败 | 命令可能已执行完但结果被丢弃 | **不要盲目重试**，先检查实际效果再决定 |

## 例外（仅在用户明确要求时）

只有当用户明确要求"去操作另一个项目 / RTSClient"时，才把**那一次**调用指向另一个实例：`unity_instance="RTSClient@6365de15"`（stdio 模式也支持端口号 `6401`）。这类跨项目操作必须在回复中**显式声明**"本次操作的是 RTSClient"。

## 参考

- 官方文档：`https://coplaydev.github.io/unity-mcp/guides/multi-instance`
- 实例清单资源：`mcpforunity://instances`；当前实例：`mcpforunity://project/info`
- 源码依据（`mcpforunityserver` 10.2.x）：`transport/legacy/unity_connection.py:578-596` —— 实例数为 1 时自动选中；**≥2 且未钉选时直接抛错**（旧的"路由到最近心跳"因会让未绑定会话串到别的项目，已按上游 issue #1023 删除）。会话级活动实例见 `transport/unity_instance_middleware.py`。
