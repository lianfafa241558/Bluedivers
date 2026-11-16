# -*- coding: utf-8 -*-
"""列出 7 个 ToonLit shader 相对共用 CBUFFER 的缺口（用于补 Properties 声明）。"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
BASE = os.path.join(ROOT, 'Assets', 'Shader', 'ToonLit', 'Main')

SHADERS = ['ToonLit', 'ToonLit_Unit', 'ToonLit_Scene', 'ToonLit_Stone',
           'ToonLit_Face', 'ToonLit_Hair', 'ToonLit_MouthEye']

PROP_RE = re.compile(r'^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(', re.M)
CBUF_RE = re.compile(r'^\s*(float|half|fixed|int|uint)([1-4])?(x[1-4])?\s+(_\w+)\s*;', re.M)

htext = open(os.path.join(BASE, 'ToonLit_Shared.hlsl'), encoding='utf-8', errors='replace').read()
c0 = htext.find('CBUFFER_START(UnityPerMaterial)')
c1 = htext.find('CBUFFER_END', c0)
cbuf_block = htext[c0:c1]
cbuf = {}
for m in CBUF_RE.finditer(cbuf_block):
    typ = m.group(1) + (m.group(2) or '') + (m.group(3) or '')
    cbuf[m.group(4)] = typ

print('CBUFFER 条目 %d 个' % len(cbuf))


def prop_type(name, typ):
    if name.endswith('_ST'):
        return '2D', 'white'
    if typ.startswith(('int', 'uint')):
        return 'Int', '0'
    if typ.startswith(('float4', 'half4', 'fixed4')):
        return 'Color', '(0,0,0,0)'
    if typ.startswith(('float3', 'half3', 'fixed3')):
        return 'Vector', '(0,0,0,0)'
    if typ.startswith(('float2', 'half2', 'fixed2')):
        return 'Vector', '(0,0,0,0)'
    return 'Float', '0'


for s in SHADERS:
    p = os.path.join(BASE, s + '.shader')
    stext = open(p, encoding='utf-8', errors='replace').read()
    m = re.search(r'Properties\s*\{', stext)
    names = set()
    # 只在 Properties 块内扫
    i = m.end()
    depth = 1
    j = i
    while j < len(stext) and depth > 0:
        if stext[j] == '{':
            depth += 1
        elif stext[j] == '}':
            depth -= 1
        j += 1
    block = stext[i:j - 1]
    for line in block.split('\n'):
        mm = PROP_RE.match(line)
        if mm:
            names.add(mm.group(1))

    missing = []
    for cn in sorted(cbuf):
        base_name = cn[:-3] if cn.endswith('_ST') else cn
        if base_name not in names:
            missing.append((cn, *prop_type(cn, cbuf[cn])))
    print()
    print('==== %s ：Properties %d 个，缺 %d 个' % (s, len(names), len(missing)))
    for cn, t, d in missing:
        print('    %-28s %-7s %s' % (cn, t, d))
