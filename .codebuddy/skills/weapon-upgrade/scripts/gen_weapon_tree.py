# -*- coding: utf-8 -*-
"""spec(JSON) -> WUD/WMD 资产 + meta + prefab 补丁。纯文本，不依赖 Unity。

用法：
    python gen_weapon_tree.py --spec examples/Logic.json --dry-run   # 只报告差异
    python gen_weapon_tree.py --spec examples/Logic.json             # 写入（prefab 自动备份）
    python gen_weapon_tree.py --spec ... --force                     # 覆盖已存在资产

设计要点（都是踩过的坑）：
  * 属性一律写枚举名，编号由 attr_whitelist 从 C# 源码解析 —— 不手记数字。
  * 生成前校验 ① 属性存在 ② 修饰符被 attributeConfigs 允许 ③ 属性有真实基础值。
  * prefab 用"锚点 + 唯一命中断言"替换，绝不整文件重写；写入前自动 .bak。
  * 幂等：算出来的文本与磁盘一致就不写，所以重复跑是 no-op。
"""

import argparse
import hashlib
import json
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import attr_whitelist  # noqa: E402

for _s in (sys.stdout, sys.stderr):   # Windows 控制台默认 GBK，中文/箭头会炸
    try:
        _s.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

WUD_SCRIPT = "150ca7a623f31424aad6112aba22d37e"
WMD_SCRIPT = "744367736a7aee740922335e2157ba81"
WMD_NONE_GUID = "a19bae1a0785866409ae31f377bde9cf"
QUALITY = {"None": 0, "Clean": 1, "Balanced": 2, "Unstable": 3}
MAX_PER_TIER = 3  # 存档 buyIndex 只有 3 位（GetBuy 硬判 x > 2）


def project_root(start):
    p = os.path.abspath(start)
    for _ in range(8):
        if os.path.isdir(os.path.join(p, "Assets")) and os.path.isdir(os.path.join(p, ".codebuddy")):
            return p
        p = os.path.dirname(p)
    raise SystemExit("找不到项目根（需要同时存在 Assets 与 .codebuddy）")


def guid_of(salt, key):
    return hashlib.md5(("%s.%s" % (salt, key)).encode("utf-8")).hexdigest()


def q(s):
    """Unity YAML 字符串：双引号 + \\uXXXX 转义，与工程内现有资产一致。"""
    return json.dumps(s, ensure_ascii=True)


def num_text(v):
    return str(v)


def render_wud(asset, name, category, icon, desc, cost, mods, info):
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % WUD_SCRIPT,
        "  m_Name: WUD_%s" % asset,
        "  m_EditorClassIdentifier: ",
        "  name: %s" % q(name),
        "  type: %s" % q(category),
        "  icon: {fileID: 21300000, guid: %s, type: 3}" % icon,
        "  desc: %s" % q(desc),
        "  cost:",
        "  - Key: 0",
        "    Value: %d" % cost,
        "  modifys:",
    ]
    for m in mods:
        lines += ["  - name: ",
                  "    type: %d" % info.num(m["attr"]),
                  "    modifier: %d" % attr_whitelist.MOD_NAMES[m["modifier"]],
                  "    value: %s" % num_text(m["value"])]
    return "\n".join(lines) + "\n"


def render_wmd(asset, name, quality, icon, desc, mods, info):
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % WMD_SCRIPT,
        "  m_Name: WMD_%s" % asset,
        "  m_EditorClassIdentifier: ",
        "  name: %s" % q(name),
        "  type: %d" % QUALITY[quality],
        "  icon: {fileID: 21300000, guid: %s, type: 3}" % icon,
        "  desc:",
    ]
    for d in desc:
        lines += ["  - Key: %d" % (1 if d["good"] else 0),
                  "    Value: %s" % q(d["text"])]
    lines.append("  modifys:")
    for m in mods:
        lines += ["  - name: ",
                  "    type: %d" % info.num(m["attr"]),
                  "    modifier: %d" % attr_whitelist.MOD_NAMES[m["modifier"]],
                  "    value: %s" % num_text(m["value"])]
    return "\n".join(lines) + "\n"


