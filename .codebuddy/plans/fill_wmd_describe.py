# -*- coding: utf-8 -*-
"""给 WeaponModule 下的 WMD_*.asset 写入 describe（风味描述）。

口径：
- 只写 `describe`，不动 name/type/desc/modifys；引导为幂等（已存在则整行替换）。
- 字符串按 Unity 习惯写成 \\uXXXX 转义（与既有 `name:` 一致）。
- 保留原文件 BOM 与 CRLF；写完回读校验。
"""
import os
import re
import sys

ROOT = r'd:\Pro\Bluedivers\Assets\Resources\GameData\WeaponModule'

# key = 相对 ROOT 的路径（/ 分隔）
DESCRIBE = {
    'ArcPistol/WMD_QuickdrawCylinder.asset': '战地快拆设计，散热排片徒手就能换下来。工程部管这叫「边打边修」。',
    'ArcPistol/WMD_MatchTrigger.asset': '从竞技队流出来的扳机组件，行程短到枪比你更早扣完。',
    'ArcPistol/WMD_SixShooter.asset': '先把热量攒着，够你多打几发。攒到哪去，那是散热器的问题。',
    'ArcPistol/WMD_ElephantRounds.asset': '弹头大得枪都替你叹气。装上它，每一发都是拿热量换的伤害。',
    'ArcPistol/WMD_MoonClipFeed.asset': '整条回路泡在冷却液里工作。渗出来的东西，工程部建议别舔。',
    'ArcPistol/WMD_OverboreChamber.asset': '贴着零件还能撑住的那条线走。没人规定不能贴着线走。',

    'BeamRifle/WMD_AggressiveVenting.asset': '把余热直接导成火。枪管更烫了，但对面更烫。',
    'BeamRifle/WMD_OvertunedParticleAccelerator.asset': '出厂标注的「安全区间」被调到了「实验区间」，多出来的能量总要有去处。',
    'BeamRifle/WMD_ThermalFeedback.asset': '射出去的热量被拐回来喂给电池。工程部管这个叫报复。',
    'BeamRifle/WMD_ThermalLiquidCoolant.asset': '配方保密，因为里面有两样东西是被明令禁止的。降温是真降。',

    'LightSword/WMD_EfficiencyTweaks.asset': '把每一毫焦都算清楚的改法。不炫技，就是让你多挥几下。',

    'Logic/WMD_AIStabilityEngine.asset': '稳得像有人在替你瞄准。它确实在替你瞄，所以慢一点、轻一点。',
    'Logic/WMD_CompactAmmo.asset': '装药压得更紧，弹壳削得更薄。能带更多，也飞得更直。',
    'Logic/WMD_JumboRounds.asset': '废气全拐回弹膛再用一遍。散、重、啃不动弱点，破甲倒是真狠。',
    'Logic/WMD_OverclockedFiring.asset': '击发机构跑在额定频率之上。它还能动，只是不再按说明书动。',

    'PlasmaCannon/WMD_EnergyRerouting.asset': '电容线路重接，能量少走几步路。充得更快，装得也更多。',
    'PlasmaCannon/WMD_HeavyHitter.asset': '每发都往电池里多舀一勺。硬是硬了，电池开始不够用。',
    'PlasmaCannon/WMD_MagneticCooling.asset': '拿磁场把热量甩出去。没有风扇，没有液体，原理别问。',
    'PlasmaCannon/WMD_Overcharger.asset': '蓄力档位再往上顶一格。顶到多少算过载，通常由爆炸半径决定。',

    'Rationality/WMD_HairTrigger.asset': '枪管下面多挂了一截。少装几次弹，能带的总量也就少了。',
    'Rationality/WMD_HeavyPayload.asset': '弹头里灌了些不该灌的东西。飞得慢，落地之后没人想靠近。',
    'Rationality/WMD_OverclockedChamber.asset': '弹膛咬得更狠，循环快了一截。装填时你得等它自己松开。',
    'Rationality/WMD_PenetratorLoad.asset': '军方那边的装药配比。代价是弹药袋跟着缩水。',
    'Rationality/WMD_SteadyAim.asset': '枪托配重重新找平。稳得不像同一把枪，换弹都顺手了点。',

    'RoyalRose/WMD_ActiveStabilitySystem.asset': '陀螺仪替你吃掉抖动。它也要占地方，从你的弹匣里占。',
    'RoyalRose/WMD_Hipster.asset': '不用瞄，反正也打不准那个位置。数量补上精度的缺口。',
    'RoyalRose/WMD_MinimalClips.asset': '弹匣做薄了，装得更多、塞得更快。金属工程师看完只说了句「行吧」。',
    'RoyalRose/WMD_SupercoolingChamber.asset': '枪膛冷到结霜，代价是你得站着不动。狙击手觉得这很浪漫。',

    'SecretarySanction/WMD_BackfeedingModule.asset': '把尾迹的残余能量抽回弹仓。弹更多了，尾巴更短了。',
    'SecretarySanction/WMD_Hellfire.asset': '尾迹不再只是残留，是烧起来的一条路。烧得猛，也就散得快。',
    'SecretarySanction/WMD_UltraMagneticCoils.asset': '线圈撑得更开，尾迹留得更久。走过的地方都还带电。',

    'Smart/WMD_WUD_ChainExplorative.asset': '每发命中都附一次小爆炸。省了瞄准，也省了子弹。',
    'Smart/WMD_WUD_Executioner.asset': '锁定快到你来不及犹豫，也来不及多锁几层。',
    'Smart/WUD_EveryoneGetsShare.asset': '把伤害摊给所有目标。够分，就是不够深。',

    'WMD_None.asset': '出厂原样。工程部还没来得及对它做点什么。',
}


def esc(s):
    return ''.join(c if ord(c) < 128 else '\\u%04X' % ord(c) for c in s)


def main():
    dry = '--dry-run' in sys.argv
    missing = []
    changed = []
    for rel, text in DESCRIBE.items():
        path = os.path.join(ROOT, rel.replace('/', os.sep))
        if not os.path.exists(path):
            missing.append(rel)
            continue
        raw = open(path, 'rb').read()
        bom = raw.startswith(b'\xef\xbb\xbf')
        body = raw[3:] if bom else raw
        src = body.decode('utf-8')
        nl = '\r\n' if '\r\n' in src else '\n'
        new_line = '  describe: "%s"' % esc(text)

        m = re.search(r'^  name: .*$', src, re.M)
        if not m:
            missing.append(rel + ' (无 name 行)')
            continue

        old = re.search(r'^  describe: .*$', src, re.M)
        if old:
            if old.group(0) == new_line:
                continue
            src = src.replace(old.group(0), new_line, 1)
        else:
            src = src[:m.end()] + nl + new_line + src[m.end():]

        if not dry:
            data = src.encode('utf-8')
            if bom:
                data = b'\xef\xbb\xbf' + data
            open(path, 'wb').write(data)
        changed.append(rel)

        # 回读校验
        if not dry:
            chk = open(path, 'rb').read()
            ok_bom = chk.startswith(b'\xef\xbb\xbf') == bom
            chk_s = (chk[3:] if bom else chk).decode('utf-8')
            cnt = len(re.findall(r'^  describe: ', chk_s, re.M))
            assert ok_bom, 'BOM 变化: ' + rel
            assert cnt == 1, 'describe 行数 %d: %s' % (cnt, rel)
            assert new_line in chk_s, '未写入: ' + rel

    print('dry-run' if dry else '写入完成', '| 变更', len(changed), '| 缺失', missing)
    for c in changed:
        print('  ', c)


if __name__ == '__main__':
    main()
