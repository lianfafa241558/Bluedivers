# -*- coding: utf-8 -*-
"""把过长的 WeaponUpgradeEditorWindow.cs 按既有「========== 分区」切成多个 partial 文件。

约定：新文件与主类同目录、文件名 = 主类名_分部名.cs；保留 UTF-8 BOM 与 CRLF。
只做行范围搬移，不改任何代码内容（主类声明除外：加 partial）。
"""
import os

SRC = r'e:\Bluedivers\Assets\Editor\WeaponUpgradeEditorWindow.cs'
DIR = os.path.dirname(SRC)

raw = open(SRC, 'rb').read()
has_bom = raw.startswith(b'\xef\xbb\xbf')
text = raw.decode('utf-8-sig')
nl = '\r\n' if '\r\n' in text else '\n'
lines = text.splitlines(keepends=True)


def seg(a, b):
    """1-based 闭区间行号。"""
    return ''.join(lines[a - 1:b])


USINGS = nl.join([
    'using System;',
    'using System.Collections.Generic;',
    'using System.Linq;',
    '',
    'using FPSGame.Game;',
    'using UnityEditorInternal;',
    '',
    'using UnityEditor;',
    'using UnityEngine;',
    'using FPSGame.EditorExt;',
    'using FPSGame.GameData;',
    'using FPSGame.Weapon;',
    'using FPSGame.Core;',
    'using FPSGame.Gameplay;',
    '',
    'namespace FPSGame.Game.Editor',
    '{',
]) + nl


def part_file(name, summary, a, b):
    body = seg(a, b)
    if not body.endswith(nl):
        body += nl
    content = (USINGS
               + '    /// <summary>' + nl
               + '    /// ' + summary + nl
               + '    /// </summary>' + nl
               + '    public partial class WeaponUpgradeEditorWindow' + nl
               + '    {' + nl
               + body
               + '    }' + nl
               + '}' + nl)
    path = os.path.join(DIR, name)
    data = content.encode('utf-8')
    if has_bom:
        data = b'\xef\xbb\xbf' + data
    open(path, 'wb').write(data)
    return path, content.count(nl)


# ---- 主文件：1..681（窗口骨架 / 左列表 / 右侧 Tab / 属性页）----
main_body = seg(1, 681)
main_body = main_body.replace(
    'public class WeaponUpgradeEditorWindow : EditorWindow',
    'public partial class WeaponUpgradeEditorWindow : EditorWindow', 1)
main_body += '    }' + nl + '}' + nl
main_data = main_body.encode('utf-8')
if has_bom:
    main_data = b'\xef\xbb\xbf' + main_data
open(SRC, 'wb').write(main_data)

# ---- 分部文件 ----
report = [
    part_file('WeaponUpgradeEditorWindow_Score.cs',
              '评分页：权重表 / 锚点 / 明细行 / 对比表 / 评级（只画，不算）。', 682, 1366),
    part_file('WeaponUpgradeEditorWindow_ScoreCompute.cs',
              '评分计算：BuildScoreReport 与各类取值/曲线工具（口径改动看这里）。', 1367, 1887),
    part_file('WeaponUpgradeEditorWindow_Derivative.cs',
              '衍生物折算（持续效果 / 临时武器 / 地面火焰）与伤害成分口径（对齐 Health.HandleDamage）。', 1889, 2169),
    part_file('WeaponUpgradeEditorWindow_Upgrade.cs',
              '改装树 / 显示属性 / 模组 / 底部 SO 检查器 / 增删选项。', 2171, 3071),
    part_file('WeaponUpgradeEditorWindow_Common.cs',
              '样式构建与内部数据结构（评分报告 / 列表项）。', 3073, 3263),
]

print('main lines:', main_body.count(nl))
for p, n in report:
    print(os.path.basename(p), n)
print('bom:', has_bom, 'newline:', 'CRLF' if nl == '\r\n' else 'LF')