def render_meta(guid):
    return ("fileFormatVersion: 2\n"
            "guid: %s\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n") % guid


def render_folder_meta(guid):
    return ("fileFormatVersion: 2\n"
            "guid: %s\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n") % guid


def show_attr_hex(names, info):
    return "".join(info.num(n).to_bytes(4, "little").hex() for n in names)


def validate(spec, info, cfg_numbers):
    """所有会让"改了不生效 / 运行时才报错"的问题都在这里拦掉。"""
    errors, warnings = [], []
    icons = spec.get("icons", {})

    def mods_of(owner, mods, only_buff):
        for m in mods:
            ok, why = info.check(m["attr"], m["modifier"])
            if not ok:
                errors.append("%s: %s" % (owner, why))
                continue
            ok, why = info.has_base(m["attr"], cfg_numbers)
            if not ok:
                errors.append("%s: %s" % (owner, why))
            if only_buff and info.is_buff(m["attr"], float(m["value"])) is False:
                errors.append("%s: `%s` 的 %s %s 是**弱化项** ⇒ 武器升级只允许强化，"
                              "想让玩家付出代价请做成模组（WMD_）"
                              % (owner, m["attr"], m["modifier"], m["value"]))

    for n in spec.get("showAttr", []):
        if n not in info.enum_num:
            errors.append("showAttr: 枚举里没有 `%s`" % n)
    for c in spec.get("cfgAdd", []):
        if c["attr"] not in info.enum_num:
            errors.append("cfgAdd: 枚举里没有 `%s`" % c["attr"])

    for tier in spec.get("tiers", []):
        if len(tier["items"]) > MAX_PER_TIER:
            errors.append("Key=%s 有 %d 项，超过每档 %d 上限（buyIndex 只有 3 位）"
                          % (tier["key"], len(tier["items"]), MAX_PER_TIER))
        if len(tier["items"]) != MAX_PER_TIER:
            warnings.append("Key=%s 只有 %d 项 ⇒ 若旧存档该档是 3 项，"
                            "`selectIndex` 会悬空，需清对应存档键" % (tier["key"], len(tier["items"])))
        for it in tier["items"]:
            if it.get("icon") and it["icon"] not in icons:
                errors.append("%s: 未定义的 icon 别名 `%s`" % (it["asset"], it["icon"]))
            mods_of("WUD_" + it["asset"], it["modifys"], True)   # 升级项只允许强化

    for md in spec.get("modules", []):
        if md["quality"] not in QUALITY:
            errors.append("%s: quality 必须是 %s" % (md["asset"], "/".join(QUALITY)))
        if md.get("icon") and md["icon"] not in icons:
            errors.append("%s: 未定义的 icon 别名 `%s`" % (md["asset"], md["icon"]))
        mods_of("WMD_" + md["asset"], md["modifys"], False)  # 模组才允许正负成对
        goods = sum(1 for d in md["desc"] if d["good"])
        if md["quality"] != "Clean" and goods == len(md["desc"]):
            warnings.append("%s 是 %s 却只有增益文案" % (md["asset"], md["quality"]))
        if md["quality"] == "Clean" and goods != len(md["desc"]):
            warnings.append("%s 是 Clean 却带代价文案（品质应为 Balanced）" % md["asset"])

    return errors, warnings


