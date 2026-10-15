# -*- coding: utf-8 -*-
r"""拆分 MissionInterceptTrain 的三个 partial（2026-10-16）。

产出：
  MissionInterceptTrain_Turret.cs  ← _Train 的「附加炮塔」region + 两个配置字段 + turretPlan/turretEntries
  MissionInterceptTrain_Guard.cs   ← _Train 的「卫队」region + guardSpawnBack
  RailPathPlanner.cs               ← _Rail 的「起终点 / 路径规划(A*) / 折线工具 / 坡度」+ MaxPlanCells
  RailMetrics.cs                   ← _Rail 的 MeasureRail + RailMetrics 结构体

原则：
  * 纯搬运，不改行为；只做"字段换名（字段移到别的类 ⇒ 加 _ 前缀）+ 调用点改写"。
  * **断言全过才写盘**；写盘前把两个源文件备份到 Temp/split_backup/。
  * 行号是 1 基、基于拆分前的文件内容（改了源文件就别再跑本脚本）。
用法：python split_intercept_train.py
"""
import io
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MAIN = os.path.join(ROOT, 'Assets', 'Scripts', '06Gameplay', 'Mission', 'Main')
TRAIN = os.path.join(MAIN, 'MissionInterceptTrain_Train.cs')
RAIL = os.path.join(MAIN, 'MissionInterceptTrain_Rail.cs')
TUR = os.path.join(MAIN, 'MissionInterceptTrain_Turret.cs')
GUARD = os.path.join(MAIN, 'MissionInterceptTrain_Guard.cs')
PLANNER = os.path.join(MAIN, 'RailPathPlanner.cs')
METRICS = os.path.join(MAIN, 'RailMetrics.cs')
BAK = os.path.join(ROOT, 'Temp', 'split_backup')

BOM = b'\xef\xbb\xbf'

TRAIN_USINGS = ['using System.Collections.Generic;', 'using FPSGame.Attributes;', 'using FPSGame.AI;',
                'using FPSGame.Core;', 'using FPSGame.Game;', 'using FPSGame.GameContract;',
                'using FPSGame.Gameplay;', 'using FPSGame.Utils;', 'using UnityEngine;']
RAIL_USINGS = ['using System.Collections.Generic;', 'using FPSGame.Utils;', 'using UnityEngine;']


def load(path):
    text = io.open(path, 'rb').read().decode('utf-8-sig')
    eol = '\r\n' if '\r\n' in text else '\n'
    lines = text.split(eol)
    if lines and lines[-1] == '':
        lines.pop()
    return lines, eol


def save(path, lines, eol):
    io.open(path, 'wb').write(BOM + eol.join(lines).encode('utf-8'))


def blk(lines, a, b):
    """1 基闭区间切片"""
    return list(lines[a - 1:b])


def one(text, old, new, n=1):
    c = text.count(old)
    assert c == n, '期望 %d 处、实际 %d 处：\n%s' % (n, c, old[:160])
    return text.replace(old, new)


train, eol = load(TRAIN)
rail, eol2 = load(RAIL)
assert eol == eol2
assert len(train) == 626, len(train)
assert len(rail) == 810, len(rail)

# ─────────────────────────── 边界断言（改过文件就别再跑） ───────────────────────────
assert train[24] == '        [InspectorName("卫队落点后移(米)")]', train[24]
assert train[25].startswith('        [SerializeField] private float guardSpawnBack'), train[25]
assert train[27] == '        [Foldout("附加炮塔", true)]', train[27]
assert 'additionalTurrets = new()' in train[29], train[29]
assert 'turretMountName = "Additional"' in train[31], train[31]
assert train[50] == '        /// <summary>', train[50]
assert train[55] == '        private int[][] turretPlan;', train[55]
assert 'turretEntries = new();' in train[57], train[57]
assert train[58] == '' and train[59] == '        /// <summary>已发车班次（0 基）</summary>', repr(train[58:60])
assert train[206] == '        #region 附加炮塔', train[206]
assert train[490] == '        #endregion', train[490]
assert train[492] == '        #region 卫队', train[492]
assert train[558] == '        #endregion', train[558]
assert train[560] == '        #region 收尾与进度', train[560]

