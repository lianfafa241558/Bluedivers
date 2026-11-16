# -*- coding: utf-8 -*-
"""统计所有材质上 `_RenderRef` 的取值分布（决定能否在 shader 里写死成常量）。

背景：ToonLit 家族的 Pass 里写了 `Ref [_RenderRef]`，这在 ShaderLab 渲染状态里引用了材质属性，
Unity 会把整个 shader 判定为「SRP Batcher 不兼容」⇒ 每个材质各来一次 setpass。
若 `_RenderRef` 全项目只有一个值，就能改成常量，一次性让 80% 槽位恢复合批。
"""
import os
import re
import collections

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

VAL_RE = re.compile(r'^\s*_RenderRef:\s*(-?[\d.]+)\s*$', re.M)

hist = collections.Counter()
per_value = collections.defaultdict(list)
total = 0

for dp, _dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if not f.endswith('.mat'):
            continue
        p = os.path.join(dp, f)
        t = open(p, encoding='utf-8', errors='replace').read()
        m = VAL_RE.search(t)
        if not m:
            continue
        v = m.group(1)
        hist[v] += 1
        total += 1
        if len(per_value[v]) < 8:
            per_value[v].append(os.path.relpath(p, ROOT).replace(os.sep, '/'))

print('带 _RenderRef 的材质：%d 个' % total)
print('取值分布：')
for v, n in hist.most_common():
    print('   _RenderRef = %-6s  材质 %d 个   例：%s' % (v, n, per_value[v][0]))
    for extra in per_value[v][1:]:
        print('        %s' % extra)
print()
if len(hist) == 1:
    only = list(hist)[0]
    print('⇒ 只有一个取值 %s，可以直接在 shader 里写死 `Ref %s`，风险极低。' % (only, only))
else:
    print('⇒ 有 %d 种取值，不能无条件写死；需要按取值拆 shader 或改用其它机制。' % len(hist))
