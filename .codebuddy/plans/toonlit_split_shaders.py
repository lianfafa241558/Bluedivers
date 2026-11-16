# -*- coding: utf-8 -*-
"""把 ToonLit 拆成「固定类」与「单位」两个 shader，让它们恢复 SRP Batcher 兼容。

背景：`Stencil { Ref [_RenderRef] }` 在 Pass 渲染状态里引用了材质属性 ⇒ 整个 shader 不兼容 SRP Batcher
（实测 ApplyShader 0.9ms / 212~231 calls）。印花材质用 `_StencilRef=1, _StencilComp=NotEqual`
⇒ 只在 stencil != 1 处绘制，所以：
    Ref 0 = 固定类（会被印花覆盖）
    Ref 1 = 单位（不会被印花覆盖）
`_RenderRef=3`（Pyroxene）对印花与 0 等价（3 != 1），并入 0 组。

用法：
    python toonlit_split_shaders.py            # 只报告
    python toonlit_split_shaders.py --apply    # 落盘
"""
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
BASE = os.path.join(ROOT, 'Assets', 'Shader', 'ToonLit', 'Main')

# 文件名 -> 写死的 stencil 值
FIX = {
    'ToonLit': 0,          # 固定类（原 25 个 =0 材质不变；Pyroxene 由 3 并入 0，对印花等价）
    'ToonLit_Scene': 0,    # 全是 0
    'ToonLit_Stone': 0,    # 全是 0
    'ToonLit_Face': 1,     # 全是 1（角色面部 = 单位）
    'ToonLit_Hair': 1,     # 全是 1
    'ToonLit_MouthEye': 1, # 全是 1
}

UNIT_NEW = 'ToonLit_Unit'   # 新增：Ref 1 的壳
# ⚠ 行尾要容 \r：这些 .shader 是 CRLF
REF_LINE = re.compile(r'^([ \t]*Ref)[ \t]*\[_RenderRef\][ \t]*\r?$', re.M)


def sub_ref(m, val):
    # ⚠ 必须补一个空格：有的行写成 `Ref[_RenderRef]`（无空格），直接拼会得到非法的 `Ref0`
    return '%s %d' % (m.group(1), val)


def read(p):
    raw = open(p, 'rb').read()
    bom = b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
    return raw[len(bom):].decode('utf-8'), bom


def write(p, text, bom):
    open(p, 'wb').write(bom + text.encode('utf-8'))


def main():
    apply = '--apply' in sys.argv
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    print('模式:', 'APPLY' if apply else 'DRY-RUN')
    print()

    for name, val in FIX.items():
        p = os.path.join(BASE, name + '.shader')
        text, bom = read(p)
        hits = REF_LINE.findall(text)
        if not hits:
            print('!! %-20s 没找到 Ref [_RenderRef]（可能已改过）' % name)
            continue
        new = REF_LINE.sub(lambda m: sub_ref(m, val), text)
        # 新壳：从原始文本出发，改成 Ref 1 并换 shader 名
        if name == 'ToonLit':
            unit = REF_LINE.sub(lambda m: sub_ref(m, 1), text)
            unit = unit.replace('Shader "ToonLit/ToonLit"', 'Shader "ToonLit/%s"' % UNIT_NEW, 1)
            unit = ('// 「单位」版本的 ToonLit：与 ToonLit 完全同源，只有 Stencil 的 Ref 从 0 改成 1。\n'
                    '// 为什么要拆：`Stencil { Ref [_RenderRef] }` 在渲染状态里引用材质属性会让整个 shader\n'
                    '// 失去 SRP Batcher 兼容；印花用 `_StencilRef=1 / NotEqual` 判「单位不覆盖」，\n'
                    '// 所以只要两个常量版本就够。实现全部在 ToonLit_Shared.hlsl，本文件只是壳。\n'
                    + unit)
            up = os.path.join(BASE, UNIT_NEW + '.shader')
            if apply:
                write(up, unit, bom)
                print('== %-20s Ref -> %d   (%d 处)   并生成 %s' % (name, val, len(hits), UNIT_NEW))
            else:
                print('== %-20s Ref -> %d   (%d 处)   将生成 %s' % (name, val, len(hits), UNIT_NEW))
        else:
            print('== %-20s Ref -> %d   (%d 处)' % (name, val, len(hits)))
        if apply:
            write(p, new, bom)

    print()
    if not apply:
        print('（未落盘，加 --apply 执行）')
    else:
        print('完成。下一步：刷新 Unity → 取 %s.shader 的 GUID → 把 Ref=1 的材质指过去。' % UNIT_NEW)


if __name__ == '__main__':
    main()
