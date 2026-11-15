import re

def curves(path):
    s = open(path, encoding='utf-8').read()
    i = s.find('\n  m_FloatCurves:')
    j = s.find('\n  m_PPtrCurves:')
    b = s[i:j]
    d = {}
    for m in re.finditer(r'- serializedVersion: 2\n    curve:.*?\n    attribute: ([^\n]*)\n    path:([^\n]*)', b, re.S):
        vals = [(float(t), float(v)) for t, v in
                re.findall(r'\n        time: ([-\d.e]+)\n        value: ([-\d.e]+)', m.group(0))]
        d[m.group(1)] = vals
    return d

for f in ('ProneWalk.anim', 'CrouchWalk.anim'):
    c = curves('Assets/Art/Anim/Student/Noa/' + f)
    print('=====', f, 'channels', len(c))
    for k in ('RootT.x', 'RootT.y', 'RootT.z', 'RootQ.x', 'RootQ.y', 'RootQ.z', 'RootQ.w',
              'Spine Front-Back', 'Left Upper Leg Front-Back', 'Left Lower Leg Stretch',
              'Left Foot Up-Down', 'Head Nod Down-Up'):
        if k in c:
            v = c[k]
            print('  %-28s n=%-4d' % (k, len(v)), ' '.join('%.3f' % x[1] for x in v[:6]))
