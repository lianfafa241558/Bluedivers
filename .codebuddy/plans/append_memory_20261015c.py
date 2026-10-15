# -*- coding: utf-8 -*-
"""把「拦截火车：轨道扭 + 走廊清物」的结论追加到今天的记忆文件（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：轨道扭曲（转角过大）+ 铁轨走廊清树/清细节
- 病灶：逐段贪心每步都在「朝终点 ±75° 扇形」里独立选最优 ⇒ 地形噪声让相邻两步左右横跳，24m 一个硬拐点，看着像麻花
- 三层修法（MissionInterceptTrain.BuildRail）：
  ① 单步转角上限 maxTurnPerStepDeg(默认12°) 相对「上一段朝向 heading」硬约束 + 转角惩罚 turnPenalty(1 = 转1°等价多爬1°坡)；扇形全被否掉时用无上限兜底，避免绕障碍绕到背对终点卡死
  ② Chaikin 切角 SmoothPath(passes=smoothPasses 默认3)，首尾保留 ⇒ 拐点转角再减半
  ③ 铺节不再按折线顶点分段，而是按累计弧长把每节覆盖 [t, t+轨道长]（SamplePolyline+cumulative），朝向取这一小段的**弦向**、长轴按 pieceScale 缩放 ⇒ 首尾相接且相邻节角度差只剩这一小段弧的转角
- 走廊清物：TerrainClearer 在 FpsGame.MapUtils 程序集，06_Gameplay.asmdef 已引用它（可被 Mission 直接用）
  ⚠ ClearInRectXZ 只支持轴对齐矩形 ⇒ 斜轨道用它会把一大片方区清空；改用「沿弦向盖圆」ClearInRadius(半径 = 轨道半宽 + clearMargin)+盖章间距 clearStep(默认8m，与半径4.5m 有重叠不露缝)，实际是胶囊形走廊
  默认 clearTarget = VegetationAndDetail（树+石块+草花）；用 All 会连巨型悬崖覆盖物一起清掉，别轻易开
  收尾调一次 TerrainClearer.Flush() 立即兑现（跳过 4/s 树、8/s 草限流与 RefreshInterval），免得轨道先出来树还杵着
- 另外：TreeDestructor/TerrainDetailEraser 的静态口都有 Instance != null 兜底，清物请求在没有该管理器时是安全空操作
- 离线编译 06_Gameplay 0 错误
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
