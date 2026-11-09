# -*- coding: utf-8 -*-
r"""Grenade_Mine.prefab 组件裁剪（一次性脚本）：
  - 删 HealthOther / Damageable / Actor / Furniture_GrenadePickup
  - DeployableMine → RepeatableMine（保留所有已配置字段，补 TeamOverride/ExplodeTimes/RearmInterval）

⚠ 为什么走 YAML 而不是 MCP 的 modify_contents：MCP 存 prefab 走 LoadPrefabContents+SaveAsPrefabAsset，
  已实测会把没碰的嵌套字段（ImpactVfx / UseCollisionDirection）按内存旧值回写清空；这颗雷的伤害配置
  全在 DamageData 里，代价太大。文本级删除只动该动的行。
用法：python fix_grenade_mine_prefab.py
"""
import io
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PREFAB = os.path.join(ROOT, 'Assets', 'Resources', 'Prefabs', 'Grenade', 'Grenade_Mine.prefab')

NEW_GUID = '1cb61cba30e702c43bfdd0e69f0b8807'   # RepeatableMine.cs
OLD_GUID = 'b829b363b89b69e4d8f2ab27c789dbe5'   # DeployableMine.cs

ROOT_GO = 1407605918332124854
MINE_DOC = -8166166637206497874
DROP = {  # 要整块删掉的组件（fileID → 类名，只为打印）
    3884080132440999546: 'Damageable',
    2628189497172358424: 'HealthOther',
    2810349685958166275: 'Actor',
    -5913475007642917229: 'Furniture_GrenadePickup',
}


def main():
    with io.open(PREFAB, 'r', encoding='utf-8', newline='') as f:
        text = f.read()

    sep = '\r\n' if '\r\n' in text else '\n'
    docs = re.split(r'(?m)^(?=--- )', text)
    head, docs = docs[0], docs[1:]
    out, dropped, mine_hit = [], [], False

    for doc in docs:
        m = re.match(r'--- !u!(\d+) &(-?\d+)', doc)
        anchor = int(m.group(2)) if m else None

        if anchor in DROP:
            dropped.append(DROP[anchor])
            continue

        if anchor == ROOT_GO:
            # 从根 GameObject 的 m_Component 列表里摘掉被删组件
            doc = ''.join(
                ln for ln in doc.splitlines(keepends=True)
                if not any(('component: {fileID: %d}' % a) in ln for a in DROP)
            )

        if anchor == MINE_DOC:
            if OLD_GUID not in doc:
                print('!! 地雷组件里找不到 DeployableMine 的 guid，脚本可能已跑过')
                return 1
            doc = doc.replace(OLD_GUID, NEW_GUID)
            # TeamOverride 跟在 intelligent 后面（基类字段序）
            doc = doc.replace('  intelligent: 1' + sep,
                              '  intelligent: 1' + sep + '  TeamOverride: -1' + sep, 1)
            # 子类字段补到本组件末尾（下一份文档之前）
            if not doc.endswith(sep):
                doc += sep
            doc += '  ExplodeTimes: 3' + sep + '  RearmInterval: 3' + sep
            mine_hit = True

        out.append(doc)

    if not mine_hit:
        print('!! 没找到地雷组件文档')
        return 1

    result = head + ''.join(out)
    with io.open(PREFAB, 'w', encoding='utf-8', newline='') as f:
        f.write(result)

    print('已删除: %s' % ', '.join(dropped))
    print('地雷组件 → RepeatableMine（%s）' % NEW_GUID)
    for a, n in DROP.items():
        refs = len(re.findall(r'fileID: %d}' % a, result))
        print('  残留检查 %-22s %s' % (n, 'OK' if refs == 0 else '!! 仍有 %d 处引用' % refs))
    print('  TeamOverride/ExplodeTimes/RearmInterval: %s' % (
        all(k in result for k in ('TeamOverride:', 'ExplodeTimes:', 'RearmInterval:'))))
    return 0


if __name__ == '__main__':
    sys.exit(main())
