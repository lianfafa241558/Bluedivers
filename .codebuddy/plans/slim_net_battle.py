# -*- coding: utf-8 -*-
"""收尾微调：net-battle.md 压回预算内 + MEMORY.md 路由表补 gameplay-world。"""
import io
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def load(rel):
    p = os.path.join(ROOT, *rel.split('/'))
    raw = io.open(p, 'rb').read()
    bom = raw[:3] == b'\xef\xbb\xbf'
    if bom:
        raw = raw[3:]
    crlf = b'\r\n' in raw
    return {'p': p, 'bom': bom, 'crlf': crlf,
            'lines': raw.decode('utf-8').replace('\r\n', '\n').split('\n')}


def save(f):
    text = '\n'.join(f['lines'])
    if f['crlf']:
        text = text.replace('\n', '\r\n')
    raw = text.encode('utf-8')
    if f['bom']:
        raw = b'\xef\xbb\xbf' + raw
    io.open(f['p'], 'wb').write(raw)


def patch(f, key, anchor, new):
    a = anchor.split('\n')
    n = new.split('\n')
    hits = [i for i in range(len(f['lines']) - len(a) + 1) if f['lines'][i:i + len(a)] == a]
    assert len(hits) == 1, '[锚点失配] %s key=%s 命中=%d' % (f['p'], key, len(hits))
    i = hits[0]
    f['lines'][i:i + len(a)] = n


# ---------------- net-battle.md ----------------
nb = load('.codebuddy/memory/topics/net-battle.md')

patch(nb, 'nb.flows',
      '''- 已派生的流：`Terrain`/`TerrainDecor`（地形）、`MissionEntity`（任务点/朝向 `MissionBase.MissionAngle()`）、`Wave`（波次构成）、`Ai`（`06Gameplay/AI/EnemyRandom.cs` 按 `NetId`，`WaveManager.Awake` 里 `Clear()`）、`Weapon`（武器/战备散布，`WeaponBaseController.RandomSeed`）、`UnitPlacement`（巡逻队构成，按落点）、`Weather`（天气，2026-10-10）。''',
      '''- 已派生流：`Terrain`/`TerrainDecor`（地形）、`MissionEntity`（任务点/朝向）、`Wave`（波次构成）、`Ai`（`06Gameplay/AI/EnemyRandom.cs` 按 `NetId`，`WaveManager.Awake` 里 `Clear()`）、`Weapon`（武器/战备散布）、`UnitPlacement`（巡逻队构成）、`Weather`（天气）。''')

patch(nb, 'nb.todo',
      '''## 7. 仍未做 / 已知缺口
- 敌人**移动纠偏**（会漂）与优先级轮询；`DieLoot`/`NPCWalk`/`MissionOilRefining`/`UnitSkill_Summoner` 仍有非派生随机点；成员端血量非权威（只保证"死亡一致"）。
- 家具 `ExtFloatParameter`（Direction 阶段连续值）**有意不同步**（判定成败由纯函数目标角 + 阶段同步覆盖）。''',
      '''## 7. 仍未做
- 敌人**移动纠偏**（会漂）与优先级轮询；`DieLoot`/`NPCWalk`/`MissionOilRefining`/`UnitSkill_Summoner` 仍有非派生随机；成员端血量非权威（只保证"死亡一致"）。
- 家具 `ExtFloatParameter`（Direction 连续值）**有意不同步**（成败由纯函数目标角 + 阶段同步覆盖）。''')

save(nb)

# ---------------- MEMORY.md 路由表 ----------------
mm = load('.codebuddy/memory/MEMORY.md')

patch(mm, 'mm.route',
      '''| 战斗 / AI / 伤害 / 波次 / 寻路 / 相机 / 武器 / 池化 VFX / 家具 / 任务 / 事件 / 时序 / 地形 | `topics/gameplay.md` |''',
      '''| 战斗 / AI / 伤害 / 波次 / 寻路 / 相机 / 武器 / 池化 VFX / 单位存活 | `topics/gameplay.md` |
| 家具 / 手持交互 / 任务系统 / 逻辑帧时序 / 存档设置 / 地形渲染 | `topics/gameplay-world.md` |''')

save(mm)

print('ok')
for f in (nb, mm):
    print(os.path.basename(f['p']), len('\n'.join(f['lines'])))
