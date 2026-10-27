# -*- coding: utf-8 -*-
"""把 RoomMsg.cs 里混进去的"局内表现 / 局内世界状态"搬出去。

约定（用户 2026-10-27 拍板）：**不新建 TaskMsg**，开局配置/阶段闸门留在 RoomMsg。
    RoomMsg  : 房间 / 名单 / 舰桥准备 / 玩家配置 / 开局配置与阶段闸门
    BattleMsg: 位姿 + 局内**表现**（切枪·开火·生命·救援·弹药）——只同步"看得见的"
    SyncMsg  : 局内**世界状态**（战备呼叫·载具开火·波次·单位移动·伤害·护盾·场景 actor）

用法： python split_msg_20261027.py [--apply]
不带 --apply 只打印计划与校验结果，不写盘。
"""
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
MSG = os.path.join(ROOT, 'Assets', 'Scripts', '07NetGame', 'Msg')

ROOM = os.path.join(MSG, 'RoomMsg.cs')
BATTLE = os.path.join(MSG, 'BattleMsg.cs')
SYNC = os.path.join(MSG, 'SyncMsg.cs')

# 搬去 BattleMsg（局内表现）
TO_BATTLE = ['PlayerWeaponSwitch', 'PlayerShoot', 'PlayerVital', 'PlayerReviveMsg', 'PlayerAmmo']
# 搬去 SyncMsg（局内世界状态）
TO_SYNC = ['AirdropCallMsg', 'VehicleShootMsg', 'SceneActorEntry', 'SceneActorSync',
           'WaveStartMsg', 'EnemyMoveMsg', 'SceneUnitMoveMsg', 'DamageMsg', 'ShieldDamageMsg']

TYPE_RE = re.compile(r'^(?:public|internal)\s+(?:sealed\s+)?(?:class|struct|enum)\s+(\w+)')

ROOM_HEADER = """// ============================================================
// 房间 / 舰桥准备 / 开局配置（房主权威 · 局域网联机）
// 通信模型：成员 --消息--> 房主，房主决定并转发/广播。
// 判定口径：**房间期与舰桥**的消息放这里；局内"只同步看得见的"→ BattleMsg，
//           局内"房主权威的玩法状态"→ SyncMsg。
// 新增消息时在这里加 [MessagePackObject] 类，并在 CmdId 加常量。
// ============================================================"""

BATTLE_HEADER = """    // ============================================================
    // 局内**表现**同步（切枪 / 开火 / 生命 / 救援 / 弹药）
    // 只同步"看得见的东西"：缺了它们玩法照跑，所以不进 SyncMsg 的状态流。
    // ============================================================"""

SYNC_HEADER = """    // ============================================================
    // 局内**玩法状态**（战备呼叫 / 载具开火 / 波次 / 单位移动 / 伤害 / 护盾 / 场景 actor）
    // 房主权威 ⇒ 缺了它们两端会分叉。
    // ============================================================"""


