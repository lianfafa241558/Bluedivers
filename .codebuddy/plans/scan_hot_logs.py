# -*- coding: utf-8 -*-
"""统计项目里的「高频日志」：找出落在 Update/FixedUpdate/LateUpdate/OnGUI、
循环体、以及被 Update 直接/间接调用到的方法里的 Debug.Log* 调用。

只读扫描，不改任何业务文件。
用法: python .codebuddy/plans/scan_hot_logs.py
"""
import os
import re
import sys
import io
import json

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

ROOT = r'e:\Bluedivers\Assets'

# 生命周期/每帧方法名（按"每帧或每输入帧会被连续调用"归类）
PER_FRAME = {
    'Update', 'FixedUpdate', 'LateUpdate', 'OnGUI', 'OnRenderObject',
    'OnWillRenderObject', 'OnDrawGizmos', 'OnDrawGizmosSelected',
    'Tick', 'OnPreRender', 'OnPostRender',
}
PER_FRAME_PREFIX = ('Update', 'Tick', 'LateUpdate')

# 中频：每命中 / 每次进出触发 / 每次池复用，一局里会被调几十~几百次
MEDIUM = {
    'OnTriggerEnter', 'OnTriggerStay', 'OnTriggerExit',
    'OnCollisionEnter', 'OnCollisionStay', 'OnCollisionExit',
    'OnParticleCollision', 'TakeDamage', 'Kill', 'OnEnable', 'OnDisable',
    'OnSpawn', 'Respawn', 'Reset', 'ApplyRemoteState', 'ReportState',
}

DECL_RE = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*'
    r'(?:(?:public|private|protected|internal|static|virtual|override|sealed|abstract|async|extern|unsafe|new|partial)\s+)+'
    r'[A-Za-z_][\w<>\[\],\.\?\s]*\s+(?P<name>[A-Za-z_]\w*)\s*\('
)
LOOP_RE = re.compile(r'\b(for|foreach|while)\s*\(')

LOG_RE = re.compile(
    r'\b(Debug\s*\.\s*(?P<dbg>Log|LogWarning|LogError|LogFormat|LogException|Assert|AssertFormat|LogAssertion)'
    r'|UnityEngine\s*\.\s*Debug\s*\.\s*(?P<dbg2>Log|LogWarning|LogError)'
    r'|(?P<gated>NetSyncLog\s*\.\s*\w+)'
    r'|(?<![\w.])print\s*\()'
)
CALL_RE = re.compile(r'\b([A-Za-z_]\w*)\s*\(')
KEYWORDS = {
    'if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock', 'return',
    'new', 'sizeof', 'typeof', 'nameof', 'base', 'this', 'get', 'set', 'add', 'remove',
}


def strip_code(line):
    """去掉行注释与字符串内容（字符串里的 // 和 { } 会干扰）；保留块注释后的残行。"""
    out = []
    i, n = 0, len(line)
    in_str = False
    in_chr = False
    while i < n:
        c = line[i]
        if in_str:
            if c == '\\':
                i += 2
                continue
            if c == '"':
                in_str = False
            i += 1
            continue
        if in_chr:
            if c == '\\':
                i += 2
                continue
            if c == "'":
                in_chr = False
            i += 1
            continue
        if c == '/' and i + 1 < n and line[i + 1] == '/':
            break
        if c == '"':
            in_str = True
            i += 1
            continue
        if c == "'":
            in_chr = True
            i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def parse_file(path):
    """返回 (logs, methods)。logs: [{line, method, kind, loop, gated}]；methods: {name: [callee...]}"""
    try:
        raw = open(path, encoding='utf-8-sig', errors='replace').read()
    except Exception:
        return [], {}
    lines = raw.splitlines()

    depth = 0
    stack = []          # [(kind, name, depth)]  kind='method'|'block'
    pending = None
    in_block_comment = False

    logs = []
    methods = {}        # method name -> set(callees)
    cur_method = None
    # 声明行到 '{' 之间可能有属性/续行，pending 只保留最近一次

    for ln, text in enumerate(lines, 1):
        code = text
        if in_block_comment:
            if '*/' in code:
                code = code.split('*/', 1)[1]
                in_block_comment = False
            else:
                code = ''
        while '/*' in code:
            head, rest = code.split('/*', 1)
            if '*/' in rest:
                code = head + rest.split('*/', 1)[1]
            else:
                code = head
                in_block_comment = True
                break

        codeline = strip_code(code)

        # 1) 方法声明检测（不紧跟 '{' 的也先记着，下一行可能是 '{'）
        m = DECL_RE.match(codeline)
        if m and m.group('name') not in KEYWORDS:
            pending = m.group('name')
        elif pending is not None and codeline.strip() and '{' not in codeline and ';' in codeline:
            pending = None   # 表达式体/声明尾

        # 2) 括号配对维护
        for ch in codeline:
            if ch == '{':
                depth += 1
                if pending is not None:
                    stack.append(('method', pending, depth))
                    methods.setdefault(pending, set())
                    pending = None
                else:
                    kind = 'loop' if LOOP_RE.search(codeline) else 'block'
                    stack.append((kind, None, depth))
            elif ch == '}':
                if stack and stack[-1][2] == depth:
                    stack.pop()
                depth -= 1

        # 当前所在方法（最近一个 method 栈帧）
        cur_method = None
        for kind, name, d in reversed(stack):
            if kind == 'method':
                cur_method = name
                break

        # 3) 调用关系收集（用于间接热度）
        if cur_method:
            for cm in CALL_RE.finditer(codeline):
                callee = cm.group(1)
                if callee not in KEYWORDS:
                    methods[cur_method].add(callee)

        # 4) 日志检测（在原始行上找，保留字符串里的内容判定 gated）
        #    注释掉的行不算（strip_code 之后看不到 log 即为注释）
        lm = LOG_RE.search(codeline)
        if lm:
            in_loop = any(k == 'loop' for k, _, _ in stack)
            gated = lm.group('gated') is not None
            kind = 'gated:' + lm.group('gated').split('.')[-1].strip() if gated else (
                lm.group('dbg') or lm.group('dbg2') or 'print')
            logs.append({
                'line': ln,
                'method': cur_method or '<field/other>',
                'kind': kind,
                'gated': gated,
                'loop': in_loop,
                'text': text.strip()[:160],
            })

    return logs, methods


