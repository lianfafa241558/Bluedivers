# -*- coding: utf-8 -*-
"""武器属性白名单：所有信息都从 C# 源码解析，不维护第二份表（否则必然漂移）。

对外提供：
    load(root)              -> Info(enum_num, configs, auto_fill)
    Info.check(attr_name, modifier_name)  -> (ok, reason)
    Info.has_base(attr_name, prefab_add)  -> (ok, reason)
    parse_prefab_cfg(path)  -> {attr_name: value}   # 读 cfg._fakeAttrs
"""

import os
import re

MOD_NAMES = {"Base": 1, "Factor": 2, "Extra": 4, "All": 7}

FACTORY_REL = "Assets/Scripts/06Gameplay/Weapon/WeaponAttributeFactory.cs"
CONTROLLER_REL = "Assets/Scripts/06Gameplay/Weapon/WeaponPlayerController.cs"


class Info:
    """一次解析的结果，供 gen / verify 共用。"""

    def __init__(self, enum_num, configs, auto_fill):
        self.enum_num = enum_num        # name -> 编号
        self.configs = configs          # name -> {current, tags, mods}
        self.auto_fill = auto_fill      # set(name)：InitAttribute 会从 Damages 补 base

    def num(self, name):
        if name not in self.enum_num:
            raise KeyError("WeaponAttrType 里没有 `%s`（检查拼写，工具只认枚举名）" % name)
        return self.enum_num[name]

    def check(self, name, modifier):
        """校验 (属性, 修饰符) 组合是否被 attributeConfigs 允许。"""
        if name not in self.enum_num:
            return False, "枚举里没有 `%s`" % name
        cfg = self.configs.get(name)
        if cfg is None:
            return False, "`%s` 不在 attributeConfigs（缺配置会在运行时 LogError 并跳过）" % name
        m = MOD_NAMES.get(modifier)
        if m is None:
            return False, "未知 modifier `%s`（只能是 Base/Factor/Extra）" % modifier
        if not (cfg["mods"] & m):
            allow = [k for k, v in MOD_NAMES.items() if k != "All" and (cfg["mods"] & v)]
            return False, "`%s` 不接受 %s，只允许 %s" % (name, modifier, "/".join(allow))
        return True, ""

    def is_lower_better(self, name):
        """值越低越好的属性。标在两种 tag 上，必须都认：

        * `FlipPlus`：越低越好、负值显示成绿色（ReloadTime / BulletsSpreadAngle / HeatPerShot / CoolDelay / OverheatDuration…）
        * `Reciprocal`：显示倒数（ShootInterval / LockInterval）—— 间隔降低 = 射速提升
        """
        cfg = self.configs.get(name)
        if not cfg:
            return False
        return ("FlipPlus" in cfg["tags"]) or ("Reciprocal" in cfg["tags"])

    def is_buff(self, name, value):
        """这个 (属性, 值) 是不是"强化"。None = 不判（Special 或 value==0）。"""
        if name not in self.enum_num or self.configs.get(name) is None or value == 0:
            return None
        return (value < 0) if self.is_lower_better(name) else (value > 0)

    def has_base(self, name, prefab_add):
        """该属性是否有真实基础值（否则 Factor/Extra 都是在 0 上做文章 ⇒ 装了没效果）。

        prefab_add: 本工具即将写入 cfg 的属性名集合。
        依据：① 在 cfg._fakeAttrs 里（调用方从 prefab 解析）② cfgAdd 里
              ③ InitAttribute 的 Set() 列表（从 Damages 取 base）
        """
        if name in prefab_add:
            return True, ""
        if name in self.auto_fill:
            return True, ""
        return False, ("`%s` 既不在 cfg._fakeAttrs、也不由 InitAttribute 从 Damages 补 base "
                       "⇒ 基础值是 0，改了不生效。请先把它写进 cfgAdd。" % name)


def _parse_enum(src):
    seg = src[src.index("public enum WeaponAttrType"):src.index("public class GameAttribute")]
    return {n: int(v) for n, v in re.findall(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,", seg, re.M)}


def _parse_configs(src, enum_num):
    out = {}
    # { WeaponAttrType.Xxx, (true, AttrTag.A | AttrTag.B, ModifierType.All) },
    for m in re.finditer(r"\{\s*WeaponAttrType\.(\w+)\s*,\s*\(([^()]*)\)\s*\}", src):
        name, inner = m.group(1), m.group(2)
        if name not in enum_num:      # 被注释掉的条目（如 MaxDistance）
            continue
        parts = [p.strip() for p in inner.split(",")]
        if len(parts) != 3:
            continue
        mods = 0
        for tok in parts[2].replace("ModifierType.", "").split("|"):
            mods |= MOD_NAMES.get(tok.strip(), 0)
        out[name] = {"current": parts[0].lower() == "true", "tags": parts[1], "mods": mods}
    return out


def _parse_auto_fill(src):
    """InitAttribute 里 Set(Attr.Xxx, ...) 的属性 ⇒ base 来自 Damages，不写 cfg 也有值。"""
    return set(re.findall(r"Set\(Attr\.(\w+)\s*,", src))


def load(root):
    with open(os.path.join(root, FACTORY_REL), encoding="utf-8") as f:
        factory_src = f.read()
    with open(os.path.join(root, CONTROLLER_REL), encoding="utf-8") as f:
        controller_src = f.read()
    enum_num = _parse_enum(factory_src)
    configs = _parse_configs(factory_src, enum_num)
    return Info(enum_num, configs, _parse_auto_fill(controller_src))


_CFG_BLOCK = re.compile(r"(?ms)^  cfg:\n(.*?)(?=^  \w+:)")
_KV = re.compile(r"(?m)^\s*- Key: (\d+)\n\s*Value: ([-\d.]+)\s*$")


def parse_prefab_cfg(path):
    """读 WeaponPlayerController.cfg._fakeAttrs（编号 -> 值）。"""
    with open(path, encoding="utf-8") as f:
        src = f.read()
    m = _CFG_BLOCK.search(src)
    if not m:
        return {}
    return {int(k): float(v) for k, v in _KV.findall(m.group(1))}
