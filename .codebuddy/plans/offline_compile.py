# -*- coding: utf-8 -*-
"""离线编译验证器 —— 用 Unity 自带 Roslyn 按 Unity 生成的 .csproj 编译指定程序集，
只输出「编译错误清单」，**完全不触发 Unity 域重载**。

用法：
    python offline_compile.py 06_Gameplay
    python offline_compile.py 06_Gameplay 10_UI 09_Managers
"""
import os
import re
import sys
import time
import subprocess
import xml.etree.ElementTree as ET

# 工程根：优先环境变量，其次按脚本位置推导（<根>/.codebuddy/plans/本文件）⇒ 换机器不用改脚本
ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

# Unity 安装位置各机器不同（本机是 "D:\Unity Hub\Version\<版本>\Editor\Data"）⇒ 逐个探测
def _find_unity_data():
    cands = [os.environ.get('UNITY_EDITOR_DATA', ''),
             r'D:\Unity Hub\Version\2022.3.62f3\Editor\Data',
             r'D:\UnityHub\Editor\2022.3.62f3\Editor\Data',
             r'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data']
    for c in cands:
        if c and os.path.exists(os.path.join(c, 'DotNetSdkRoslyn', 'csc.dll')):
            return c
    return cands[1]


UNITY = _find_unity_data()
CSC = os.path.join(UNITY, 'DotNetSdkRoslyn', 'csc.dll')
DOTNET = os.path.join(UNITY, 'NetCoreRuntime', 'dotnet.exe')
OUTDIR = os.path.join(ROOT, 'Temp', 'offline_compile')


def parse(path):
    root = ET.parse(path).getroot()
    srcs, refs, defs = [], [], []
    projrefs = []
    lang, unsafe, asm, nowarn = '9.0', False, None, ''
    for el in root.iter():
        tag = el.tag.split('}')[-1]
        if tag == 'Compile' and el.get('Include'):
            srcs.append(el.get('Include'))
        elif tag == 'HintPath' and el.text:
            refs.append(el.text.strip())
        elif tag == 'ProjectReference':
            nm = None
            for ch in el.iter():
                if ch.tag.split('}')[-1] == 'Name' and ch.text:
                    nm = ch.text.strip()
            if not nm:
                nm = os.path.splitext(os.path.basename(el.get('Include') or ''))[0]
            if nm:
                projrefs.append(nm)
        elif tag == 'DefineConstants' and el.text:
            defs.append(el.text.strip())
        elif tag == 'LangVersion' and el.text:
            lang = el.text.strip()
        elif tag == 'AllowUnsafeBlocks' and el.text:
            unsafe = el.text.strip().lower() == 'true'
        elif tag == 'AssemblyName' and el.text:
            asm = el.text.strip()
        elif tag == 'NoWarn' and el.text:
            nowarn = el.text.strip()
    return srcs, refs, defs, lang, unsafe, asm, nowarn, projrefs


def compile_one(name):
    proj = os.path.join(ROOT, name + '.csproj')
    if not os.path.exists(proj):
        print('!! 找不到 csproj:', proj)
        return 0
    srcs, refs, defs, lang, unsafe, asm, nowarn, projrefs = parse(proj)
    src_paths, missing = [], 0
    for s in srcs:
        p = os.path.join(ROOT, s.replace('\\', os.sep))
        if os.path.exists(p):
            src_paths.append(p)
        else:
            missing += 1
    nw = ','.join(x for x in re.split(r'[;, ]+', nowarn) if x.isdigit())
    os.makedirs(OUTDIR, exist_ok=True)
    out = os.path.join(OUTDIR, (asm or name) + '.dll')
    rsp = os.path.join(OUTDIR, name + '.rsp')
    lines = ['-noconfig', '-nostdlib+', '-target:library', '-nologo', '-utf8output',
             '-langversion:' + lang, '-out:"%s"' % out]
    if unsafe:
        lines.append('-unsafe+')
    if nw:
        lines.append('-nowarn:' + nw)
    for d in defs:
        lines.append('-define:' + d)
    nref = 0
    for r_ in refs:
        if os.path.exists(r_):
            lines.append('-r:"%s"' % r_)
            nref += 1
    sa_dirs = [OUTDIR, os.path.join(ROOT, 'Library', 'ScriptAssemblies')]
    nproj = 0
    for nm in projrefs:
        p = None
        for dd in sa_dirs:
            cand = os.path.join(dd, nm + '.dll')
            if os.path.exists(cand):
                p = cand
                break
        if p:
            lines.append('-r:"%s"' % p)
            nref += 1
            nproj += 1
        else:
            print('   ! 缺兄弟程序集 dll:', nm)
    lines += ['"%s"' % p for p in src_paths]
    open(rsp, 'w', encoding='utf-8').write('\n'.join(lines))

    t0 = time.time()
    pr = subprocess.run([DOTNET, 'exec', CSC, '@' + rsp], capture_output=True,
                        text=True, encoding='utf-8', errors='replace')
    dt = time.time() - t0
    outtxt = (pr.stdout or '') + (pr.stderr or '')
    errs = [l for l in outtxt.splitlines() if ': error ' in l]
    print('== %s ==  源:%d(缺%d)  引用:%d(兄弟%d)  lang:%s  用时:%.1fs  错误:%d'
          % (name, len(src_paths), missing, nref, nproj, lang, dt, len(errs)))
    for e in errs[:40]:
        print('   ', e.replace(ROOT + '\\', '').replace(ROOT + '/', ''))
    if not errs:
        tail = [l for l in outtxt.splitlines() if l.strip()][-2:]
        for t in tail:
            print('    |', t)
        print('    输出:', out)
    return len(errs)


if __name__ == '__main__':
    names = sys.argv[1:] or ['06_Gameplay']
    total = 0
    for n in names:
        total += compile_one(n)
    print('总错误数:', total)
