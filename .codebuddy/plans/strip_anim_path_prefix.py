"""去掉 .anim 中 path 行内的指定路径前缀（按目录批量）。

用法: python strip_anim_path_prefix.py <anim目录> <前缀> [--apply]
只处理 `path:` 行，clip 名 m_Name 之类同名前缀不动。
字节级读写，保留 BOM 与原有换行符（LF/CRLF 均不动），并断言行数/换行不变。
"""
import sys
from pathlib import Path

if len(sys.argv) < 3:
    sys.exit("usage: strip_anim_path_prefix.py <dir> <prefix> [--apply]")

ANIM_DIR = Path(sys.argv[1])
PREFIX = sys.argv[2].encode("utf-8")
APPLY = "--apply" in sys.argv

if not PREFIX.endswith(b"/"):
    sys.exit("前缀需以 / 结尾")
if not ANIM_DIR.is_dir():
    sys.exit(f"目录不存在: {ANIM_DIR}")

total_files = total_hits = 0
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
    assert len(new) == len(raw) - hits * len(PREFIX), "长度不符"
    total_files += 1
    total_hits += hits
    print(f"[{'WRITE' if APPLY else 'DRY  '}] {path.name}: {hits} 处, "
          f"eol={'CRLF' if n_crlf else 'LF'}, bom={raw.startswith(b'\xef\xbb\xbf')}, "
          f"行数={len(lines)}")
    if APPLY:
        path.write_bytes(new)

print(f"\n合计 {total_files} 个文件 / {total_hits} 处  {'已写入' if APPLY else '(dry-run)'}")
