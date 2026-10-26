# -*- coding: utf-8 -*-
"""扫描"各端本地生成"的装置预制体所引用的 ImpactVfx，检查这些表现物自身是否还会结算伤害。

判据（同 topics/net-dup-damage.md）：表现物上若挂 DeployableMine / SustainedEffect / WeaponTemporaryController
⇒ 它自己会调 FpsHelper.Hit，表现弹命中时仍会两端各结算一次。
"""
import os
import re
import sys

ASSETS = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "Assets"))

# 值得关注的"自带伤害"脚本 guid
DAMAGE_SCRIPTS = {
    "b829b363b89b69e4d8f2ab27c789dbe5": "DeployableMine",
    "b72e5c4e2526a5e4cb512fc1d8fc9397": "SustainedEffect",
    "1fefde50582945046a4ddc355073c6d6": "WeaponTemporaryController",
    "64a0acf81aa70d0449c5efbf39f8bd16": "WeaponMineLauncherController",
    "1925424471e3fe949a952edf108dc17b": "WeaponEnemyController",
    "e03ea2fb50b06ab468194bc5b83375d6": "WeaponPlayerController",
}

DEVICES = [
    "Resources/Prefabs/Airdrop/Eagle Airstrike.prefab",
    "Resources/Prefabs/Airdrop/Eagle Gas.prefab",
    "Resources/Prefabs/Airdrop/Eagle Gatling.prefab",
    "Resources/Prefabs/Airdrop/Eagle Rocket.prefab",
    "Resources/Prefabs/Airdrop/Eagle Smoke.prefab",
    "Resources/Prefabs/Airdrop/Eagle_500KG.prefab",
    "Resources/Prefabs/Airdrop/Eagle_Cluster.prefab",
    "Resources/Prefabs/Airdrop/Fx_Noa_Ghost_NS.prefab",
    "Resources/Prefabs/Airdrop/Track Gatling.prefab",
    "Resources/Prefabs/Airdrop/Track Illuminator.prefab",
    "Resources/Prefabs/Airdrop/Track Laser.prefab",
    "Resources/Prefabs/Airdrop/Track Railgun.prefab",
    "Resources/Prefabs/Airdrop/Track_120MM.prefab",
    "Resources/Prefabs/Airdrop/Track_380MM.prefab",
    "Resources/Prefabs/Airdrop/Track_Airburst.prefab",
    "Resources/Prefabs/Airdrop/Track_Gas.prefab",
    "Resources/Prefabs/Airdrop/Track_Smoke.prefab",
    "Resources/Prefabs/Airdrop/Sub/Eagle Cluster_Explode.prefab",
    "Resources/Prefabs/Airdrop/MineLauncher.prefab",
    "Resources/Prefabs/Airdrop/MineLauncher_Fire.prefab",
    "Resources/Prefabs/Airdrop/MineLauncher_Gas.prefab",
    "Resources/Prefabs/Grenade/Grenade_Strafe.prefab",
    "Resources/Prefabs/Enemy/Zerg/DEC/Sub/Dec_ColossusTower.prefab",
]

GUID_RE = re.compile(r"ImpactVfx: \{fileID: -?\d+, guid: ([0-9a-f]{32})")


def build_guid_map():
    """guid -> 资产绝对路径（只扫 .prefab / .asset 的 meta）。"""
    m = {}
    for root, _, files in os.walk(ASSETS):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            if not (fn.endswith(".prefab.meta") or fn.endswith(".asset.meta")):
                continue
            p = os.path.join(root, fn)
            try:
                with open(p, "r", encoding="utf-8", errors="ignore") as f:
                    head = f.read(200)
            except OSError:
                continue
            mm = re.search(r"^guid: ([0-9a-f]{32})$", head, re.M)
            if mm:
                m[mm.group(1)] = p[:-5]
    return m


def main():
    gmap = build_guid_map()
    print("guid 索引数: %d" % len(gmap))
    rows = []
    for dev in DEVICES:
        path = os.path.join(ASSETS, dev.replace("/", os.sep))
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            text = f.read()
        for g in sorted(set(GUID_RE.findall(text))):
            vfx = gmap.get(g, "<未找到:%s>" % g)
            hits = []
            if not vfx.startswith("<"):
                try:
                    with open(vfx, "r", encoding="utf-8", errors="ignore") as f:
                        vt = f.read()
                except OSError:
                    vt = ""
                for sg, name in DAMAGE_SCRIPTS.items():
                    if sg in vt:
                        hits.append(name)
            rel = vfx.replace(ASSETS + os.sep, "").replace(os.sep, "/") if not vfx.startswith("<") else vfx
            rows.append((dev, rel, ",".join(hits)))

    bad = [r for r in rows if r[2]]
    print("\n=== 自带伤害的 ImpactVfx ===")
    for dev, vfx, hits in bad:
        print("%-55s -> %-60s %s" % (dev, vfx, hits))
    print("\n=== 纯表现 ===")
    for dev, vfx, hits in rows:
        if not hits:
            print("%-55s -> %s" % (dev, vfx))
    print("\n合计: %d 条引用，其中自带伤害 %d 条" % (len(rows), len(bad)))


if __name__ == "__main__":
    sys.exit(main())
