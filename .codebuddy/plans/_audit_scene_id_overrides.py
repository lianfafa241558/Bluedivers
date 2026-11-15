# -*- coding: utf-8 -*-
"""解析场景里对 prefab 组件的 `Id` / `ShowName` 覆盖，判断它落在哪个脚本上
（BaseObject = 运行时会生效；Furniture_General = 旧字段残留，代码不读）。
只读。用法：python _audit_scene_id_overrides.py <scene 相对路径>
"""
import io
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets')


def script_index():
    idx = {}
    for dp, _d, fs in os.walk(ASSETS):
        for f in fs:
            if f.endswith('.cs.meta'):
                m = re.search(r'guid:\s*([0-9a-f]{32})',
                              io.open(os.path.join(dp, f), encoding='utf-8', errors='ignore').read())
                if m:
                    idx[m.group(1)] = f[:-8]
    return idx


def prefab_index():
    idx = {}
    for dp, _d, fs in os.walk(ASSETS):
        for f in fs:
            if f.endswith('.prefab.meta'):
                m = re.search(r'guid:\s*([0-9a-f]{32})',
                              io.open(os.path.join(dp, f), encoding='utf-8', errors='ignore').read())
                if m:
                    idx[m.group(1)] = os.path.join(dp, f[:-5])
    return idx


def component_script(prefab_path, file_id, sidx, pidx, depth=0):
    """解析组件所在脚本；遇到 stripped（嵌套 prefab 实例）就顺着 m_CorrespondingSourceObject 继续找。"""
    if depth > 3:
        return '<嵌套过深>'
    if not os.path.exists(prefab_path):
        return '<源 prefab 缺失>'
    txt = io.open(prefab_path, encoding='utf-8', errors='ignore').read()
    m = re.search(r'^--- !u!114 &%s(.*?)(?=^--- |\Z)' % file_id, txt, flags=re.M | re.S)
    if not m:
        return '<源 prefab 里没有这个组件 %s>' % file_id
    body = m.group(1)
    g = re.search(r'm_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})', body)
    if g:
        return sidx.get(g.group(1), '<非脚本>')
    c = re.search(r'm_CorrespondingSourceObject:\s*\{fileID:\s*(\d+),\s*guid:\s*([0-9a-f]{32})', body)
    if c:
        src = pidx.get(c.group(2))
        if src:
            return component_script(src, c.group(1), sidx, pidx, depth + 1) + ' (经嵌套)'
    return '?'


def main():
    scene = sys.argv[1]
    sidx = script_index()
    pidx = prefab_index()
    txt = io.open(os.path.join(ROOT, scene), encoding='utf-8', errors='ignore').read()

    cur_guid = None
    rows = []
    for m in re.finditer(r'- target:\s*\{fileID:\s*(\d+),\s*guid:\s*([0-9a-f]{32}),\s*\n\s*type:\s*\d+\}\s*\n\s*propertyPath:\s*([^\n]+)\n\s*value:\s*([^\n]*)\n', txt):
        fid, g, prop, val = m.group(1), m.group(2), m.group(3).strip(), m.group(4).strip()
        if prop not in ('Id', 'ShowName', 'identitySource'):
            continue
        rows.append((g, fid, prop, val))

    print('共 %d 条 Id/ShowName 覆盖' % len(rows))
    for g, fid, prop, val in rows:
        src = pidx.get(g, '<未知 prefab %s>' % g)
        scr = component_script(src, fid, sidx, pidx) if src.startswith(ASSETS) else '?'
        print('  %-14s %-16s <- %-22s %s' % (prop, val, scr, os.path.relpath(src, ROOT) if src.startswith(ASSETS) else src))


if __name__ == '__main__':
    main()
