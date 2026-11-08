"""列出所有含 NavMeshSurface 的 prefab/scene，并标出是否已烘焙（m_NavMeshData 是否为 0）。

用法: python scan_navmesh.py
"""
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")


def main():
    rows = []
    for dirpath, _, filenames in os.walk(ASSETS):
        for fn in filenames:
            if not fn.endswith((".prefab", ".unity")):
                continue
            p = os.path.join(dirpath, fn)
            try:
                with open(p, "r", encoding="utf-8", errors="ignore") as f:
                    t = f.read()
            except OSError:
                continue
            n = t.count("m_VoxelSize")
            if n == 0:
                continue
            baked = t.count("m_NavMeshData: {fileID: 23800000")
            rows.append((os.path.relpath(p, ROOT), n, n - baked))

    print(f"{'文件':<62} surfaces  未烘焙")
    for path, n, empty in sorted(rows):
        flag = "  ⚠" if empty else ""
        print(f"{path:<62} {n:^8} {empty:^6}{flag}")
    print(f"\n合计 {len(rows)} 个文件含 NavMeshSurface")


if __name__ == "__main__":
    main()
