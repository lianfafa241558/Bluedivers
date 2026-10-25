"""检查 SelectMapWnd 预制体里 mapRoot / areaRoot 的子物体名与顺序。

用法: python selectmap_roots.py <prefab 路径>
目的：核对「mapRoot 子物体下标」与 TaskManager.OrderedMaps()（按 mapId 排序）是否同序。
"""
import re
import sys

TAG = re.compile(r'^--- !u!(\d+) &(\d+)')
NAME = re.compile(r'^  m_Name: (.*)$')
GO = re.compile(r'^  m_GameObject: \{fileID: (\d+)\}')
CHILD = re.compile(r'^  - \{fileID: (\d+)\}')
FIELD = re.compile(r'^  (mapRoot|areaRoot|areaSelectLayout|mapInfoLayout): \{fileID: (\d+)\}')


def parse(path):
    tfs, go_name, tf2go, fields = {}, {}, {}, {}
    cur, kind, field = None, None, None
    with open(path, encoding='utf-8') as f:
        for line in f:
            m = TAG.match(line)
            if m:
                cur, kind, field = m.group(2), int(m.group(1)), None
                continue
            if cur is None:
                continue
            m = FIELD.match(line)
            if m:
                fields[m.group(1)] = m.group(2)
                continue
            if kind == 1:
                m = NAME.match(line)
                if m:
                    go_name[cur] = m.group(1)
                continue
            if kind in (4, 224):
                m = GO.match(line)
                if m:
                    tf2go[cur] = m.group(1)
                    continue
                m = CHILD.match(line)
                if m:
                    tfs.setdefault(cur, []).append(m.group(1))
    return tfs, go_name, tf2go, fields


def main(path):
    tfs, go_name, tf2go, fields = parse(path)
    print('fields:', fields)
    for key in ('mapRoot', 'areaRoot', 'areaSelectLayout', 'mapInfoLayout'):
        tf = fields.get(key)
        if not tf:
            continue
        kids = tfs.get(tf, [])
        print(f'\n{key}: {len(kids)} children')
        for i, c in enumerate(kids):
            name = go_name.get(tf2go.get(c), '?')
            gk = tfs.get(c, [])
            print(f'  GetChild({i}) = {name}   (sub={len(gk)})')


if __name__ == '__main__':
    main(sys.argv[1])
