# -*- coding: utf-8 -*-
"""追加 2026-11-14 更正：Furniture_General 的 Id 盘点结论修正（append 模式）。"""
import io
import os

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'memory', '2026-11-14.md')

NOTE = """
## 更正：Furniture_General 的 Id 盘点（2026-11-14 傍晚，MCP 实读编辑器场景）
- ⚠ 上一条"7 条死配置"**有误**：场景里放的是 **prefab 实例 + override**，脚本 guid 不出现在 .unity 里 ⇒ 我的文件扫描漏了。
- 编辑器实读 Utnapishitim（`Resources.FindObjectsOfTypeAll<Furniture_General>()` + 同物体 `IEntity`；⚠ **编辑模式下 `Furniture_Attached.Id` 是空的**，因为 Identity 在 Awake 才解析 —— 必须直接读 BaseObject/Actor）：
  - ConfigureEquipmentCabinet/Plane → SelectVehicle（激活终端）
  - tableSideB/Plane (3) → SelectAirdrop（配置战备）
  - TableSideA/Plane (3) → SelectRole（切换角色）
  - Beacon/Plane、(1)(2)(3) ×4 → SelectTask（激活终端）
  - Npc/GameObject → GuideWnd；Sceen/Console → GuideWnd 但**组件 enabled=false**
  - EngineeringCabinet/Plane → **Id 空 + canOperate=false**（desc 激活终端）
- 结论修订：**SettingWnd / MedicalBag / SignaTower / AirdropConfigWnd 场景里都没有**；其中 SignaTower、MedicalBag 用户确认为废案；AirdropConfigWnd 那条 Id 覆盖是悬空 target（源 prefab 里组件已不存在），且没有任何物体 BaseObject.Id 等于它 ⇒ 战备配置面板当前点不出来（待用户确认是不是 EngineeringCabinet 那个柜子）。
- 工具：`.codebuddy/plans/_audit_scene_id_overrides.py`（解析场景里 Id/ShowName 覆盖落在哪个脚本，支持 stripped 嵌套）；MCP execute_code 用 **codedom**（本机没装 Microsoft.CodeAnalysis，roslyn 直接不可用）。
"""

if __name__ == '__main__':
    with io.open(P, 'a', encoding='utf-8', newline='') as f:
        f.write(NOTE)
    print('appended', P)