def read_text(p):
    raw = open(p, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    crlf = '\r\n' in text
    return text.replace('\r\n', '\n').split('\n'), bom, crlf


def write_text(p, lines, bom, crlf):
    text = '\n'.join(lines)
    if crlf:
        text = text.replace('\n', '\r\n')
    raw = text.encode('utf-8')
    if bom:
        raw = b'\xef\xbb\xbf' + raw
    with open(p, 'wb') as f:
        f.write(raw)


def split_blocks(lines):
    """按"类型声明 + 它头顶的注释/attribute"切块，返回 [(start, end, name)]（end 含）。"""
    marks = []
    for i, line in enumerate(lines):
        m = TYPE_RE.match(line.strip())
        if m:
            s = i
            while s - 1 >= 0:
                ps = lines[s - 1].strip()
                if ps.startswith('///') or ps.startswith('[') or ps.startswith('//'):
                    s -= 1
                else:
                    break
            marks.append([s, i, m.group(1)])

    blocks = []
    for k, (s, _decl, name) in enumerate(marks):
        e = (marks[k + 1][0] - 1) if k + 1 < len(marks) else None
        if e is None:
            # 最后一块：跳过命名空间收尾 `}`（只跳一个），块内保留类型自己的 `}`
            e = len(lines) - 1
            while e >= 0 and lines[e].strip() == '':
                e -= 1
            if e >= 0 and lines[e].strip() == '}':
                e -= 1
            else:
                raise SystemExit('文件末尾没有命名空间收尾 `}`：%r' % lines[e] if e >= 0 else '')
        while e > s and lines[e].strip() == '':
            e -= 1
        blocks.append((s, e, name))
    return blocks


def indented(block, pad='    '):
    out = []
    for line in block:
        out.append(pad + line if line.strip() else line)
    return out


def main():
    apply = '--apply' in sys.argv

    lines, bom, crlf = read_text(ROOM)
    blocks = split_blocks(lines)
    names = [b[2] for b in blocks]
    print('[RoomMsg] types = %d' % len(names))
    print('  ' + ' '.join(names))

    expect = set(TO_BATTLE) | set(TO_SYNC)
    missing = [n for n in expect if n not in names]
    if missing:
        print('!! 找不到这些类型：%s' % missing)
        return 1

    head = lines[:4]          # using + 空行 + namespace + {   （idx 4 起是文件头注释）
    assert lines[2].strip().startswith('namespace'), lines[2]
    assert lines[3].strip() == '{', lines[3]

    kept, battle, sync = [], [], []
    for s, e, name in blocks:
        block = lines[s:e + 1]
        (battle if name in TO_BATTLE else sync if name in TO_SYNC else kept).append((name, block))

    print('保留 %d / 去 Battle %d / 去 Sync %d' % (len(kept), len(battle), len(sync)))

    # ---------- 新 RoomMsg ----------
    new_room = list(head)
    new_room.append('')
    new_room.extend(ROOM_HEADER.split('\n'))
    for _name, block in kept:
        new_room.append('')
        new_room.extend(block)
    new_room.append('}')
    new_room.append('')

    # ---------- 追加到 Battle / Sync ----------
    def append_to(path, header, groups):
        tl, tbom, tcrlf = read_text(path)
        end = len(tl) - 1
        while end >= 0 and tl[end].strip() == '':
            end -= 1
        assert tl[end].strip() == '}', '末尾不是命名空间收尾：%s' % path
        body = tl[:end]
        while body and body[-1].strip() == '':
            body.pop()
        body.append('')
        body.extend(header.split('\n'))
        for _name, block in groups:
            body.append('')
            body.extend(indented(block))
        body.append('}')
        body.append('')
        return body, tbom, tcrlf

    new_battle, bbom, bcrlf = append_to(BATTLE, BATTLE_HEADER, battle)
    new_sync, sbom, scrlf = append_to(SYNC, SYNC_HEADER, sync)

    # ---------- 校验：类型不丢不重 ----------
    def type_names(ls):
        return [TYPE_RE.match(l.strip()).group(1) for l in ls if TYPE_RE.match(l.strip())]

    all_new = type_names(new_room) + type_names(new_battle) + type_names(new_sync)
    print('[after] total types in 3 files = %d' % len(all_new))
    missing2 = sorted(set(names) - set(all_new))
    if missing2:
        print('!! 原 RoomMsg 的类型丢了：%s' % missing2)
        return 1
    if len(all_new) != len(set(all_new)):
        from collections import Counter as C2
        dup = [k for k, v in C2(all_new).items() if v > 1]
        print('!! 类型重复：%s' % dup)
        return 1

    # 内容不丢：按"非空行去缩进"做多重集比对
    from collections import Counter
    old_c = Counter(l.strip() for l in lines[10:] if l.strip())   # 前 10 行是旧文件头，已由 ROOM_HEADER 取代
    new_c = Counter(l.strip() for l in new_room + new_battle + new_sync if l.strip())
    lost = old_c - new_c
    added = new_c - old_c
    if lost:
        print('!! 丢了这些行：')
        for k, v in list(lost.items())[:15]:
            print('   %dx %s' % (v, k[:110]))
        return 1
    print('新增行（抬头注释等）：%d 种' % len(added))
    for k in list(added)[:8]:
        print('   + %s' % k[:110])

    print('RoomMsg 行数 %d → %d' % (len(lines), len(new_room)))
    print('BattleMsg 行数 → %d（+%d）' % (len(new_battle), len(new_battle) - len(read_text(BATTLE)[0])))
    print('SyncMsg 行数 → %d（+%d）' % (len(new_sync), len(new_sync) - len(read_text(SYNC)[0])))

    if not apply:
        print('\n[dry-run] 未写盘；加 --apply 执行')
        return 0

    write_text(ROOM, new_room, bom, crlf)
    write_text(BATTLE, new_battle, bbom, bcrlf)
    write_text(SYNC, new_sync, sbom, scrlf)
    print('\n已写入 3 个文件')
    return 0


if __name__ == '__main__':
    sys.exit(main())
