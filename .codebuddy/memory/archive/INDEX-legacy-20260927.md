# 旧 daily 标题索引（2026-09-27 之前）

> 正文已归档删除。**要读全文**：`git checkout HEAD -- .codebuddy/memory/<日期>.md`（这些文件曾提交进 HEAD）。
> 本索引只保留 `##`/`###` 标题，用于回答「哪天讨论过什么」。

## 2026-09-12（4800 字符，8 节）
- 咨询：敌人单位死亡时"检视器配置事件（禁用/显示物体）"该怎么做
  - 诊断结论
  - 给出的方案（待用户选择）
  - 沉淀为长期原则（已写入 MEMORY.md「数据分层原则」）
  - 补充：划分唯一依据（回答用户追问"是不是再加一个结构类似的字段"）
- 修复：AirdropPod 对同一单位重复造成伤害
- 实现：VFXAirdropEffect 三处"武器强化影响战备"的 TODO（56-74）
- 修复：爆炸伤害点（伤害数字/受击反馈位置）显示错误

## 2026-09-14（9144 字符，8 节）
- 修复：ToonLit 附加光阴影不生效
- 修复：ToonLit 背光面也受光（与 URP Lit 不一致）
- 排查：ToonLit 材质不投射阴影
  - 真凶（用户给的具体材质）：`disabledShaderPasses: - SHADOWCASTER`
  - DepthNormalsOnly vs DepthOnly（URP 14）
  - 已做：溶解/嘴部裁切在阴影与深度 pass 里生效
  - 已查清：`disabledShaderPasses` 到底是怎么来的（用户最关心的点）
  - 已做：给 ToonLit 系列补 `DepthOnly` pass

## 2026-09-15（22236 字符，45 节）
- 修复：敌人受击闪白"执行到一半就停住（残留亮斑）"
  - 现象
  - 根因
  - 修复
  - 相关常识
- 重构：MPB 所有权从"条目"下沉到"渲染槽位"（解决多条目互相整块覆盖）
  - 问题
  - 方案（槽位为中心）
  - 附带收益
- 新增：战备配置界面 AirdropConfigWnd（战备购买 + 偏好）
  - 数据层
  - UI
  - ArmamentWnd
  - AirdropConfigWnd 预制体落地（MCP 构建）
  - AirdropConfigWnd 增加分组共用预览模型（eagleView / shopView）
  - AirdropConfigItem 条目子物体语义变更（用户改造 + 脚本跟进）
  - DisplayFieldDrawer 扩展类型 + 修空白占位（2026-09-16）
  - [DisplayField] 用法确认
- 环境调研：插件市场里与 Unity 相关的资源
- 接入 Unity MCP（CoplayDev/unity-mcp）
- 环境澄清 + unity-mcp 落地（本地嵌入包）
- 排查：unity-mcp「连不上 Unity 实例」
- ✅ unity-mcp 已连通（2026-09-15 15:45）
- 沉淀：新建 skill `.codebuddy/skills/unity-mcp/`
- 环境：安装 uv / uvx（Unity MCP 的前置依赖，已完成）
- MCP for Unity 插件窗口汉化（Editor UI 全面中文化）
  - 已汉化范围
  - 未汉化（有意保留）
  - 风险与回滚
- 更正 + 补充：unity-mcp skill 最终落地情况
- 修复：Unity 警告「meta 存在但文件夹不存在」+ MCP 包编译失败（严重）
  - 根因（两层，缺一不可）
  - 后果（Editor.log 实证）
  - 修复
  - 顺带发现（已按用户要求入库）
  - 教训
- 安装 uv / uvx（Python 3.12.10）
- unity-mcp skill 重写 + 双机（D 盘 / E 盘）路径修正
  - 起因
  - 关键发现（修正了此前"已连通"的认知）
  - 工具清单按源码核实（此前 skill 里有一批"假工具名"，其实是 action）
  - skill 改造
  - 双机（写入 MEMORY.md）
  - 实测备注
  - 后续：「连接上了吗」排查 + 修掉 server 启动失败

## 2026-09-16（14432 字符，14 节）
- MCP for Unity 编辑器状态读取
- MEMORY.md 整理
- UIImageChannelMix.shader 不响应 RectMask2D 的修复
- 单行内联（[Singleline]）在 AirdropData_SO 上失效的原因
- 实施：单行内联绘制统一（方案 B）
- AirdropData_SOEditor 补画 labels
- AirdropConfigWnd 列表改为「类型 + 标签」分组
- AirdropConfigWnd 展示模型：不再执行任何 Awake
- AirdropConfigWnd 展示模型：按体型自适应缩放（FitModelScale）
- 修正：FitModelScale 把模型缩到 0.002 的根因
- AirdropConfigWnd：默认朝向 180° + 模型"靠下"的根因（相机没正对挂点）
- AirdropConfigWnd：模型"太小" + 无人机"靠下"（第二轮）
- AirdropConfigWnd：预制体自身旋转叠加到默认朝向
- 诊断：RobotMine 爆炸后不消失

