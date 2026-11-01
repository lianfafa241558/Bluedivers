"""临时分析脚本：打印 Unity prefab 粒子/线渲染器的关键字段。"""
import re
import sys


def load_docs(path):
    txt = open(path, encoding='utf-8').read()
    parts = re.split(r'^--- !u!(\d+) &(-?\d+).*$', txt, flags=re.M)
    docs = {}
    for i in range(1, len(parts), 3):
        docs[parts[i + 1]] = (parts[i], parts[i + 2])
    return docs


def gname(docs, fid):
    cls, body = docs.get(fid, ('', ''))
    m = re.search(r'^  m_Name: (.*)$', body, flags=re.M)
    return m.group(1) if m else '?'


def block(body, name):
    """取 `  Name:` 到下一个同级 `  Word:` 之间的内容。"""
    m = re.search(r'\n  %s:\n(.*?)(?=\n  [A-Za-z_][A-Za-z0-9_]*:)' % name, body, flags=re.S)
    return m.group(1) if m else None


def gradient(blk, key):
    """取 xxx: 后面紧跟的 gradient 段（到下一个 4 空格字段）。"""
    m = re.search(r'\n    %s:\n(.*?)(?=\n    [a-zA-Z])' % key, blk, flags=re.S)
    return m.group(1) if m else None


def show_grad(blk, label):
    if not blk:
        print('     %s: <none>' % label)
        return
    print('     %s keys=%s' % (label, re.findall(r'key\d: \{([^}]*)\}', blk)))
    print('     %s ctime=%s atime=%s num=%s' % (
        label,
        re.findall(r'ctime\d: (\d+)', blk),
        re.findall(r'atime\d: (\d+)', blk),
        re.findall(r'm_Num(ColorKeys|AlphaKeys): (\d+)', blk)))


def report(path):
    docs = load_docs(path)
    print('=' * 110)
    print(path)
    for fid, (cls, body) in docs.items():
        if cls not in ('198', '120', '199', '96'):
            continue
        m = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
        name = gname(docs, m.group(1)) if m else ''
        mats = re.findall(r'- \{fileID: 2100000, guid: ([0-9a-f]{32}), type: 2\}', body)
        print('\n--- cls=%s go="%s" mats=%s' % (cls, name, mats))
        if cls == '120':
            par = block(body, 'm_Parameters') or ''
            print('   widthMultiplier=', re.search(r'widthMultiplier: ([-\d.eE+]+)', par).group(1))
            show_grad(gradient(par, 'colorGradient'), 'line color')
        if cls == '199':
            print('   renderMode=', re.search(r'm_RenderMode: (\d+)', body).group(1),
                  'sortMode=', re.search(r'm_SortMode: (\d+)', body).group(1))
        if cls == '198':
            init = block(body, 'InitialModule') or ''
            sg = gradient(init, 'startColor') or ''
            print('   startColor keys=', re.findall(r'key\d: \{([^}]*)\}', sg)[:2],
                  'state=', re.findall(r'minMaxState: (\d+)', sg))
            print('   startSize scalar=', re.findall(r'startSize:\n      scalar: ([-\d.eE+]+)', init),
                  'startSpeed=', re.findall(r'startSpeed:\n      scalar: ([-\d.eE+]+)', init),
                  'duration=', re.findall(r'duration:\n      scalar: ([-\d.eE+]+)', init))
            cm = block(body, 'ColorModule') or ''
            ce = re.findall(r'enabled: (\d+)', cm)
            print('   ColorModule enabled=', ce[:1])
            if ce and ce[0] == '1':
                show_grad(gradient(cm, 'gradient'), 'ps color')
            em = block(body, 'EmissionModule') or ''
            print('   emis enabled=', re.findall(r'enabled: (\d+)', em)[:1],
                  'rate=', re.findall(r'rateOverTime:\n      scalar: ([-\d.eE+]+)', em))
            sm = block(body, 'ShapeModule') or ''
            print('   shape enabled=', re.findall(r'enabled: (\d+)', sm)[:1],
                  'type=', re.findall(r'\n    type: (\d+)', sm)[:1],
                  'angle=', re.findall(r'\n    angle: ([-\d.eE+]+)', sm)[:1])
            tm = block(body, 'TrailModule') or ''
            te = re.findall(r'enabled: (\d+)', tm)
            print('   TrailModule enabled=', te[:1])
            if te and te[0] == '1':
                show_grad(gradient(tm, 'colorOverTrail'), 'trail color')
            vel = block(body, 'VelocityModule') or ''
            print('   vel enabled=', re.findall(r'enabled: (\d+)', vel)[:1],
                  'x=', re.findall(r'x:\n      scalar: ([-\d.eE+]+)', vel)[:1])


for p in sys.argv[1:]:
    report(p)
