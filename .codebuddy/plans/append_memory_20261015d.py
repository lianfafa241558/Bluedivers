# -*- coding: utf-8 -*-
"""把「拦截火车：路径提前规划 + 路基整平 + 共享已占圆登记表」的结论追加到今天的记忆文件（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：改成「提前规划路径」+ 修路基 + 新增共享「已占圆」登记表
### 新增薄数据：TerrainUtils.ReservedCircles（00Tools/TerrainMainUtils_Reserved.cs）
- 与 AreaCircles 同款「谁生产谁登记、消费方避让」，但**必须分开存**：AreaCircles 会被 RockCoverDestructor.SyncAreaCircles 整体重建（Clear → 把自己登记的加回），混一起会被误删
- 生命周期：TerrainUtils.Main setter 里 ClearReservedCircles()；MissionController.InitializeAsync 开头也清一次（本局从零登记）
- 生产者：MissionController.GenerateNewMissionPoint 选完点后 AddReservedCircle(bestPoint, newRange)；铁轨走廊也登记（registerCorridor）
- 消费者：MissionController.IsOverlapWithExistingPoints 多查一份；铁轨的 A* 规划把两类占位圆都当障碍
- 为什么走薄数据：06_Gameplay 不能反向引用 09_Managers（MissionController 的 missionCreatPoints 是 private）
- 时序已验证：missionCreatPoints 在 InitializeAsync 里全部生成完，之后（IsStartBattle 后）才 EventStart 各任务 ⇒ 规划时快照是完整的

### MissionInterceptTrain 改为「先规划」
- 用 8 邻域 A* 栅格替代逐段贪心（贪心看不到后面的地形/占位，撞上只能硬拐）
  代价 = 距离 ×(1 + slopeWeight×坡度/45°)；不可走 = 出空气墙圆 / 撞 ReservedCircles / 撞 AreaCircles（都加 avoidMargin）
  格距 = stepLength/2（默认 12m），格数上限 40000（超了格距×1.5）；起终点格强制放行
  斜向不许贴角穿过两个正交堵格；规划完用「视线拉直」去锯齿（8 邻域锯齿靠 Chaikin 抹不干净）
- 拉直后再 Chaikin 平滑到「单顶点转角 ≤ maxVertexTurnDeg(8°)」为止（SmoothToTurnLimit）
- 铺节照旧按累计弧长切片、弦向摆放、长轴缩放；新增路基：一节的前后+左右高差 > flattenThreshold(0.8m) 就
  yield return TerrainUtils.ModifyHeightMapRect(center, inner(len/2+1, W/2+余量), inner+blend, depth=0, yawDeg, true, false)
  （isSet=true 时压平目标 = 该点当前高度 - depth，与传入 pos.y 无关；yawDeg = Atan2(dir.x, dir.z)，即 LookRotation 的 Y 角）
  清物半径要把 flattenBlend 一起算进去，否则淡化带里的树被抬一半（ModifyTerrain 里同样的坑）
  最后 MarkTerrainChanged（只有真整平过）+ Flush() 一次兑现

### 坑（血泪）
- ⚠ 补 BOM 千万别写 open(p,'w').write(open(p).read())：**先截断后读**，文件直接被清空（本次把 TerrainMainUtils_Reserved.cs 清空了，且 Unity 已刷新进 csproj ⇒ 报「TerrainUtils 未包含 ClearReservedCircles」）。已固化脚本 `.codebuddy/plans/add_bom.py`（先读进内存再写）
- 新建 .cs 后离线编译看不到（它按 Unity 生成的 csproj 取源文件）⇒ 先 MCP refresh_unity（本次实例 Bluedivers@3d9f2357，端口 6400）
- 三证齐（本次）：Library/ScriptAssemblies/*.dll 15:19 前进 + Console 0 error + offline_compile 4 个程序集 0 错误
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
