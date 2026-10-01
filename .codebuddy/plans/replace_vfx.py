# -*- coding: utf-8 -*-
"""把 `ServiceLocator.Vfx` 消费点机械替换为 `FPSGame.Core.VfxPool`。

按评审的 3 条注意实现：
  1) 覆盖**两种写法**：全限定 `FPSGame.GameContract.ServiceLocator.Vfx` 与短名 `ServiceLocator.Vfx`
     （`FpsHelper_Hit.cs:320/326/339` 用的是短名）。
  2) `ServiceLocator.Vfx?.` → `FPSGame.Core.VfxPool.`（静态类不能用 `?.`，去掉死判空）。
  3) 只匹配 `.Vfx` 后缀，**不碰**同文件里的 `.Battle/.Flow/.Res`。

跳过：契约层（00GameContract/，槽位本身要删）与实现处（VFXManager.cs，已手工改）。
逐文件保留原有 BOM。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCAN = os.path.join(ROOT, "Assets", "Scripts")
SKIP_DIRS = ("00GameContract",)
SKIP_FILES = ("VFXManager.cs",)
ROOTS_SKIP = ("00Core",)   # 新增的 VfxPool.cs 自身不动

PAT = re.compile(r"(?:FPSGame\.GameContract\.)?ServiceLocator\.Vfx\??")
# ⚠ 第一版写成 "FPSGame.Core.VfxPool."（带尾点）⇒ 与残留的 ".Creat" 拼成 "..Creat"。
#   正确写法**不带尾点**：`ServiceLocator.Vfx.Creat` 与 `ServiceLocator.Vfx?.Creat` 两种情况
#   都会自然残留那个 "."（`?` 已被 `\??` 吃掉）⇒ 结果都是 `VfxPool.Creat`。
REPL = "FPSGame.Core.VfxPool"
# 幂等修复：把上一版产出的双点纠回来
FIX_DOT = re.compile(r"FPSGame\.Core\.VfxPool\.\.")


def main():
    only_list = len(sys.argv) > 1
    total_files = 0
    total_hits = 0
    for dirpath, _, fns in os.walk(SCAN):
        rel_dir = dirpath.replace("\\", "/")
        if any(d in rel_dir for d in SKIP_DIRS):
            continue
        for fn in sorted(fns):
            if not fn.endswith(".cs") or fn in SKIP_FILES:
                continue
            full = os.path.join(dirpath, fn)
            raw = open(full, "rb").read()
            bom = raw.startswith(b"\xef\xbb\xbf")
            txt = raw.decode("utf-8-sig")
            txt, nfix = FIX_DOT.subn("FPSGame.Core.VfxPool.", txt)
            new, n = PAT.subn(REPL, txt)
            n += nfix
            if n == 0:
                continue
            rel = os.path.relpath(full, ROOT)
            if only_list:
                print("  %-70s %d" % (rel, n))
                continue
            out = new.encode("utf-8")
            if bom:
                out = b"\xef\xbb\xbf" + out
            open(full, "wb").write(out)
            total_files += 1
            total_hits += n
            print("  %-70s %d  (BOM=%s)" % (rel, n, bom))
    if not only_list:
        print("---")
        print("files=%d  hits=%d" % (total_files, total_hits))


if __name__ == "__main__":
    main()