assert rail[67] == '        /// <summary>规划栅格的节点上限（超了就放大格距）：栅格是 O(格数) 的内存与搜索，别为了一张大图卡住开局</summary>', rail[67]
assert rail[68] == '        private const int MaxPlanCells = 40000;', rail[68]
assert rail[69] == '', repr(rail[69])
assert rail[282] == '        #region 起终点', rail[282]
assert rail[367] == '        #endregion', rail[367]
assert rail[369] == '        #region 路径规划（A*）', rail[369]
assert rail[653] == '        #endregion', rail[653]
assert rail[655] == '        #region 折线工具', rail[655]
assert rail[696] == '        #endregion', rail[696]
assert rail[698] == '        #region 地表处理与尺寸', rail[698]
assert rail[722].startswith('        /// <summary>世界点的坡度'), rail[722]
assert 'private static float SteepnessDeg' in rail[723], rail[723]
assert rail[730] == '        }', rail[730]
assert rail[732] == '        /// <summary>', rail[732]
assert 'private static RailMetrics MeasureRail' in rail[740], rail[740]
assert rail[792] == '        }', rail[792]
assert rail[795] == '        private struct RailMetrics', rail[795]
assert rail[805] == '        }', rail[805]
assert rail[807] == '        #endregion', rail[807]

# ─────────────────────────── 1. 附加炮塔 → MissionInterceptTrain_Turret.cs ───────────────────────────
turret_fields = blk(train, 28, 32)
turret_plan = blk(train, 51, 58)
turret_region = blk(train, 207, 491)
# 顺手修掉一处多缩进（Debug 遗留行下面的 return）
turret_region = [l.replace('                return turret;', '            return turret;') for l in turret_region]

turret_file = (TRAIN_USINGS + ['', 'namespace FPSGame.Mission', '{',
    '    /// <summary>',
    '    /// 拦截火车 · 车厢附加炮塔（partial）：配置与概率计划表、房主生成（<c>CreatUnit</c> + 4052）、',
    '    /// 成员按槽位复刻（含"车厢还没建出来"时的挂起补挂）、随车厢一起收尾。',
    '    /// <para>⚠ 静态同步：概率计划表在本任务的<b>第一个 yield 之前</b>算好（两端同一份 BattleRandom 流），',
    '    /// 发车只查表 ⇒ 连"该不该挂"都一致；复刻位置一律取<b>本端车厢挂点</b>，不用房主消息里的落点',
    '    /// （否则会把 RTT 期间车厢已开出的距离算进去）。</para>',
    '    /// </summary>',
    '    public partial class MissionInterceptTrain',
    '    {']
    + turret_fields + [''] + turret_plan + [''] + turret_region + ['    }', '}'])

# ─────────────────────────── 2. 卫队 → MissionInterceptTrain_Guard.cs ───────────────────────────
# ⚠ 原字段靠"上一个带 [Foldout] 的字段"归组（EditorOverride 的 prevFold 规则）⇒ 搬出来必须自带 [Foldout]
guard_field = ['        [Foldout("火车卫队", true)]'] + blk(train, 25, 26)
guard_region = blk(train, 493, 559)

guard_file = (TRAIN_USINGS + ['', 'namespace FPSGame.Mission', '{',
    '    /// <summary>',
    '    /// 拦截火车 · 火车卫队（partial）：随列车发车生成（房主 <c>CreatPatrol</c> + 4052 复刻）、',
    '    /// 按 Tick 节拍把巡逻点挪到火车当前位置、任务收尾时静默移除。',
    '    /// <para>⚠ 引导必须"先发车、后引导"且落在<b>同一帧</b>内（理由见 <c>Tick</c> 里的注释）。</para>',
    '    /// </summary>',
    '    public partial class MissionInterceptTrain',
    '    {']
    + guard_field + [''] + guard_region + ['    }', '}'])

# ─────────────────────────── 3. 量尺 → RailMetrics.cs ───────────────────────────
measure_doc_and_body = blk(rail, 733, 793)          # MeasureRail 的 doc + 方法体
metrics_fields = blk(rail, 798, 805)                # 结构体字段（含各自的 doc）
measure_text = eol.join(measure_doc_and_body)
measure_text = one(measure_text, 'private static RailMetrics MeasureRail(GameObject prefab)',
                   'public static RailMetrics Measure(GameObject prefab)')

metrics_file = (['using UnityEngine;', '', 'namespace FPSGame.Mission', '{',
    '    /// <summary>',
    '    /// 一个模型预制体的"量尺"结果（车头 / 车厢 / 轨道通用）：长轴长度与轴向、把长轴转到 +Z 的偏航、',
    '    /// 底面到 pivot 的高度。铺轨、摆车厢、算清物走廊都按它来 ⇒ 换模型不用改代码。',
    '    /// </summary>',
    '    internal struct RailMetrics',
    '    {']
    + metrics_fields + ['']
    + measure_text.split(eol) + ['    }', '}'])

# ─────────────────────────── 4. 规划算法 → RailPathPlanner.cs ───────────────────────────
edge = blk(rail, 283, 368)
astar = blk(rail, 370, 654)
poly = blk(rail, 656, 697)
slope = blk(rail, 723, 731)
const_lines = blk(rail, 68, 69)

