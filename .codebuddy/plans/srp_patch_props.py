# -*- coding: utf-8 -*-
"""给 ToonLit 系列 shader 的 Properties 块补齐 CBUFFER 里缺失的声明。

背景：7 个 shader 共用 ToonLit_Shared.hlsl 里的一份 UnityPerMaterial CBUFFER（54 条 union），
但各自的 Properties 块只声明了自己用的子集。SRP Batcher 要求二者**一一对应**，
否则 Unity 报 code=19 `UnityPerMaterial var is not declared in shader property section (_X)`
⇒ 整个 ToonLit 家族都不合批。

做法：把 CBUFFER 里有、Properties 里没有的条目，用 [HideInInspector] 补进 Properties 块末尾。
这些属性在本 shader 里确实不用（代码走的是静态统一分支），声明只为满足 SRP Batcher 的对齐要求，
不会出现在材质 Inspector 上。重复执行是安全的（会先删掉上一次的自动段再重建）。

⚠ 扫描时必须先剥掉 `//` 与 `/* */` 注释：ToonLit.shader 把整个嘴部区块注释掉了，
   但 CBUFFER 里仍有 `_MouthMap_ST`，Unity 会因此判定不合批。

用法：python srp_patch_props.py [Shader名...] [--apply]
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
BASE = os.path.join(ROOT, 'Assets', 'Shader', 'ToonLit', 'Main')

DEFAULT = ['ToonLit', 'ToonLit_Unit', 'ToonLit_Scene']
# 只补默认那 3 个：覆盖本局 45 个 ToonLit 材质里的 39 个；其余 4 个 shader 各只有 1~2 个材质，
# 为它们添 33~45 条隐藏字段不划算（本来也没合批，保持现状）。

PROP_RE = re.compile(r'^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(', re.M)
CBUF_RE = re.compile(r'^\s*(float|half|fixed|int|uint)([1-4])?(x[1-4])?\s+(_\w+)\s*;', re.M)
MARKER = '// ===== SRP Batcher 对齐：本 shader 未使用，仅为满足 UnityPerMaterial 一一对应（自动生成）====='

names = [a for a in sys.argv[1:] if not a.startswith('--')] or DEFAULT
APPLY = '--apply' in sys.argv


def strip_comments(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    s = re.sub(r'//[^\n]*', '', s)
    return s


def load(path):
    raw = open(path, 'rb').read()
    bom = raw[:3] == b'\xef\xbb\xbf'
    text = raw.decode('utf-8-sig')
    crlf = '\r\n' in text
    return text.replace('\r\n', '\n'), bom, crlf


def save(path, text, bom, crlf):
    if crlf:
        text = text.replace('\n', '\r\n')
    data = text.encode('utf-8')
    if bom:
        data = b'\xef\xbb\xbf' + data
    open(path, 'wb').write(data)


# --- 共用 CBUFFER ---
htext, _, _ = load(os.path.join(BASE, 'ToonLit_Shared.hlsl'))
c0 = htext.find('CBUFFER_START(UnityPerMaterial)')
c1 = htext.find('CBUFFER_END', c0)
cbuf = {}
for m in CBUF_RE.finditer(htext[c0:c1]):
    cbuf[m.group(4)] = m.group(1) + (m.group(2) or '') + (m.group(3) or '')


def decl(name, typ):
    if name.endswith('_ST'):
        base = name[:-3]
        return '%s("%s", 2D) = "white" {}' % (base, base)
    if typ.startswith(('int', 'uint')):
        return '%s("%s", Int) = 0' % (name, name)
    if typ.startswith(('float4', 'half4', 'fixed4')):
        return '%s("%s", Color) = (0,0,0,0)' % (name, name)
    if typ.startswith(('float3', 'half3', 'fixed3', 'float2', 'half2', 'fixed2')):
        return '%s("%s", Vector) = (0,0,0,0)' % (name, name)
    return '%s("%s", Float) = 0' % (name, name)


for s in names:
    path = os.path.join(BASE, s + '.shader')
    text, bom, crlf = load(path)

    m = re.search(r'Properties\s*\n(\s*)\{', text)
    if not m:
        print('!! %s 找不到 Properties 块' % s)
        continue
    open_brace = text.index('{', m.start())
    depth, i = 0, open_brace
    while i < len(text):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                break
        i += 1
    close_brace = i
    block = text[open_brace + 1:close_brace]

    # 先删掉上一次生成的自动段（保证可重复执行）
    if MARKER in block:
        block = block[:block.index(MARKER)].rstrip() + '\n'

    declared = set()
    for line in strip_comments(block).split('\n'):
        mm = PROP_RE.match(line)
        if mm:
            declared.add(mm.group(1))

    missing = [cn for cn in cbuf
               if (cn[:-3] if cn.endswith('_ST') else cn) not in declared]
    if not missing:
        print('== %s 无需补齐（已对齐）' % s)
        continue

    indent = '        '
    for line in block.split('\n'):
        if line.strip() and not line.strip().startswith('//'):
            indent = line[:len(line) - len(line.lstrip())] or indent
            break

    add = ['', indent + MARKER]
    for cn in missing:
        add.append('%s[HideInInspector] %s' % (indent, decl(cn, cbuf[cn])))
    stripped = block.rstrip()
    # `}` 的缩进与 Properties 的 `{` 对齐（同一层级）
    brace_indent = m.group(1)
    block = stripped + '\n' + '\n'.join(add) + '\n' + brace_indent

    print('== %s 补 %d 条：%s' % (s, len(missing), ', '.join(missing)))
    if APPLY:
        save(path, text[:open_brace + 1] + block + text[close_brace:], bom, crlf)

print()
print('APPLY' if APPLY else '（dry-run，加 --apply 才写文件）')
