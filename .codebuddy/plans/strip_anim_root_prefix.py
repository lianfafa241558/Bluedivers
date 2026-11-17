"""去掉 .anim 中 path 行内的 'Droid_Kurokame_AR/' 前缀。

只处理 `path:` 行，clip 名 m_Name 之类同名前缀不动。
字节级按行处理，天然保留 BOM 与原有换行符（LF/CRLF 均不动）。
用法: python strip_anim_root_prefix.py [--apply]
"""
import sys
from pathlib import Path

ANIM_DIR = Path(r"e:/Bluedivers/Assets/Art/Modle/Enemy/Robot/NewRobot/Anim")
PREFIX = b"Droid_Kurokame_AR/"
APPLY = "--apply" in sys.argv

total_files = 0
total_hits = 0
for path in sorted(ANIM_DIR.glob("*.anim")):
    raw = path.read_bytes()
    lines = raw.splitlines(keepends=True)
    hits = 0
    out = []
    for line in lines:
        if line.lstrip().startswith(b"path:") and PREFIX in line:
            hits += line.count(PREFIX)
            line = line.replace(PREFIX, b"")
        out.append(line)
    if hits == 0:
        continue
    new = b"".join(out)
    n_crlf = raw.count(b"\r\n")
    assert new.count(PREFIX) == 0
    assert new.count(b"\r\n") == n_crlf, "换行符被改动"
    assert len(new) == len(raw) - hits * len(PREFIX)
    total_files += 1
    total_hits += hits
    print(f"[{'WRITE' if APPLY else 'DRY  '}] {path.name}: {hits} 处, "
          f"eol={'CRLF' if n_crlf else 'LF'}, bom={raw.startswith(b'\xef\xbb\xbf')}, "
          f"行数={len(lines)}")
    if APPLY:
        path.write_bytes(new)

print(f"\n合计 {total_files} 个文件 / {total_hits} 处  {'已写入' if APPLY else '(dry-run)'}")
