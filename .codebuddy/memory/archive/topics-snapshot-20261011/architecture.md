# 主题 · 程序集(asmdef) / 命名空间 / 目录

> 由 `MEMORY.md` 路由表按触发词加载。**只增不删**，但同一条只留一处（发现重复就合并）。
> 预算 ≤ 8000 字符，超了按小节再拆。

## asmdef 层级与归属
- 拆分 100% 完成：预定义程序集 0；自有 **28** asmdef；`Assets/Scripts` 497 cs + `Assets/Editor` 47 cs
- 层级（下→上）：`00_Core`→`01_GameContract`→`00_Utils`→`03_Audio`→`04_Data`→`05_UnitCore`/`05_EffectComp`→`06_Gameplay`→`09_Managers`→`10_UI`/`10_Effect`
- ⚠ `references` 写 asmdef 的 **name 字段**，与所在**目录名常常不同**（目录 `08Map` 的 name=`FpsGame.MapUtils`；目录 `10UI` 的 name=`10_UI`）⇒ 对照表见「目录约定」；asmdef/`.meta` 带 BOM ⇒ 读用 `utf-8-sig`
- 工具：`.codebuddy/plans/asmdef_refs.py`（GUID references → 程序集名）
- **类型归属**：下界 = 依赖层 ∩ 上界 = 消费者最小层；**基类只能待在 `min(子类层, 消费者层)`**
- ⚠⚠⚠ 搬 `.cs` 一律 `AssetDatabase.MoveAsset`（文件系统 `Move-Item` 会让 Unity 判 guid 冲突 ⇒ 给新路径**重分配 guid** ⇒ **Missing Script**）。**权威判定 = 悬空引用检测**：`plans/find_dangling_guids.py`（引用 guid − 磁盘 meta guid）；修复 `plans/fix_guid_dangling.py --apply`（旧 guid→现文件用「内容哈希→唯一同名→类名」三路匹配，比 `p53_guid_repair.py` 纯同名法更全）→ `AssetDatabase.Refresh(ForceUpdate)` → 复查悬空=0。⚠ 日志 `A meta data file (.meta) exists but its asset ... can't be found` 就是此病的**表象**（多为 Warning 非 Error）
- ⚠ 抽代码块到新文件**using 不会跟着走**；动 asmdef 前扫三类隐形引用：扩展方法 / `internal` 成员 / 废弃 using
- 合并 asmdef 判据：被并方消费者里是否含"并入方的下层"；被并方层级 = `max(自身依赖层)`
- ⚠⚠⚠ **asmdef 下的 `Editor/` 会失去 editor-only 特权**（Unity 手册）⇒ 要么自带 `#if UNITY_EDITOR`（本项目惯例），要么独立 `includePlatforms:["Editor"]` 的 asmdef

## 目录约定
- 程序集根下子目录用**纯英文名词、无数字前缀**
- 历史：`Assets/Scripts` 早期有"数字前缀 + 功能"分层（`00Core`/`01Manager`/`02Game`…），现已换成程序集名（`00Core`→`00_Core`、`01Manager`→`09_Managers`、`02Game`→`06_Gameplay`）⇒ 旧路径出现在注释/脚本里时注意换算
- ⚠⚠ **「目录名 ↔ asmdef name」权威对照**（2026-10-02 按磁盘+asmdef 实测，共 16 组）：

| 目录 | asmdef name | 目录 | asmdef name |
|---|---|---|---|
| `00Attributes` | `00_Attribute` | `06Gameplay` | `06_Gameplay` |
| `00Core` | `00_Core` | `08Map` | `FpsGame.MapUtils` |
| `00GameContract` | `01_GameContract` | `09Manager` | `09_Managers` |
| `00Tools` | `00_Utils` | `10_Effect` | `10_Effect` |
| `02Rendering` | `02_Rendering` | `10UI` | `10_UI` |
| `03Audio` | `03_Audio` | `DayNightSystem` | `DayNightSystem` |
| `04Data` | `04_Data` | `NetTmp` | `02_Net` |
| `05UnitCore` | `05_UnitCore` | `05_EffectComp` | `05_EffectComp` |

- ⚠ 记忆/daily 里的**旧版路径**按此换算：`01Manager`→`09Manager`、`02Data`→`04Data`、`02Game`→`06Gameplay`、`04UI`(旧 asmdef `04_UI`)→`10UI`(`10_UI`)、`00Attribute`→`00Attributes`、`00_WndTools`(旧 asmdef，已删并)→`00Tools`+`10UI`；**定位代码一律写目录名路径**（`Assets/Scripts/<目录>/…`），asmdef 名只在改 `references` 时用

