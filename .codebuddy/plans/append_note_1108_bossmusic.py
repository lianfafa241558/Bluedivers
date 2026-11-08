# -*- coding: utf-8 -*-
import io

TEXT = """
### 二轮：#6 Boss 死亡音乐还是没还回去 → 根因是**解锁顺序**

- `AudioSvc.PlayMusic(MusicGroup type, float volme)`(`03Audio/AudioSvc.cs:108`) 第一句就是 `if (musicLock) return;`
  ⇒ 原来（`Hide()` 与上一版 `End()`）都是 **先 `PlayMusic(Game)` 后 `SetLockMusic(false)`**，
  而此刻锁还在 ⇒ **切歌那行是纯空操作**，Boss 音乐原样压着。这解释了"改了也没变回去"。
- 修：`HpItemBoss` 抽出 `RestoreGameMusic()`：**先 `SetLockMusic(false)` 再 `PlayMusic(Game, 0.5f)`**，
  `Hide()` 与 `End()` 都走它（`End()` 仍以 `_visible` 为门：血条早就 Hide 过的话不许重放战斗 BGM）。
- 经验：凡"锁 + 切"成对出现的地方（Boss 条 / 撤离 / 转场），**解锁必须在切歌之前**；`musicLock` 是纯 bool，没有引用计数。

### `WaveManager.TakeTierUnit` 降级改为**逐级下降**

- 原来 `TierItemWeight.FirstOrDefault()` 兜底：字典顺序不定 ⇒ 要"小型"的刷怪器可能刷出巨型，排查时看不懂构成。
- 现在抽出 `ResolveTierWeightList(tier)`：命中即用；否则 `(int)tier-1 → … → Small(1)` 逐级往下找第一个非空的 tier
  （`None(0)` 不参与）；还是没有 ⇒ `LogError` + 返回 null ⇒ `CreatUnit/CreatSpawnerUnit` 本次不创建。
- ⚠ **只降不升**：越级往上是凭空加强度。另外降级路径**不消耗额外随机数**，两端派生流仍对齐。
- `Init` 建表时已 `Where(uw.unit != null)` 且空列表不入表 ⇒ "表里有这个 tier" == "至少一把可用 prefab"。
"""

p = 'd:/Pro/Bluedivers/.codebuddy/memory/2026-11-08.md'
with io.open(p, 'a', encoding='utf-8', newline='\n') as f:
    f.write(TEXT)
print('ok')
