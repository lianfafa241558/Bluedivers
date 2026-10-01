# -*- coding: utf-8 -*-
"""定位 Unity 安装目录（为离线编译找自带 Roslyn / dotnet）。"""
import os
import re
import glob

log = os.path.expandvars(r'%LOCALAPPDATA%\Unity\Editor\Editor.log')
print('Editor.log =', log, os.path.exists(log))
if os.path.exists(log):
    txt = open(log, 'rb').read().decode('utf-8', 'replace')
    for pat, label in [(r'([A-Za-z]:\\[^"\r\n]*?Unity\.exe)', 'Unity.exe'),
                       (r'([A-Za-z]:\\[^"\r\n]*?Editor\\Data)', 'Editor\\Data')]:
        s = sorted(set(re.findall(pat, txt)))
        print('  %s (%d):' % (label, len(s)))
        for p in s[:12]:
            print('     ', p)

print('\n== 常见根 ==')
roots = [r'C:\Program Files\Unity', r'C:\Program Files\Unity\Hub\Editor', r'D:\Program Files\Unity',
         r'D:\Program Files\Unity\Hub\Editor', r'C:\Unity', r'D:\Unity', r'E:\Unity',
         r'D:\Program Files', r'E:\Program Files', r'D:\Soft', r'D:\Tools']
for r_ in roots:
    if os.path.isdir(r_):
        print('  EXIST', r_)
        for x in sorted(os.listdir(r_))[:15]:
            print('       -', x)

print('\n== 项目版本 ==')
pv = r'd:\Pro\Bluedivers\ProjectSettings\ProjectVersion.txt'
if os.path.exists(pv):
    print('  ', open(pv).read().strip().replace('\n', ' | '))