## 命名空间
- 全量 `FPSGame.*`（21 个子命名空间：`Game`/`AI`/`GameContract`/`Core`/`Mission`/`Gameplay`/`Utils`/`Furn`/`Attribute`/`Data`/`Managers`/`UI`/`Effect`/`Net`/`Rendering`/`Audio`/`MapUtils`/`WndTools`/`EditorExt`/`DayNightSystem`/`EffectComp`…）
- 非 FPSGame 只剩第三方：`RootMotion.*`/`MackySoft.*`/官方 NavMeshComponents（**禁改**）/`Pixeye.Unity`
- ⚠ **命名空间与程序集可以错位**（例：`06Gameplay/Common/{TargetData,IVehicleUIController}.cs` 声明 `FPSGame.GameContract` 却编进 `06_Gameplay`）⇒ 此时下层即使 `using` 了该命名空间也看不到
- ⚠⚠ `FPSGame.Attribute` 遮蔽 `System.Attribute` ⇒ 一律写 `System.Attribute.X`
- ⚠⚠⚠ 补/改 `.cs` 命名空间会打坏资产的 `[SerializeReference]`（YAML 存 `type:{class,ns,asm}`）⇒ 事后必跑 `sr_ns_audit.py` / `sr_ns_repair.py --apply`
- **补 using 三段式**：批量加 ns+using → `prune_using.py` 按 asmdef 剪枝（⚠ 同程序集是盲区，需"同 asmdef 放行"）→ 编译兜底；误删用 `fix_using_ns.py <ns>` 恢复
- ⚠ using 坑：普通 using 必须排在 `using static`/别名之前；`using var` 会被误判成块尾；拆行重拼用配套 `split/join`（防 `\r\r\n`）；扫描白名单 = `Assets/Scripts` + `Assets/Editor`
- ⚠ **`05UnitCore/UnitEventSub.cs` 声明 `namespace FPSGame.Game`（却在 `05_UnitCore` 程序集）** ⇒ `FPSGame.Game` **横跨两个程序集**；`06Gameplay/Weapon`、`Game/` 等文件离开 `FPSGame.Game` 时必须补 `using FPSGame.Game;` 才能看到 `UnitEventSub`/`NoiseData`（步骤④ 实测踩过，`WeaponBaseController` 因此报 CS0103/CS0246）
- ✅ **06Gameplay「目录↔ns」已对齐（2026-10-02 完成）**：`AI/`→`FPSGame.AI`、`Mission/`→`.Mission`、`Weapon/`→**`.Weapon`(新)**、`Data/`→**`.GameData`(新，避开 04_Data 的 `.Data`)**、`Interactable`+`Common`+`Bag`+`Player`+`Projectile`+`Airdrop`+`Effect`+`Events`+`Npc`→`.Gameplay`、`Game/`→`.Game`、`AI/StateMachine/Editor`→`.AI.Editor`；原 `Furn` 与 06 内的 `GameContract` 错位声明已并入 `.Gameplay`；`06_Gameplay.asmdef` `rootNamespace=FPSGame.Gameplay`。核对 = `plans/p2_ns_plan.py` 应输出「需改 ns 文件: 0」
- 搬 ns 工具链：`plans/ns_move.py <moves.json> --no-self --apply`（改 ns + 同步 using + 修 `using static <oldNs>.<Type>`）+ `add_usings.py`（按编译报错补 using）+ `repl_bytes.py`（全限定引用 `OldNs.Type` 的字节级替换，带次数校验）⇒ 每步用 `offline_compile.py` 秒级闭环验证
- ⭐ **新增 `07_NetGame`（2026-10-08 落地）**：`NetTmp` 一分为二 —— `02_Net` 收窄为**纯传输内核**（`ClientSession`/`HostSession`/`MessageCenter`/`NetMsgCodec`/`NetInbox`/`NetCmdId`/`Msg/PingMsg`，7 文件，`references` 仍 `[]`）；新增 `07_NetGame`（`NetSvc`/`NetHostSvc`/`NetRoomFlow`/`NetTransformFlow`/`RoomMeta`/`CmdId`/`Msg/{RoomMsg,BattleMsg}`，8 文件，`references: ["02_Net"]`）。层级 = `02_Net → 07_NetGame → 09_Managers → 10_UI`（09/10 的 `"02_Net"` 已改成 `"07_NetGame"`）。断开会话→服务反向耦合的手法 = 队列下沉为 `02_Net/NetInbox`。⚠ 新程序集首帧会打印 `will not be compiled, because it has no scripts`（建目录时的陈旧日志）⇒ 用 `CompilationPipeline.GetAssemblies()` 复核，别据此判失败。详见 `topics/net.md`。