planner_body = eol.join(edge + [''] + astar + [''] + poly + [''] + slope)

# 4.1 字段换名：先改 XML cref（同名 token 混在一起会一起被换掉，顺序必须"先 cref 后代码"）
planner_body = one(planner_body, '<see cref="slopeWeight"/>', '<see cref="_slopeWeight"/>', 1)
planner_body = one(planner_body, '<see cref="avoidMargin"/>', '<see cref="_avoidMargin"/>', 1)
planner_body = one(planner_body, '<see cref="minAngleSeparation"/>', '<see cref="_minAngleSeparation"/>', 2)
planner_body = one(planner_body, '<see cref="minChordRatio"/>', '<see cref="_minChordRatio"/>', 2)
for name, count in (('stepLength', 3), ('planExpand', 2), ('slopeWeight', 2), ('avoidMargin', 3),
                    ('minAngleSeparation', 1), ('minChordRatio', 1), ('random', 1)):
    # ⚠ 必须按"词边界"替换：前一步已经造出 _slopeWeight 这类词，纯子串匹配会把它们再算一次
    found = re.findall(r'\b%s\b' % name, planner_body)
    assert len(found) == count, (name, len(found), count)
    planner_body = re.sub(r'\b%s\b' % name, '_' + name, planner_body)

# 4.2 避让余量改由构造注入 ⇒ 摘掉 margin 形参（两处）与实参（两处调用）
planner_body = one(planner_body,
    eol.join(['        private bool TryPickEdgePair(Vector2 center, float radius, List<TerrainUtils.AreaCircle> obstacles, float margin,',
              '            bool requireFree, out Vector2 startPoint, out Vector2 endPoint)']),
    eol.join(['        public bool TryPickEdgePair(Vector2 center, float radius, List<TerrainUtils.AreaCircle> obstacles,',
              '            bool requireFree, out Vector2 startPoint, out Vector2 endPoint)']))
planner_body = one(planner_body,
    '            List<TerrainUtils.AreaCircle> obstacles, float margin, bool requireFree, out Vector2 point, out float angle)',
    '            List<TerrainUtils.AreaCircle> obstacles, bool requireFree, out Vector2 point, out float angle)')
planner_body = one(planner_body,
    'TryPickEdgePoint(center, radius, -1f, obstacles, margin, requireFree, out startPoint, out float startAngle)',
    'TryPickEdgePoint(center, radius, -1f, obstacles, requireFree, out startPoint, out float startAngle)')
planner_body = one(planner_body,
    'TryPickEdgePoint(center, radius, startAngle, obstacles, margin, requireFree, out endPoint, out _)',
    'TryPickEdgePoint(center, radius, startAngle, obstacles, requireFree, out endPoint, out _)')
# ⚠ TryPickEdgePoint 内部那个 margin 也得跟着换名（它原来是形参；PlanPath 自己的 margin 形参保留、不能动）
planner_body = one(planner_body, 'HitCircle(obstacles, candidate, margin)',
                   'HitCircle(obstacles, candidate, _avoidMargin)')
# 4.3 对外的三个入口要 public
planner_body = one(planner_body, '        private static List<TerrainUtils.AreaCircle> CollectObstacles(float mapRadius)',
                   '        public static List<TerrainUtils.AreaCircle> CollectObstacles(float mapRadius)')
planner_body = one(planner_body, '        private List<Vector2> PlanWithRelaxation(',
                   '        public List<Vector2> PlanWithRelaxation(')
planner_body = one(planner_body, '        private static void SmoothToTurnLimit(',
                   '        public static void SmoothToTurnLimit(')

planner_file = (RAIL_USINGS + ['', 'namespace FPSGame.Mission', '{',
    '    /// <summary>',
    '    /// 拦截火车 · 轨道路径规划器：地图边缘选点 → 栅格 A*（含坡度代价与"拉直"）→ Chaikin 平滑，',
    '    /// 外加地形坡度采样。整块都是纯算法、不碰 <c>MonoBehaviour</c> 状态，参数一律由构造注入。',
    '    /// <para>▍为什么独立成类：规划涉及的旋钮（节距 / 外扩 / 坡度权重 / 避让余量 / 夹角弦长）与任务的',
    '    /// 生成流程无关，混在一个 partial 里会让"轨道生成编排"被 400 行算法淹没。</para>',
    '    /// <para>⚠ 只在本端算、结果两端一致（只依赖 <see cref="_random"/> 与两端相同的序列化参数）。</para>',
    '    /// </summary>',
    '    internal sealed class RailPathPlanner',
    '    {']
    + const_lines + ['']
    + ['        private readonly float _stepLength;',
       '        private readonly float _planExpand;',
       '        private readonly float _slopeWeight;',
       '        private readonly float _avoidMargin;',
       '        private readonly float _minAngleSeparation;',
       '        private readonly float _minChordRatio;',
       '        private readonly System.Random _random;',
       '',
       '        public RailPathPlanner(float stepLength, float planExpand, float slopeWeight, float avoidMargin,',
       '            float minAngleSeparation, float minChordRatio, System.Random random)',
       '        {',
       '            _stepLength = stepLength;',
       '            _planExpand = planExpand;',
       '            _slopeWeight = slopeWeight;',
       '            _avoidMargin = avoidMargin;',
       '            _minAngleSeparation = minAngleSeparation;',
       '            _minChordRatio = minChordRatio;',
       '            _random = random;',
       '        }',
       '']
    + planner_body.split(eol) + ['    }', '}'])

