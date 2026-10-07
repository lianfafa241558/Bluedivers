# -*- coding: utf-8 -*-
"""把 topics/contract.md 尾部的「家具身份单源化」整段拆到 topics/furniture-identity.md，
contract.md 原位只留一行指针。

依据：记忆体检规则「topic 超 8000 字符预算 ⇒ 按小节再拆一篇」。
本段约 5000 字符，拆出后 contract.md 回到预算内。

用法：
    python split_contract_furniture.py           # 只报告（dry-run）
    python split_contract_furniture.py --apply   # 落盘（DST 已存在则拒绝）
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MEM = os.path.join(ROOT, ".codebuddy", "memory")
SRC = os.path.join(MEM, "topics", "contract.md")
DST = os.path.join(MEM, "topics", "furniture-identity.md")

ANCHOR = "## 家具身份「单一数据源」改造"

POINTER = [
    "## 家具身份单源化（`Furniture_Attached.Identity`，`Furniture_Base` 已删）",
    "",
    "- 全文见 `topics/furniture-identity.md`：改造判据 / 资产批量处理 / 编译后终检 / 遗留项。",
]

DST_HEADER = [
    "# 主题 · 家具身份单源化（Furniture_Attached.Identity）",
    "",
    "> 由 `topics/contract.md` 拆出（2026-10-02：contract.md 超 8000 字符预算）。"
    "触发词：家具身份 / Identity / 家具 Id / BaseObject / Furniture_Base / Furniture_Attached。",
    "",
]


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    apply = "--apply" in sys.argv

    raw = open(SRC, "rb").read()
    bom = raw[:3] == b"\xef\xbb\xbf"
    text = raw.decode("utf-8-sig")
    eol = "\r\n" if "\r\n" in text else "\n"

    pos = text.find(ANCHOR)
    if pos < 0:
        print("找不到锚点：%s" % ANCHOR)
        return
    if text.find(ANCHOR, pos + 1) >= 0:
        print("锚点不唯一，放弃")
        return

    head = text[:pos].rstrip("\r\n \t") + eol + eol + eol.join(POINTER) + eol
    body = text[pos:]
    header_text = eol.join(DST_HEADER) + eol

    print("SRC 原：%d 字符 / %d 行" % (len(text), len(text.splitlines())))
    print("  拆出 body：%d 字符 / %d 行" % (len(body), len(body.splitlines())))
    print("  留下 head：%d 字符 / %d 行" % (len(head), len(head.splitlines())))
    print("DST（含标题头）：%d 字符" % (len(header_text) + len(body)))
    print("BOM=%s  EOL=%s  DST已存在=%s" % (bom, repr(eol), os.path.exists(DST)))

    if not apply:
        print("（dry-run 未落盘；加 --apply）")
        return
    if os.path.exists(DST):
        print("DST 已存在，放弃（避免覆盖）")
        return

    enc = b"\xef\xbb\xbf" if bom else b""
    with open(SRC, "wb") as fp:
        fp.write(enc + head.encode("utf-8"))
    with open(DST, "wb") as fp:
        fp.write(enc + (header_text + body).encode("utf-8"))

    c = open(SRC, "rb").read().decode("utf-8-sig")
    f = open(DST, "rb").read().decode("utf-8-sig")
    print("落盘后 contract.md：%d 字符；furniture-identity.md：%d 字符" % (len(c), len(f)))
    print("指针命中：%s   旧锚点残留于 contract.md：%s"
          % ("topics/furniture-identity.md" in c, ANCHOR in c))


if __name__ == "__main__":
    main()
