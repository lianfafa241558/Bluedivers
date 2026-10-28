# -*- coding: utf-8 -*-
"""给指定玩家武器 prefab 写入 WeaponRecoilProfile（按档位），幂等、带备份、写完回读校验。

只改 `WeaponPlayerController` 组件块内的 `RecoilProfile` 子块；其它字段一个字节都不动。
⚠ prefab 变体（PrefabInstance）不能这么写（覆盖要进 m_Modifications），脚本会跳过并列出。

用法：
    python apply_recoil_presets.py --dry-run
    python apply_recoil_presets.py
"""
import os
import re
import shutil
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
WPC_GUID = 'e03ea2fb50b06ab468194bc5b83375d6'   # WeaponPlayerController.cs.meta
DOC_RE = re.compile(r'(?ms)^--- !u!\d+ &\d+\n.*?(?=^--- !u!|\Z)')

# 字段顺序与 C# 声明一致，读起来和 Inspector 一样
KEYS = ['PitchKick', 'YawKick', 'YawStep', 'YawRandom', 'KickSharpness', 'RecoveryDelay',
        'RecoverySpeed', 'YawRecoveryRatio', 'MaxPitch', 'MaxYaw', 'AdsScale',
        'WeaponKickBack', 'WeaponKickPitch', 'WeaponKickRoll', 'WeaponSpring',
        'SpreadBloomPerShot', 'SpreadBloomMax', 'SpreadBloomDecay', 'ShakePerShot']

PRESETS = {
    # 连发（高射速）：每发小、回正快，靠散布膨胀惩罚长按
    'auto': dict(PitchKick=0.6, YawKick=0.35, YawStep=0.08, YawRandom=0.4, KickSharpness=50,
                 RecoveryDelay=0.06, RecoverySpeed=10, YawRecoveryRatio=0.7, MaxPitch=14, MaxYaw=6,
                 AdsScale=0.8, WeaponKickBack=0.03, WeaponKickPitch=1.2, WeaponKickRoll=1.0,
                 WeaponSpring=18, SpreadBloomPerShot=0.1, SpreadBloomMax=2.5, SpreadBloomDecay=10,
                 ShakePerShot=0.03),
    # 单发/重手枪：一枪一顿、回正快，别让第二发等太久
    'single': dict(PitchKick=2.2, YawKick=0.25, YawStep=0.1, YawRandom=0.3, KickSharpness=45,
                   RecoveryDelay=0.08, RecoverySpeed=15, YawRecoveryRatio=0.7, MaxPitch=8, MaxYaw=4,
                   AdsScale=0.6, WeaponKickBack=0.06, WeaponKickPitch=3.5, WeaponKickRoll=1.5,
                   WeaponSpring=16, SpreadBloomPerShot=0, SpreadBloomMax=2, SpreadBloomDecay=8,
                   ShakePerShot=0.15),
    # 炮/榴弹：慢而重，模型后坐大，屏震明显
    'heavy': dict(PitchKick=3.0, YawKick=0.5, YawStep=0.2, YawRandom=0.5, KickSharpness=25,
                  RecoveryDelay=0.15, RecoverySpeed=6, YawRecoveryRatio=0.7, MaxPitch=16, MaxYaw=8,
                  AdsScale=0.6, WeaponKickBack=0.12, WeaponKickPitch=6, WeaponKickRoll=3,
                  WeaponSpring=9, SpreadBloomPerShot=0, SpreadBloomMax=2, SpreadBloomDecay=8,
                  ShakePerShot=0.45),
}

TARGETS = {
    'Assets/Resources/Weapons/BeamRifle.prefab': 'auto',
    'Assets/Resources/Weapons/Weapon_Blaster.prefab': 'auto',
    'Assets/Resources/Weapons/ArcPistol.prefab': 'single',
    'Assets/Resources/Weapons/RoyalRose.prefab': 'single',
    'Assets/Resources/Weapons/PlasmaCannon.prefab': 'heavy',
    'Assets/Resources/Weapons/Weapon_Launcher.prefab': 'heavy',
}

BACKUP = os.path.join(ROOT, '.codebuddy', 'plans', 'backup', 'recoil')


def render(preset):
    lines = ['  RecoilProfile:']
    for k in KEYS:
        lines.append('    %s: %s' % (k, fmt(preset[k])))
    return '\n'.join(lines) + '\n'


def fmt(v):
    if isinstance(v, float) and v == int(v):
        return '%d' % int(v)
    return ('%.4f' % v).rstrip('0').rstrip('.')


def find_wpc_block(src):
    for m in DOC_RE.finditer(src):
        if 'guid: %s' % WPC_GUID in m.group(0):
            return m.start(), m.end(), m.group(0)
    return None


def patch(src, preset_text):
    hit = find_wpc_block(src)
    if not hit:
        return None, 'no WeaponPlayerController component (prefab 变体？)'
    start, end, block = hit

    if '  RecoilProfile:' in block:
        # 已有：替换整个子块（到下一个 2 空格键为止）
        pat = re.compile(r'(?ms)^  RecoilProfile:\n(?:    [^\n]*\n)+')
        new_block, n = pat.subn(preset_text, block)
        if n != 1:
            return None, '现有 RecoilProfile 块格式异常，跳过'
        return src[:start] + new_block + src[end:], 'replace'

    if '\n  RecoilForce: ' not in block:
        return None, 'WPC 块里找不到 RecoilForce 锚点'
    new_block = re.sub(r'(?m)^(  RecoilForce: [^\n]*\n)', r'\1' + preset_text.replace('\\', r'\\'), block, count=1)
    return src[:start] + new_block + src[end:], 'insert'


def verify(src, preset, name):
    hit = find_wpc_block(src)
    if not hit:
        return 'FAIL 找不到 WPC 块'
    block = hit[2]
    m = re.search(r'(?ms)^  RecoilProfile:\n(.*?)(?=^  \S)', block + '\n')
    if not m:
        return 'FAIL 回读不到 RecoilProfile'
    got = {}
    for line in m.group(1).strip().splitlines():
        k, _, v = line.strip().partition(': ')
        got[k] = float(v)
    bad = [k for k in KEYS if abs(got.get(k, 1e9) - preset[k]) > 1e-4]
    return 'OK %d 项' % len(got) if not bad else 'FAIL 不一致: %s' % bad


def main():
    dry = '--dry-run' in sys.argv
    os.makedirs(BACKUP, exist_ok=True)
    for rel, key in TARGETS.items():
        path = os.path.join(ROOT, rel.replace('/', os.sep))
        if not os.path.exists(path):
            print('  [SKIP] 不存在:', rel)
            continue
        src = open(path, encoding='utf-8', newline='').read()
        preset = PRESETS[key]
        out, how = patch(src, render(preset))
        if out is None:
            print('  [SKIP] %s -> %s' % (rel, how))
            continue
        if out == src:
            print('  [SAME] %s (%s)' % (rel, key))
            continue
        chk = verify(out, preset, rel)
        print('  [%s] %-46s 档=%s 方式=%s 校验=%s' % ('DIFF' if dry else 'WRITE', rel, key, how, chk))
        if chk.startswith('FAIL') or dry:
            continue
        bak = os.path.join(BACKUP, os.path.basename(rel) + '.bak')
        if not os.path.exists(bak):
            shutil.copy2(path, bak)
        open(path, 'w', encoding='utf-8', newline='').write(out)
    print('RESULT: OK')


if __name__ == '__main__':
    main()
