# -*- coding: utf-8 -*-
"""删掉 TaskManager 预制体与场景里已经失效的 `OccupierIcon:` 序列化块。

背景：图标已搬进 `OccupierData_SO.factions`（名字 + 图标同一处），`TaskManager.OccupierIcon`
字段从类里删掉了 ⇒ 这两份 YAML 里留下的是"类里已经没有的字段"，Unity 会忽略但一直挂着。
这里按**精确缩进边界**整段删除（块内是 DisplayDic 的嵌套字段，必须在下一个 2 空格顶层字段处收手）。

⚠ 一次性脚本；`git diff` 应只有这两处各减 37 行。
"""
import os
import sys

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
TARGETS = ['Assets/Resources/Prefabs/Manager/TaskManager.prefab',
           'Assets/Scene/GameEnd.unity']


def block_end(lines, start):
    """从 `  OccupierIcon:` 往下找到"下一个恰好 2 空格缩进的顶层字段"。"""
    j = start + 1
    while j < len(lines):
        ln = lines[j]
        if ln.startswith('  ') and not ln.startswith('   ') and ln.strip():
            break
        if ln and not ln.startswith(' '):
            break
        j += 1
    return j


def main():
    for rel in TARGETS:
        path = os.path.join(ROOT, rel)
        with open(path, encoding='utf-8') as f:
            lines = f.read().splitlines()

        anchors = [i for i, ln in enumerate(lines) if ln == '  OccupierIcon:']
        if len(anchors) != 1:
            raise SystemExit('%s 里 `  OccupierIcon:` 出现 %d 次，拒绝盲删' % (rel, len(anchors)))

        i = anchors[0]
        j = block_end(lines, i)
        removed = j - i
        if removed != 37:
            raise SystemExit('%s 待删行数 %d ≠ 期望 37，边界可能变了，人工确认后再跑' % (rel, removed))

        print('%s：删除第 %d~%d 行（%d 行），紧接 %r' % (rel, i + 1, j, removed, lines[j]))
        del lines[i:j]
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
