# -*- coding: utf-8 -*-
"""找出把 shader 踢出 SRP Batcher 的「ShaderLab 渲染状态引用材质属性」。

Unity 规则：只要某个 Pass 的渲染状态里引用了材质属性，例如
    Ref [_RenderRef] / Cull [_Cull] / ZTest [_ZTest] / ZWrite [_ZWrite]
    Blend [_SrcBlend] [_DstBlend] / Offset [_X] [_Y] / ColorMask [_X] / Comp [_X]
该 shader 就**不再 SRP Batcher 兼容**（Unity 文档：Render state uses material property）。

用法：python shader_srp_break.py
"""
import os
import re

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

# ShaderLab 状态关键字后紧跟 [_Prop] 的写法
STATE_RE = re.compile(
    r'^\s*(Ref|Comp|Pass|Fail|ZFail|ZTest|ZWrite|Cull|Blend|BlendOp|'
    r'ColorMask|Offset|AlphaToMask|Stencil)\b[^\n]*\[_\w+\]', re.M)

# 只统计到 HLSLPROGRAM/HLSLINCLUDE 之前的部分（状态只在 ShaderLab 段出现，避免误报）
HLSL_RE = re.compile(r'HLSLPROGRAM|CGPROGRAM|HLSLINCLUDE|CGINCLUDE')

rows = []
for dp, _dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if not f.endswith('.shader'):
            continue
        p = os.path.join(dp, f)
        t = open(p, encoding='utf-8', errors='replace').read()
        m = re.search(r'Shader\s+"([^"]+)"', t)
        name = m.group(1) if m else f

        # 逐行扫，遇到 HLSL 块就跳过（简化：统计整份，但把 HLSL 行排除）
        hits = []
        for i, line in enumerate(t.split('\n'), 1):
            if line.strip().startswith('//'):
                continue
            mm = STATE_RE.match(line)
            if mm:
                hits.append((i, line.strip()))
        if hits:
            rows.append((name, os.path.relpath(p, ROOT).replace(os.sep, '/'), hits))

rows.sort(key=lambda x: x[0])
print('命中 %d 个 shader 在 Pass 状态里引用了材质属性：' % len(rows))
print()
for name, path, hits in rows:
    print('## %s' % name)
    print('   %s' % path)
    seen = set()
    for line_no, text in hits:
        if text in seen:
            continue
        seen.add(text)
        print('   L%-5d %s' % (line_no, text))
    print()
