import subprocess, re, os

files = ['Death.anim', 'Ready.anim', 'Idle 2.anim', 'Idle 1.anim', 'Walk.anim',
         'CrouchWalk.anim', 'ProneWalk.anim']
base = 'Assets/Art/Anim/Student/Noa/'

KEYS = ['m_MuscleClip', 'm_MuscleClipSize', 'm_ClipBindingConstant',
        'm_FloatCurves', 'm_EditorCurves', 'm_HasGenericRootTransform',
        'm_HasMotionFloatCurves', 'm_SampleRate', 'm_StopTime']

print('%-16s %-6s %s' % ('file', 'ver', ' '.join(k.replace('m_', '')[:14] for k in KEYS)))
for f in files:
    p = base + f
    cur = open(p, encoding='utf-8').read()
    try:
        head = subprocess.run(['git', 'show', 'HEAD:' + p], capture_output=True,
                              text=True, encoding='utf-8').stdout
    except Exception:
        head = ''
    for tag, s in (('HEAD', head), ('WORK', cur)):
        if not s:
            continue
        row = []
        for k in KEYS:
            c = s.count('\n  ' + k + ':')
            row.append(str(c))
        print('%-16s %-6s %s' % (f[:16], tag, ' '.join(row)))
