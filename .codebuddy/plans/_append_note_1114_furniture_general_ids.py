# -*- coding: utf-8 -*-
"""追加 2026-11-14 日记忆：Furniture_General 派发表 Id 实测清单（append 模式）。"""
import io
import os

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'memory', '2026-11-14.md')

NOTE = """
## Furniture_General 派发表 × 工程实际 Id（全 prefab+scene 扫描）
- 关键：运行时 `Id` = **Identity.Id（BaseObject）**，prefab 里 Furniture_General 块内的 `Id:` 是旧字段残留、代码不读。
- 表里 14 条，工程里真有物体用的 7 条：GuideWnd（Utnapishitim 场景 2 个，其一 m_Enabled=0）、KeyScreen（KeyPlatform_Millennium / KeyScreen / BlackBox 根）、BlackBox（BlackBox 子物体）、Supply（VFX_SupplyAirdrop 内 4 个）、SignaTower、PipeTarget（MissionPoint_SubDriller）、Caisson。
- **死配置 7 条**（全工程无任何物体该 Id）：SelectRole / SelectTask / SelectVehicle / SelectAirdrop / SettingWnd / AirdropConfigWnd / MedicalBag。无 `AddComponent<Furniture_General>` 运行时代码，故可确认。
- 没配好：
  - `SignaTower` 只有 `_InOperateUpdate`、**无 `_Operate`** ⇒ `Operate()` 只 invoke 空委托 ⇒ 连 base.Operate 都不跑（无 inOperate / 无事件 / 无 4059），旋转所依赖的 `inOperate==true` 永不成立。
  - `Mission_LidarStation` / `Mission_JammerTower` / `UploadData` / `MissionBroadcastEntity` 都是 **SignaTower.prefab(guid 9467f67…) 的实例且未覆盖 BaseObject.Id** ⇒ 运行时 Id 全是 `SignaTower`，点的就是上面那条空分支。
  - `Supply`：`BaseOp()` 在 `owner != null && TryGetComponent<PlayerController>` 的 if 内 ⇒ 4059 重放（owner=null/盟友镜像）不上报。
  - `Plane.prefab`（被 Beacon.prefab 与 Utnapishitim 引用，canOperate=1、desc=激活终端）BaseObject 的 Id/ShowName 均为空 ⇒ 查表失败 ⇒ 点了什么都不发生。
- 工具：`.codebuddy/plans/_audit_furniture_general_ids.py`（扫 prefab+scene，按 m_GameObject/父子关系解析 Furniture_General 真正生效的 Id）。
"""

if __name__ == '__main__':
    with io.open(P, 'a', encoding='utf-8', newline='') as f:
        f.write(NOTE)
    print('appended', P)