def scan():
    files = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in ('Library', 'Temp', 'obj')]
        for fn in filenames:
            if fn.endswith('.cs'):
                files.append(os.path.join(dirpath, fn))

    parsed = {}
    for p in files:
        logs, methods = parse_file(p)
        if logs or methods:
            parsed[p] = (logs, methods)

    # 全局方法表（跨类访问也算，够用）
    all_methods = {}
    decl_count = {}
    for p, (_, methods) in parsed.items():
        for name, callees in methods.items():
            all_methods.setdefault(name, set()).update(callees)
            decl_count[name] = decl_count.get(name, 0) + 1

    # 从每帧方法出发 BFS，得到"被每帧路径间接调用"的方法集合（含距离）
    # ⚠ 只跟随**全工程唯一命名**的调用名：同名方法（Init/Update/Refresh…）跨类歧义是主要误报源
    hot = {}
    path = {}
    frontier = [m for m in all_methods if m in PER_FRAME]
    depth = 1
    while frontier and depth <= 2:
        nxt = []
        for m in frontier:
            for callee in all_methods.get(m, ()):
                if callee in hot or callee in PER_FRAME:
                    continue
                if decl_count.get(callee, 0) != 1:
                    continue
                hot[callee] = depth
                path[callee] = path.get(m, m) + '→' + callee
                nxt.append(callee)
        frontier = nxt
        depth += 1

    rows = []
    for p, (logs, _) in parsed.items():
        rel = os.path.relpath(p, r'e:\Bluedivers').replace('\\', '/')
        editor_only = '/Editor/' in rel or rel.endswith('EditorWindow.cs')
        for lg in logs:
            reasons = []
            if lg['method'] in PER_FRAME or any(lg['method'].startswith(x) for x in PER_FRAME_PREFIX):
                reasons.append('每帧方法')
            if lg['loop']:
                reasons.append('循环体')
            if lg['method'] in hot:
                reasons.append('被每帧调 ' + path.get(lg['method'], lg['method']))
            if lg['method'] in MEDIUM:
                reasons.append('中频(' + lg['method'] + ')')
            if not reasons:
                continue
            rows.append({
                'file': rel, 'line': lg['line'], 'method': lg['method'],
                'kind': lg['kind'], 'gated': lg['gated'],
                'reasons': reasons, 'editorOnly': editor_only,
                'text': lg['text'],
            })

    # 排序：裸日志优先、每帧优先
    def score(r):
        return (r['gated'], '每帧方法' not in r['reasons'], r['editorOnly'], r['file'], r['line'])
    rows.sort(key=score)
    return rows, len(files), len(all_methods)


def section(title, items, show_text=True):
    print('\n' + '=' * 78)
    print('%s  (%d 条)' % (title, len(items)))
    print('=' * 78)
    for r in items:
        print('%-34s:%d  [%s] %s' % (
            r['file'].replace('Assets/Scripts/', ''), r['line'], r['kind'],
            '/'.join(r['reasons'])))
        if show_text:
            print('    %s' % r['text'])


if __name__ == '__main__':
    rows, nfiles, nmethods = scan()
    print('扫描 .cs 文件: %d 个，方法: %d 个' % (nfiles, nmethods))

    own = [r for r in rows if not r['editorOnly'] and '/Plugins/' not in r['file']]
    editor = [r for r in rows if r['editorOnly']]
    plug = [r for r in rows if '/Plugins/' in r['file']]

    def direct(r):
        return ('每帧方法' in r['reasons']) or ('循环体' in r['reasons'])

    def medium(r):
        return any(x.startswith('中频(') for x in r['reasons'])

    bare = [r for r in own if not r['gated']]
    gated = [r for r in own if r['gated']]
    section('A1. 裸 Debug.Log* · 确定每帧/循环（高置信，必处理）', [r for r in bare if direct(r)])
    section('A2. 裸 Debug.Log* · 中频（每次命中/进出/池复用）', [r for r in bare if medium(r) and not direct(r)])
    section('A3. 裸 Debug.Log* · 调用图推测（需人工确认）',
            [r for r in bare if not direct(r) and not medium(r)])
    section('B1. NetSyncLog · 确定每帧/循环', [r for r in gated if direct(r)])
    section('B2. NetSyncLog · 中频', [r for r in gated if medium(r) and not direct(r)])
    section('B3. NetSyncLog · 调用图推测', [r for r in gated if not direct(r) and not medium(r)])
    print('\n' + '=' * 78)
    print('C. 其余（不需处理/只做提示）')
    print('=' * 78)
    print('  第三方插件 Plugins: %d 条' % len(plug))
    print('  Editor 专用（不进包）: %d 条' % len(editor))
    print('  合计: %d 条' % len(rows))

    with open(r'e:\Bluedivers\.codebuddy\plans\hot_logs.json', 'w', encoding='utf-8') as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)
    print('\n明细已导出 .codebuddy/plans/hot_logs.json')
