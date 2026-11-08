"""读 mp4 的 avcC 盒子，打印 H.264 profile/level（判断是否 baseline）。

用法: python mp4_profile.py <file.mp4>
"""
import sys

PROFILES = {
    66: "Baseline",
    77: "Main",
    88: "Extended",
    100: "High",
    110: "High 10",
    122: "High 4:2:2",
    244: "High 4:4:4",
}
LEVELS = {30: "3.0", 31: "3.1", 32: "3.2", 40: "4.0", 41: "4.1", 42: "4.2", 50: "5.0", 51: "5.1", 52: "5.2"}


def main():
    p = sys.argv[1]
    with open(p, "rb") as f:
        data = f.read()
    idx = data.find(b"avcC")
    if idx < 0:
        print("没有 avcC（可能不是 H.264/AVC）")
        return
    prof = data[idx + 5]
    level = data[idx + 7]
    print(f"文件: {p}")
    print(f"AVC profile_idc={prof} ({PROFILES.get(prof, '未知')})  level_idc={level} (≈{LEVELS.get(level, level/10)})")
    print("⇒ baseline 要求 profile=Baseline(66) 且 level ≤ 3.1", "✔ 已满足" if prof == 66 and level <= 31 else "✘ 需转码")


if __name__ == "__main__":
    main()
