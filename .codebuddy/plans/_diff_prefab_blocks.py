# -*- coding: utf-8 -*-
"""按 fileID 逐块比对 prefab 与 HEAD 版本，用于确认 MCP 保存没有动到原有序列化内容。"""
import subprocess
import re
import io
import difflib
import sys

path = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Resources/UI/Wnd/FreeCameraWnd.prefab'
head = subprocess.run(['git', 'show', 'HEAD:' + path], capture_output=True).stdout.decode('utf-8-sig')
cur = io.open(path, encoding='utf-8-sig').read()


def blocks(text):
    out = {}
    for p in re.split(r'(?m)^--- ', text):
        m = re.match(r'!u!(\d+) &(\d+)', p)
        if m:
            out[(m.group(1), m.group(2))] = p.rstrip('\n')
    return out


hb, cb = blocks(head), blocks(cur)
print('head blocks=%d cur blocks=%d' % (len(hb), len(cb)))
missing = [k for k in hb if k not in cb]
print('missing ids=%d %s' % (len(missing), missing[:10]))

changed = []
for k in hb:
    if k not in cb:
        continue
    if hb[k] != cb[k]:
        diff = [l for l in difflib.unified_diff(hb[k].splitlines(), cb[k].splitlines(), lineterm='', n=0)
                if (l.startswith('-') or l.startswith('+')) and not l.startswith(('---', '+++'))]
        changed.append((k, diff))

print('changed blocks=%d' % len(changed))
for k, d in changed:
    print('  %s' % (k,))
    for line in d:
        print('     ' + line)
