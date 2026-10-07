# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：IL2CPP 下 MessagePack 动态 resolver 崩溃的修复（append-only）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 修掉打包版联机即崩：IL2CPP 不支持 Reflection.Emit ⇒ MessagePack 动态 resolver

- 报错（只在**打包版**、一发消息就炸）：`PlatformNotSupportedException: Operation is not supported on this platform`
  at `AssemblyBuilder.DefineDynamicAssembly` ← `MessagePack.Internal.DynamicAssemblyFactory` ←
  `DynamicObjectResolver.FormatterCache<PoseBatchMsg>..cctor` ← `MessagePackSerializer.Serialize`
  ← `MessageCenter.Pack<T>` ← `NetHostSvc.BroadcastPoses`。
- **根因**：`MessageCenter.Pack/Dispatch` 用的是 `MessagePackSerializer` 的**默认 options** ⇒ `StandardResolver → DynamicObjectResolver`，
  后者靠 `System.Reflection.Emit` 现造 formatter，而 **IL2CPP 不支持动态代码生成**；编辑器走 Mono 所以完全看不出来。
- **事实核查（实测，别再猜）**：① MessagePack 3.1.8 的 nupkg **不含 analyzer/源生成器**（`install_deps.py --dry-run` 只列出 `lib/*/MessagePack.dll`）；
  ② `MessagePack.GeneratedMessagePackResolver` 确实在 KCPNet.dll 里（`inspect_dll.py`），但**是 internal**（外部引用报 `CS0122`），
  KCPNet 内部显然显式传了自己的 options（`probe_dll_strings.py` 在 KCPNet.dll 里搜到 `MessagePackSerializerOptions`）⇒ **库侧本来就 AOT 安全，只有我们的 DTO 踩了默认 resolver**。
- **修法（落码）**：新增 `Assets/Scripts/NetTmp/Services/NetMsgCodec.cs`（**AOT 安全的 DTO 编解码器**）——
  只用 `MessagePackWriter` / `MessagePackReader` 写读**标准 MessagePack 字节**，成员枚举 + 字段读写自己用反射做：
  `[MessagePackObject]` 类型写 **array 格式**（长度 = 成员数、按 `[Key]` 升序）；读端 `count < members.Length` ⇒ 末尾字段保持字段初始化的默认值（**保留"新字段追加在末尾"的兼容语义**），
  `count > members.Length` ⇒ 多出的 `reader.Skip()`。全程不碰 Reflection.Emit、不需要预生成 formatter ⇒ **新增消息零额外工作**。
  `MessageCenter.Pack` 改 `NetMsgCodec.Serialize(msg, typeof(T))`、`Dispatch` 改 `NetMsgCodec.Deserialize(handler.MsgType, msg.Data)`（并删掉 `using MessagePack`）。
  新增 `Assets/link.xml`：`preserve="all"` 保全 **`02_Net`**（反射读字段）与 **`MessagePack.Annotations`**（`[Key]` 特性运行时要读）
  —— 反射用法对裁剪器不可见，不保留的话打包后表现为"反序列化全是默认值"（编辑器同样看不出来）。
- **验证**：① 离线编译 `02_Net`（新文件用 `tmp_compile_files.py 02_Net ...`）+ `09_Managers/10_UI/10_Effect` 全 **0 错误**；
  ② MCP `refresh_unity(force,assets,compile)` 后 Console **0 error**；③ MCP `execute_code` 对 02_Net 里**全部 21 个 `[MessagePackObject]` 类型**做
  "填非默认值 → 序列化 → 反序列化 → 再序列化（逐字节比对）+ 逐字段比对" —— **tested=21 failed=0**（如 `PoseBatchMsg=OK(49B)`、`PlayerListSync=OK(83B)`）。
- 顺手记录：`MessagePackWriter(ArrayBufferWriter<byte>)` + `WrittenSpan.ToArray()`、`KeyAttribute.IntKey`、`MessagePackObjectAttribute`
  这些 API 全部一次编译通过（之前对 3.1.8 的 API 猜测无误）。
- 备选路（未走）：① 用 mpc/源生成器预生成 resolver（这台机器要额外装 .NET SDK 工具链，且 Unity 2022 的 Roslyn 版本是另一个风险）；
  ② Standalone 切 Mono（一个设置立刻能跑，但放弃 IL2CPP）；③ 给 21 个类型手写 formatter（≈700 行，被 `NetMsgCodec` 泛化掉了）。
- 已同步更新 skill `kcpnet-online` 的「AOT 提醒」为**踩过的坑 + 现成修法**，避免下次重走。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
