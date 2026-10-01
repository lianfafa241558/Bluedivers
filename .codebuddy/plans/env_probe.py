# -*- coding: utf-8 -*-
"""探测离线编译工具链：dotnet/msbuild/csc、Unity 自带 Roslyn、项目 .sln/.csproj、Library/ScriptAssemblies。"""
import os
import glob
import shutil
import time

ROOT = r'd:\Pro\Bluedivers'

print('== PATH 上的工具 ==')
for t in ['dotnet', 'msbuild', 'csc', 'nuget']:
    print('  %-8s %s' % (t, shutil.which(t)))

print('\n== Unity Hub 编辑器 & 自带 Roslyn ==')
for base in [r'C:\Program Files\Unity\Hub\Editor', r'D:\Program Files\Unity\Hub\Editor',
             r'C:\Program Files\Unity', r'D:\Unity']:
    if not os.path.isdir(base):
        continue
    for ver in sorted(os.listdir(base)):
        data = os.path.join(base, ver, 'Editor', 'Data') if os.path.isdir(os.path.join(base, ver, 'Editor', 'Data')) else os.path.join(base, ver, 'Data')
        cands = [
            os.path.join(data, 'Tools', 'Roslyn', 'csc.exe'),
            os.path.join(data, 'DotNetSdkRoslyn', 'csc.dll'),
            os.path.join(data, 'NetCoreRuntime', 'dotnet.exe'),
        ]
        print('  %s' % os.path.join(base, ver))
        for c in cands:
            if os.path.exists(c):
                print('     OK  %s  (%d MB)' % (c, os.path.getsize(c) // 1048576))

print('\n== 项目解决方案文件 ==')
for pat in ['*.sln', '*.csproj']:
    fs = glob.glob(os.path.join(ROOT, pat))
    print('  %-10s %d 个 %s' % (pat, len(fs), [os.path.basename(x) for x in fs[:8]]))

print('\n== Library/ScriptAssemblies ==')
sa = os.path.join(ROOT, 'Library', 'ScriptAssemblies')
if os.path.isdir(sa):
    ds = sorted(glob.glob(os.path.join(sa, '*.dll')), key=os.path.getmtime, reverse=True)
    print('  dll 数 =', len(ds))
    for d in ds[:6]:
        print('   %s  %s' % (time.strftime('%m-%d %H:%M', time.localtime(os.path.getmtime(d))), os.path.basename(d)))
else:
    print('  不存在')

print('\n== Unity 安装目录下的 UnityAssemblies（引用用）==')
for base in [r'C:\Program Files\Unity\Hub\Editor', r'D:\Program Files\Unity\Hub\Editor']:
    if not os.path.isdir(base):
        continue
    for ver in sorted(os.listdir(base)):
        for sub in ['Editor/Data/Managed/UnityEngine', 'Editor/Data/Managed']:
            p = os.path.join(base, ver, sub)
            if os.path.isdir(p):
                print('  OK ', p)
