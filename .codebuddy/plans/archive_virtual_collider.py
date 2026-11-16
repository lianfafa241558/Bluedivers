# -*- coding: utf-8 -*-
"""把 VirtualCollider 相关文件「整份注释掉」（保留代码当参考，不再参与编译）。

用法：python archive_virtual_collider.py          # 加注释
      python archive_virtual_collider.py --restore # 撤销注释，恢复编译
"""
import os
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

FILES = [
    'Assets/Scripts/05UnitCore/VirtualCollision/VirtualCollider.cs',
    'Assets/Scripts/05UnitCore/VirtualCollision/VirtualColliderMath.cs',
    'Assets/Scripts/05UnitCore/VirtualCollision/VirtualUnitCollider.cs',
    'Assets/Editor/VirtualColliderBenchWindow.cs',
]

HEADER = """// ============================================================================
// 【已归档 · 2026-11-16】虚拟碰撞体（VirtualCollider）Step 1 原型 —— 整份注释掉，不参与编译
//
// 结论：不采用。Play 实测（299 Actor / 863 部位碰撞体）Physics.SyncColliderTransform
//       ≈ 0.000~0.013 ms/帧，不是瓶颈；而数学求交单条查询比物理 SphereCast 慢 2~30 倍
//       （UnitQuery 粗筛 6.8~22 µs vs 物理 0.73 µs）⇒ 替代后是净亏。
// 恢复：删掉本段与文件末尾的 */ 即可（细节见 .codebuddy/memory/topics/virtual-collider.md）
// ============================================================================
/*
"""

RESTORE_TAIL = '*/'


def main():
    restore = '--restore' in sys.argv
    for rel in FILES:
        p = os.path.join(ROOT, rel)
        raw = open(p, 'rb').read()
        bom = b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
        text = raw[len(bom):].decode('utf-8')
        crlf = '\r\n' in text
        nl = '\r\n' if crlf else '\n'

        if restore:
            if not text.startswith(HEADER.split(nl)[0]):
                print('skip(未注释)', rel)
                continue
            # 砍掉头部（到第一个 "/*" 之后）和末尾的 "*/"
            idx = text.index('/*' + nl)
            body = text[idx + 3 + len(nl):]
            end = body.rstrip().rfind(RESTORE_TAIL)
            body = body[:end].rstrip() + nl if end >= 0 else body
            out = body
        else:
            if text.startswith(HEADER.split(nl)[0]):
                print('skip(已注释)', rel)
                continue
            hdr = HEADER if not crlf else HEADER.replace('\n', '\r\n')
            out = hdr + text.rstrip() + nl + RESTORE_TAIL + nl

        open(p, 'wb').write(bom + out.encode('utf-8'))
        print(('restored ' if restore else 'commented ') + rel + ('  [CRLF]' if crlf else '  [LF]'))


if __name__ == '__main__':
    main()
