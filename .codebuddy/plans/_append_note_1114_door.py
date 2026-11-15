# -*- coding: utf-8 -*-
"""追加 2026-11-14 日记忆：Furniture_Door 4059 空引用修复（append 模式，绝不截断）。"""
import io
import os

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'memory', '2026-11-14.md')

NOTE = """
## 4059 家具门 NRE（Furniture_Door，06Gameplay）
- 崩溃点：`Furniture_Door.Operate` 的 `front = Vector3.Angle(owner.transform.position - Forward, Forward)<90` —— `front` 是**死字段**（全仓无读者）⇒ 已连同赋值整段删除。
- `owner` 为 null 的两条路：① 4059 远端重放时 `NetFurnitureBridge.ResolveRemoteUser` 解析不到操作者（桥已有"操作者为空"告警）；② 基类 `Operate` 在 SwitchState 关回 / Disposable 用完时把 owner 清空。实测 `Door.prefab` flags=17（AutoOperate+PlayAnim）不命中② ⇒ 实际是①。
- 同文件连修 `InOperateUpdate`：原来直接 `owner.transform.position`，owner 为 null / 已销毁（操作者退出）会每帧 NRE ⇒ 改为 owner 有效时比距离，无效时退化成新增 `AnyActorWithin(3f)`（语义仍 = "门口没人就关门"，避免这一端永远卡在开态）。
- 验证：`offline_compile.py 06_Gameplay` 0 error；diff 只动这两处，CRLF/BOM 未被改写。
- ⚠ 遗留未定：房主侧 sid→盟友实例表 `_friends` 缺该 sid（或实例当时尚未生成）才导致操作者为 null；定位靠日志 `家具/操作·收 … 操作者为空` + `家具/键表`。
"""

if __name__ == '__main__':
    with io.open(P, 'a', encoding='utf-8', newline='') as f:
        f.write(NOTE)
    print('appended', P)
