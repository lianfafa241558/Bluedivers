# -*- coding: utf-8 -*-
"""给 Healdrone 变体预制体加 HealthOther（AutoDestroy=1）。

▍为什么直接改 YAML 而不是 MCP 的 LoadPrefabContents+SaveAsPrefabAsset：后者会把整份变体重写，
  而 Healdrone_Laser.prefab 里还有未提交的武器数值改动。字节级插入只碰两处：
  ①根 PrefabInstance 的 m_AddedComponents 追加一条；②文件末尾追加一个 MonoBehaviour 块。
▍模板取自 Healdrone_HaloBomb.prefab（同族里已经有这个组件的那个），字段值完全一致。
用法：python healdrone_add_health.py [--apply]
"""
import hashlib
import re
import sys

BASE_GUID = "2f7f2b02984600145bdbad132e552728"    # HealdroneBase.prefab
BASE_ROOT_GO = "3934286715313500991"              # HealdroneBase 的根 GameObject fileID
HEALTH_GUID = "353e2a5a83e815841b7088f2d19c38b9"  # HealthOther.cs
TEMPLATE = "Assets/Resources/Prefabs/Airdrop/Healdrone_HaloBomb.prefab"
TARGETS = [
    "Assets/Resources/Prefabs/Airdrop/Healdrone_Normal.prefab",
    "Assets/Resources/Prefabs/Airdrop/Healdrone_Fire.prefab",
    "Assets/Resources/Prefabs/Airdrop/Healdrone_Laser.prefab",
]


def read(path):
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def new_id(path, tag):
    h = hashlib.md5((tag + ":" + path).encode("utf-8")).hexdigest()
    return str(1_000_000_000_000_000_000 + int(h[:16], 16) % 8_000_000_000_000_000_000)


def template_block():
    """取 HaloBomb 里那个 HealthOther 的**单个** YAML 块（按 `--- ` 切，别用跨块的非贪婪匹配）。"""
    text = read(TEMPLATE)
    for chunk in text.split("\n--- "):
        if HEALTH_GUID not in chunk or "AboGaugeArr" not in chunk:
            continue
        blk = chunk if chunk.startswith("--- ") else "--- " + chunk
        if not blk.startswith("--- !u!114"):
            continue
        if not blk.endswith("\n"):
            blk += "\n"
        if len(blk.splitlines()) > 30:
            raise SystemExit("模板块异常长（%d 行），拒绝使用" % len(blk.splitlines()))
        return blk
    raise SystemExit("模板块没找到（Healdrone_HaloBomb 结构变了？）")


def root_instance_range(text):
    for m in re.finditer(r"--- !u!1001 &(\d+)\n", text):
        start = m.start()
        nxt = text.find("\n--- ", start + 5)
        end = nxt + 1 if nxt > 0 else len(text)
        if "guid: " + BASE_GUID in text[start:end]:
            return start, end, m.group(1)
    raise SystemExit("找不到 source=HealdroneBase 的 PrefabInstance")


def root_go_id(text):
    """根 GameObject 在本文件里的 stripped 副本 fileID；没有（文件里没人引用过它）⇒ None。"""
    for m in re.finditer(r"--- !u!1 &(\d+) stripped\nGameObject:\n(?:.*\n)*?  m_PrefabAsset: \{fileID: 0\}\n", text):
        if "fileID: " + BASE_ROOT_GO + ", guid: " + BASE_GUID in m.group(0):
            return m.group(1)
    return None


def stripped_go_block(go_id, iid):
    """新建一个 stripped 根 GameObject（新增组件要按 m_GameObject 指向它）。"""
    return ("--- !u!1 &%s stripped\n"
            "GameObject:\n"
            "  m_CorrespondingSourceObject: {fileID: %s, guid: %s,\n"
            "    type: 3}\n"
            "  m_PrefabInstance: {fileID: %s}\n"
            "  m_PrefabAsset: {fileID: 0}\n") % (go_id, BASE_ROOT_GO, BASE_GUID, iid)


def insert_added_component(text, rng, hid):
    """在根实例的 m_AddedComponents 段里追加一条，返回新文本。"""
    start, end, _ = rng
    body = text[start:end]
    m = re.search(r"(?m)^    m_AddedComponents:[^\n]*\n", body)
    if not m:
        raise SystemExit("root PrefabInstance 没有 m_AddedComponents")
    line_start = start + m.start()
    line_end = start + m.end()
    entry = ("    - targetCorrespondingSourceObject: {fileID: %s, guid: %s,\n"
             "        type: 3}\n"
             "      insertIndex: -1\n"
             "      addedObject: {fileID: %s}\n") % (BASE_ROOT_GO, BASE_GUID, hid)

    if m.group(0).strip().endswith("[]"):
        return text[:line_start] + "    m_AddedComponents:\n" + entry + text[line_end:]

    # 已有条目：插在本段最后一个 addedObject 行之后
    last = None
    for item in re.finditer(r"(?m)^      addedObject: \{[^\n]*\}\n", text[line_start:end]):
        last = line_start + item.end()
    if last is None:
        raise SystemExit("m_AddedComponents 非空但找不到 addedObject 行")
    return text[:last] + entry + text[last:]


def main():
    apply = "--apply" in sys.argv
    blk_tpl = template_block()
    for path in TARGETS:
        text = read(path)
        if HEALTH_GUID in text:
            print("skip（已有 HealthOther）:", path)
            continue

        rng = root_instance_range(text)
        go_id = root_go_id(text)
        hid = new_id(path, "HealthOther")
        need_go = go_id is None
        if need_go:
            go_id = new_id(path, "RootGameObject")

        text = insert_added_component(text, rng, hid)

        blk = re.sub(r"--- !u!114 &\d+", "--- !u!114 &" + hid, blk_tpl, count=1)
        blk = blk.replace("7527687919712059693", go_id)      # m_GameObject → 本文件的根副本
        if not text.endswith("\n"):
            text += "\n"
        text += blk
        if need_go:
            text += stripped_go_block(go_id, rng[2])

        print("add HealthOther: %s  instance=%s go=%s(%s) component=%s" %
              (path, rng[2], go_id, "新建" if need_go else "已有", hid))
        if apply:
            write(path, text)
    print("apply =", apply)


if __name__ == "__main__":
    main()
