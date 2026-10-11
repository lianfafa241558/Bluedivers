# -*- coding: utf-8 -*-
"""net-battle.md 再压 ~260 字符（细删冗余修饰，判据全保留）。"""
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


nb = load('.codebuddy/memory/topics/net-battle.md')

patch(nb, 'a1',
      '''- 现状 = **两端各跑一份世界** + 房主权威关键量。**不做**帧同步/lockstep（`ProjectileStandard`/`Damageable` 走 PhysX + 动画/补间 ⇒ 跨机确定性拿不到）；**不做**子弹权威（各端本地模拟 + 命中上报）。''',
      '''- 现状 = **两端各跑一份世界** + 房主权威关键量。**不做** lockstep（PhysX + 动画/补间 ⇒ 跨机确定性拿不到）；**不做**子弹权威（各端本地模拟 + 命中上报）。''')

patch(nb, 'a2',
      '''- ⚠ **"开火同步"≠"命中/血量同步"**：`FpsHelper.Hit` 是**本地**子弹碰撞结算入口（调用者全是本地模拟事件）⇒ 远端开火（4015/4016）刻意不生成子弹、不结算；玩家血量在**各自机器上本地权威**，同步只为"给别人看"（盟友 `PlayerFriend.prefab` 上刻意**没有 `Damageable`**，血量由 4017/4018 镜像进它新增的 `HealthPlayer`）。''',
      '''- ⚠ **"开火同步"≠"命中/血量同步"**：`FpsHelper.Hit` 是**本地**子弹碰撞结算入口 ⇒ 远端开火（4015/4016）刻意不生成子弹、不结算；玩家血量**各自机器本地权威**，同步只为"给别人看"（盟友 `PlayerFriend.prefab` 刻意**没有 `Damageable`**，血量由 4017/4018 镜像进其 `HealthPlayer`）。''')

patch(nb, 'a3',
      '''- ⚠ 远程弹道"穿过去没命中"已修：`SpawnVisualBullet(muzzle, direction, endPoint)` 把命中点传下去（`ProjectileBase.SetVisualEndPoint` 复位、`ProjectileStandard.ReachedVisualEndPoint` 用**本帧位移投影**判定）；`FpsHelper.PlayImpactFx(hitData)` 抽出来给表现弹复用。''',
      '''- ⚠ 远程弹道"穿过去没命中"已修：`SpawnVisualBullet(muzzle, direction, endPoint)` 把命中点传下去（`SetVisualEndPoint` 复位、`ReachedVisualEndPoint` 用**本帧位移投影**判定）；`FpsHelper.PlayImpactFx` 抽出来给表现弹复用。''')

patch(nb, 'a4',
      '''- 场景单位（NPC）：`NPCWalk` 自记在场表（`static s_all` + `FindById`），`SceneUnitMoveSink`（`[RuntimeInitializeOnLoadMethod]` 自举常驻）转发 4032。⚠ **不要扫 `ActorsManager.Actors`**——`ActorsManager.Awake` 会 `Actors = new()` 把先注册的 NPC 全丢掉（大厅实测只剩 7 项）。''',
      '''- 场景单位（NPC）：`NPCWalk` 自记在场表（`static s_all` + `FindById`），`SceneUnitMoveSink`（`[RuntimeInitializeOnLoadMethod]` 自举常驻）转发 4032。⚠ **不要扫 `ActorsManager.Actors`**（`ActorsManager.Awake` 会 `Actors = new()` 丢掉先注册的 NPC）。''')

patch(nb, 'a5',
      '''- **家具(4036)**：键 = `SyncId = FNV1a(Id + 位置0.1m)`（`Awake` 算一次缓存；`Id` 不唯一、`NumberID` 各端自增 ⇒ 都不能用）。**一处收口**：远端 `ApplyRemoteOperate(remoteUser)` 跑同一条 `Operate()` ⇒ `OnOOPartCollect`/`OnSubmitOOPart`/`OnKeiSubmit` 不需单独同步。⚠ 操作者只能传**远端单位**（`PlayerInputHandler/PlayerWeaponsManager.OnOperation` 靠 `user == gameObject` 过滤）。⚠ 重放期间 `IsRemoteReplay = true` ⇒ 非幂等副作用（管道 `ModifyHeightMap`）要跳过。''',
      '''- **家具(4036)**：键 = `SyncId = FNV1a(Id + 位置0.1m)`（`Awake` 算一次缓存；`NumberID` 各端自增 ⇒ 不能用）。**一处收口**：远端 `ApplyRemoteOperate(remoteUser)` 跑同一条 `Operate()` ⇒ `OnOOPartCollect`/`OnSubmitOOPart`/`OnKeiSubmit` 不需单独同步。⚠ 操作者只能传**远端单位**（`OnOperation` 靠 `user == gameObject` 过滤）。⚠ 重放期间 `IsRemoteReplay = true` ⇒ 非幂等副作用（管道 `ModifyHeightMap`）要跳过。''')

save(nb)
print('net-battle.md', len('\n'.join(nb['lines'])))
