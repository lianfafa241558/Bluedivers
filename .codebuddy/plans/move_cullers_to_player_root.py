# -*- coding: utf-8 -*-
r"""把 Player.prefab 上挂在 Main Camera 的 CameraCullDistance / UnitAnimatorCuller 挪到根 Player 上。

为什么：这两个组件在倒地时必须继续跑 —— 主相机是整台 SetActive(false)，挂在它上面的组件会一起停摆。
根 Player 全程 active，且 GameplayCamera 能解析出"当下出图的是哪台相机"。

只动三处：两个 GO 的 m_Component 列表 + 两个组件的 m_GameObject。字段值原样保留。
用法：python -X utf8 .codebuddy/plans/move_cullers_to_player_root.py [--check]
"""
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, 'Assets/Resources/Prefabs/BattleBase/Player.prefab')

CAM_GO = '343697537709790001'      # Main Camera（会随倒地整台关掉）
PLAYER_GO = '514622668125285558'   # Player 根（全程 active）
MOVED = ['6973093062856404911',    # CameraCullDistance
         '8753566656369767023']    # UnitAnimatorCuller


def split_docs(text):
    """按 `--- !u!<class> &<id>` 切块，保留头部与顺序（返回 [head, doc, doc, ...]）。"""
    parts = re.split(r'(?m)^(--- !u!\d+ &-?\d+\n)', text)
    out = [parts[0]]
    for i in range(1, len(parts), 2):
        out.append(parts[i] + parts[i + 1])
    return out


def find_doc(docs, anchor):
    hits = [d for d in docs if d.startswith('--- ') and ('&%s\n' % anchor) in d.split('\n', 1)[0] + '\n']
    if len(hits) != 1:
        raise SystemExit('!! anchor %s 命中 %d 个，拒绝改' % (anchor, len(hits)))
    return hits[0]


def main():
    check_only = '--check' in sys.argv
    raw = open(PATH, 'r', encoding='utf-8', newline='').read()
    if '\r\n' in raw:
        raise SystemExit('!! 该文件已变成 CRLF，先确认再改')

    docs = split_docs(raw)
    cam_doc = find_doc(docs, CAM_GO)
    player_doc = find_doc(docs, PLAYER_GO)

    cam_lines = cam_doc.split('\n')
    removed = 0
    new_cam = []
    for line in cam_lines:
        if any(line.strip() == '- component: {fileID: %s}' % fid for fid in MOVED):
            removed += 1
            continue
        new_cam.append(line)
    if removed != len(MOVED):
        raise SystemExit('!! Main Camera 上只找到 %d/%d 个待挪组件' % (removed, len(MOVED)))

    pl_lines = player_doc.split('\n')
    last_comp = max(i for i, l in enumerate(pl_lines) if l.strip().startswith('- component: {fileID:'))
    for fid in MOVED:
        if any(l.strip() == '- component: {fileID: %s}' % fid for l in pl_lines):
            raise SystemExit('!! 根 Player 上已经有 %s，拒绝重复添加' % fid)
    pl_lines[last_comp + 1:last_comp + 1] = ['  - component: {fileID: %s}' % fid for fid in MOVED]

    mono_docs = {}
    for fid in MOVED:
        d = find_doc(docs, fid)
        old = '  m_GameObject: {fileID: %s}' % CAM_GO
        if old not in d:
            raise SystemExit('!! 组件 %s 的 m_GameObject 不是 Main Camera，拒绝改' % fid)
        mono_docs[fid] = d.replace(old, '  m_GameObject: {fileID: %s}' % PLAYER_GO, 1)

    new_docs = []
    for d in docs:
        if d == cam_doc:
            new_docs.append('\n'.join(new_cam))
        elif d == player_doc:
            new_docs.append('\n'.join(pl_lines))
        elif any(d == v for v in [find_doc(docs, f) for f in MOVED]):
            # 找到它是哪个 fid 再替换
            for fid in MOVED:
                if d == find_doc(docs, fid):
                    new_docs.append(mono_docs[fid])
                    break
        else:
            new_docs.append(d)

    text = ''.join(new_docs)
    print('待挪组件 %d 个 → 根 Player；Main Camera 组件数 %d→%d，根组件数 %d→%d'
          % (len(MOVED), len(cam_lines), len(new_cam), len(pl_lines), len(pl_lines)))
    if check_only:
        print('[check] 不写盘')
        return 0

    open(PATH, 'w', encoding='utf-8', newline='').write(text)
    print('已写入', PATH)
    return 0


if __name__ == '__main__':
    sys.exit(main())