def build(spec, root):
    info = attr_whitelist.load(root)
    prefab_path = os.path.join(root, spec["prefab"])
    existing_cfg = attr_whitelist.parse_prefab_cfg(prefab_path)
    add_nums = {info.num(c["attr"]) for c in spec.get("cfgAdd", [])}
    cfg_numbers = set(existing_cfg) | add_nums
    rev = {v: k for k, v in info.enum_num.items()}
    cfg_names = {rev.get(n, str(n)) for n in cfg_numbers}   # has_base 比的是名字，不是编号

    errors, warnings = validate(spec, info, cfg_names)
    if errors:
        return {"errors": errors, "warnings": warnings, "files": {}, "prefab": None, "info": info}

    salt = spec.get("guidSalt", spec["name"])
    icons = spec["icons"]
    files = {}

    # 目录 meta（已存在则不动，避免无意义 diff）
    for key in ("wudDir", "wmdDir"):
        d = spec[key]
        if not os.path.exists(os.path.join(root, d + ".meta")):
            files[d + ".meta"] = render_folder_meta(guid_of(salt, d))

    guids = {}
    for tier in spec["tiers"]:
        for it in tier["items"]:
            g = guid_of(salt, "WUD_" + it["asset"])
            guids["WUD_" + it["asset"]] = g
            p = "%s/WUD_%s.asset" % (spec["wudDir"], it["asset"])
            files[p] = render_wud(it["asset"], it["name"], it["category"], icons[it["icon"]],
                                  it["desc"], it.get("cost", 1000), it["modifys"], info)
            files[p + ".meta"] = render_meta(g)

    for md in spec["modules"]:
        g = guid_of(salt, "WMD_" + md["asset"])
        guids["WMD_" + md["asset"]] = g
        p = "%s/WMD_%s.asset" % (spec["wmdDir"], md["asset"])
        files[p] = render_wmd(md["asset"], md["name"], md["quality"], icons[md["icon"]],
                              md["desc"], md["modifys"], info)
        files[p + ".meta"] = render_meta(g)

    guard = check_adopt(spec, guids, root)   # 落差门：宁可不动，也不覆盖人工结构
    if guard:
        return {"errors": errors + guard, "warnings": warnings, "files": {},
                "prefab": None, "info": info, "guids": guids}

    prefab = patch_prefab(prefab_path, spec, info, guids, cfg_numbers)
    if prefab["errors"]:
        errors += prefab["errors"]

    return {"errors": errors, "warnings": warnings, "files": files,
            "prefab": prefab, "info": info, "guids": guids}


def current_prefab_refs(path):
    """读出 prefab 现在 Upgrade/Modules 里引用的 guid 集合。"""
    with open(path, encoding="utf-8") as f:
        src = f.read()
    m = re.search(r"(?ms)^  Upgrade:\n(.*?)^  ActiveModule:", src)
    return set(re.findall(r"guid: ([0-9a-f]{32})", m.group(1))) if m else set()


def check_adopt(spec, guids, root):
    """落差门：prefab 若已被人工改过（引用 spec 之外的资产），拒绝生成。

    ⚠ 这类事故真实发生过：spec 落后于手工改动时跑 gen，会把编辑器里的新结构整块覆盖。
    确认要接管就在 spec 里写 "adopt": true。
    """
    if spec.get("adopt"):
        return []
    prefab_path = os.path.join(root, spec["prefab"])
    if not os.path.exists(prefab_path):
        return []
    none_guid = spec.get("noModule", WMD_NONE_GUID)
    refs = current_prefab_refs(prefab_path)
    unknown = refs - set(guids.values()) - {none_guid}
    if unknown:
        return ["prefab 的 Upgrade/Modules 引用了 %d 个 spec 之外的资产（结构已被人工改过）：%s\n"
                "  ⇒ 先让 spec 对齐现状，确实要用 spec 覆盖时在 spec 里加 \"adopt\": true"
                % (len(unknown), ", ".join(sorted(unknown)))]
    return []


