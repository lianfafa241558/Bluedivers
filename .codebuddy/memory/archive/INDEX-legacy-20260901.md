# 旧 daily 标题索引（2026-09-01 之前）

> 正文已归档删除。**要读全文**：`git checkout HEAD -- .codebuddy/memory/<日期>.md`（这些文件曾提交进 HEAD）。
> 本索引只保留 `##`/`###` 标题，用于回答「哪天讨论过什么」。

## 2026-07-01（4488 字符，7 节）
- 为智能体编写 Unity C# 编码规范
- 创建项目专用 Skill
- 评估是否需要创建 Agent（结论：不需要）
- 拆解 DamageData 为接口 + 两套伤害配置
- 为 SustainedDamageData 添加自定义检视器
- 排查 SKVP 在 DamageData 里不单行显示的问题（结论：自定义检视器导致）
- 在 Drawer 内手动实现 SKVP 单行列表绘制

## 2026-07-02（862 字符，0 节）

## 2026-07-03（1143 字符，0 节）

## 2026-07-04（6922 字符，20 节）
- DataEditorWindow.cs 增强
  - 设计要点
- DataEditorWindow 架构重构（接口 + 泛型基类）
  - 新增文件结构（`Assets/Editor/DataTabs/`）
  - 架构要点
- 二次优化：移除 Item 包装 struct（方案 A3）
  - 改动
  - 收益
- 三次优化：RefreshData 模板方法化
  - 改动
  - 收益
- 异常状态伤害逻辑重构
  - 改动
  - 设计要点
- 异常状态满槽行为（Electric/Freeze/Vertigo/Terror）
  - 改动
  - 设计要点
- 异常状态满槽行为补充（Freeze/Toxicity/Hacker）
  - 改动
  - 设计要点

## 2026-07-05（676 字符，2 节）
- 重构 GenerateNoiseTerrain.cs 地形生成系统
  - 改动概要

## 2026-07-07（1379 字符，5 节）
- CustomLabelDrawer.cs 修复
- KaiserWave 类 & PhoenixEagleController 修改
  - PhoenixEagleController.cs 修改
  - WaveManager.cs — 新增 KaiserWave 类
- PhoenixEagleController.cs 彻底修复

## 2026-07-11（2547 字符，6 节）
- 武器升级编辑器窗口
- 武器热量系统
- ModifyAttrData 检视器 modifier 下拉框过滤（最终方案）
- 伤害类型系数 WeaponAttrType
- 爆炸伤害组伤害类型系数
- WeaponUpgradeEditorWindow 空引用/越界修复

## 2026-07-12（2337 字符，7 节）
- WeaponUpgradeController.cs 修复
- WeaponUpgradeController.cs 新增调试字段
- ShowText + SetParameter + AttrTag.TextOnly
- TryModify / ApplyAttrInfo ChangeValue 职责修正
- 回退记录
- CampData_SODrawer.cs 中文编码修复
- SimpleDecal.shader 印花摄像机在物体内部时不可见修复

## 2026-07-13（2142 字符，5 节）
- PlayerWeaponsManager 后坐力改为事件驱动
- 新增蓄力热量倍率 WeaponAttrType.ChargeHeatScale
- 修复 SelectRoleWnd 切换角色后 GameEndWnd/ArmamentWnd 显示旧模型的 bug
- GlobalEventSub 新增 OnSelectRolePreview 事件
- 修复 TerrainMainUtils.GetHeights 越界崩溃

## 2026-07-14（386 字符，0 节）

## 2026-07-15（1226 字符，8 节）
- DetectionModule 修复：敌人有目标但卡在 Idle 状态
  - 问题
  - 根因
  - 修复
- 修复：已死亡单位仍活动且无法被击杀
  - 问题
  - 根因
  - 修复

## 2026-07-16（924 字符，2 节）
- SelectRoleWnd.cs tipRoot 多分辨率适配修复
- MovieWnd.cs 黑边多分辨率适配修复

## 2026-07-17（2081 字符，5 节）
- 修复：Actor.OnRevive 未重置 ActorState
- 新增：Furniture_Vehicle 死亡后禁止进入驾驶
- 修复：EnemyMobile Follow 状态下单位不靠近目标反而远离
- 新增：UI Image Channel Mix Shader
- 修复：RobotWave 创建的单位碰撞体偶尔关闭

## 2026-07-18（2332 字符，8 节）
- VehicleController 代码规范化优化
- PlayerController 第一/第三人称切换功能
  - 参数设计
  - 第三人称逻辑（PlayerController_ThirdPerson.cs）
  - 第三人称瞄准 FOV 缩放
  - 切回第一人称瞬间跳转
  - 第三人称武器瞄准
- Furniture_NPCChat 自动搭话系统