## 2026-09-17（5841 字符，14 节）
- 咨询：如何让另一个 Unity 工程也用上 MCP for Unity
  - 结论
  - 本机其他 Unity 工程（探测结果）
  - 接入步骤（目标工程视角）
  - 已沉淀
- 多实例路由：本工程落地工作区规则
  - 事实核对（实时数据 + 源码双向确认）
  - 已做修改
- 诊断：屏幕空间贴花（SimpleDecal）的两个视觉问题
  - 现象一：开雾后整个贴花足迹变成肉眼可见的色块
  - 现象二：相机进到 cube 内部就看不见印花
  - 顺带（同源隐患，不必马上改）
  - 已实施（同日）
  - 为什么 SimpleDecal_Colour 不会出方块（推导结论，可复用）

## 2026-09-18（1533 字符，1 节）
- 积雪幽灵：溶解成透明的物体为什么还盖雪（PipelineA / polySurface28）

## 2026-09-19（4395 字符，4 节）
- FakeAreaLight.shader 菲涅尔支持负值反向
- 炼油任务（MissionOilRefining）补全 Repair 阶段
- 新增工具：材质引用查询窗口 `Assets/Editor/MaterialUsageFinder.cs`
  - ⚠ 本项目下 MCP 改脚本后必须手动触发刷新

## 2026-09-20（30973 字符，28 节）
- 动态撤离任务（MissionEvacuateMobile）+ 凯伊带队撤离（SpecUnitKei）
- 验证
- 待办（已告知用户，未擅自改 prefab）
- 【修正】改用实例引用 + 实例事件，去掉 BattleEventSub 新增项
- 【追加】IsFast 快速模式跳过等待交互
- 【追加】动态撤离不做 StartPoint 顶替
- 【追加】撤离期间凯伊不响应呼叫
- 【追加】动态撤离增援改为「逐玩家追击+无限续刷」
- 【追加】追击增援：center 持续跟踪 + 倒地暂停/复活恢复
- 【追加】ZergWave 空投点跟随重部署
- 【追加】凯伊回收标记朝向
- 【修正】上轮"基类 InitMission 建了孤儿"的判断作废
- 【追加】动态撤离收尾与静态撤离统一 + Medivac 强制起飞
- 【追加】地形树：既无碰撞也不可摧毁 → 落地 A 方案（TreeDestructor）
- 【追加】TreeDestructor 增加"树种白名单"：只允许指定树种被爆炸摧毁
- 【追加】"完全没长树"排查：是数量公式 + 小图，不是 TreeDestructor 的锅
- 【落地】树生成改成与草一致的逐格概率算法
- 【落地】石块与树分开生成（关键前提：0-6 是石块，7-10 才是真树）
  - 【修正】CS1061：漏改"使用地形预设"日志
- 【落地】ModifyTerrain 的弹坑与附加地形都清除区域内的树（7-10）
- 【追加】LimitedLife 增加「延时回收」`EndDelay`
- 环境变化
- 【咨询】石头/地形装饰物自动使用地图纹理：方案结论（未改任何代码）
- 【追加】B 方案"不越界"实现要点（`ToonLit_Shared.hlsl` 被 7 个 shader 共用）
  - 【已实施】用户确认只 SetGlobalTexture、不管 tiling（不用 SetGlobalVector）
  - 【已执行 2+3】宏精简为两个 pass + 消除死采样
  - 【审计】ToonLit_Stone.shader 可删参数清单（已答复，未删）
  - 【已执行 A+B+E】ToonLit_Stone 参数瘦身 + 注释清理

## 2026-09-21（41356 字符，37 节）
- 【诊断】"子弹和人都能穿过树、别处却有空气墙"——MCP 实测结论
  - 1. 树穿模的根因：树的碰撞体压根没生成
  - 2. 运行时层掩码（GameContract.LayerDefinition @ 01_GameContract）
  - 3. 树原型上挂的碰撞体（实测，全部 layer=Default，含"树冠级"大碰撞体）
  - 4. 当前（开关关闭时）能撞到的"空气墙"清单
  - 4.5 【落地】细节（草/花）擦除：`08Map/TerrainDetailEraser.cs`
  - 4.8 【优化分析】弹坑的"三段昂贵操作"如何省（用户确认弹坑三件事都必须做）
  - 4.9 【落地】给 TerrainClearer 打耗时计时（用户拿去实测）
  - 4.10 【实测数据 + 已修 P0/P1】TerrainClearer 清除开销
  - 4.11 【桥直接测量 + 二次修复】碰撞体重建 10.75ms 纯属浪费
  - 4.13 【定案】"跳不起来 + 打到看不见的墙" = 树冠碰撞体（半径 7.4m 的隐形巨胶囊）
  - 4.12 【修正 4.11】树的碰撞体**确实存在**，10.75ms 是必要开销；"空气墙"= 树冠碰撞体
  - 5. 建议的修复顺序（未执行写操作）
