# -*- coding: utf-8 -*-
r"""[一次性验证] 把新脚本临时塞进 06_Gameplay.csproj 副本，供 offline_compile.py 编译校验。

用法：
    python tmp_compile_newfile.py <相对 Assets 的 .cs 路径>
例：
    python tmp_compile_newfile.py Assets\Scripts\06Gameplay\Mission\MissionPickupHandEquip.cs
"""
import os
import sys
import subprocess

ROOT = r'd:\Pro\Bluedivers'
PROJ = '06_Gameplay.csproj'
TMP = '06_Gameplay_tmp.csproj'
ANCHOR = r'<Compile Include="Assets\Scripts\06Gameplay\Mission\MissionOperationFurn.cs" />'

rel = sys.argv[1].replace('/', '\\')
src = os.path.join(ROOT, PROJ)
dst = os.path.join(ROOT, TMP)
text = open(src, encoding='utf-8').read()
line = r'    <Compile Include="%s" />' % rel
if line in text:
    print('!! 该行已在 csproj 中，无需临时副本:', rel)
    sys.exit(0)
text = text.replace(ANCHOR, ANCHOR + '\n' + line, 1)
open(dst, 'w', encoding='utf-8').write(text)

try:
    subprocess.run([sys.executable, os.path.join(ROOT, '.codebuddy', 'plans', 'offline_compile.py'), TMP[:-7]],
                   cwd=ROOT)
finally:
    if os.path.exists(dst):
        os.remove(dst)
        print('已清理临时 csproj:', dst)
