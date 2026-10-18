# -*- coding: utf-8 -*-
r"""精简 MissionInterceptTrain 的 Inspector 字段：42 → 18（2026-10-18）。

判据（实测 Robbery.prefab vs 代码默认值）：42 个字段里只有 5 个在 prefab 上偏离默认
（departureCount 8 / trainSpeed 3 / edgeInset 5 / smoothPasses 3 / clearTarget -1），
其余都是"写完没再动过"的旋钮 ⇒ 收成 `const`（取值一律照 prefab 现值 ⇒ 行为不变），
Inspector 只留"换地图 / 换预制体 / 调节奏"才会改的 18 项。

⚠ 唯一有意的行为修正：`clearTarget` 从 prefab 里的 **-1**（所有位都置上 ⇒ 连 Cover
即巨型悬崖一起清）改成 `VegetationAndDetail`（项目口径，见记忆 topics §4）。

原则：断言全过才写盘；先备份到 Temp/split_backup/。
用法：python slim_intercept_fields.py
"""
import io
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MAIN = os.path.join(ROOT, 'Assets', 'Scripts', '06Gameplay', 'Mission', 'Main')
RAIL = os.path.join(MAIN, 'MissionInterceptTrain_Rail.cs')
GUARD = os.path.join(MAIN, 'MissionInterceptTrain_Guard.cs')
TUR = os.path.join(MAIN, 'MissionInterceptTrain_Turret.cs')
TRAIN = os.path.join(MAIN, 'MissionInterceptTrain_Train.cs')
BAK = os.path.join(ROOT, 'Temp', 'split_backup')
BOM = b'\xef\xbb\xbf'


def load(path):
    text = io.open(path, 'rb').read().decode('utf-8-sig')
    eol = '\r\n' if '\r\n' in text else '\n'
    lines = text.split(eol)
    if lines and lines[-1] == '':
        lines.pop()
    return lines, eol


def save(path, lines, eol):
    io.open(path, 'wb').write(BOM + eol.join(lines).encode('utf-8'))


def replace_block(lines, a, b, new_lines, exp_first, exp_last):
    """1 基闭区间替换（边界断言）"""
    assert lines[a - 1] == exp_first, (a, lines[a - 1])
    assert lines[b - 1] == exp_last, (b, lines[b - 1])
    return lines[:a - 1] + list(new_lines) + lines[b:]


def rename(lines, name, new, expect):
    """按词边界整文件改名（const 用帕斯卡，字段名 camelCase）"""
    pat = re.compile(r'\b%s\b' % name)
    hits = sum(len(pat.findall(l)) for l in lines)
    assert hits == expect, (name, hits, expect)
    return [pat.sub(new, l) for l in lines]


RAIL_NEW = """        #region 调参常量（原 Inspector 旋钮；取值 = 2026-10-18 前 Robbery.prefab 的实际值）
        // 这些旋钮（选点 / A* / 路基 / 清物）只在开发期调过一次，之后再没动过 ⇒ 收成常量，
        // Inspector 只留"换地图、换轨道预制体才会改"的几项。要再调就直接改这里（改回字段即可回到 Inspector）。
        private const float MinAngleSeparation = 120f;      // 起终点最小夹角(度)
        private const float MinChordRatio = 0.5f;           // 起终点最小弦长(占地图半径比例)
        private const float StepLength = 24f;               // 参考节距(米)
        private const float PlanExpand = 200f;              // 规划外扩(米)
        private const float AvoidMargin = 15f;              // 避让余量(米)
        private const float SlopeWeight = 2f;               // 坡度代价权重
        private const float MaxVertexTurnDeg = 8f;          // 单顶点最大转角(度)
        private const int SmoothPasses = 3;                 // 路径平滑次数
        private const float BedStep = 8f;                   // 路基取样间距(米)
        private const float BedSmoothLength = 60f;          // 路基纵坡平滑长度(米)
        private const float FlattenThreshold = 1.2f;        // 路基整平阈值(米)
        private const float FlattenBlend = 6f;              // 路基淡化带(米)
        private const float ClearStep = 8f;                 // 清物盖章间距(米)
        private const float ClearMargin = 1.5f;             // 清物余量(米)
        private const float ClearCanopy = 5f;               // 清物树冠补偿(米)
        private const bool RegisterCorridor = true;         // 登记走廊为已占(供后来者避让)
        private const float CorridorRegisterRadius = 12f;   // 走廊登记半径(米)
        // ⚠ 清物目标只给"植被 + 草花"：含 Cover 会把巨型悬崖（RockCoverDestructor）一起清掉。
        //   prefab 里原本存的是 -1（所有位都置上 ⇒ 连悬崖一起清），2026-10-18 一并纠正。
        private const TerrainClearTarget ClearTarget = TerrainClearTarget.VegetationAndDetail;
        #endregion

        [Foldout("轨道", true)]
        [InspectorName("轨道条数")]
        [SerializeField] private int trackCount = 3;
        [InspectorName("轨道预制体")]
        [SerializeField] private GameObject railPrefab;
        [InspectorName("边缘内缩(米)")]
        [SerializeField] private float edgeInset = 5f;""".split('\n')

