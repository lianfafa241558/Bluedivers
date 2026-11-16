# -*- coding: utf-8 -*-
"""审核 ToonLit 的 ShaderLab Properties 与 UnityPerMaterial CBUFFER 是否一一对应。

SRP Batcher 的硬性要求（Unity 文档 SRP Batcher compatibility）：
  · 所有 per-material 属性必须声明在**唯一的** `CBUFFER_START(UnityPerMaterial)` 里；
  · 该 CBUFFER 在**每个 Pass** 里布局一致；
  · Pass 的渲染状态（Blend/ZTest/Stencil Ref…）不能引用材质属性。

这里对比「Properties 里声明的属性」与「Shared.hlsl 的 CBUFFER 里的条目」，
列出所有对不上的 —— 每一个都可能是 still-not-batched 的原因。
"""
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
BASE = os.path.join(ROOT, 'Assets', 'Shader', 'ToonLit', 'Main')

PROP_RE = re.compile(r'^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(', re.M)
CBUF_ENTRY_RE = re.compile(r'^\s*(?:float|half|fixed|int|uint)[1-4]?(?:x[1-4])?\s+(_\w+)\s*;', re.M)

shader = sys.argv[1] if len(sys.argv) > 1 else 'ToonLit'

sp = os.path.join(BASE, shader + '.shader')
hp = os.path.join(BASE, 'ToonLit_Shared.hlsl')

stext = open(sp, encoding='utf-8', errors='replace').read()
htext = open(hp, encoding='utf-8', errors='replace').read()

# Properties 块
m = re.search(r'Properties\s*\{(.*?)\n\s*\}\s*\n', stext, re.S)
props_block = m.group(1) if m else ''
props = []
for line in props_block.split('\n'):
    s = line.strip()
    if not s or s.startswith('//'):
        continue
    mm = PROP_RE.match(line)
    if mm:
        props.append(mm.group(1))

# CBUFFER 块
c0 = htext.find('CBUFFER_START(UnityPerMaterial)')
c1 = htext.find('CBUFFER_END', c0)
cbuf_block = htext[c0:c1] if c0 >= 0 and c1 > c0 else ''
cbuf = CBUF_ENTRY_RE.findall(cbuf_block)

# `_X (…, 2D)` 会带 _ST，CBUFFER 里出现的是 _X_ST
cbuf_set = set(cbuf)
def covered(p):
    return p in cbuf_set or (p + '_ST') in cbuf_set

missing = [p for p in props if not covered(p)]
extra = [c for c in cbuf if not c.endswith('_ST') and c not in props]

print('shader      :', shader)
print('Properties  :', len(props), '个')
print('CBUFFER 条目:', len(cbuf), '个（含 _ST）')
print()
if missing:
    print('!! Properties 里有、CBUFFER 里没有（= 可能破坏 SRP Batcher）：')
    for p in missing:
        print('     ', p)
else:
    print('Properties 与 CBUFFER 完全对应 ✅')
print()
if extra:
    print('CBUFFER 里有、Properties 里没有（代码内部量，正常）：')
    for p in extra:
        print('     ', p)
