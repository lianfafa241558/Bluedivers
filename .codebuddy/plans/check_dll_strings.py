# -*- coding: utf-8 -*-
"""检查 Unity 已编译 DLL 是否包含新/旧版本的字符串标记"""
import sys

DLL = r"E:\Bluedivers\Library\ScriptAssemblies\ProjectEditors.dll"

with open(DLL, "rb") as fp:
    data = fp.read()

# .NET 元数据字符串以 UTF-16 保存，同时检查 UTF-8(用户字符串堆实际是 UTF-16)
utf16 = data.decode("utf-16-le", errors="ignore")
utf8 = data.decode("utf-8", errors="ignore")

markers = ["下坠速度", "速度倍率", "命中噪声", "DDbg", "基础", "满蓄", "SplitLabelWidth", "SplitBoxesV3"]
print("dll size:", len(data))
for m in markers:
    print(f"{m}: utf16={m in utf16} utf8={m in utf8}")
