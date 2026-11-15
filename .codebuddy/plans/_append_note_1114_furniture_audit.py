# -*- coding: utf-8 -*-
"""追加 2026-11-14 日记忆：全套家具（Furniture_*）4059 空引用体检结论（append 模式）。"""
import io
import os

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'memory', '2026-11-14.md')

NOTE = """
## 家具 NRE 体检（19 个 Furniture_* 子类，只读排查）
- 高危（会抛异常）：
  - `GuideController.Operate`（06Gameplay/Npc/GuideController.cs:237）`_guideInfos[_currentIndex]`，`_currentIndex` 初值/引导结束后是 **-1** ⇒ ArgumentOutOfRangeException；且不调 `base.Operate()` ⇒ 天然不同步。
  - `Furniture_Pipe.Place`（Furniture_Pipe.cs:164）`lastPipeline.lastPipeline.IsValid()` 里 `lastPipeline` 自身未判空；同函数 `root.TryGetComponent`(:144) `root` 未判空。该函数**在 4059 重放路径上**（Operate→switch Id=="Pipe"）。
  - `Furniture_Pipe.Link`(:197-198) `Instantiate(Temp,...)` + `GetComponentInChildren<Furniture_Pipe>()` 未判空 ⇒ `nextPipeline.lastPipeline` NRE；重放时两端各造一根。
  - `Furniture_Artillery.Awake`(:50) / `Furniture_PlayerArtillery.Awake`(:57) `m_weapon.OnShoot += OnShoot` 均未判空（PlayerArtillery 的 OnDestroy 反而判了）。实测两块 prefab 都有 WeaponController（Artillery.prefab 挂的是 `WeaponEnemyController : WeaponController`），属"改配置就炸"。
  - `Furniture_WeaponPickup.ShowName`(:58) `m_Weapon.WeaponName` 未判空（`Id`/`Icon` 都判了）；`FormatGroundState`(:184) `m_Weapon.GetAmmoForSync` 同样。实测 `Weapons/Support/ThrowLauncher.prefab` 上该组件**真的没有** WeaponPlayerController（模型是 ThrowLauncher.fbx，嵌套 Parabolic 也无武器脚本）⇒ 一旦有调用方读 ShowName 就 NRE（当前 CanOperate 的 m_Weapon 判空把它挡在目标外，故属潜伏）。
  - `Furniture_General` 派发表：`["Supply"]`(:149) `transform.parent.parent.GetComponent<Animator>()` 三级都不判空**且在 owner 判空之前**（重放也照跑）；`["BlackBox"]`(:163) `relatedTrans.gameObject`、`["KeyScreen"]`(:131) `relatedTrans.position` 未判空。
- 中危（不崩但静默失效/不同步）：`Furniture_AttachedGeneral` 与 `Furniture_General` 的 `Operate` 都不调 `base.Operate()`，`_Operate` 里没显式 `BaseOp()` 的条目既不置 inOperate 也不发 4059；`Furniture_AttachedGeneral` 的 furnData **整块被注释** ⇒ 该组件等同于死组件（只打一条 warning）。
- 已核实安全：Equip/KeiSubmit/OOPart/Artillery.Operate/ReturnBag/PlayerDown/Supplies/HandEquip/GrenadePickup/Pipe.InOperateUpdate 都有 owner 判空；`AudioManagerBase.PlaySound` 对 null clip 有兜底（只报一次错）；`PlayMeetSpeech`/`OperationWnd.RefreshDisplay` 对 null user 早退；`MapMarkRegistry` 对 null Identity 有兜底；`Furniture_Equip.ContGo` 在 6 个 prefab 上都是空数组（`foreach` 空安全）。
- 工具：`.codebuddy/plans/_audit_furniture_prefabs2.py`（按 YAML 块读家具组件字段）、`_prefab_scripts.py`（列 prefab 实际挂的脚本，判断"代码依赖的组件是否真挂"）。
"""

if __name__ == '__main__':
    with io.open(P, 'a', encoding='utf-8', newline='') as f:
        f.write(NOTE)
    print('appended', P)
