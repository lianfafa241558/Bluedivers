# -*- coding: utf-8 -*-
"""把 prefab/场景里死掉的家具脚本引用换成新类（字节级替换，不动其它任何内容）。

  Furniture_Base      374b3c2e13591b84cbe9709dac4f9994  ->  Furniture_Attached  ba80bf5efd3411047bb9ef36cbd235d6
  Furniture_EquipActor 80cf9cb3e42ed604db0ec93341c33388 ->  Furniture_Equip     3704b6bfebbc2374da4a9b194065d456

为什么用字节级脚本而不是 replace_in_file：
  ① 多文件批量；② prefab/场景 YAML 大小可达数千行，逐个人工替换不划算；
  ③ 需要「命中次数断言 + 替换后残留为 0」的硬校验。
用法：
    python swap_furniture_script_guid.py            # 预演（只报告）
    python swap_furniture_script_guid.py --apply    # 落盘
"""
import os
import sys

ROOT = r'd:\Pro\Bluedivers'
EXTS = ('.prefab', '.unity', '.asset')
MAP = {
    b'374b3c2e13591b84cbe9709dac4f9994': b'ba80bf5efd3411047bb9ef36cbd235d6',  # Furniture_Base       -> Furniture_Attached
    b'80cf9cb3e42ed604db0ec93341c33388': b'3704b6bfebbc2374da4a9b194065d456',  # Furniture_EquipActor -> Furniture_Equip
}
APPLY = '--apply' in sys.argv


def main():
    hits, files, total = [], 0, 0
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, 'Assets')):
        for fn in filenames:
            if not fn.endswith(EXTS):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, 'rb') as f:
                raw = f.read()
            new = raw
            n = 0
            for old, rep in MAP.items():
                c = raw.count(old)
                if c:
                    n += c
                    hits.append('%s  %s -> %s  x%d'
                                % (os.path.relpath(path, ROOT).replace('\\', '/'),
                                   old.decode(), rep.decode(), c))
                new = new.replace(old, rep)
            if n:
                files += 1
                total += n
                if APPLY and new != raw:
                    with open(path, 'wb') as f:
                        f.write(new)
    print('命中文件数: %d   替换处: %d   %s' % (files, total, '【已落盘】' if APPLY else '【预演】'))
    for h in hits:
        print('  ', h)
    if APPLY:
        left = 0
        for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, 'Assets')):
            for fn in filenames:
                if not fn.endswith(EXTS):
                    continue
                path = os.path.join(dirpath, fn)
                with open(path, 'rb') as f:
                    raw = f.read()
                for old in MAP:
                    if old in raw:
                        left += 1
                        print('  !! 仍有残留:', os.path.relpath(path, ROOT), old.decode())
        print('残留文件数:', left)


main()
