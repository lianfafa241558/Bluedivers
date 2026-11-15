# -*- coding: utf-8 -*-
"""列出若干 prefab 里每个 MonoBehaviour 挂的脚本（guid -> .cs 路径），用于核对"代码里依赖的组件是否真挂了"。
用法：python _prefab_scripts.py <prefab 相对路径> [...]
"""
import io
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets')


def build_index():
    idx = {}
    for dirpath, _d, files in os.walk(ASSETS):
        for f in files:
            if f.endswith('.cs.meta'):
                p = os.path.join(dirpath, f)
                m = re.search(r'guid:\s*([0-9a-f]{32})', io.open(p, encoding='utf-8', errors='ignore').read())
                if m:
                    idx[m.group(1)] = f[:-8]
    return idx


def main():
    idx = build_index()
    for rel in sys.argv[1:]:
        p = os.path.join(ROOT, rel)
        if not os.path.exists(p):
            print('缺文件', rel)
            continue
        txt = io.open(p, encoding='utf-8', errors='ignore').read()
        print('\n=== %s' % rel)
        for m in re.finditer(r'^--- !u!(\d+) &(\d+)(.*?)(?=^--- |\Z)', txt, flags=re.M | re.S):
            cls, body = m.group(1), m.group(3)
            if cls == '114':
                g = re.search(r'm_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})', body)
                if g:
                    print('   script: %-55s %s' % (idx.get(g.group(1), '<非脚本>' + g.group(1)), ''))
            elif cls == '1001':
                pass
        # 嵌套 prefab 实例的源
        for m in re.finditer(r'm_SourcePrefab:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})', txt):
            print('   nested-prefab guid: %s' % m.group(1))


if __name__ == '__main__':
    main()
