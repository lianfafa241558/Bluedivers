# -*- coding: utf-8 -*-
"""检查项目内自定义 shader 是否具备 SRP Batcher 兼容条件（UnityPerMaterial 常量缓冲）。

SRP Batcher 兼容的判据（Unity 文档）：所有 per-material 属性都声明在
`CBUFFER_START(UnityPerMaterial) ... CBUFFER_END` 里，且该 CBUFFER 在**每个 Pass** 里都有。
这里只做"有没有 UnityPerMaterial"的粗筛，够用来定位那 3 个占 80% 槽位的 ToonLit。
"""
import os
import re

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

TARGETS = ['ToonLit_Scene', 'ToonLit', 'ToonLit_Stone', 'ToonLit_Face',
           'ToonLit_MouthEye', 'ToonLit_Hair', 'Texture2', 'GlowTexture2',
           'FakeAreaLight', 'WarningWall']

rows = []
for dp, _dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if not (f.endswith('.shader') or f.endswith('.shadergraph')):
            continue
        p = os.path.join(dp, f)
        try:
            t = open(p, encoding='utf-8', errors='replace').read()
        except Exception:
            continue
        m = re.search(r'Shader\s+"([^"]+)"', t)
        name = m.group(1) if m else f
        short = name.split('/')[-1]
        if short not in TARGETS:
            continue
        passes = len(re.findall(r'^\s*Pass\s*\{?', t, re.M)) + len(re.findall(r'Name\s+"[^"]+"\s*\n\s*Tags', t))
        cbuf = len(re.findall(r'CBUFFER_START\s*\(\s*UnityPerMaterial\s*\)', t))
        props = len(re.findall(r'^\s*_\w+\s*\(', t, re.M))
        rows.append((name, os.path.relpath(p, ROOT).replace(os.sep, '/'), cbuf, props, len(t)))

rows.sort(key=lambda x: x[0])
print('%-34s %-6s %-8s %-6s %s' % ('shader', 'CBUFFER', 'perMat属性', '源大小', '路径'))
for name, path, cbuf, props, size in rows:
    print('%-34s %-6d %-8d %-6d %s' % (name, cbuf, props, size, path))
print()
print('命中 %d 个 shader；CBUFFER_START(UnityPerMaterial) 出现次数为 0 的 = 基本不可能 SRP Batcher 兼容'
      % len(rows))
