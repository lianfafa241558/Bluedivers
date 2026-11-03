# -*- coding: utf-8 -*-
"""把 Vfx_AboHacker.prefab 里唯一那个 VelocityModule 的 orbitalX / orbitalZ
的 minMaxState 从 0(Constant) 改成 3(Two Curves)，与 orbitalY 对齐。

按行号精确改动，不整体重写（保留 BOM / 行尾），避免抹掉其它序列化字段。
"""
import io

P = r"d:\Pro\Bluedivers\Assets\Resources\VFX\AboState\Vfx_AboHacker.prefab"

with open(P, "rb") as f:
    raw = f.read()

bom = raw.startswith(b"\xef\xbb\xbf")
text = raw.decode("utf-8-sig" if bom else "utf-8")
lines = text.splitlines(keepends=True)

# 定位 VelocityModule（该 prefab 只有 1 个）
mod = None
for i, ln in enumerate(lines):
    if ln.strip() == "VelocityModule:":
        mod = i
        break
assert mod is not None, "VelocityModule not found"

# 模块结束：下一个缩进 <= 模块缩进的非空行
mod_indent = len(lines[mod]) - len(lines[mod].lstrip())
end = len(lines)
for i in range(mod + 1, len(lines)):
    s = lines[i]
    if s.strip() and (len(s) - len(s.lstrip())) <= mod_indent:
        end = i
        break

changed = []
for i in range(mod, end):
    key = lines[i].strip().rstrip(":")
    if key in ("orbitalX", "orbitalZ"):
        # 找该 key 下面第一个 minMaxState
        for j in range(i + 1, end):
            if "minMaxState:" in lines[j]:
                old = lines[j]
                indent = old[: len(old) - len(old.lstrip())]
                if old.strip() == "minMaxState: 0":
                    lines[j] = indent + "minMaxState: 3\n"
                    changed.append((key, j + 1))
                break

assert len(changed) == 2, "expected 2 changes, got %r" % (changed,)

out = "".join(lines)
with io.open(P, "w", encoding="utf-8-sig" if bom else "utf-8", newline="") as f:
    f.write(out)

print("bom=%s changed=%r" % (bom, changed))
