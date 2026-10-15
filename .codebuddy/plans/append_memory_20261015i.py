# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「路基改统一成型（SmoothCorridorBed）」实现（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：路基从「逐块压平」改成「统一成型」（消除台阶）
- 病灶：路基是逐块 `ModifyHeightMapRect`（把一块矩形整体压成水平面）⇒ 斜坡上板间高差 = 纵坡 × 板长（8m），肉眼就是台阶
- 新工具（用户提议："先定路径，再统一改地形让它平滑"）：
  `TerrainUtils.SmoothCorridorBed(Vector3[] centerline, innerHalfWidth, blendWidth, deviationThreshold, refresh)`
  位置 `Assets/Scripts/00Tools/TerrainMainUtils_Corridor.cs`（partial，能直接用 Main/data/heightmapRes/maxTimePerFrame）
  - 做法：一次 GetHeights 覆盖"中心线包围盒 + reach"，逐段扫断面（局部坐标 = 沿向[0,段长) × 侧向[-reach,reach]），
    **目标高度沿弧长线性插值**（段 i 用 h_i→h_{i+1}）⇒ 段与段严丝合缝、整体连续，这才是"没有台阶"的关键
  - 断面权重：内宽内 =1，到 reach 用 SmoothStep 收边；段末 40% 降权让给下一段
  - 软门：`deviationThreshold`（|地面-设计| < 阈值一半不动土，> 阈值全量动）⇒ 平的、本来就贴的地方不白白整平
  - 收尾 SetHeightsDelayLOD + SyncHeightmap；实测统计走 `LastCorridorPixels` / `LastCorridorMaxDelta`（与文件里其它"实测用"静态量同款）
  - ⚠ 与 ModifyHeightMapRect 的分工：那个仍是"把内框压成一个水平面"，只适合平台/坑；斜坡成型必须用这个
- 任务侧：取样间距用 `bedStep * 0.5`（4m，折线化才不会又带出轻微阶梯感），innerHalfWidth = 轨道半宽 + 清物余量，
  blend = 路基淡化带，refresh=false，最后统一 Flush；日志加 `路基整平：动土 N 像素 / 最大高差 X m`
- 验证：783 行 + 离线编译 00_Utils(源34)/06_Gameplay 0 错误 + 两个 dll 16:12:14 前进 + Console 0 error
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
