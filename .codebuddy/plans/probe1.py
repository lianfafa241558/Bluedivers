# -*- coding: utf-8 -*-
import os
import re
import glob

ROOT = r'd:\Pro\Bluedivers'
ET = None

print('== 06_Gameplay.dll 里的命名空间字符串 ==')
for p in [os.path.join(ROOT, 'Temp', 'offline_compile', '06_Gameplay.dll'),
          os.path.join(ROOT, 'Library', 'ScriptAssemblies', '06_Gameplay.dll')]:
    if os.path.exists(p):
        b = open(p, 'rb').read()
        print(' ', os.path.relpath(p, ROOT), '  大小', len(b))
        for s in [b'FPSGame.GameData', b'FPSGame.Game', b'ArchivesData_SO', b'ArchSettingData', b'FPSGame.Furn']:
            print('     %-20s %s' % (s.decode(), s in b))
    else:
        print('  缺', p)

print('\n== 10_UI.csproj 的 ProjectReference ==')
p = os.path.join(ROOT, '10_UI.csproj')
if os.path.exists(p):
    t = open(p, encoding='utf-8', errors='replace').read()
    print('  ', re.findall(r'<ProjectReference Include="([^"]+)"', t))
    print('  06_Gameplay 在其中:', '06_Gameplay.csproj' in t)
else:
    print('  缺 10_UI.csproj')

print('\n== 全仓声明 ArchivesData_SO / ArchSettingData 的文件 ==')
for name in ['ArchivesData_SO', 'ArchSettingData', 'ArchivesFloat']:
    hits = []
    for r, d, fs in os.walk(os.path.join(ROOT, 'Assets')):
        for f in fs:
            if not f.endswith('.cs'):
                continue
            pth = os.path.join(r, f)
            try:
                t = open(pth, encoding='utf-8-sig', errors='replace').read()
            except OSError:
                continue
            if re.search(r'\b(?:class|struct|enum|interface)\s+' + name + r'\b', t):
                ns = re.search(r'namespace\s+([\w\.]+)', t)
                hits.append((os.path.relpath(pth, ROOT).replace('\\', '/'), ns.group(1) if ns else ''))
    print('  %s -> %s' % (name, hits))
