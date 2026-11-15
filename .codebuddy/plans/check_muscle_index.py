import re

MUSCLES = """Spine Front-Back
Spine Left-Right
Spine Twist Left-Right
Chest Front-Back
Chest Left-Right
Chest Twist Left-Right
UpperChest Front-Back
UpperChest Left-Right
UpperChest Twist Left-Right
Neck Nod Down-Up
Neck Tilt Left-Right
Neck Turn Left-Right
Head Nod Down-Up
Head Tilt Left-Right
Head Turn Left-Right
Left Eye Down-Up
Left Eye In-Out
Right Eye Down-Up
Right Eye In-Out
Jaw Close
Jaw Left-Right
Left Upper Leg Front-Back
Left Upper Leg In-Out
Left Upper Leg Twist In-Out
Left Lower Leg Stretch
Left Lower Leg Twist In-Out
Left Foot Up-Down
Left Foot Twist In-Out
Left Toes Up-Down
Right Upper Leg Front-Back
Right Upper Leg In-Out
Right Upper Leg Twist In-Out
Right Lower Leg Stretch
Right Lower Leg Twist In-Out
Right Foot Up-Down
Right Foot Twist In-Out
Right Toes Up-Down
Left Shoulder Down-Up
Left Shoulder Front-Back
Left Arm Down-Up
Left Arm Front-Back
Left Arm Twist In-Out
Left Forearm Stretch
Left Forearm Twist In-Out
Left Hand Down-Up
Left Hand In-Out
Right Shoulder Down-Up
Right Shoulder Front-Back
Right Arm Down-Up
Right Arm Front-Back
Right Arm Twist In-Out
Right Forearm Stretch
Right Forearm Twist In-Out
Right Hand Down-Up
Right Hand In-Out
Left Thumb 1 Stretched
Left Thumb Spread
Left Thumb 2 Stretched
Left Thumb 3 Stretched
Left Index 1 Stretched
Left Index Spread
Left Index 2 Stretched
Left Index 3 Stretched
Left Middle 1 Stretched
Left Middle Spread
Left Middle 2 Stretched
Left Middle 3 Stretched
Left Ring 1 Stretched
Left Ring Spread
Left Ring 2 Stretched
Left Ring 3 Stretched
Left Little 1 Stretched
Left Little Spread
Left Little 2 Stretched
Left Little 3 Stretched
Right Thumb 1 Stretched
Right Thumb Spread
Right Thumb 2 Stretched
Right Thumb 3 Stretched
Right Index 1 Stretched
Right Index Spread
Right Index 2 Stretched
Right Index 3 Stretched
Right Middle 1 Stretched
Right Middle Spread
Right Middle 2 Stretched
Right Middle 3 Stretched
Right Ring 1 Stretched
Right Ring Spread
Right Ring 2 Stretched
Right Ring 3 Stretched
Right Little 1 Stretched
Right Little Spread
Right Little 2 Stretched
Right Little 3 Stretched""".split('\n')


def parse(path):
    s = open(path, encoding='utf-8').read()
    i = s.find('\n  m_FloatCurves:')
    j = s.find('\n  m_PPtrCurves:')
    blk = s[i:j]
    names = [m.group(1) for m in re.finditer(
        r'- serializedVersion: 2\n    curve:.*?\n    attribute: ([^\n]*)\n    path:', blk, re.S)]
    k = s.find('\n  m_ClipBindingConstant:')
    e = s.find('\n  m_AnimationClipSettings:')
    bb = s[k:e]
    idxs = [int(m.group(1)) for m in re.finditer(r'\n      attribute: (-?\d+)\n', bb)]
    return names, idxs


def main():
    base = 'Assets/Art/Anim/Student/Noa/'
    for f in ['Idle 1.anim', 'Ready.anim', 'Walk.anim', 'CrouchWalk.anim', 'ProneWalk.anim',
              'Death.anim', 'Idle 2.anim']:
        names, idxs = parse(base + f)
        if len(names) != len(idxs):
            print('%-16s 曲线%d 绑定%d  (数量不等，无法按位置配对)' % (f, len(names), len(idxs)))
            continue
        ok = bad = nonmuscle = 0
        bads = []
        for n, a in zip(names, idxs):
            if n in MUSCLES:
                if 0 <= a < 95 and MUSCLES[a] == n:
                    ok += 1
                else:
                    bad += 1
                    bads.append((n, a, MUSCLES[a] if 0 <= a < 95 else '?'))
            else:
                nonmuscle += 1
        print('%-16s 曲线%-4d 肌肉通道 匹配%-4d 错位%-4d 非肌肉通道%-3d' % (f, len(names), ok, bad, nonmuscle))
        for n, a, got in bads[:6]:
            print('      %-30s attribute=%-3d 实际会驱动 %s' % (n, a, got))
        if len(bads) > 6:
            print('      ... 共 %d 条错位' % len(bads))


main()
