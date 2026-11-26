# -*- coding: utf-8 -*-
"""按窗口分段统计 InputManager.prefab 里绑了哪些键（键码 是 KeyCode 整数值）。"""
import io
import re
import sys
import collections

path = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Resources/Prefabs/Manager/InputManager.prefab'
text = io.open(path, encoding='utf-8-sig').read()

names = ['Esc', 'Horizontal', 'Vertical', 'Shift', 'Jump', 'Operate', 'Throw', 'Reload', 'Fire', 'Aim', 'FlareGun',
         'Weapon1', 'Weapon2', 'Weapon3', 'Weapon4', 'Airdrop', 'Left', 'Up', 'Right', 'Down',
         'Rise', 'Fall', 'Equip', 'Mule', 'H', 'J', 'Acceler', 'Deceler', 'MiniMap', 'Perspective', 'UseBag',
         'Prone', 'Crouching']
windows = {0: 'All', 1: 'Game', 2: 'UI', 3: 'Airdrop', 4: 'FreeCamera'}
keycode = {27: 'Escape', 32: 'Space', 9: 'Tab', 13: 'Enter', 304: 'LShift', 303: 'RShift',
           273: 'Up', 274: 'Down', 275: 'Right', 276: 'Left', 122: 'Z', 99: 'C', 109: 'M', 121: 'Y', 116: 'T',
           118: 'V', 100: 'D', 97: 'A', 119: 'W', 115: 'S', 101: 'E', 113: 'Q', 114: 'R', 102: 'F', 103: 'G'}

items = re.findall(r'- window: (\d+)\s*\n\s+key: (\d+)\s*\n\s+positiveMainValue: (\d+)\s*\n\s+positiveSpareValue: (\d+)\s*\n'
                   r'\s+negativeMainValue: (\d+)\s*\n\s+negativeSpareValue: (\d+)', text)
by_win = collections.defaultdict(list)
for w, k, pm, ps, nm, ns in items:
    w, k = int(w), int(k)
    nm_name = names[k] if k < len(names) else '?%d' % k
    codes = [int(pm), int(ps), int(nm), int(ns)]
    codes = [c for c in codes if c]
    lbl = ' / '.join(keycode.get(c, 'code%d' % c) for c in codes)
    by_win[w].append((nm_name, k, lbl))

print('total items = %d' % len(items))
for w in sorted(by_win):
    print('\n[window %d = %s] %d 条' % (w, windows.get(w, '?'), len(by_win[w])))
    for nm_name, k, lbl in by_win[w]:
        print('   %-12s key=%-2d  %s' % (nm_name, k, lbl))
