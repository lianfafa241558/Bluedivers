# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「路基切出 V 形锯齿」根因修复（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：路基被切出"V 形锯齿"的根因与修法（地形工具）
- 现象：轨道本身是顺的，但床面沿路出现周期性 V 形楔块（弯道段尤其明显），看起来像"一格一格"
- 根因（`TerrainUtils.SmoothCorridorBed` 旧实现）：**按段顺序就地 lerp**
  - 段内用"局部坐标系（沿向 along∈[0,段长) × 侧向）"判定像素归属，再对同一 pixel **顺序** `Lerp(current, target, weight)`
  - 弯道处相邻段的断面矩形**会重叠覆盖同一批像素**（段的局部矩形是按段端点包围盒取的），于是同一像素被推两次，
    两次 target 不同（各自沿向插值）⇒ 硬拼接出 V 形；段末 40% 降权那个补丁在弯道上也对不齐
- 修法（新实现）：**每个像素只由"最近的那一段"成型一次**
  1. 扫全段：像素到线段的**最近点投影参数 t**（不是局部坐标系）⇒ 弯道不会重叠；用 `bestDistance[]` 选最小者胜出
  2. `targetHeight[] = Lerp(hFrom, hTo, t)`（t 即弧长比例 ⇒ 目标沿弧长连续）
  3. 全部扫完后再统一写入：横向权重只看"到折线的距离"，**软门用原始高度**判断要不要动土
  - 额外代价：两个 patch 级 float 数组（≤8MB，一次性）
- 顺带结论（调"更平滑"的三个旋钮）：
  - `路基纵坡平滑长度`(默认 60m)：调大 ⇒ 纵坡更笔直（60→120~200），贴挖更坚决但可能多挖多填
  - `路基整平阈值`(默认 1.2m)：调大 ⇒ 起伏不大的路段**完全不动土**（避免"半切"产生的波纹），轨道直接贴地
  - `路基淡化带`(默认 6m)：调大 ⇒ 护坡更缓（视觉更柔），走廊更宽
- 验证：离线编译 00_Utils/06_Gameplay 0 错误；`00_Utils.dll` 17:42:28 前进；Console 0 error
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
