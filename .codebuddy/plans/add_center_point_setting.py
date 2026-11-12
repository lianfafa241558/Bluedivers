# -*- coding: utf-8 -*-
"""向 Archive_Default.asset 的 settingDic 追加「显示屏幕中心点」开关（LF 保留）。"""
p = r"e:\Bluedivers\Assets\Resources\GameData\Archive_Default.asset"

with open(p, "r", encoding="utf-8", newline="") as f:
    s = f.read()

anchor = '    - Key: "UI\\u7F29\\u653E\\u7CFB\\u6570"\n'
block = (
    '    - Key: "\\u663E\\u793A\\u5C4F\\u5E55\\u4E2D\\u5FC3\\u70B9"\n'
    '      Value:\n'
    '        titile: \n'
    '        type: 1\n'
    '        value:\n'
    '          value: 0\n'
    '        showTexts: []\n'
    '        sliderRange: {x: 0, y: 0}\n'
    '        sliderSuffix: \n'
)

assert s.count(anchor) == 1, "锚点数量异常: %d" % s.count(anchor)
assert "663E\\u793A\\u5C4F" not in s, "设置项已存在"

s = s.replace(anchor, block + anchor)

with open(p, "w", encoding="utf-8", newline="") as f:
    f.write(s)

print("ok, lines =", s.count("\n"))
