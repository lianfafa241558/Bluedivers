# -*- coding: utf-8 -*-
"""扫描全工程 prefab 里的武器组件（WeaponBaseController 及其子类），列出：
   - ShootType（0=Manual 1=Automatic 2=Charge 3=Laser 4=Lock?）
   - Damages 每条 entry 的 BulletPrefab 是否为空
   - 已废弃的武器级 ProjectilePrefab（字段已被注释掉，序列化残留）
用途：定位 "IVfxSink.Creat<T> 只支持 ProjectileBase 模板"（= CurrentDamgeData.BulletPrefab 为空）
      这类运行期报错到底出自哪个预制体。
只读，不写盘。
"""
import io
import os
import re
import sys

ROOT = r"E:/Bluedivers/Assets"
SHOOT_TYPE = {0: "Manual", 1: "Automatic", 2: "Charge", 3: "Laser", 4: "Lock", 5: "?"}
# 武器脚本 guid -> 类名（取自各 .cs.meta）
SCRIPT = {
    "b816effe12903f14faca310da805ceb3": "WeaponBase",
    "3ad90ba0b1477764da373f5ec01a8cb5": "WeaponController",
    "1925424471e3fe949a952edf108dc17b": "WeaponEnemy",
    "e03ea2fb50b06ab468194bc5b83375d6": "WeaponPlayer",
    "d76c548a35bb41f4d84390674bcd4626": "WeaponUpgrade",
}


def blocks(text):
    """按 '--- !u!114' 切 MonoBehaviour 块"""
    parts = re.split(r"^--- !u!114 &(\d+)\s*$", text, flags=re.M)
    # parts = [head, id1, body1, id2, body2, ...]
    for i in range(1, len(parts), 2):
        yield parts[i], parts[i + 1]


def scan(path):
    text = io.open(path, encoding="utf-8", errors="replace").read()
    if "BulletFlag:" not in text:
        return []
    out = []
    for fid, body in blocks(text):
        if "BulletFlag:" not in body:
            continue
        m_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
        m_name = re.search(r"^  m_Name: (.*)$", body, flags=re.M)
        m_st = re.search(r"^  ShootType: (-?\d+)\s*$", body, flags=re.M)
        m_script = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]+)", body)
        shoot = int(m_st.group(1)) if m_st else None
        legacy = re.search(r"^  ProjectilePrefab: \{fileID: (\d+)", body, flags=re.M)
        legacy_set = bool(legacy and legacy.group(1) != "0")
        # Damages 列表
        dm = re.search(r"^  Damages:\s*(\[\])?\s*$", body, flags=re.M)
        entries = []
        if dm and not dm.group(1):
            seg = body[dm.end():]
            # 到下一条 '^  [A-Za-z_]' 顶层字段为止
            stop = re.search(r"^  [A-Za-z_]", seg, flags=re.M)
            if stop:
                seg = seg[:stop.start()]
            cur = None
            for line in seg.splitlines():
                if re.match(r"^  - ", line):
                    cur = {"bullet": None}
                    entries.append(cur)
                    line_off = line[4:]
                    mm = re.match(r"BulletPrefab: \{fileID: (-?\d+)", line_off)
                    if mm:
                        cur["bullet"] = mm.group(1)
                elif cur is not None:
                    mm = re.match(r"^    BulletPrefab: \{fileID: (-?\d+)", line)
                    if mm:
                        cur["bullet"] = mm.group(1)
                if cur is not None and "bullet" in cur and cur["bullet"] is None:
                    # 该 entry 里有 BulletPrefab 行吗？没有就仍算空
                    pass
        out.append({
            "file": path,
            "fid": fid,
            "name": m_name.group(1).strip() if m_name else "?",
            "cls": SCRIPT.get(m_script.group(1), "?") if m_script else "?",
            "shoot": shoot,
            "legend": legacy_set,
            "entries": entries,
        })
    return out


def main():
    only_charge = "--charge" in sys.argv
    hits = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        for fn in filenames:
            if not fn.endswith(".prefab"):
                continue
            p = os.path.join(dirpath, fn)
            try:
                hits.extend(scan(p))
            except Exception as e:  # noqa
                print("ERR", p, e)
    rel = lambda p: os.path.relpath(p, ROOT).replace("\\", "/")
    print("=== 武器组件数:", len(hits))
    bad = []
    for h in hits:
        if only_charge and h["shoot"] != 2:
            continue
        empties = [i for i, e in enumerate(h["entries"]) if not e["bullet"] or e["bullet"] == "0"]
        if empties or not h["entries"]:
            bad.append((h, empties))
    print("=== 有问题的武器:", len(bad))
    for h, empties in bad:
        no_list = "（Damages 为空！）" if not h["entries"] else ""
        print("%-58s ShootType=%-9s entries=%d 空BulletPrefab下标=%s 旧ProjectilePrefab=%s %s"
              % (rel(h["file"]), SHOOT_TYPE.get(h["shoot"], h["shoot"]), len(h["entries"]),
                 empties, h["legend"], no_list))
    print("--- 全部 Charge 武器 ---")
    for h in hits:
        if h["shoot"] == 2:
            ok = all(e["bullet"] and e["bullet"] != "0" for e in h["entries"])
            print("%-58s %-15s entries=%d %s 旧ProjectilePrefab=%s"
                  % (rel(h["file"]), h["cls"], len(h["entries"]), "OK" if ok else "★缺弹", h["legend"]))
    print("--- 全部 Enemy 武器（WeaponEnemyController）---")
    for h in hits:
        if h["cls"] == "WeaponEnemy":
            empties = [i for i, e in enumerate(h["entries"]) if not e["bullet"] or e["bullet"] == "0"]
            print("%-58s ShootType=%-9s entries=%d 空下标=%s %s"
                  % (rel(h["file"]), SHOOT_TYPE.get(h["shoot"], h["shoot"]), len(h["entries"]), empties,
                     "（Damages 为空！）" if not h["entries"] else ""))


main()
