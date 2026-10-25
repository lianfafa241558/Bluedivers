"""打印 Unity 预制体的层级 + 每个子物体的 GetChild 索引路径。

用法: python prefab_tree.py <prefab 路径>
用途：确认某节点相对 root 的 GetChild(...) 索引（改 UI 代码前核对结构，别靠肉眼数）。
"""
import re
import sys

TAG = re.compile(r'^--- !u!(\d+) &(\d+)')
NAME = re.compile(r'^  m_Name: (.*)$')
GO = re.compile(r'^  m_GameObject: \{fileID: (\d+)\}')
CHILD = re.compile(r'^  - \{fileID: (\d+)\}')
FATHER = re.compile(r'^  m_Father: \{fileID: (\d+)\}')
ACTIVE = re.compile(r'^  m_IsActive: (\d)')


def parse(path):
    """返回 (transforms{id:(father, [children])}, go_name{id:name}, go_active{id:0/1}, tf2go{id:go})"""
    tfs, go_name, go_active, tf2go = {}, {}, {}, {}
    cur, kind, comps = None, None, []
    with open(path, encoding='utf-8') as f:
        for line in f:
            m = TAG.match(line)
            if m:
                cur, kind = m.group(2), int(m.group(1))
                comps = []
                if kind == 1:
                    go_active[cur] = 1
                continue
            if cur is None:
                continue
            m = NAME.match(line)
            if m and kind == 1:
                go_name[cur] = m.group(1)
                continue
            m = ACTIVE.match(line)
            if m and kind == 1:
                go_active[cur] = int(m.group(1))
                continue
            if kind in (4, 224):  # Transform / RectTransform
                m = GO.match(line)
                if m:
                    tf2go[cur] = m.group(1)
                    continue
                m = FATHER.match(line)
                if m:
                    tfs.setdefault(cur, [m.group(1), []])[0] = m.group(1)
                    tfs.setdefault(cur, ['0', []])
                    continue
                m = CHILD.match(line)
                if m:
                    tfs.setdefault(cur, ['0', []])[1].append(m.group(1))
    return tfs, go_name, go_active, tf2go


def main(path):
    tfs, go_name, go_active, tf2go = parse(path)
    roots = [t for t, (f, _) in tfs.items() if f == '0' and t in tf2go]
    print('roots:', len(roots))

    def walk(tf, prefix):
        go = tf2go.get(tf)
        print(f"{prefix}{go_name.get(go, '?')}  [active={go_active.get(go, 1)}] tf={tf}")
        for i, c in enumerate(tfs.get(tf, ['0', []])[1]):
            walk(c, prefix + f'  GetChild({i}) -> ')

    for r in roots:
        walk(r, '')


if __name__ == '__main__':
    main(sys.argv[1])
