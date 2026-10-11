# -*- coding: utf-8 -*-
"""
batch1 补丁 b：PatrolContriller 的报告补丁（原主脚本里的整段锚点含空行、整段匹配不稳 ⇒ 挪到这里，
改用"不含空行的短锚点"分两处插入）。

同时把主脚本 net_sync_b1.py 里的 PC 段禁用（`if not F(PC).has(...)` → `if False:`），避免重复执行报锚点失配。

用法：python .codebuddy/plans/net_sync_b1b.py [--apply]
"""
import io
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def P(*parts):
    return os.path.join(ROOT, *parts)


class File:
    def __init__(self, rel):
        self.rel = rel
        self.path = P(*rel.split('/'))
        raw = io.open(self.path, 'rb').read()
        self.bom = raw[:3] == b'\xef\xbb\xbf'
        if self.bom:
            raw = raw[3:]
        self.crlf = b'\r\n' in raw
        self.lines = raw.decode('utf-8').replace('\r\n', '\n').split('\n')
        self.orig = '\n'.join(self.lines)
        self.dirty = False

    def has(self, key):
        return key in self.orig

    def patch(self, key, anchor, new, count=1):
        a = anchor.split('\n')
        n = new.split('\n')
        hits = [i for i in range(len(self.lines) - len(a) + 1)
                if self.lines[i:i + len(a)] == a]
        assert len(hits) == count, \
            '[锚点失配] %s key=%s 命中=%d 期望=%d' % (self.rel, key, len(hits), count)
        i = hits[0]
        self.lines[i:i + len(a)] = n
        self.dirty = True

    def write(self):
        if not self.dirty:
            return False
        text = '\n'.join(self.lines)
        if self.crlf:
            text = text.replace('\n', '\r\n')
        raw = text.encode('utf-8')
        if self.bom:
            raw = b'\xef\xbb\xbf' + raw
        io.open(self.path, 'wb').write(raw)
        return True


FILES = {}


def F(rel):
    if rel not in FILES:
        FILES[rel] = File(rel)
    return FILES[rel]


# ---------------------------------------------------------------------
# 1) 主脚本：禁用 PC 段（避免它再报锚点失配）
# ---------------------------------------------------------------------
MAIN = '.codebuddy/plans/net_sync_b1.py'
if not F(MAIN).has('# PC 段移到 net_sync_b1b.py'):
    F(MAIN).patch(
        'main.skip.pc',
        """if not F(PC).has('NetPatrolBridge.Report'):""",
        """if False:  # PC 段移到 net_sync_b1b.py（原锚点含空行、整段匹配不稳）""")

# ---------------------------------------------------------------------
# 2) PatrolContriller：收集逐单位目标点后交给桥
# ---------------------------------------------------------------------
PC = 'Assets/Scripts/09Manager/Battle/PatrolContriller.cs'
if not F(PC).has('NetPatrolBridge.Report'):
    f = F(PC)

    f.patch(
        'pc.collect',
        """            foreach (var item in list)
            {""",
        """            // ★ 联机（2026-10-11）：逐单位目标点收集起来交给桥 —— 房主广播后成员端才能复刻出同一支（4047）。
            //   成员端这一步不会走到：CreatPatrol 有成员闸门 ⇒ list 为空 ⇒ 上面的早退已 return。
            var targets = new List<Vector3>(list.Count);

            foreach (var item in list)
            {""")

    f.patch(
        'pc.report',
        """                controller.PatrolPos = UnityEngine.AI.NavMesh.SamplePosition(
                    point, out var hit, PATROL_TARGET_UNIT_SAMPLE_RADIUS, UnityEngine.AI.NavMesh.AllAreas)
                    ? hit.position
                    : baseTarget;
            }
        }""",
        """                Vector3 unitTarget = UnityEngine.AI.NavMesh.SamplePosition(
                    point, out var hit, PATROL_TARGET_UNIT_SAMPLE_RADIUS, UnityEngine.AI.NavMesh.AllAreas)
                    ? hit.position
                    : baseTarget;

                controller.PatrolPos = unitTarget;
                targets.Add(unitTarget);
            }

            NetPatrolBridge.Report(spawnPos, targets, PatrolTargetKind.PatrolPos);
        }""")


def main():
    apply = '--apply' in sys.argv

    changed = [f.rel for f in FILES.values() if f.dirty]
    print('=== 待写入：%d ===' % len(changed))
    for rel in changed:
        print('  M ' + rel)

    if not changed:
        print('没有需要改的东西（已打过补丁）。')
        return

    if not apply:
        print('\n[dry run] 校验全部通过。加 --apply 落盘。')
        return

    n = 0
    for f in FILES.values():
        if f.write():
            n += 1
    print('\n已落盘：改 %d 个文件。' % n)


if __name__ == '__main__':
    main()
