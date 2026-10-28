# -*- coding: utf-8 -*-
"""【已执行的一次性脚本】把 OccupierData.asset 的 factions 从「纯名字」补成「名字 + 图标」。

图标来源：TaskManager.prefab 的 OccupierIcon 字典（`{fileID, guid, type}` 字面量原样搬）。
⚠ 只改 `factions:` 这一段，`maps:` 及以下一个字都不动（数值是策划手调的）。
⚠ 现已无实际用途（资产里图标已就位，且 TaskManager 上那段字典已删除）；留着只是为了说明当初怎么搬的。

用法：python .codebuddy/plans/patch_occupier_icons.py
"""
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT_ASSET = os.path.join(ROOT, 'Assets/Resources/GameData/OccupierData.asset')
TASK_PREFAB = os.path.join(ROOT, 'Assets/Resources/Prefabs/Manager/TaskManager.prefab')


def esc(s):
    """按 Unity 的写法转义成双引号标量（非 ASCII 一律 \\uXXXX）。"""
    out = []
    for ch in s:
        o = ord(ch)
        if ch == '"':
            out.append('\\"')
        elif ch == '\\':
            out.append('\\\\')
        elif 32 <= o < 127:
            out.append(ch)
        else:
            out.append('\\u%04X' % o)
    return '"' + ''.join(out) + '"'


def faction_icons(prefab_path):
    """读 TaskManager.prefab 里 OccupierIcon 字典：返回 [(名字, '{fileID: ..., guid: ..., type: 3}' 字面量)]。"""
    out = []
    inside = False
    pending = None
    with open(prefab_path, encoding='utf-8-sig') as f:
        for ln in f.read().splitlines():
            s = ln.strip()
            if s.startswith('OccupierIcon:'):
                inside = True
                continue
            if not inside:
                continue
            if s.startswith('- Key:'):
                pending = json.loads(s[len('- Key: '):])
            elif s.startswith('Value:') and pending is not None:
                out.append((pending, s[len('Value: '):].strip()))
                pending = None
            elif s.startswith('meetReset'):
                break
    return out


def main():
    with open(OUT_ASSET, encoding='utf-8') as f:
        lines = f.read().splitlines()

    start = lines.index('  factions:')
    end = lines.index('  maps:')
    old = [json.loads(ln.strip()[2:]) for ln in lines[start + 1:end] if ln.strip().startswith('- ')]

    pairs = faction_icons(TASK_PREFAB)
    if not pairs:
        raise SystemExit('没从 %s 读到 OccupierIcon 字典（字段可能已经从 TaskManager 删掉了）' % TASK_PREFAB)
    icons = dict(pairs)

    order = [n for n, _ in pairs if n in old]
    order += [n for n in old if n not in icons]
    if sorted(order) != sorted(old):
        raise SystemExit('名册名字对不上，拒绝改（避免丢条目）')

    block = ['  factions:']
    for name in order:
        block.append('  - name: ' + esc(name))
        block.append('    icon: ' + icons.get(name, '{fileID: 0}'))

    lines[start:end] = block
    with open(OUT_ASSET, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')

    print('名册已补图标：%d 项（地图/任务点未改动）' % len(order))


if __name__ == '__main__':
    main()
