# -*- coding: utf-8 -*-
"""Tank_Siege 迫击炮炮弹"半空自爆"修复（数据侧）。

实测：Speed=150 / Gravity=150 ⇒ 理论最长飞行时间 = 2v/g = 2.0s，
而 DamageData.MaxLifeTime = 2 ⇒ 生命引信和高抛弹道打平，
炮弹总在落地前 0.02s（离地约 3m）被引信点爆 ⇒ 看到"半空爆炸+爆点特效"。

修法：
  MaxLifeTime 2 -> 6（给足余量，与同族 Tank_Mortar 的 6 保持一致）
  MaxRange 显式写 300（原本 -1 时 CurrentWeaponRange = MaxLifeTime*Speed，
  改成 6 后会变成 900，AI 攻击距离会被一起放大；显式 300 = 保持现有行为）

只做字节级替换，保持 LF 换行、无 BOM。
"""
import io
import os
import sys

PREFAB = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "Assets", "Resources", "Prefabs", "Enemy", "Robot", "PMC", "Tank_Siege.prefab",
)

TARGET = "{fileID: 6312367457582549104, guid: c3837f462facaab41aa6e4b14f8e0413,"

OLD = (
    "    - target: " + TARGET + "\n"
    "        type: 3}\n"
    "      propertyPath: Damages.Array.data[0].MaxLifeTime\n"
    "      value: 2\n"
    "      objectReference: {fileID: 0}\n"
)

NEW = (
    "    - target: " + TARGET + "\n"
    "        type: 3}\n"
    "      propertyPath: Damages.Array.data[0].MaxLifeTime\n"
    "      value: 6\n"
    "      objectReference: {fileID: 0}\n"
    "    - target: " + TARGET + "\n"
    "        type: 3}\n"
    "      propertyPath: Damages.Array.data[0].MaxRange\n"
    "      value: 300\n"
    "      objectReference: {fileID: 0}\n"
)


def main():
    raw = open(PREFAB, "rb").read()
    if raw[:3] == b"\xef\xbb\xbf":
        print("!! 意外：文件带 BOM，终止")
        return 1
    text = raw.decode("utf-8")
    if "\r\n" in text:
        print("!! 意外：文件是 CRLF，终止")
        return 1
    if text.count(OLD) != 1:
        print("!! 目标块命中次数 = %d（期望 1），终止" % text.count(OLD))
        return 1
    if "Damages.Array.data[0].MaxRange" in text:
        print("!! 已存在 MaxRange 覆盖，终止")
        return 1

    new_text = text.replace(OLD, NEW, 1)
    with io.open(PREFAB, "wb") as f:
        f.write(new_text.encode("utf-8"))
    print("OK: MaxLifeTime 2 -> 6, 追加 MaxRange = 300")
    return 0


if __name__ == "__main__":
    sys.exit(main())