## 2026-07-19（4304 字符，10 节）
- SettingWnd.cs 添加次级 layout 分类切换
- TerrainUtils.cs + AdditionTerrain 高度修复（最终方案）
- Infrared.cs 线段抽搐修复
- FreeCameraController.cs 添加 C 键轨道旋转
- FreeCameraController.cs 鼠标滚轮控制前进后退
- SubtitleNPC.cs 添加距离淡出效果
- AIInputUnitController.cs Turret.Rotate Idle 状态下疯狂绕 X 轴旋转修复
- PlayerController.OnEnable MissingReferenceException 修复
- VehicleController 第三人称进入载具 Camera.main 消失修复（v2：标志位方案）
- VehicleUI.cs 窗口状态感知显隐

## 2026-07-20（671 字符，1 节）
- 修复 SettingWnd 关闭时恢复错误的 WindowState（最终方案：WndManager.OnWindowStateSet）

## 2026-07-24（443 字符，1 节）
- PhoenixEagleController 新增 SetDiveDuration

## 2026-07-25（469 字符，1 节）
- GenerateKeyCodeItems.cs

## 2026-07-26（3126 字符，6 节）
- InputManager.cs — 添加 JSON 存档功能
- FpsHelper.cs PreventUnderground 重构 + GetNavMeshPoint 空值修复
- PlayerController.cs 倒地镜头改为第三人称轨道旋转控制
- FpsHelper.cs Teleport — 修复"怎么闪都返回原地"bug
- VehicleWeaponsManager.cs — 修复载具激光武器 NRE（Owner 为空）
- BaseSelfMoveableController.cs — 修复高台阶上下抖动

## 2026-07-27（696 字符，3 节）
- SubtitleWnd 第三人称载具 UI 抖动修复
  - 尝试过的方案（均失败）
  - 最终根因与修复

## 2026-07-28（745 字符，1 节）
- Scripts 目录 asmdef 分层分析（仅分析，用户手动改）

## 2026-08-01（10636 字符，7 节）
- WaveManager 首领唯一限制
- SoundGroup_SO 组合音效组
- EnemyControllerFX 音效组支持
- SoundGroup_SO 预览播放
- WeaponEnemyController Gizmos 弹道模拟
- AIInputUnitController.Turret 炮台重构
- GuideController 引导控制器

## 2026-08-02（7719 字符，14 节）
- GuideController 多阶段引导系统
- GuideController NavMesh 路径寻路
- GuideController 到达后对齐朝向 + 停止距离
- GuideController 到达后平滑转向
- GuideController 被障碍阻挡时绕行
- GuideController 绕行方向改进（障碍移除后不恢复）
- GuideController 小坡跳跃（NavMesh 路径上坡）
- GuideController 卡住检测重写（消除无意义绕行）
- GuideController 到达时空中卡死修复
- GuideController 重力始终有效 + 掉出保护
- GuideController 自动进入第0阶段
- GuideController 提示物（Hint）功能（已改为场景物体，不实例化）
- AirdropController NRE 修复
- Actor.WaitSetPos 主动注册到 unitQueryGrid

## 2026-08-03（1550 字符，3 节）
- AI 近战单位垂直高度够不着判定
- 第 7 点：丢失后搜索最后已知目标位置
- 第 8 点：弱点受击僵直

## 2026-08-06（1044 字符，3 节）
- 全工程调研 + skill/记忆落地
  - 调研结论
  - 写入文件

## 2026-08-07（6903 字符，9 节）
- 拆分 WaveManager.cs
- 清理运行时误引 UnityEditor
- 更新 memory 和 skill 文件
- 矿物提交闭环（进行中，核心代码已写）
- 技术债清单文件
- 第4项 结算保存（已完成，2026-08-07）
- 第3项 全队强化系统（已完成，2026-08-07）
- 新增 WeaponBag（武器背包，2026-08-07）
- 激光制导火箭弹 ProjectilePlayerHoming（2026-08-07）

## 2026-08-08（3398 字符，5 节）
- SoundGroup_SO 工具方法：变成另一个 SO 的子资源
- UseBag 键改造（BagBase 系列输入统一）
- RoleData_SO 数据栏增强（DataEditorWindow）
- RoleData_SO.speechGroups 自定义检视器
- speechGroups 添加按钮改为子资源选择窗

## 2026-08-09（4307 字符，4 节）
- SO 选择弹窗泛用框架重构（SOPickerPopup 统一）
- 沉淀为复用约定（文档化）
- 玩家新增语音调用（SpeechTypeEnum：CollOOPartsFail/ReLoad/Install/Uninstall/FinalMaga）
- ArmamentWnd 团队强化改为展开面板布局

## 2026-08-10（397 字符，0 节）

## 2026-08-11（528 字符，0 节）

## 2026-08-12（1694 字符，2 节）
- ArmamentWnd 团队强化禁止重复选择
- DRG 解包资源文件夹重命名（D:\岩深解包\DRGPacker4.27\Audio\SFX）

## 2026-08-16（2128 字符，4 节）
- OOPartBagWnd 改造：事件驱动刷新 + 拾取显示 + 5秒淡出
- PlayerOOPartInventory 改每种类型上限5（旧为总上限10）
- KeiSubmitWnd 加淡出（参考 OOPartBagWnd 结构）
- BattleManager.InitTerrain 三次 `TerrainUtils.Main = terrain` 分析

