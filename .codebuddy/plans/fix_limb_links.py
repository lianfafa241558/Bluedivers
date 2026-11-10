# -*- coding: utf-8 -*-
"""字节级修补两处 prefab 的 Damageable.LineArmor（避免 replace_in_file 整份改换行符）：
1) Dec_Ravager：bone_M_R_leg_1 的 LineArmor 误挂成左腿末端(8477769561650769273)，改成右腿末端 1795249228165504040
2) Dec_Spire：bone_root 的 LineArmor 补上 CoreArmor 2647102820983967314
"""
import io

EDITS = [
    (
        r"d:\Pro\Bluedivers\Assets\Resources\Prefabs\Enemy\Zerg\DEC\Dec_Ravager.prefab",
        b"  LineArmor:\n  - {fileID: 8477769561650769273}\n  - {fileID: 2073300126036910494}\n",
        b"  LineArmor:\n  - {fileID: 1795249228165504040}\n  - {fileID: 2073300126036910494}\n",
    ),
    (
        r"d:\Pro\Bluedivers\Assets\Resources\Prefabs\Enemy\Zerg\DEC\Dec_Spire.prefab",
        b"  LineArmor:\n  - {fileID: 2647102821158558576}\n",
        b"  LineArmor:\n  - {fileID: 2647102821158558576}\n  - {fileID: 2647102820983967314}\n",
    ),
]


def main():
    for path, old, new in EDITS:
        with open(path, "rb") as f:
            data = f.read()
        n = data.count(old)
        print(path.split("\\")[-1], "命中次数", n)
        if n != 1:
            print("  !! 命中数不是 1，跳过（需人工确认）")
            continue
        with open(path, "wb") as f:
            f.write(data.replace(old, new))
        print("  已写入")


if __name__ == "__main__":
    main()
