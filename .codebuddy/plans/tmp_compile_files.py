# -*- coding: utf-8 -*-
r"""[一次性验证] 把**尚未进 .csproj 的新脚本**临时塞进 csproj 副本，供 offline_compile.py 校验。

为什么需要：Unity 只在刷新资产时重写 .csproj；新写的 .cs 还没刷新 ⇒ offline_compile 编译不到它。
本脚本复制 `<asm>.csproj` → `<asm>_tmp.csproj`（插入要编译的新文件），编译，然后删除副本。

用法：
    python -X utf8 .codebuddy/plans/tmp_compile_files.py 02_Net Assets/Scripts/NetTmp/Client/RoomMeta.cs ...
    # 新加了 asmdef 引用（Unity 还没重写 csproj）时，用 --ref 补上兄弟程序集引用：
    python -X utf8 .codebuddy/plans/tmp_compile_files.py 10_UI Assets/Scripts/10UI/Wnd/PasswordWnd.cs --ref 02_Net
"""
import os
import subprocess
import sys

ROOT = r'd:\Pro\Bluedivers'
PLANS = os.path.join(ROOT, '.codebuddy', 'plans')


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    argv = sys.argv[1:]
    asm = argv[0]
    files, extra_refs = [], []
    i = 1
    while i < len(argv):
        if argv[i] == '--ref':
            extra_refs.append(argv[i + 1])
            i += 2
            continue
        files.append(argv[i].replace('/', '\\'))
        i += 1

    src = os.path.join(ROOT, asm + '.csproj')
    dst = os.path.join(ROOT, asm + '_tmp.csproj')
    if not os.path.exists(src):
        print('!! 找不到 csproj:', src)
        return 1

    text = open(src, encoding='utf-8').read()

    added = []
    for rel in files:
        line = '    <Compile Include="%s" />' % rel
        if line in text:
            print('   （已在 csproj，跳过）', rel)
            continue
        if '</ItemGroup>' not in text:
            print('!! csproj 结构异常，找不到 </ItemGroup>')
            return 1
        text = text.replace('</ItemGroup>', line + '\n  </ItemGroup>', 1)
        added.append(rel)

    for r in extra_refs:
        if ('<Name>%s</Name>' % r) in text:
            print('   （已有该 ProjectReference，跳过）', r)
            continue
        ref = ('    <ProjectReference Include="..\\%s.csproj">\n'
               '      <Name>%s</Name>\n'
               '    </ProjectReference>\n' % (r, r))
        text = text.replace('</ItemGroup>', ref + '  </ItemGroup>', 1)
        print('   + ProjectReference:', r)

    open(dst, 'w', encoding='utf-8').write(text)
    print('临时 csproj: %s_tmp.csproj  (+%d 个新文件)' % (asm, len(added)))

    try:
        subprocess.run([sys.executable, os.path.join(PLANS, 'offline_compile.py'), asm + '_tmp'],
                       cwd=ROOT)
    finally:
        if os.path.exists(dst):
            os.remove(dst)
            print('已清理:', dst)
    return 0


if __name__ == '__main__':
    sys.exit(main())
