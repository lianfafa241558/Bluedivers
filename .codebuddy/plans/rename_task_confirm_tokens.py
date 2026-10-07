# -*- coding: utf-8 -*-
r"""[一次性] 按用户确认的阶段模型重命名联机消息与流程方法（token 级、带残留断言）。

阶段模型（用户 2026-10-06 口径）：
    Bridge（选任务）→ Ready（等所有人就位）→ Armament（舰桥选战备）→ Transition（同时加载场景）→ Game
⇒ `StartGameNtf`/`NetHostSvc.StartGame` 实际只是"**本局配置确认**"（Bridge→Ready），名字误导；
   `SelectMapWnd.StartTask` 也只是"选完任务"。统一改成 `TaskConfirm*` / `ConfirmTask`。

用法： python -X utf8 .codebuddy/plans/rename_task_confirm_tokens.py
"""
import io
import os
import re
import sys

ROOT = r'd:\Pro\Bluedivers\Assets\Scripts'

# 顺序敏感：长的/带限定符的 token 必须先替换，否则会被短 token 吃掉（如 CmdId.StartGameNtf）。
RULES = [
    ('CmdId.StartGameNtf', 'CmdId.TaskConfirm'),
    ('public const int StartGameNtf = 4006;', 'public const int TaskConfirm = 4006;'),
    ('StartGameNtf', 'TaskConfirmNtf'),          # 类名 / 注释 / Register<> / HandleStartGameNtf 后缀
    ('OnStartGame', 'OnTaskConfirm'),
    ('HandleStartGame', 'HandleTaskConfirm'),
    ('<see cref="StartGame"/>', '<see cref="ConfirmTask"/>'),
    ('StartGame(', 'ConfirmTask('),              # 声明 + 调用点 + Demo 的本地方法
    ('StartTask', 'ConfirmTask'),
]

LEFTOVER = re.compile(r'StartGameNtf|CmdId\.StartGame|StartTask|OnStartGame(?!Ntf\()|HandleStartGame(?!Ntf\()')


def main():
    total = 0
    per_rule = [0] * len(RULES)
    leftovers = []
    for dirpath, _, names in os.walk(ROOT):
        for name in names:
            if not name.endswith('.cs'):
                continue
            path = os.path.join(dirpath, name)
            with io.open(path, encoding='utf-8') as f:
                text = f.read()
            old_text = text
            for i, (old, new) in enumerate(RULES):
                n = text.count(old)
                if n:
                    text = text.replace(old, new)
                    per_rule[i] += n
                    total += n
            if text != old_text:
                with io.open(path, 'w', encoding='utf-8', newline='') as f:
                    f.write(text)
                print('  改了:', os.path.relpath(path, ROOT))

    print('\n每规则命中数:')
    for (old, new), n in zip(RULES, per_rule):
        print('  %-46s -> %-22s %d' % (old, new, n))
    print('总替换:', total)

    print('\n残留检查:')
    for dirpath, _, names in os.walk(ROOT):
        for name in names:
            if not name.endswith('.cs'):
                continue
            path = os.path.join(dirpath, name)
            with io.open(path, encoding='utf-8') as f:
                for no, line in enumerate(f, 1):
                    if LEFTOVER.search(line):
                        leftovers.append('%s:%d: %s' % (os.path.relpath(path, ROOT), no, line.strip()))
    if leftovers:
        print('\n!! 仍有旧 token（必须清零）:')
        for item in leftovers:
            print('  ', item)
        return 1
    print('  OK：旧 token 已清零')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
