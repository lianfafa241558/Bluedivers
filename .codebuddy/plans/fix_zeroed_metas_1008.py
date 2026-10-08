# -*- coding: utf-8 -*-
"""修复被写成「等长全 NUL」的 .cs.meta（2026-10-08）。

现象：TimerHost.cs.meta / FlowState.cs.meta 长度 243B（与正常 meta 一致）但内容全是 \\x00。
后果：Unity 资产库拿不到 guid（assetType=Unknown）⇒ 该 .cs 不进编译 ⇒ GameRootBase.cs
     报 CS0103「TimerHost 不存在」。

处理：按同目录/同程序集正常 meta 的**逐字节格式**（LF、无 BOM、243B）重写；
      两个类都是 public static class（非 MonoBehaviour/SO）⇒ 无 prefab/scene 引用旧 GUID，可安全换新。
      GUID 已预先全项目扫描确认无冲突。

断言：仅当目标文件当前确实是「全 NUL」时才写；写后校验字节数 = 243 且能解析出 guid。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

TARGETS = {
    "Assets/Scripts/00Core/Timer/TimerHost.cs.meta": "920bd7143e9e47e9954b7f7b2c2f8299",
    "Assets/Scripts/04Data/FlowState.cs.meta": "d3e563a5a84c46b6a433e01af7b196dd",
}

TEMPLATE = (
    "fileFormatVersion: 2\n"
    "guid: {guid}\n"
    "MonoImporter:\n"
    "  externalObjects: {{}}\n"
    "  serializedVersion: 2\n"
    "  defaultReferences: []\n"
    "  executionOrder: 0\n"
    "  icon: {{instanceID: 0}}\n"
    "  userData: \n"
    "  assetBundleName: \n"
    "  assetBundleVariant: \n"
)


def collect_existing_guids():
    guids = set()
    for dirpath, _, fns in os.walk(os.path.join(ROOT, "Assets")):
        for fn in fns:
            if not fn.endswith(".meta"):
                continue
            try:
                txt = open(os.path.join(dirpath, fn), "r", encoding="utf-8-sig", errors="ignore").read()
            except OSError:
                continue
            for m in re.finditer(r"guid:\s*([0-9a-fA-F]{32})", txt):
                guids.add(m.group(1).lower())
    return guids


def main():
    existing = collect_existing_guids()
    print("existing guids in project:", len(existing))
    ok = True
    for rel, guid in TARGETS.items():
        full = os.path.join(ROOT, rel)
        if not os.path.exists(full):
            print("  [SKIP] missing", rel)
            ok = False
            continue
        raw = open(full, "rb").read()
        if raw.strip(b"\x00") != b"":
            print("  [SKIP] not all-NUL (len=%d) -> %s" % (len(raw), rel))
            ok = False
            continue
        if guid.lower() in existing:
            print("  [ABORT] guid collision", guid, rel)
            ok = False
            continue
        data = TEMPLATE.format(guid=guid).encode("utf-8")
        with open(full, "wb") as f:
            f.write(data)
        back = open(full, "rb").read()
        m = re.search(rb"guid: ([0-9a-f]{32})", back)
        print("  [FIXED] %-52s %d -> %d bytes  guid=%s" % (
            rel, len(raw), len(back), m.group(1).decode() if m else "<none>"))
        if len(back) != 243 or not m or m.group(1).decode() != guid:
            print("  [FAIL ] post-check failed", rel)
            ok = False
    print("RESULT:", "OK" if ok else "PROBLEM")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
