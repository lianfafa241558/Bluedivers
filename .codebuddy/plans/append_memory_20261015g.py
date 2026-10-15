# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「还是直线且没躲避 = A* 无解走兜底」诊断与修法（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：真相是「A* 无解 ⇒ 走直线兜底」（诊断过程很关键）
- 现象：轨道仍然笔直且完全没躲避。**证据来自运行日志**（MCP read_console 过滤"火车"）：
  只抓到我自己那条 `拦截火车：规划不出路径（已占圆太多？），退化成直线` ⇒ 不是判据问题，是规划**无解**
- 排除：`execute_code` 读运行时数据（编译器只能 CodeDom=C#6：不能用 `$""` 插值 / 局部函数 / tuple，只能字符串拼接）
  - 当前不在 Play（Main 地形为空），`Constants.TaskBorder=64`（不是 128）、`ReservedCircles=0`（退出 Play 后 Main 被置 null ⇒ setter 清空），TreePrototypeRange=(7,18) / RockRange=(0,6)
  - ⇒ 事后读运行期静态表不可行，只能靠"运行期日志 + 修法兜底"
- 最可能成因：起终点压在占位圆（半径 = 任务点 mapEntitySize + avoidMargin）边上、或若干大圆把"规划外扩框"内的走廊封死
- 修法（MissionInterceptTrain）：
  ① `CollectObstacles(mapRadius)`：剔除半径 ≥ 40% 地图可用半径的圆（那种尺寸计入就是全图堵死，本身也绕不开）
  ② `PlanWithRelaxation`：完全避让 → 余量减半 → 余量 0 三级重试；日志打印"用到的避让余量"
  ③ 换起终点重试：规划仍失败就重新摇一对着重点（原来那对可能整个被包住），最多 3 轮
  ④ 清物半径加 `clearCanopy(5m)` 树冠补偿 —— 树是按**树干**位置清的，不加这项目测"树冠压在轨道上"
  ⑤ 日志补齐判据：`[拦截火车] 规划：避让圆 N 个（已占 a + 占地 b）/ 用到的避让余量 Xm / 路点 K / 起 终`、
     `[拦截火车] 轨道 N 节 / ... / 清物半径 Rm（树管理器 在/不在）/ 整平 ...`
- 验证：760 行 + 离线编译 06_Gameplay 0 错误 + `06_Gameplay.dll` 15:51:44 前进；控制台已清空，方便下次只看新日志
- ⚠ 经验：这类"行为完全不符合预期"的问题，**先读运行日志/运行时数据**再改代码；本次若不读日志，会继续在判据上瞎调
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