GUARD_NEW = """        [Foldout("火车卫队", true)]
        [InspectorName("卫队落点后移(米)")]
        [SerializeField] private float guardSpawnBack = 25f;
        [InspectorName("护航位前导(米, 建议=火车速度×TickTime)")]
        [SerializeField] private float guardLead = 24f;

        #region 调参常量（原 Inspector 旋钮；取值 = prefab 实际值）
        // 纯手感旋钮，要改直接改这里（改回字段即可回到 Inspector）。
        private const float GuardLateral = 10f;             // 护航位侧向(米, 车头两侧)
        private const float GuardLineSpacing = 8f;          // 护航位错开间距(米)
        private const float GuardHoldDistance = 15f;        // 速度分档距离(米)
        private const float GuardCatchUpSpeedRatio = 1.35f; // 追赶速度系数(×火车速度)
        #endregion""".split('\n')

TUR_NEW = """        [Foldout("附加炮塔", true)]
        [InspectorName("附加炮塔(概率, 预制体)")]
        [SerializeField] private List<SKVP<int, GameObject>> additionalTurrets = new();

        /// <summary>车厢上的炮塔挂点（车厢子物体名；固定值，不进 Inspector）</summary>
        private const string TurretMountName = "Additional";""".split('\n')

TRAIN_NEW = """        /// <summary>车厢间距系数（0.92 = 相邻车厢留一点缝；固定值，不进 Inspector）</summary>
        private const float CarSpacingFactor = 0.92f;""".split('\n')

rail, eol = load(RAIL)
guard, eol2 = load(GUARD)
tur, eol3 = load(TUR)
train, eol4 = load(TRAIN)
assert eol == eol2 == eol3 == eol4

# ── 1. 用常量块替换字段块（边界断言） ──
rail = replace_block(rail, 20, 68, RAIL_NEW,
                     '        [Foldout("轨道", true)]',
                     '        [SerializeField] private float corridorRegisterRadius = 12f;')
guard = replace_block(guard, 23, 35, GUARD_NEW,
                      '        [Foldout("火车卫队", true)]',
                      '        [SerializeField] private float guardCatchUpSpeedRatio = 1.35f;')
tur = replace_block(tur, 22, 26, TUR_NEW,
                    '        [Foldout("附加炮塔", true)]',
                    '        [SerializeField] private string turretMountName = "Additional";')
train = replace_block(train, 23, 25, TRAIN_NEW,
                      '        [Foldout("列车生成", true)]',
                      '        [SerializeField] private float carSpacingFactor = 0.92f;')

# ── 2. 字段名 → 常量名（帕斯卡），并断言命中数 ──
for old, new, n in (('minAngleSeparation', 'MinAngleSeparation', 1),
                    ('minChordRatio', 'MinChordRatio', 1),
                    ('stepLength', 'StepLength', 2),
                    ('planExpand', 'PlanExpand', 1),
                    ('avoidMargin', 'AvoidMargin', 1),
                    ('slopeWeight', 'SlopeWeight', 1),
                    ('maxVertexTurnDeg', 'MaxVertexTurnDeg', 1),
                    ('smoothPasses', 'SmoothPasses', 1),
                    ('bedStep', 'BedStep', 1),
                    ('bedSmoothLength', 'BedSmoothLength', 1),
                    ('flattenThreshold', 'FlattenThreshold', 3),
                    ('flattenBlend', 'FlattenBlend', 2),
                    ('clearTarget', 'ClearTarget', 1),
                    ('clearStep', 'ClearStep', 1),
                    ('clearMargin', 'ClearMargin', 2),
                    ('clearCanopy', 'ClearCanopy', 1),
                    ('registerCorridor', 'RegisterCorridor', 1),
                    ('corridorRegisterRadius', 'CorridorRegisterRadius', 3)):
    rail = rename(rail, old, new, n)

for old, new, n in (('guardLateral', 'GuardLateral', 1),
                    ('guardLineSpacing', 'GuardLineSpacing', 1),
                    ('guardHoldDistance', 'GuardHoldDistance', 3),
                    ('guardCatchUpSpeedRatio', 'GuardCatchUpSpeedRatio', 2)):
    guard = rename(guard, old, new, n)

tur = rename(tur, 'turretMountName', 'TurretMountName', 4)
train = rename(train, 'carSpacingFactor', 'CarSpacingFactor', 1)

# ── 3. 写盘（先备份） ──
os.makedirs(BAK, exist_ok=True)
for src, lines in ((RAIL, rail), (GUARD, guard), (TUR, tur), (TRAIN, train)):
    dst = os.path.join(BAK, os.path.basename(src) + '.before-slim.bak')
    if not os.path.exists(dst):
        io.open(dst, 'wb').write(io.open(src, 'rb').read())
    save(src, lines, eol)
    fields = sum(1 for l in lines if 'SerializeField' in l)
    consts = sum(1 for l in lines if re.search(r'private const ', l))
    print('%4d 行  字段 %2d  常量 %2d  %s' % (len(lines), fields, consts, os.path.basename(src)))
print('OK（目标：字段 18 个）')