- 【修复】天空层（星星/月亮/云）"吃雾就看不见、看得见就不吃雾"——已落地
  - 根因（两条独立的时序问题）
  - 落地实现
  - 副作用与替代方案
- 【调研】`Art/Nature/Rocks` 怎么加进场景（地形树 vs 场景预制体）——MCP 实测
- 【落地】地形树实例随机旋转（已改，未编译）
- 【决策待定】"按地形类型放置石块（地形覆盖）"放哪：A=GenerateNoiseTerrain / B=MissionController
- 【定案方向】石头=巨型悬崖（需可寻路），放 `GenerateNoiseTerrain` 的列表 + 概率进 `TerrainPresetData`
- 【已落地】地形覆盖石（巨型悬崖）①②③ —— 全在 `GenerateNoiseTerrain.cs`
- 【已落地】任务点/兴趣点避让悬崖（B 方案）—— `01Manager/Battle/MissionController.cs`
  - 【更正/重构】上面那版"MissionController 认识 GenerateNoiseTerrain"已废弃 → 改为 TerrainUtils 纯数据中转
- 【已落地】`08Map/RockCoverDestructor.cs`（地形覆盖物管理）+ ModifyTerrain 清悬崖
- 【核实/更正】ModifyTerrain 到底刷不刷 NavMesh + missionCreatPoints 的读取时机
  - 【最终形态】MissionController 改成"现读 + 遍历两张表"（16:11:06 编译通过，0 error）
- 【已落地】MapData_SO 加"树倍率 / 悬崖倍率"（16:43:31 编译通过，0 error）
- 【修 bug】① 悬崖浮空 ② 树碰撞体"初始能穿、改一下 TerrainData 就好了"（都在 MCP 实测后定位，17:23:57 编译通过）
  - ① 浮空 = 素材 pivot 不在网格底面（不是落点公式的锅）
  - ② 树碰撞体 = `SetTreeInstances` **不会**更新它，只有 TerrainCollider 重建才会
- 【已落地】去掉"出生点禁放"（用户：出生地也是动态生成的）
- 【修 bug】ModifyTerrain 没清"范围内的石块"→ 挖/填地形后石块悬空（17:49:50 编译通过）
- 【重构】三个清除管理器统一成一个门面：`08Map/TerrainClearer.cs`（17:55:43 编译通过，0 error）
  - 【已落地】TerrainClearer 改成"逐帧合并处理器"（18:01:26 编译通过，0 error）
- 【修复】开局即夜晚时夜间照明战备不解锁（BattleManager 漏掉开局昼夜事件）
  - 【修复·续】上面的"补发"会把白天授权减成 -1 → 战备永久锁死（用户发现，已改）

## 2026-09-22（1570 字符，1 节）
- 【功能】`DeployableMine` 加"爆炸延迟 + 触发爆炸的 UnityEvent"（编译 0 error）

## 2026-09-23（15842 字符，12 节）
- MEMORY.md 维护
- 咨询：Unity 能否用脚本注入 Terrain 树与细节
- 追问澄清：把 List<GameObject> 塞进"Terrain 树的层"
- 树原型没有 LODGroup 会怎样（查证 2022.3 官方手册，2026-09-23）
- 落地：TerrainTreePrototypePreparer（新工具，已运行完成）
- 重构：GenerateNoiseTerrain 去掉"花"并精简 TerrainPresetData（2026-09-23）
- 接入 MapData_SO 的原型数组（2026-09-23，续上一条）
- `VegetationSpawnData.prototypeRange` 去写死（2026-09-23，续）
- MEMORY.md 维护（第二次）
- 全屏雾强度接入「雾色渐变 A 通道」（EnvironmentLightingModule.UpdateFullscreenFog）
- 修复：细节（草）层换图残留 + 覆盖石坡度/峰顶判定（GenerateNoiseTerrain.cs）
- 咨询：代码注入的细节（草）要不要设 GPU 实例化（查证 + 实测）

## 2026-09-25（6777 字符，6 节）
- 第三人称交互时拉近相机视距（视距减半）
- 手持装备支持交互键（E）放下
- 记忆文件维护
- 方案 a 落地 + MapData_SO 天空/赤道色注入
- 「雾色 vs 赤道色」是否统一（数据核查结论）
- 天空盒：改用自写三段渐变天空盒（方案 B）
