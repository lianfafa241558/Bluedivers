# -*- coding: utf-8 -*-
r"""把"刚新建、Unity 还没重生成 csproj"的 .cs 临时塞进指定程序集的 csproj 副本，供 offline_compile.py 校验。

⚠ 只用于**在 MCP/Unity 还没刷新时**提前验证新文件；Unity 一刷新（csproj 自动包含它）就别再用了。

用法：
    python tmp_compile_newfile.py <程序集名> <相对根目录的 .cs 路径> [更多 .cs ...]
例：
    python tmp_compile_newfile.py 09_Managers Assets/Scripts/09Manager/Battle/NetEnemySpawnBridge.cs
（兼容旧用法：只给一个路径 ⇒ 当作 06_Gameplay 的文件）
"""
import os
import sys
import subprocess

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 1

    if len(args) == 1:                      # 旧用法兼容
        asm, files = '06_Gameplay', args
    else:
        asm, files = args[0], args[1:]

    src = os.path.join(ROOT, asm + '.csproj')
    dst = os.path.join(ROOT, asm + '_tmp.csproj')
    if not os.path.exists(src):
        print('!! 找不到 csproj:', src)
        return 1

    text = open(src, encoding='utf-8').read()
    lines = text.splitlines(keepends=True)

    add = []
    for f in files:
        rel = f.replace('/', '\\')
        line = '    <Compile Include="%s" />\r\n' % rel
        if line in lines or ('<Compile Include="%s" />' % rel) in text:
            print('!! 该行已在 csproj 中，无需临时副本:', rel)
            continue
        add.append(line)

    if not add:
        return 0

    # 插在**最后一个** <Compile Include=...> 之后（不依赖具体锚点文件）
    idx = None
    for i, l in enumerate(lines):
        if '<Compile Include=' in l:
            idx = i
    if idx is None:
        print('!! csproj 里找不到任何 <Compile Include=...>')
        return 1
    lines[idx + 1:idx + 1] = add
    open(dst, 'w', encoding='utf-8').write(''.join(lines))
    print('临时 csproj:', dst, '（含 %d 个新文件）' % len(add))

    try:
        subprocess.run([sys.executable, os.path.join(ROOT, '.codebuddy', 'plans', 'offline_compile.py'),
                        asm + '_tmp'], cwd=ROOT)
    finally:
        if os.path.exists(dst):
            os.remove(dst)
            print('已清理临时 csproj:', dst)
    return 0


if __name__ == '__main__':
    sys.exit(main())
