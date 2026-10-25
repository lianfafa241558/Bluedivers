"""打印预制体每个 GameObject 的 Layer 与组件清单（含 MonoBehaviour 的脚本名）。

用法: python prefab_components.py <prefab 路径> [名字过滤正则]
用途：排查"某物体为什么不吃伤害/不打中"这类问题 —— 一眼看出有没有 Collider / Damageable / Actor。
只读；不做嵌套 prefab 展开（嵌套实例会显示为 PrefabInstance）。
"""
import os
import re
import sys

TAG = re.compile(r'^--- !u!(\d+) &(-?\d+)')
NAME = re.compile(r'^  m_Name: (.*)$')
GO = re.compile(r'^  m_GameObject: \{fileID: (-?\d+)\}')
FATHER = re.compile(r'^  m_Father: \{fileID: (-?\d+)\}')
CHILD = re.compile(r'^  - \{fileID: (-?\d+)\}')
LAYER = re.compile(r'^  m_Layer: (\d+)')
ACTIVE = re.compile(r'^  m_IsActive: (\d)')
SCRIPT = re.compile(r'^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]+)')
SOURCE = re.compile(r'^  m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)')
ENABLED = re.compile(r'^  m_Enabled: (\d)')
TRIGGER = re.compile(r'^  m_IsTrigger: (\d)')
SIZE = re.compile(r'^  m_Size: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+), z: ([\d.eE+-]+)\}')
RADIUS = re.compile(r'^  m_Radius: ([\d.eE+-]+)')
CENTER = re.compile(r'^  m_Center: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+), z: ([\d.eE+-]+)\}')

CLASS = {1: 'GameObject', 4: 'Transform', 224: 'RectTransform', 33: 'MeshFilter', 23: 'MeshRenderer',
         54: 'Rigidbody', 64: 'MeshCollider', 65: 'BoxCollider', 135: 'SphereCollider', 136: 'CapsuleCollider',
         114: 'MonoBehaviour', 208: 'NavMeshObstacle', 108: 'Light', 137: 'SkinnedMeshRenderer',
         1001: 'PrefabInstance', 95: 'Animator', 198: 'ParticleSystem'}


def build_guid_map(root='Assets/Scripts'):
    m = {}
    for dirpath, _, files in os.walk(root):
        for f in files:
            if f.endswith('.cs.meta'):
                p = os.path.join(dirpath, f)
                try:
                    with open(p, encoding='utf-8', errors='ignore') as fh:
                        for line in fh:
                            if line.startswith('guid:'):
                                m[line.split(':')[1].strip()] = f[:-5]
                                break
                except OSError:
                    pass
    return m


def parse(path):
    gos, tfs, comps = {}, {}, {}   # gos: id->(name,layer,active); tfs: tf->(father,[children]); comps: go->[desc]
    tf2go = {}
    cur, kind = None, None
    pending = None                 # 正在读取的组件描述
    with open(path, encoding='utf-8', errors='ignore') as f:
        for line in f:
            m = TAG.match(line)
            if m:
                cur, kind = m.group(2), int(m.group(1))
                pending = None
                continue
            if cur is None:
                continue
            if kind == 1:
                m = NAME.match(line)
                if m:
                    nm = gos.setdefault(cur, ['?', 0, 1])
                    nm[0] = m.group(1)
                    continue
                m = LAYER.match(line)
                if m:
                    gos.setdefault(cur, ['?', 0, 1])[1] = int(m.group(1))
                    continue
                m = ACTIVE.match(line)
                if m:
                    gos.setdefault(cur, ['?', 0, 1])[2] = int(m.group(1))
                continue
            if kind in (4, 224):
                m = GO.match(line)
                if m:
                    tf2go[cur] = m.group(1)
                    continue
                m = FATHER.match(line)
                if m:
                    tfs.setdefault(cur, ['0', []])[0] = m.group(1)
                    continue
                m = CHILD.match(line)
                if m:
                    tfs.setdefault(cur, ['0', []])[1].append(m.group(1))
                continue
            if kind == 1001:
                m = SOURCE.match(line)
                if m:
                    comps.setdefault('instance:' + cur, []).append('src=' + m.group(1))
                continue
            # 普通组件
            m = GO.match(line)
            if m:
                pending = comps.setdefault(m.group(1), [])
                if kind == 114:
                    pending.append('MonoBehaviour(?)')
                elif kind in (65, 135, 136, 64):
                    pending.append(CLASS.get(kind, f'!u!{kind}'))
                elif kind in CLASS:
                    pending.append(CLASS[kind])
                continue
            if pending is None:
                continue
            if kind == 114:
                m = SCRIPT.match(line)
                if m:
                    pending[-1] = 'Mono:' + m.group(1)
                    continue
            if kind in (65, 135, 136):
                m = TRIGGER.match(line)
                if m:
                    pending[-1] += '/trigger=%s' % m.group(1)
                    continue
            if kind == 65:
                m = SIZE.match(line)
                if m:
                    pending[-1] += '/size=(%s,%s,%s)' % m.groups()
                    continue
            if kind in (135, 136):
                m = RADIUS.match(line)
                if m:
                    pending[-1] += '/r=%s' % m.group(1)
                    continue
            if kind in (65, 135, 136):
                m = CENTER.match(line)
                if m:
                    pending[-1] += '/c=(%s,%s,%s)' % m.groups()
                    continue
            m = ENABLED.match(line)
            if m and pending:
                pending[-1] += '/enabled=%s' % m.group(1)
    return gos, tfs, comps, tf2go


def main(path, pattern=None):
    gos, tfs, comps, tf2go = parse(path)
    gmap = build_guid_map()
    flt = re.compile(pattern) if pattern else None
    roots = [t for t, (f, _) in tfs.items() if f == '0' and t in tf2go]

    def walk(tf, prefix):
        go = tf2go.get(tf, tf)
        nm, layer, act = gos.get(go, ['?', 0, 1])
        comp = comps.get(go, [])
        comp = ['Mono:' + gmap.get(c[5:], c[5:])[0:] if c.startswith('Mono:') else c for c in comp]
        if flt is None or flt.search(nm) or any(flt.search(c) for c in comp):
            print(f"{prefix}{nm} [layer={layer} active={act}]  {', '.join(comp)}")
            for i, c in enumerate(tfs.get(tf, ['0', []])[1]):
                walk(c, prefix + '  ')
        else:
            for i, c in enumerate(tfs.get(tf, ['0', []])[1]):
                walk(c, prefix + '  ')

    for r in roots:
        walk(r, '')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