def patch_prefab(path, spec, info, guids, cfg_numbers):
    with open(path, encoding="utf-8", newline="") as f:
        src = f.read()
    nl = "\r\n" if "\r\n" in src else "\n"
    text = src.replace("\r\n", "\n")
    old = text
    errors = []

    # 1) cfg._fakeAttrs 补缺失项（已有的不动）
    if spec.get("cfgAdd"):
        m = re.search(r"(?m)^    _fakeAttrs:\n((?:    - Key: \d+\n      Value: [^\n]*\n)*)", text)
        if not m:
            errors.append("prefab: 找不到 cfg._fakeAttrs")
        else:
            block = m.group(1)
            have = {int(k) for k in re.findall(r"(?m)^    - Key: (\d+)", block)}
            add = ""
            for c in spec["cfgAdd"]:
                if info.num(c["attr"]) not in have:
                    add += "    - Key: %d\n      Value: %s\n" % (info.num(c["attr"]), num_text(c["value"]))
            if add:
                text = text.replace(m.group(0), m.group(0) + add, 1)

    # 2) showAttr
    if spec.get("showAttr"):
        hexstr = show_attr_hex(spec["showAttr"], info)
        text, n = re.subn(r"(?m)^  showAttr: [0-9a-fA-F]*$", "  showAttr: " + hexstr, text)
        if n != 1:
            errors.append("prefab: showAttr 命中 %d 次（应为 1）" % n)

    # 3) Upgrade 整块
    tiers = []
    for tier in spec["tiers"]:
        tiers.append("  - Key: %d\n    Value:" % tier["key"])
        for it in tier["items"]:
            tiers.append("    - {fileID: 11400000, guid: %s, type: 2}" % guids["WUD_" + it["asset"]])
    block = "  Upgrade:\n" + "\n".join(tiers) + "\n"
    text, n = re.subn(r"(?ms)^  Upgrade:\n.*?(?=^  Modules:)", block, text)
    if n != 1:
        errors.append("prefab: Upgrade 块命中 %d 次（应为 1）" % n)

    # 4) Modules + ActiveModule
    none_guid = spec.get("noModule", WMD_NONE_GUID)
    lines = ["  Modules:", "  - {fileID: 11400000, guid: %s, type: 2}" % none_guid]
    for md in spec["modules"]:
        lines.append("  - {fileID: 11400000, guid: %s, type: 2}" % guids["WMD_" + md["asset"]])
    def _mod_repl(m):
        # 匹配到的块以 "\n" 结尾时必须补回，否则会把下一行（ActiveModule）接到同一行
        return "\n".join(lines) + ("\n" if m.group(0).endswith("\n") else "")

    text, n = re.subn(r"(?m)^  Modules:(?: \[\]|\n(?:  - [^\n]*\n)+)", _mod_repl, text)
    if n != 1:
        errors.append("prefab: Modules 块命中 %d 次（应为 1）" % n)
    text, n = re.subn(r"(?m)^  ActiveModule: [^\n]*$",
                      "  ActiveModule: {fileID: 11400000, guid: %s, type: 2}" % none_guid, text)
    if n != 1:
        errors.append("prefab: ActiveModule 命中 %d 次（应为 1）" % n)

    return {"path": path, "old": old, "new": text, "nl": nl,
            "changed": text != old, "errors": errors}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", required=True)
    ap.add_argument("--root", default=None)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true", help="覆盖已存在的资产")
    args = ap.parse_args()

    root = args.root or project_root(os.path.dirname(os.path.abspath(__file__)))
    with open(args.spec, encoding="utf-8") as f:
        spec = json.load(f)

    res = build(spec, root)
    for w in res["warnings"]:
        print("  [WARN]", w)
    if res["errors"]:
        for e in res["errors"]:
            print("  [ERROR]", e)
        print("RESULT: FAILED（未写入任何文件）")
        return 1

    same = new = 0
    for rel, text in sorted(res["files"].items()):
        p = os.path.join(root, rel)
        cur = None
        if os.path.exists(p):
            with open(p, encoding="utf-8", newline="") as f:
                cur = f.read()
        if cur == text:
            same += 1
            continue
        new += 1
        if not args.dry_run:
            if cur is not None and not args.force and rel.endswith(".asset"):
                print("  [SKIP] 已存在且内容不同（加 --force 覆盖）:", rel)
                continue
            os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
        print("  [%s] %s" % ("DIFF" if args.dry_run else "WRITE", rel))

    pf = res["prefab"]
    if pf["changed"]:
        print("  [%s] %s" % ("DIFF" if args.dry_run else "WRITE", os.path.relpath(pf["path"], root)))
        if not args.dry_run:
            shutil.copyfile(pf["path"], pf["path"] + ".bak")
            with open(pf["path"], "w", encoding="utf-8", newline="") as f:
                f.write(pf["new"].replace("\n", pf["nl"]))
    else:
        print("  [SAME] prefab 无需改动")

    print("资产: 一致 %d / 变更 %d；tiers=%s modules=%d"
          % (same, new, [len(t["items"]) for t in spec["tiers"]], len(spec["modules"])))
    print("RESULT:", "OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
