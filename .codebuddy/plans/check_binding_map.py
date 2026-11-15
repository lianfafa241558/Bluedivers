import re
import sys


def parse(path):
    s = open(path, encoding='utf-8').read()
    # m_FloatCurves 顺序里的 attribute 名
    i = s.find('\n  m_FloatCurves:')
    j = s.find('\n  m_PPtrCurves:')
    blk = s[i:j]
    names = [m.group(1) for m in re.finditer(
        r'- serializedVersion: 2\n    curve:.*?\n    attribute: ([^\n]*)\n    path:', blk, re.S)]
    # ClipBindingConstant 顺序里的 attribute 索引
    k = s.find('\n  m_ClipBindingConstant:')
    e = s.find('\n  m_AnimationClipSettings:')
    bb = s[k:e]
    idxs = [int(m.group(1)) for m in re.finditer(r'\n      attribute: (-?\d+)\n', bb)]
    paths = [int(m.group(1)) for m in re.finditer(r'\n      path: (-?\d+)\n', bb)]
    return names, idxs, paths


def main():
    base = 'Assets/Art/Anim/Student/Noa/'
    files = ['Idle 1.anim', 'Ready.anim', 'ProneWalk.anim', 'CrouchWalk.anim', 'Walk.anim']
    ref = {}
    for f in files:
        names, idxs, paths = parse(base + f)
        print('== %-16s floatCurves=%d bindings=%d' % (f, len(names), len(idxs)))
        if len(names) != len(idxs):
            print('   !!! 曲线数(%d) != 绑定数(%d)' % (len(names), len(idxs)))
        if f == 'Idle 1.anim':
            for n, a in zip(names, idxs):
                ref[a] = n
            print('   建立参考表 attribute->name，共 %d 条' % len(ref))
            continue
        bad = []
        for n, a in zip(names, idxs):
            exp = ref.get(a)
            if exp is not None and exp != n:
                bad.append((a, n, exp))
        if bad:
            print('   ### 有 %d 条曲线的 attribute 索引指向别的肌肉：' % len(bad))
            for a, n, exp in bad:
                print('      attribute %-4d 写的是 %-28s 但按参考表应是 %s' % (a, n, exp))
        else:
            print('   OK 与 Idle 1 参考表一致')


main()
