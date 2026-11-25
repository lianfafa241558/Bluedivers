# -*- coding: utf-8 -*-
"""扫描所有 prefab（含变体），检查 WeaponEnemyController 实例的 cfg._fakeAttrs 是否含 Key: 2（射击间隔）。

两层来源：
  1) 文件里直接存在的 MonoBehaviour 文档（基 prefab / 变体里新增的组件）—— 看 _fakeAttrs 列表；
  2) 变体的 m_Modifications —— 看 propertyPath 以 cfg._fakeAttrs 开头的覆盖（改数组 size / 改某项 Key）。
WeaponAttrType.ShootInterval = 2。
"""
import os
import re

GUID_ENEMY = '1925424471e3fe949a952edf108dc17b'
ROOT = r'd:\Pro\Bluedivers\Assets'

doc_re = re.compile(r'(?m)^--- !u!(\d+) &(-?\d+)([^\n]*)$')
key_re = re.compile(r'(?m)^\s*- Key: *(-?\d+)')
mod_re = re.compile(
    r'propertyPath: (?P<path>[^\n]+)\n\s*value: (?P<val>[^\n]*)\n\s*objectReference: \{fileID: (?P<ref>-?\d+)\}',
    re.S)

doc_hits = []        # (path, keys)
variant_hits = []    # (path, [mod paths])


def scan_file(p, text):
    marks = list(doc_re.finditer(text))
    for i, m in enumerate(marks):
        start = m.start()
        end = marks[i + 1].start() if i + 1 < len(marks) else len(text)
        block = text[start:end]
        if 'm_Script: {fileID: 11500000, guid: ' + GUID_ENEMY not in block:
            continue
        if '_fakeAttrs:' not in block:
            continue
        keys = [int(k) for k in key_re.findall(block)]
        doc_hits.append((p, keys))

    if 'PrefabInstance:' not in text:
        return
    mods = [m for m in mod_re.finditer(text) if m.group('path').startswith('cfg._fakeAttrs')]
    if mods:
        variant_hits.append((p, mods))


for dirpath, _, files in os.walk(ROOT):
    for f in files:
        if not f.endswith('.prefab'):
            continue
        p = os.path.join(dirpath, f)
        try:
            with open(p, encoding='utf-8', errors='replace') as fh:
                scan_file(p, fh.read())
        except OSError:
            continue

print('== 直接含 _fakeAttrs 的 WeaponEnemyController 文档 ==')
print('实例数:', len(doc_hits), ' 缺 Key2:', sum(1 for _, k in doc_hits if 2 not in k))
for p, keys in doc_hits:
    flag = 'OK  ' if 2 in keys else 'MISS'
    print(f'  {flag} {os.path.relpath(p, ROOT)} -> {keys}')

print()
print('== 变体里覆盖了 cfg._fakeAttrs 的文件 ==')
print('数量:', len(variant_hits))
for p, mods in variant_hits:
    paths = []
    for m in mods:
        paths.append(f"{m.group('path')}={m.group('val').strip()}")
    joined = '; '.join(sorted(set(paths)))
    print(f'  {os.path.relpath(p, ROOT)} -> {joined[:400]}')
