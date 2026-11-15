import re, sys, subprocess

def parse(path, text=None):
    s = text if text is not None else open(path, encoding='utf-8').read()
    def block(key):
        i = s.find('\n  ' + key + ':')
        if i < 0:
            return ''
        j = s.find('\n  m_', i + 5)
        return s[i:j if j > 0 else len(s)]
    out = {}
    for key in ('m_FloatCurves', 'm_EditorCurves'):
        b = block(key)
        d = {}
        # each curve entry: find 'attribute: X' then the m_Curve values up to next '- serializedVersion'
        for m in re.finditer(r'- serializedVersion: 2\n    curve:.*?\n    attribute: ([^\n]*)\n    path:([^\n]*)', b, re.S):
            attr = m.group(1)
            vals = re.findall(r'\n        time: ([-\d.e]+)\n        value: ([-\d.e]+)', m.group(0))
            d[attr] = [(float(t), float(v)) for t, v in vals]
        out[key] = d
    return out

def cmp(path, text=None, tag=''):
    r = parse(path, text)
    f, e = r['m_FloatCurves'], r['m_EditorCurves']
    print('==', path, tag, '| float', len(f), 'editor', len(e))
    only_f = set(f) - set(e)
    only_e = set(e) - set(f)
    if only_f: print('   只在 FloatCurves:', sorted(only_f))
    if only_e: print('   只在 EditorCurves:', sorted(only_e))
    diff = []
    for k in set(f) & set(e):
        a, b = f[k], e[k]
        if len(a) != len(b):
            diff.append((k, 'N=%d/%d' % (len(a), len(b))))
            continue
        for (t1, v1), (t2, v2) in zip(a, b):
            if abs(v1 - v2) > 1e-4 or abs(t1 - t2) > 1e-4:
                diff.append((k, 't=%.3f v=%.4f vs %.4f' % (t1, v1, v2)))
                break
    if diff:
        print('   ⚠ 不一致 %d 条:' % len(diff))
        for k, msg in diff[:15]:
            print('     ', k, '->', msg)
    else:
        print('   ✅ 两个块一致')

for f in ['ProneWalk.anim', 'Idle 1.anim', 'Walk.anim', 'CrouchWalk.anim']:
    p = 'Assets/Art/Anim/Student/Noa/' + f
    cmp(p, tag='[WORK]')
    head = subprocess.run(['git', 'show', 'HEAD:' + p], capture_output=True, text=True,
                          encoding='utf-8').stdout
    if head:
        cmp(p, head, tag='[HEAD]')
