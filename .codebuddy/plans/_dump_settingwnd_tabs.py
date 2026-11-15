# -*- coding: utf-8 -*-
"""诊断 SettingWnd 的 expandRoot / layoutRoot 子项顺序与 Button 传参，定位 nowSelect 错位。
注意：MonoBehaviour 里 expandRoot/layoutRoot/nowSelect 存的是 **RectTransform(224)** 的 fileID。"""
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
P = Path(r"d:\Pro\Bluedivers\Assets\Resources\UI\Wnd\SettingWnd.prefab")
TEXT = P.read_text(encoding="utf-8", errors="replace")
WND_SCRIPT_ID = "2129698854457866593"

blocks = {}
for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)([^\n]*)$", TEXT, flags=re.M):
    start = m.end()
    nxt = re.search(r"^--- !u!", TEXT[start:], flags=re.M)
    end = start + (nxt.start() if nxt else len(TEXT) - start)
    blocks[m.group(2)] = (m.group(1), TEXT[start:end])

gos, trs = {}, {}
for fid, (cls, body) in blocks.items():
    if cls == "1":
        nm = re.search(r"^\s*m_Name: (.*)$", body, flags=re.M)
        act = re.search(r"m_IsActive: (\d)", body)
        gos[fid] = dict(name=nm.group(1).strip() if nm else "?", active=act.group(1) if act else "1")
    elif cls in ("4", "224"):
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
        fa = re.search(r"m_Father: \{fileID: (-?\d+)\}", body)
        kids = re.findall(r"^\s+- \{fileID: (-?\d+)\}\s*$", body, flags=re.M)
        trs[fid] = dict(go=go.group(1) if go else "0", father=fa.group(1) if fa else "0", kids=kids)

wnd = blocks[WND_SCRIPT_ID][1]


def ref(name):
    m = re.search(r"^\s*" + name + r": \{fileID: (-?\d+)\}", wnd, flags=re.M)
    return m.group(1) if m else None


F = {k: ref(k) for k in ("expandRoot", "layoutRoot", "nowSelect", "settingRoot",
                         "secondaryLayoutRoot", "nowsecondarySelect")}
print("字段(Transform fileID):", F)


def tname(trfid):
    d = trs.get(trfid)
    if not d:
        return "<未知 %s>" % trfid
    return gos.get(d["go"], {}).get("name", "<go %s>" % d["go"])


def dump(trfid, label):
    d = trs.get(trfid)
    print("\n=== %s  tr=%s  自身名=%r  father=%s  子项数=%s ===" % (
        label, trfid, tname(trfid), d["father"] if d else "?", len(d["kids"]) if d else "?"))
    for i, k in enumerate(d["kids"]):
        kd = trs.get(k)
        act = gos.get(kd["go"], {}).get("active", "?") if kd else "?"
        print("  [%d] tr=%-20s name=%-16r active=%s kids=%s" % (
            i, k, tname(k), act, len(kd["kids"]) if kd else "?"))
        if i == 0 or True:
            for j, kk in enumerate(kd["kids"] if kd else []):
                print("         [%d] name=%r" % (j, tname(kk)))


dump(F["expandRoot"], "expandRoot")
dump(F["layoutRoot"], "layoutRoot")

exp_tr, lay_tr = F["expandRoot"], F["layoutRoot"]
tr_of_go = {d["go"]: t for t, d in trs.items()}

print("\n=== Button -> SwitchExpand 传参（arg 是 GameObject fileID）===")
for fid, (cls, body) in blocks.items():
    if cls != "114" or "SwitchExpand" not in body:
        continue
    arg = re.search(r"m_ObjectArgument: \{fileID: (-?\d+)\}", body)
    bgo = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
    if not arg:
        continue
    a = arg.group(1)
    atr = tr_of_go.get(a, "<无 transform>")
    ie = trs[exp_tr]["kids"].index(atr) if atr in trs.get(exp_tr, {}).get("kids", []) else None
    il = trs[lay_tr]["kids"].index(atr) if atr in trs.get(lay_tr, {}).get("kids", []) else None
    btn = gos.get(bgo.group(1), {}).get("name", "?") if bgo else "?"
    print("  Button %-10r -> arg=%s %-12r idxInExpandRoot=%-5s idxInLayoutRoot=%-5s father=%s" % (
        btn, a, gos.get(a, {}).get("name", "?"), ie, il, tname(trs[atr]["father"]) if atr in trs else "?"))

print("\n=== nowSelect 的 position/sizeDelta 与 layoutRoot 子项的 anchoredPosition ===")
print("nowSelect:", tname(F["nowSelect"]), "father=", tname(trs[F["nowSelect"]]["father"]))
for i, k in enumerate(trs[lay_tr]["kids"]):
    body = blocks.get(k, ("", ""))[1]
    ap = re.search(r"m_AnchoredPosition: \{x: ([^,]+), y: ([^}]+)\}", body)
    sd = re.search(r"m_SizeDelta: \{x: ([^,]+), y: ([^}]+)\}", body)
    print("  [%d] %-14r anchored=%s size=%s" % (
        i, tname(k), ap.groups() if ap else None, sd.groups() if sd else None))
