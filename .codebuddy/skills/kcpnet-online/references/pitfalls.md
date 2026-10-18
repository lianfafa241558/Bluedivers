# KCPNet 长尾坑（原文留档）

> 从 `SKILL.md` 的「踩坑清单 / 工具约定」搬出（2026-10-18 瘦身）。按症状搜，不必通读。

- **MessagePack 版本写法**：`MessageCenter.Dispatch` 用的是非泛型 `MessagePackSerializer.Deserialize(Type, ReadOnlyMemory<byte>)`，参数顺序 `Type` 在前，`byte[]` 隐式转 `ReadOnlyMemory<byte>`，**不需要 `ref`**（3.1.8 的正确写法）。
- **`*.dll.meta` 里 `Editor: enabled: 0` 是无害的**：那是 Unity “Any Platform 托管插件”的标准写法（`Any.enabled: 1` 才是关键），不代表编辑器不能用。
- **`Assembly 'X' will not be loaded due to errors:` 怎么查**：Console 只给标题，真原因（`Unable to resolve reference 'Y'`）在 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 里紧跟其后几行。这类错误会**连锁**：`MessagePack.dll` 加载失败 ⇒ 引用它的 `KCPNet.dll` 失败 ⇒ 引用 KCPNet 的 `Assembly-CSharp.dll` 失败（伴随 `Assembly-CSharp-Editor.dll` 失败）。修好最底层那一个即可全通。
- **API 兼容级别是 .NET Standard 2.0（不是 2.1）**：`PlayerSettings.GetApiCompatibilityLevel(Standalone) == NET_Standard_2_0`（`ProjectSettings.asset` 里 `apiCompatibilityLevel: 6`），所以必须取 NuGet 的 `lib/netstandard2.0/` 资产；`lib/netstandard2.1/`（MessagePack 也有，依赖更少）在 Std2.0 profile 下不可用。改 API 级别属于全局变更，别顺手改。
- **别重复提供 Unity 自带的 BCL**：Unity 自带 `System.Memory 4.0.99.0`，能满足对 `4.0.1.2/4.0.1.0` 的引用；同理 `System.Buffers`/`System.Numerics.Vectors`/`System.Reflection.Emit*`/`System.Threading.Tasks.Extensions` 都不用补。多塞会触发“同名预编译程序集重复”。
- **改已有 DLL 文件后**：Unity 可能不重新导入，用 `AssetDatabase.ImportAsset(path, ForceUpdate)` 或 `refresh_unity`；**新建** `.cs` 必须先 `refresh_unity(mode=force, scope=assets)`。
- **「同一条 socket 被库反复重握手」**（2026-10-09）：房主 Console 看起来像"一直有人进进出出"、sid 一串，其实只有一个客机。查法：`HostSession.PeerAddress`（反射读库私有 `m_remotePoint`）看对端是不是同一个；`HostSession` 已给 `OnConnected/OnDisConnected` **限流**（前 3 次 + 之后每 10s，带累计次数）。
- **「未入房会话循环重连」**：库按"**收不到数据**判掉线"（~15s），而"连上 → 入房"之间只有心跳一条报文 ⇒ 房主对**未入房**会话必须也回 Pong（`OnPingReq` 不要先判 `_players` 里有没有），否则它被自己的库判死并重连。
- **`memberIdleTimeout` 默认 8s，比库自己的 15s 更严** ⇒ 客户端一卡帧（尤其中途加载战斗场景）就超时被踢；而战斗期 `_joinClosed`/`_rosterFrozen` 会**拒绝重连** ⇒ 表现是"没关游戏却掉线且回不来"。要动就三选一：抬高超时、转场期间不踢、支持重连复用身份。
- **那条"后进房的人也能拿到本局配置"靠的是补发**：`NetHostSvc._lastTaskConfirm` 缓存**整条**广播，`OnJoinRoomReq` 里给新人补发（在 `JoinRoomRsp` 之后，KCP 可靠有序）⇒ 消息必须**自足**（种子 + 配置内容），别设计成"让成员自己查表"。
- **`Dispatch` 的异常要打全**：`Debug.LogError($"消息 {cmdId} 处理失败:{e}")` —— 只打 `e.Message` 会把"哪个处理器、哪一行"全丢掉（打包版实测过）。
- 不得不用 PowerShell 时注意两个坑：① 命令里的 `$var` 会被吞掉，复杂逻辑先写文件再 `-File` 执行；② `.ps1` 里出现中文会被 PowerShell 5.1 按 ANSI 解码，可能吐出引号字符导致“缺少字符串终止符”——脚本保持纯 ASCII。
- ⚠ 本机 PowerShell 另有 **Safe delete bulk-guard**：`Remove-Item -Recurse` 会被拦（报 `Safe delete could not verify this bulk deletion. Nothing was deleted.`）⇒ **删文件/目录别用 `Remove-Item -Recurse`**，走工具（`delete_file`）或 `del` 单个文件。
- ⚠ PowerShell 的 `Select-String -Path "**/*.cs"` **不支持递归通配**，会静默给出"0 命中"的**假阴性** ⇒ 递归扫描一律用 `Get-ChildItem -Recurse -Filter *.cs | Select-String …`（或直接用本 skill 的 Python 脚本）。

- **"改了内容却没编上"的两种假象**（2026-10-11）：① **假空转** = dll mtime 不动 / Console **也 0 error** / `refresh_unity` 照样返成功 —— 三个"通过"全是假象，**判据改反射**（`GetType`/`GetMethod` 是否为 null），解药 `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`（⚠ `external_changes_dirty:true` 时先 `AssetDatabase.Refresh(ForceUpdate)`）；② **假缺失** = `.cs.meta` 被写成等长全 `\x00` ⇒ Unity 导不出 guid ⇒ 该 `.cs` 不进编译、报 `CS0103 名称"X"不存在` 而文件明明在 ⇒ 先查 `manage_asset get_info` 的 `guid`，**别改代码**。