# ─────────────────────────── 5. 从 _Train 里删掉搬走的行 ───────────────────────────
drop_train = [(207, 560), (51, 59), (28, 33), (25, 26)]
train_keep = [l for i, l in enumerate(train, start=1)
              if not any(a <= i <= b for a, b in drop_train)]
assert len(train_keep) == 626 - 354 - 9 - 6 - 2, len(train_keep)
train_text = eol.join(train_keep)
train_text = one(train_text, 'RailMetrics engineMetrics = MeasureRail(enginePrefab);',
                 'RailMetrics engineMetrics = RailMetrics.Measure(enginePrefab);')
train_text = one(train_text, 'RailMetrics carMetrics = carPrefab != null ? MeasureRail(carPrefab) : default;',
                 'RailMetrics carMetrics = carPrefab != null ? RailMetrics.Measure(carPrefab) : default;')

# ─────────────────────────── 6. 从 _Rail 里删掉搬走的行 + 改调用点 ───────────────────────────
drop_rail = [(68, 70), (283, 698), (723, 807)]
rail_keep = [l for i, l in enumerate(rail, start=1)
             if not any(a <= i <= b for a, b in drop_rail)]
assert len(rail_keep) == 810 - 3 - 416 - 85, len(rail_keep)
rail_text = eol.join(rail_keep)
rail_text = one(rail_text, '        #region 地表处理与尺寸', '        #region 地表处理')
rail_text = one(rail_text, 'railMetrics = MeasureRail(railPrefab);', 'railMetrics = RailMetrics.Measure(railPrefab);')
rail_text = one(rail_text, '            List<TerrainUtils.AreaCircle> obstacles = CollectObstacles(mapRadius);',
    eol.join(['            // 规划器：边缘选点 / 栅格 A* / 折线平滑整块算法都在 RailPathPlanner.cs（参数注入，不碰本类状态）',
              '            RailPathPlanner planner = new(stepLength, planExpand, slopeWeight, avoidMargin,',
              '                minAngleSeparation, minChordRatio, random);',
              '            List<TerrainUtils.AreaCircle> obstacles = RailPathPlanner.CollectObstacles(mapRadius);']))
rail_text = one(rail_text, 'obstacles, avoidMargin, true, out Vector2 startPoint, out Vector2 endPoint)',
                'obstacles, true, out Vector2 startPoint, out Vector2 endPoint)')
rail_text = one(rail_text, 'obstacles, avoidMargin, false, out startPoint, out endPoint)',
                'obstacles, false, out startPoint, out endPoint)')
rail_text = one(rail_text, 'if (!TryPickEdgePair(', 'if (!planner.TryPickEdgePair(', 2)
rail_text = one(rail_text, 'PlanWithRelaxation(', 'planner.PlanWithRelaxation(', 2)
rail_text = one(rail_text, 'SmoothToTurnLimit(track.Points, maxVertexTurnDeg, smoothPasses);',
                'RailPathPlanner.SmoothToTurnLimit(track.Points, maxVertexTurnDeg, smoothPasses);')

# ─────────────────────────── 7. 写盘（先备份源文件） ───────────────────────────
os.makedirs(BAK, exist_ok=True)
for src in (TRAIN, RAIL):
    dst = os.path.join(BAK, os.path.basename(src) + '.before-split.bak')
    if not os.path.exists(dst):
        io.open(dst, 'wb').write(io.open(src, 'rb').read())
        print('备份:', dst)

save(TUR, turret_file, eol)
save(GUARD, guard_file, eol)
save(PLANNER, planner_file, eol)
save(METRICS, metrics_file, eol)
save(TRAIN, train_text.split(eol), eol)
save(RAIL, rail_text.split(eol), eol)

for p in (TUR, GUARD, PLANNER, METRICS, TRAIN, RAIL):
    print('%5d 行  %s' % (len(io.open(p, 'rb').read().decode('utf-8-sig').splitlines()), os.path.relpath(p, ROOT)))
print('OK')