## 2026-08-18（233 字符，0 节）

## 2026-08-19（8994 字符，13 节）
- EnemyMobile 巨型单位脚下攻击 Bug 修复
- DetectionModule 增加最短转火时间
- 巨像炮塔 Y 轴瞄准偏高 排查记录（2026-08-19，未定论）
- 巨像炮塔 Y 轴瞄准偏高——真正根因与修复（2026-08-19）
- EnemyMobile.AttackStop 语义修正（2026-08-19）
- 齐射武器"残余弹量 < 单次消耗"死锁修复（2026-08-21）
- WeaponController 新增"强制连射"标旗（2026-08-19）
- 武器齐射逻辑下沉到 WeaponBaseController（2026-08-19）
- 子弹初始化传入炮口 Transform（2026-08-19）
- 发射点位字段封装为 protected + GetMuzzle(index)（2026-08-19）
- WeaponEnemyController.OnDrawGizmosSelected 修复（2026-08-19）
- 齐射激光子弹永不释放 Bug 修复（2026-08-19）
- Compare 条件显示对 List 字段无效/报错——根因与修复（2026-08-19）

## 2026-08-20（1978 字符，3 节）
- 修复 LX/Texture2 shader
- 新增护盾恢复技能 UnitSkill_ShieldRestore
- 新增环绕装甲技能 UnitSkill_OrbitArmor

## 2026-08-21（2964 字符，4 节）
- EnemyController 武器数组精简删除
- WeaponTemporaryController 改为输入驱动以支持射击分类
- ProjectileLaser 射线判定改为 SphereCast
- 无敌状态统一：ActorFlag.Invincible 为唯一真相源

## 2026-08-22（1979 字符，2 节）
- HpItemBoss 优化：同步 invincibleArmor + 单位无敌 FillR 变白
- Damageable 爆炸免疫字段化 + 批量迁移工具

## 2026-08-23（5898 字符，5 节）
- AI 状态机两套架构对比结论
- EnemyMobile 状态机结构试点重构（switch → 委托表）
- 伤害抗性系统重构（DRG式 → 绝地潜兵2式穿甲结算）
- AIInputUnitController 泛型化彻底下沉（switch 结构 → 泛型状态机基类）
- 状态机框架进一步下沉到 AIInputBaseController<T> + EnemyNestBuild 接入（2026-08-24）

## 2026-08-24（8853 字符，7 节）
- TransferDamageable 增加独立护甲等级（伤害系统重构后续）
- ArmorLevelMigrationTool 支持 TransferDamageable 独立护甲等级
- ArmorLevelMigrationTool 自动读取 Explosion 值到爆炸抗性
- 肢体编辑器窗口 LimbEditorWindow（含修复与增强）
- 拆毁值机制（DamageData 拆毁 > Health 拆毁则秒杀）
- InflictDamage 参数封装为 DamagePacket 结构体
- PlayerWeaponsManager：双持武器始终视为正在瞄准

## 2026-08-25（664 字符，1 节）
- ArchiverDataHandle.cs：ArchSettingDataDrawer 去折叠平铺

## 2026-08-26（5599 字符，4 节）
- ProjectileMine.cs 地雷触发判定改造
- DeployableMine 贴脸巨型敌人不炸 bug（2026-08-26 修复）
- SustainedEffect 伤害中心锚点改造（Collider → Transform）
- EnemyMobile 敌人"跟着左右移动但不上前/乱走"bug（2026-08-26 定位+修复）

## 2026-08-27（3580 字符，6 节）
- EnemyMobile 追击散开（最终方案：先绕后锁）
- 经验
- ProjectileStandard 子弹穿透地形排查（分析中）
- 2026-08-27 日志定位进展
- 2026-08-27 根因确认与修复
- 2026-08-27 修复确认与收尾

## 2026-08-28（2676 字符，0 节）

## 2026-08-29（13162 字符，13 节）
- 8月工作概览（跨会话回顾用）
- 单位半高度(HalfHeight) + 地雷竖直判定
- 空投舱(ProjectilePod)去武器依赖重构
- Actor 占位 Gizmo 改为胶囊体
- DamageData 是否模块化（结论：暂不动序列化结构）
- 团灭判负（HealBag 用完 + 全队阵亡）实现
- DamageDataDrawer 去重（已改）
- CountDownWnd 重构为可复用通用类（已改）
- 空投机器人落地目标点被 StopNav 清掉（已修）
- 技能释放类型枚举 + 嘲讽技能
- 技能释放类型"受击"下沉到基类 + 简化 Blink（当轮补充）
  - UnitSkill_Base 字段按释放类型条件显示（当轮）
- 巡逻队撤离时重新定向（PatrolContriller）

## 2026-08-31（4121 字符，4 节）
- DeployableMine 高空单位误引爆修复
- Damageable.cs 方法注释补全
- PlayerWeaponsManager 武器槽位映射重构（枚举与槽位解耦）
- 支援武器拾取 + 轮盘卸载系统（2026-08-31，已完成）
