# -*- coding: utf-8 -*-
"""给 InputManager.prefab 的 FreeCamera(window:4) 段补绑 G/R/C/Z（幂等）。

⚠ 只往 inputList 里插条目，不写注释（prefab YAML 的 # 注释会吞掉后续列表项）。
"""
import io
import re

PATH = 'Assets/Resources/Prefabs/Manager/InputManager.prefab'

# (key, KeyCode, 名称)
NEW = [(6, 103, 'Throw/G'), (7, 114, 'Reload/R'), (32, 99, 'Crouching/C'), (31, 122, 'Prone/Z')]

text = io.open(PATH, encoding='utf-8-sig', newline='').read()
nl = '\r\n' if '\r\n' in text else '\n'

added = []
for key, code, label in NEW:
    exists = re.search(r'- window: 4\s*\r?\n\s+key: %d\b' % key, text)
    if exists:
        print('skip (已存在) %s key=%d' % (label, key))
        continue
    block = ('  - window: 4' + nl +
             '    key: %d' % key + nl +
             '    positiveMainValue: %d' % code + nl +
             '    positiveSpareValue: 0' + nl +
             '    negativeMainValue: 0' + nl +
             '    negativeSpareValue: 0' + nl +
             '    lastTime: Infinity' + nl)
    m = re.search(r'(?m)^  nowState:', text)
    if not m:
        raise SystemExit('ERR: 找不到 nowState 锚点')
    text = text[:m.start()] + block + text[m.start():]
    added.append(label)

io.open(PATH, 'w', encoding='utf-8-sig', newline='').write(text)
print('added =', added)
print('total items =', len(re.findall(r'- window: \d', text)))
